"""Evaluation of the integrated semantic interpreter (p1-ai-language-s18, ADR-0024) on tests/action_bench_v1, by REPLAY: `server/.venv/Scripts/python.exe tools/bench/semantic_eval.py --split dev|test|test2 --replay cloud-gemma31b-full [--write]`.

The pipeline under test is the real one: the shipped gate and Interpreter, `server/interpret/semantic.SemanticInterpreter` with the STRICT reader, the deterministic checks, and the engine's own EvidenceStore (`evidence-check`) for what is finally accepted. Only the model call is replaced by a ReplayProvider that serves the recorded raw replies of
tools/bench/cloud_eval.py (artifacts/ai-bench/rubric/<tag>-<split>.jsonl), so the evaluation costs nothing and cannot differ from run to run. Consequences, stated plainly:
  * a recorded reply that the strict validator rejects (a fenced block, an unknown key...) is INVALID here and the pipeline falls back to the shipped decision; the fence-stripped post-hoc files are NEVER used by this tool;
  * the recordings used the bare prompt of the experiments, so the effect of the scenario context in the new user prompt is NOT measured; a retry cannot change a replayed reply (a live retry might repair a format failure), so the format-failure effect is a worst case;
  * rows carry no scenario state, so the same per-row state as tools/bench/interpretation_bench.py is used (in window; a waiting question or read-back for the acts that need one; out of window; without that context);
  * labels are the synthetic, builder-written labels of the benchmark; the baseline gate was tuned on parts of these splits; test2 was used by the earlier Qwen experiment; none of this is independent validation (EX-01).
Reports go to reports/ai/semantic-replay-<tag>-<split>.json (machine readable). The frozen safety final set is never read."""
from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import interpretation_bench as IB  # noqa: E402
from server.interpret.llm.provider import ReplayProvider  # noqa: E402
from server.interpret.llm.reader import SemanticReader  # noqa: E402
from server.interpret.semantic import SemanticInterpreter  # noqa: E402

RAW = ROOT / "artifacts" / "ai-bench" / "rubric"
CALL_TIMEOUT = 8.0
READINGS: list = []                      # every ReadOutcome of the run, in call order (live runs report from these)
REPORTS = ROOT / "reports" / "ai"


class BudgetedProvider:
    """Wraps a live provider for the evaluation: one model reading per row and attempt (the first condition's reading is reused for the other conditions of the same row, but a RETRY is a new call), a hard cap on calls and on tokens, and a stop at the first 401/402/403/429 (a quota
    or billing signal): after that every call is answered NOT_CONFIGURED and nothing more is sent. Keeps every raw reply (text, finish reason, tokens, time; no key, no header) for the report and for a reproducible record. No sign-in, billing or purchase code exists here."""
    name = "budgeted"
    BLOCKING = (401, 402, 403, 429)

    def __init__(self, inner, max_calls: int, max_tokens: int = 0):
        self.inner, self.max_calls, self.max_tokens, self.calls, self.tokens, self.stopped, self.memo, self.log = inner, max_calls, max_tokens, 0, 0, "", {}, []

    def complete(self, system, user, schema=None, key="", timeout=None):
        from server.interpret.llm.provider import ProviderResult
        attempt = 2 if "Your previous answer" in user else 1
        if (key, attempt) in self.memo:
            return self.memo[(key, attempt)]
        if self.stopped:
            return ProviderResult(error="NOT_CONFIGURED")
        if self.calls >= self.max_calls:
            self.stopped = "MAX_CALLS"
            return ProviderResult(error="NOT_CONFIGURED")
        if self.max_tokens and self.tokens >= self.max_tokens:
            self.stopped = "TOKEN_CAP"
            return ProviderResult(error="NOT_CONFIGURED")
        self.calls += 1
        r = self.inner.complete(system, user, schema, key=key, timeout=timeout)
        self.tokens += r.prompt_tokens + r.output_tokens
        self.log.append({"key": key, "attempt": attempt, "error": r.error, "status": r.status, "text": r.text, "done_reason": r.done_reason, "wall_ms": r.wall_ms, "prompt_tokens": r.prompt_tokens, "output_tokens": r.output_tokens, "answered_by": r.answered_by})
        if r.status in self.BLOCKING:
            self.stopped = f"BLOCKER_HTTP_{r.status}"
        if not r.error:
            self.memo[(key, attempt)] = r
        return r


def run(split: str, replay_tag: str, ollama: str, mode: str = "head", provider=None) -> tuple[dict, list]:
    provider = provider or ReplayProvider(RAW / f"{replay_tag}-{split}.jsonl")
    readers = []

    def factory(clf):
        si = SemanticInterpreter(clf, SemanticReader(provider, max_attempts=2, call_timeout=CALL_TIMEOUT), confidence_mode=mode)
        inner = si.interpret

        def logged(*args, **kw):
            out = inner(*args, **kw)
            if si.last_reading is not None:
                READINGS.append(si.last_reading)
            return out
        si.interpret = logged
        readers.append(si)
        return si
    original = IB.Interpreter
    IB.Interpreter = factory
    try:
        data = IB.run(split, ollama)
    finally:
        IB.Interpreter = original
    return data, readers


