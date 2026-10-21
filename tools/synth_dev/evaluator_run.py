"""DEVELOPMENT run of the production Gemma evaluator (server/evaluate, ADR-0025) over tests/synthetic_dev_v1: builds the engine-style context of each frozen specification, asks the model (free plan, hard call and token ceilings, stops on 401/402/403/429),
and stores every raw reply and outcome so that metrics can be recomputed without a model call. Usage: server/.venv/Scripts/python.exe tools/synth_dev/evaluator_run.py --out artifacts/ai-bench/live/<name>.json [--max-calls N --max-tokens N]
The prompt under test is the one in server/evaluate (its hash is recorded); nothing here changes it."""
from __future__ import annotations

import argparse
import json
import os
import sys
import threading
import time
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from server.evaluate import prompt as P  # noqa: E402
from server.evaluate import rubric  # noqa: E402
from server.evaluate.provider import OllamaCloudProvider, ProviderResult  # noqa: E402
from server.evaluate.reader import Reader  # noqa: E402

OUT = Path(os.environ["SYNTH_SET_DIR"]) if os.environ.get("SYNTH_SET_DIR") else ROOT / "tests" / "synthetic_dev_v1"      # the held-out acceptance run points this at tests/synthetic_heldout_v1
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
CRITICAL = set(MAPPING["critical_acts"])
PARTIAL = {"partial": OUT / "partial_credit_v1.jsonl", "partial2": OUT / "partial_credit_v2.jsonl", "heldout_partial": OUT / "partial_credit.jsonl"}


def context_of(spec: dict, verified: list[str] | None = None) -> dict:
    """the engine's InterpretationContext as the service receives it (steps, eligible acts of those steps, waiting question / read-back, earlier turns), built from the frozen specification, never from a model."""
    steps = [spec["step"]] if spec.get("step") else list(range(1, 16))
    eligible = [{"act": a, "behaviour": b["id"], "critical": a in CRITICAL} for b in MAPPING["behaviours"] if set(b["steps"]) & set(steps) for a in (b.get("ai_acts") or [])]
    turns = []
    if spec.get("prior_turn"):
        who = spec["prior_speaker"] or "patient"
        text = spec["prior_turn"]
        if text.startswith("(") and ")" in text:
            who, text = text[1:text.index(")")], text[text.index(")") + 1:].strip()
        turns.append({"speaker": who, "text": text})
    return {"steps": steps, "eligible": eligible, "patient_question_pending": bool(spec.get("patient_question_pending")), "readback_pending": bool(spec.get("readback_pending")), "last_turns": turns,
            "verified_actions": verified or [], "speaker": "trainee", "addressee": spec.get("recipient", "patient")}


class Budget:
    BLOCKING = (401, 402, 403, 429)

    def __init__(self, inner, max_calls: int, max_tokens: int):
        self.inner, self.max_calls, self.max_tokens, self.calls, self.tokens, self.stopped, self.log, self.lock = inner, max_calls, max_tokens, 0, 0, "", [], threading.Lock()

    def complete(self, system, user, schema=None, key="", timeout=None):
        with self.lock:
            if self.stopped:
                return ProviderResult(error="NOT_CONFIGURED")
            if self.calls >= self.max_calls:
                self.stopped = "MAX_CALLS"
                return ProviderResult(error="NOT_CONFIGURED")
            if self.tokens >= self.max_tokens:
                self.stopped = "TOKEN_CAP"
                return ProviderResult(error="NOT_CONFIGURED")
            self.calls += 1
        r = self.inner.complete(system, user, schema, key=key, timeout=timeout)
        with self.lock:
            self.tokens += r.prompt_tokens + r.output_tokens
            self.log.append({"key": key, "error": r.error, "status": r.status, "wall_ms": r.wall_ms, "prompt_tokens": r.prompt_tokens, "output_tokens": r.output_tokens, "done_reason": r.done_reason, "answered_by": r.answered_by})
            if r.status in self.BLOCKING:
                self.stopped = f"BLOCKER_HTTP_{r.status}"
        return r


def run(items: list[dict], max_calls: int, max_tokens: int, workers: int = 3, model: str = "gemma4:31b-cloud", base: str = "http://127.0.0.1:11434") -> dict:
    prov = Budget(OllamaCloudProvider(model, base, "", timeout=15.0), max_calls, max_tokens)
    reader = Reader(prov, max_attempts=2, budget_ms=25000, call_timeout=15.0)

    def one(it):
        o = reader.read(it["context"], it["utterance"], key=it["id"])
        return it["id"], {"status": o.status, "codes": o.codes, "attempts": o.attempts, "prompt_tokens": o.prompt_tokens, "output_tokens": o.output_tokens, "wall_ms": o.wall_ms,
                          "raw": o.raw[-1] if o.raw else "", "evaluation": o.evaluation.to_dict() if o.evaluation else None}
    t0 = time.perf_counter()
    with ThreadPoolExecutor(workers) as ex:
        res = dict(ex.map(one, items))
    return {"prompt_version": P.VERSION, "prompt_hash": P.prompt_hash(), "model": model, "results": res, "log": prov.log, "calls": prov.calls, "tokens": prov.tokens, "stopped": prov.stopped, "wall_s": round(time.perf_counter() - t0, 1), "workers": workers}


def dataset_items() -> list[dict]:
    rows = [json.loads(l) for l in (OUT / "dataset.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    return [{"id": r["id"], "utterance": r["utterance"], "context": context_of(r["spec"]), "spec": r["spec"], "gold": r["gold"]} for r in rows]


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--max-calls", type=int, default=650)
    ap.add_argument("--max-tokens", type=int, default=2_600_000)
    ap.add_argument("--set", default="dataset", choices=["dataset", "partial", "partial2", "heldout_partial"])
    a = ap.parse_args(argv)
    if a.set == "dataset":
        items = dataset_items()
    else:
        items = [json.loads(l) for l in PARTIAL[a.set].read_text(encoding="utf-8").splitlines() if l.strip()]
        for it in items:
            it["context"] = context_of(it["spec"], it.get("verified"))
    d = run(items, a.max_calls, a.max_tokens)
    Path(a.out).parent.mkdir(parents=True, exist_ok=True)
    Path(a.out).write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
    st = {}
    for v in d["results"].values():
        st[v["status"]] = st.get(v["status"], 0) + 1
    print(json.dumps({"prompt": d["prompt_version"], "hash": d["prompt_hash"][:12], "calls": d["calls"], "tokens": d["tokens"], "stopped": d["stopped"], "wall_s": d["wall_s"], "status": st}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
