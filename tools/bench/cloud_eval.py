"""Ollama Cloud adapter for the context-conditioned semantic rubric experiment (EXPERIMENT; nothing in the application imports it): `tools/.venv/Scripts/python.exe tools/bench/cloud_eval.py smoke|run|show-plan ...`.

Question: does a larger open-weight model served by Ollama Cloud read trainee speech better than the existing classifier gate? It reuses, unchanged, the evaluation contract of tools/bench/rubric_eval.py (the approved objectives of content/assessment/taxonomy.json, the annotation rubric's tie-breaks, the
per-objective verdict / actor / modality / evidence / reason schema, `evaluate`, and `credit`, which keeps interpretation apart from deterministic eligibility and refuses every critical act) and the scorer tools/bench/rubric_compare.py, so the cloud model and the baseline gate are scored on the SAME rows with the SAME labels.
A model output is only ever a CANDIDATE INTERPRETATION: `evaluate` validates it, `credit` decides, the production gate, thresholds and guards are not touched and `speech_interpretation` stays OFF.

Safety rails of this adapter (each tested by tools/ci/test_cloud_eval.py, no network):
  * `run` refuses to start unless a successful `smoke` result for the same model exists, and `smoke` records WHICH model answered (the `model` field of the reply), the HTTP status and the latency. A 401 (not signed in) or any 402/403/429 is reported as a BLOCKER and nothing else is sent.
  * a hard cap on calls (`--max-calls`, default 100) and a stop at the first 401/402/403/429 or after 3 failures in a row; no retry loop; no paid-plan or credit action exists in this file (it cannot sign in, purchase or upgrade: the owner signs in and watches the usage page).
  * only approved, non-sensitive benchmark sentences (tests/action_bench_v1, synthetic) and the approved definitions are sent; the API key, if one is used for direct mode, is read from OLLAMA_API_KEY and never printed or stored.
  * results go to artifacts/ai-bench/rubric/ under a `cloud-` tag (git-ignored); the frozen safety final set (tests/safety_challenge/final.jsonl) is never read here, and tools/bench/safety_frozen.py --check must still pass.
Usage figures: the cloud reply carries token counts; the account's own usage page (ollama.com) is the only authority on allowance and charges and is read BY A PERSON before and after the smoke test; this tool does not claim to read it.
"""
from __future__ import annotations

import argparse
import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import rubric_eval as R  # noqa: E402

OUT = R.OUT
SMOKE = ROOT / "artifacts" / "ai-bench" / "cloud" / "smoke.json"
LOCAL = "http://127.0.0.1:11434"                  # a locally signed-in Ollama relays `:cloud` models
DIRECT = "https://ollama.com"                      # direct API, needs OLLAMA_API_KEY
DEFAULT_MODEL = "gpt-oss:20b-cloud"                # smallest reasoning model of the public cloud list on 20 Oct 2026 (13.8 GB, open weights); no Ministral model is listed
BLOCKING = (401, 402, 403, 429)
FORMAT_HINT = ('\n\nOUTPUT FORMAT (the service did not enforce the schema for this model): answer with ONE JSON object and nothing else, exactly of the form '
               '{"findings":[{"objective":"<an eligible objective id>","verdict":"SUPPORTED|NOT_SUPPORTED|UNCERTAIN","actor":"trainee|other|unclear","modality":"actual|negated|intended|hypothetical|reported|incomplete","evidence":"<words copied from the utterance>","reason":"<one reason code>"}]}; '
               'use {"findings":[]} when no objective applies. The top-level key is "findings", never "objectives".')
SMOKE_PROMPT = "Reply with the single word: pong"  # harmless, non-sensitive


def endpoint(mode: str) -> tuple[str, dict]:
    if mode == "direct":
        key = os.environ.get("OLLAMA_API_KEY", "")
        return DIRECT, ({"Authorization": "Bearer " + key} if key else {})
    return LOCAL, {}


def post(url: str, body: dict, headers: dict, timeout: float):
    import httpx
    return httpx.post(url, json=body, headers=headers, timeout=timeout)


def chat(model: str, mode: str, messages: list[dict], timeout: float, fmt: dict | None = None, think: str | None = "low", num_predict: int = 1024, _post=post) -> dict:
    """One cloud call; never raises for an ordinary failure. The reply's own `model` field is returned so the model that really answered is on record."""
    base, headers = endpoint(mode)
    body = {"model": model, "stream": False, "messages": messages, "options": {"temperature": 0, "seed": 7, "num_predict": num_predict}}
    if fmt is not None:
        body["format"] = fmt
    if think:
        body["think"] = think
    t = time.perf_counter()
    try:
        r = _post(base + "/api/chat", body, headers, timeout)
    except Exception as e:                                        # timeouts and transport errors are results, not crashes
        return {"error": ("TIMEOUT" if "Timeout" in type(e).__name__ else "UNREACHABLE:" + type(e).__name__), "wall_ms": round((time.perf_counter() - t) * 1000)}
    wall = round((time.perf_counter() - t) * 1000)
    if r.status_code >= 400:
        return {"error": f"HTTP_{r.status_code}", "status": r.status_code, "detail": r.text[:200], "wall_ms": wall}
    d = r.json()
    m = d.get("message") or {}
    return {"content": m.get("content", ""), "thinking": m.get("thinking", "") or "", "wall_ms": wall, "answered_by": d.get("model", ""), "done_reason": d.get("done_reason"),
            "total_ms": round(d.get("total_duration", 0) / 1e6), "prompt_tokens": d.get("prompt_eval_count", 0), "output_tokens": d.get("eval_count", 0),
            "load_ms": round(d.get("load_duration", 0) / 1e6), "prompt_ms": round(d.get("prompt_eval_duration", 0) / 1e6), "gen_ms": round(d.get("eval_duration", 0) / 1e6)}