def reader_stats(split: str, replay_tag: str, data: dict, si: SemanticInterpreter) -> dict:
    """what the strict reader did with the recorded replies of this split (one pass over the rows in the in-window condition)."""
    provider = ReplayProvider(RAW / f"{replay_tag}-{split}.jsonl")
    reader = SemanticReader(provider, max_attempts=1)           # one attempt: a replayed reply is the same on a retry
    status, codes = collections.Counter(), collections.Counter()
    tokens_in = tokens_out = 0
    for r in data["rows"]:
        o = reader.read(None, r["text"], key=r["id"])
        status[o.status] += 1
        for c in o.codes:
            codes[c.split(":")[0]] += 1
        tokens_in += o.prompt_tokens
        tokens_out += o.output_tokens
    n = len(data["rows"])
    return {"rows": n, "status": dict(status), "rejection_codes": dict(codes), "strict_valid_rate": round(status["VALID"] / n, 4), "prompt_tokens": tokens_in, "output_tokens": tokens_out}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    ap.add_argument("--replay", default="cloud-gemma31b-full")
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--mode", default="head", choices=["head", "engine_floor"], help="confidence handed to the engine for a model-qualified candidate (engine_floor is PENDING the owner, DT-236)")
    ap.add_argument("--live", default="", help="a REAL model call per row through the signed-in Ollama (e.g. gemma4:31b-cloud): costs free-tier allowance, needs the owner's approval, capped by --max-calls")
    ap.add_argument("--max-calls", type=int, default=100)
    ap.add_argument("--max-tokens", type=int, default=0, help="stop after this many prompt+output tokens (0 = no token cap)")
    ap.add_argument("--stride", type=int, default=5)
    ap.add_argument("--timeout", type=float, default=8.0)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    live = None
    if a.live:
        import nli_zero_shot as N
        from server.interpret.llm.provider import OllamaCloudProvider
        rows_all = N.load_rows(a.split)
        N.load_rows = lambda split, _r=rows_all: _r[::a.stride]            # a subset for a budgeted live run
        live = BudgetedProvider(OllamaCloudProvider(a.live, a.ollama, "", timeout=a.timeout), a.max_calls, a.max_tokens)
    data, readers = run(a.split, a.replay, a.ollama, a.mode, provider=live)
    out = IB.summarise(a.split, data)
    out["replay"] = a.replay
    out["confidence_mode"] = a.mode
    if live is None:
        out["reader"] = reader_stats(a.split, a.replay, data, readers[0])
    else:
        from server.interpret.llm import prompt as P
        from server.interpret.llm.strict import Invalid, parse
        utt = {r["id"]: r["text"] for r in data["rows"]}
        first = {x["key"]: x for x in live.log if x["attempt"] == 1}
        second = {x["key"]: x for x in live.log if x["attempt"] == 2}
        ids = P.objective_ids()

        def check(x, key):
            if x["error"]:
                return "ERROR:" + x["error"].split(":")[0]
            try:
                parse(x["text"], x["done_reason"], utt[key], ids)
                return "VALID"
            except Invalid as e:
                return e.code
        f1 = collections.Counter(check(x, k) for k, x in first.items())
        final = {}
        for k, x in first.items():
            final[k] = check(second[k], k) if k in second else check(x, k)
        fin = collections.Counter(final.values())
        walls = sorted(x["wall_ms"] for x in live.log if not x["error"])
        n1 = max(1, len(first))
        out["reader"] = {"rows_read": len(first), "rows_in_benchmark": len(data["rows"]), "model_requested": a.live, "answered_by": sorted({x["answered_by"] for x in live.log if x["answered_by"]}), "provider_calls": live.calls, "stopped": live.stopped,
                         "first_attempt": dict(f1), "first_attempt_valid_rate": round(f1["VALID"] / n1, 4), "retried_rows": len(second), "retry_rate": round(len(second) / n1, 4), "final": dict(fin), "final_valid_rate": round(fin["VALID"] / n1, 4),
                         "prompt_tokens": sum(x["prompt_tokens"] for x in live.log), "output_tokens": sum(x["output_tokens"] for x in live.log), "tokens_total": live.tokens, "truncated_replies": sum(1 for x in live.log if x["done_reason"] not in (None, "stop") and not x["error"]),
                         "wall_ms": {"median": walls[len(walls) // 2] if walls else None, "p95": walls[int(len(walls) * 0.95)] if walls else None, "max": walls[-1] if walls else None, "n": len(walls)}, "provider_errors": dict(collections.Counter(x["error"] for x in live.log if x["error"])),
                         "pipeline_decisions_with_a_reading": dict(collections.Counter(r.status for r in READINGS))}
        raw_path = ROOT / "artifacts" / "ai-bench" / "live"
        raw_path.mkdir(parents=True, exist_ok=True)
        (raw_path / f"{a.live.replace(':', '_')}-{a.mode}-{a.split}-s{a.stride}.jsonl").write_text("".join(json.dumps(x, ensure_ascii=False) + "\n" for x in live.log), encoding="utf-8", newline="\n")
    keep = {k: out[k] for k in ("split", "rows", "synthetic", "independent", "counts", "replay", "confidence_mode", "reader", "interpretation_in_window", "classifier_route_as_served", "out_of_window", "without_pending_question_or_readback", "critical", "gate_inputs")}
    keep["note"] = "Integrated pipeline by replay of recorded raw replies (strict validation, fallback to the shipped decision); synthetic builder-written labels; baseline tuned on parts of these splits; not independent validation."
    if a.write:
        p = REPORTS / (f"semantic-live-{a.live.replace(':', '_')}-{a.mode}-{a.split}.json" if a.live else f"semantic-replay-{a.replay}-{a.mode}-{a.split}.json")
        p.write_text(json.dumps(keep, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    ip = keep["interpretation_in_window"]
    print(json.dumps({"split": a.split, "reader": keep["reader"], "offers": ip["offers"], "credited_precision": ip["credited_precision"], "legitimate_recall": ip["legitimate_recall"], "critical_false_credit": ip["critical_false_credit"],
                      "non_assertive_false_credit": ip["non_assertive_false_credit"], "out_of_window": keep["out_of_window"], "critical": keep["critical"], "decisions": ip["legitimate_decisions"]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
