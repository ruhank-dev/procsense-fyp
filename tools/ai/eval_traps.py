"""Measures the trap wording writer (feature A7, capability A7.c2; DT-224): `uv run --project tools tools/ai/eval_traps.py [--model qwen3.5:4b-q4_K_M] [--limit N] [--write]`.

For every case of the library the writer (server/generation/trap_writer.py) rewords the case's wrong options with the real model. Reported: the wrong options offered, the share whose rewording passed the lexical checks
(`accepted`; some are word-for-word the authored text, so `accepted_and_different_from_authored` is the real count) and the share left as authored (`template`), the cases whose `trap_fairness` (server/generation/quality.py) fell below the authored case's (must be 0: the consequence that makes a trap fair is never
changed), the cases that stopped passing the generation gate, and latency. What it cannot show is whether the new wording is MORE plausible than the authored one: that is a blind human rating (EX-03 in
docs/testing/SANNAN_EXTERNAL_TESTING.md); pairs are written out for it. Needs a running Ollama. `--write` saves reports/ai/trap-writer-eval.json.
"""
from __future__ import annotations

import argparse
import copy
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
    ap.add_argument("--timeout", type=float, default=300.0)
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    from composer import blocks as B
    from generation import quality, trap_writer as TW
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = [c for c in B.load_cases() if TW.wrong_options(c)]
    cases = cases[:a.limit] if a.limit else cases
    writer = TW.TrapWriter(client, a.model, a.timeout)
    offered = accepted = template = fairness_fell = changed = 0
    secs, pairs = [], []
    for c in cases:
        work = copy.deepcopy(c)
        before = quality.trap_fairness(c)[0]
        t = time.monotonic()
        rep = writer.apply(work, 1)
        secs.append(time.monotonic() - t)
        offered += len(rep["lines"])
        accepted += sum(x["method"] == "model" for x in rep["lines"])
        template += sum(x["method"] == "template" for x in rep["lines"])
        fairness_fell += quality.trap_fairness(work)[0] < before
        old = {o["id"]: o["text"] for d in c["decisions"] for o in d["options"]}
        for d in work["decisions"]:
            for o in d["options"]:
                if not o["correct"] and o["text"] != old[o["id"]]:
                    changed += 1
                if not o["correct"] and o["text"] != old[o["id"]] and len(pairs) < 12:
                    pairs.append({"case": c["id"], "option": o["id"], "authored": old[o["id"]], "reworded": o["text"]})
    secs.sort()
    out = {"synthetic": False, "model": a.model, "digest": client.models().get(a.model, ""), "cases": len(cases), "wrong_options_offered": offered, "accepted": accepted, "accepted_and_different_from_authored": changed, "left_as_authored": template,
           "accepted_share": round(accepted / offered, 3) if offered else None, "cases_with_lower_trap_fairness": fairness_fell, "seconds_per_case_p50": round(statistics.median(secs), 1) if secs else None,
           "pairs_for_human_rating": pairs, "note": "Plausibility above the authored wording needs two raters (EX-03); what is measured is that the lexical checks hold and fairness does not fall."}
    print(json.dumps(out, indent=2))
    if a.write:
        (ROOT / "reports" / "ai" / "trap-writer-eval.json").write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8", newline="\n")
