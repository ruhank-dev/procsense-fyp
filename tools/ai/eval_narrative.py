"""Measures the narrative composition step on a real model (p1-live-scenario-generation-s04, EXP-AI-5): `uv run --project tools tools/ai/eval_narrative.py --model qwen3.5:4b-q4_K_M [--cases 20] [--out artifacts/ai-bench/narrative-4b.json]`.

For the first N approved cases (in id order) every rewritable line goes through the same code as production (server/generation/narrative.py, gateway, registered prompt). Reported: how many calls gave a schema-valid
answer on the first try, how many lines were accepted by the checks (first try and after the retry), how many fell back to the authored line, latency, and a sample of before and after for a person to read.
The checks are lexical (numbers, lexicon entities, prescribing phrases, length), so this also writes every accepted pair to the output file for reading: the number the report cannot give is the share of accepted
lines that a person finds unfaithful (DT-190 is the same limit for dialogue). Results go to artifacts/ai-bench/ (git-ignored). Needs a running Ollama; changes nothing in the repository.
"""
import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--cases", type=int, default=20)
    ap.add_argument("--timeout", type=float, default=120.0)
    ap.add_argument("--out")
    a = ap.parse_args()
    import copy
    from composer import blocks as B
    from generation import narrative
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed on the host")
    cases = [c for c in B.load_cases() if c["status"].startswith("APPROVED")][: a.cases]
    narrator = narrative.Narrator(client, model=a.model, timeout=a.timeout)
    schema_ok = 0
    lines = first_ok = accepted = retried_ok = fallback = 0
    latencies, pairs = [], []
    started = time.monotonic()
    for i, case in enumerate(cases):
        work = copy.deepcopy(case)
        rep = narrator.apply(work, seed=i)
        for ln, au in zip(rep["lines"], rep["audit"]):
            lines += 1
            latencies.append(au["latency_ms"])
            schema_ok += au["first_try_schema_valid"]
            if ln["method"] == "model":
                accepted += 1
                if au["attempts"] == 1:
                    first_ok += 1
                else:
                    retried_ok += 1
                before = next(x["original"] for x in narrative.lines_of(case) if x["id"] == ln["id"])
                after = work["reason_answer"] if ln["id"] == "reason_answer" else next(h["answer"] for h in work["history"] if h["id"] == ln["id"])
                pairs.append({"case": case["id"], "line": ln["id"], "persona": rep["persona"], "before": before, "after": after})
            else:
                fallback += 1
    out = {"model": a.model, "digest": client.models().get(a.model, ""), "cases": len(cases), "lines": lines, "accepted_first_try": first_ok, "accepted_after_retry": retried_ok, "fallback_to_authored": fallback,
           "accept_rate": round(accepted / max(lines, 1), 3), "first_try_rate": round(first_ok / max(lines, 1), 3), "first_try_schema_valid_rate": round(schema_ok / max(lines, 1), 3), "latency_ms_p50": int(statistics.median(latencies)) if latencies else 0,
           "latency_ms_max": max(latencies) if latencies else 0, "seconds": round(time.monotonic() - started), "pairs": pairs}
    path = Path(a.out) if a.out else ROOT / "artifacts" / "ai-bench" / f"narrative-{a.model.split(':')[1].split('-')[0]}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print({k: v for k, v in out.items() if k != "pairs"})
    for p in pairs[:6]:
        print(f"- {p['case']} {p['line']} {p['persona']}\n    before: {p['before']}\n    after:  {p['after']}")