def smoke(model: str, mode: str, _post=post) -> dict:
    res = chat(model, mode, [{"role": "user", "content": SMOKE_PROMPT}], timeout=60.0, think="low", num_predict=200, _post=_post)
    blocked = res.get("status") in BLOCKING
    out = {"model_requested": model, "mode": mode, "ok": "error" not in res and "pong" in res.get("content", "").lower(), "answered_by": res.get("answered_by", ""), "error": res.get("error", ""),
           "blocker": ("NOT SIGNED IN or no allowance: sign in yourself (`ollama signin`) and read the usage page before and after; nothing was sent beyond this one harmless prompt" if blocked else ""),
           "wall_ms": res.get("wall_ms"), "prompt_tokens": res.get("prompt_tokens"), "output_tokens": res.get("output_tokens"), "at_utc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())}
    return out


def cloud_call(utterance: str, model: str, mode: str, timeout: float, elig: list[dict], system: str, think: str | None, num_predict: int, _post=post) -> dict:
    """`system` already carries the format hint when it is wanted (see `run`)."""
    messages = [{"role": "system", "content": system}, {"role": "user", "content": R.user_prompt(utterance, None, None, elig)}]
    return chat(model, mode, messages, timeout, fmt=R.schema([o["id"] for o in elig]), think=think, num_predict=num_predict, _post=_post)


def run(split: str, tag: str, model: str, mode: str, max_calls: int, stride: int, timeout: float, think: str | None, num_predict: int, variant: str = "clartb", _post=post, smoke_file: Path = SMOKE, hint: bool = False) -> dict:
    s = json.loads(smoke_file.read_text(encoding="utf-8")) if smoke_file.exists() else None
    if not s or not s.get("ok") or s.get("model_requested") != model:
        return {"started": False, "reason": "no successful smoke test for this model: run `cloud_eval.py smoke` first (and read the usage page before and after)"}
    import nli_zero_shot as N
    rows = N.load_rows(split)[::stride][:max_calls]
    OUT.mkdir(parents=True, exist_ok=True)
    p = OUT / f"cloud-{tag}-{split}.jsonl"
    done = {json.loads(l)["id"] for l in p.read_text(encoding="utf-8").splitlines() if l.strip()} if p.exists() else set()
    elig = R.eligible(None)
    system = R.system_prompt(variant) + (FORMAT_HINT if hint else "")
    calls, streak, stopped = 0, 0, ""
    with p.open("a", encoding="utf-8", newline="\n") as fh:
        for r in rows:
            if r["id"] in done:
                continue
            if calls >= max_calls:
                stopped = "MAX_CALLS"
                break
            raw = cloud_call(r["text"], model, mode, timeout, elig, system, think, num_predict, _post)
            calls += 1
            fh.write(json.dumps({"id": r["id"], "raw": raw}) + "\n")
            fh.flush()
            if raw.get("status") in BLOCKING:
                stopped = f"BLOCKER_HTTP_{raw['status']}"
                break
            streak = streak + 1 if "error" in raw else 0
            if streak >= 3:
                stopped = "THREE_FAILURES_IN_A_ROW"
                break
    return {"started": True, "calls": calls, "stopped": stopped, "path": str(p), "rows_planned": len(rows)}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    for name in ("smoke", "run"):
        s = sub.add_parser(name)
        s.add_argument("--model", default=DEFAULT_MODEL)
        s.add_argument("--mode", choices=["local", "direct"], default="local")
    r = sub.choices["run"]
    r.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    r.add_argument("--tag", default="gptoss20b")
    r.add_argument("--max-calls", type=int, default=100)
    r.add_argument("--stride", type=int, default=1)
    r.add_argument("--timeout", type=float, default=120.0)
    r.add_argument("--think", default="low", choices=["low", "medium", "high", "none"])
    r.add_argument("--num-predict", type=int, default=1024)
    r.add_argument("--hint", action="store_true", help="append an explicit output-format instruction (the cloud service did not enforce the JSON schema for gpt-oss)")
    sub.add_parser("show-plan")
    a = ap.parse_args(argv)
    if a.cmd == "show-plan":
        print(__doc__)
        return 0
    if a.cmd == "smoke":
        res = smoke(a.model, a.mode)
        SMOKE.parent.mkdir(parents=True, exist_ok=True)
        SMOKE.write_text(json.dumps(res, indent=1) + "\n", encoding="utf-8", newline="\n")
        print(json.dumps(res, indent=1))
        return 0 if res["ok"] else 2
    res = run(a.split, a.tag, a.model, a.mode, a.max_calls, a.stride, a.timeout, None if a.think == "none" else a.think, a.num_predict, hint=a.hint)
    print(json.dumps(res, indent=1))
    return 0 if res.get("started") and not res.get("stopped", "").startswith(("BLOCKER", "THREE")) else 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
