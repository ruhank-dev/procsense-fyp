"""Measures the scenario quality judge against seeded defects (feature A9, capability A9.c2): `uv run --project tools tools/ai/eval_judge.py [--model qwen3.5:4b-q4_K_M] [--limit 30] [--clean-limit 15] [--write]`.

The held-out seeded set of the text critic (tests/generation/text_inconsistencies_v2.json: a patient line changed to contradict the facts; clean authored cases) is scored by the judge (server/generation/judge.py).
A case is FLAGGED when its lowest score is at most FLAG_AT (2). Reported: the share of seeded defects flagged, the share of seeded defects flagged that the deterministic rules did NOT flag (the plan: catch at least half
of the poor cases that the metrics pass), and the share of clean cases flagged (the plan: under 10 percent). The seeded defects are an AI-made PROXY for poor cases: the plan's own acceptance is a Spearman
correlation of at least 0.6 with human ratings, which needs the rated set (EX-15), so nothing here passes the judge; it is advisory and unvalidated. Needs Ollama. `--write` saves reports/ai/judge-eval.json.
"""
import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools" / "generation"))
FLAG_AT = 2

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--limit", type=int, default=30)
    ap.add_argument("--clean-limit", type=int, default=15)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    import inconsistency_set as I
    import inconsistency_set_v2 as I2
    from composer import blocks as B
    from generation import judge
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = {c["id"]: c for c in B.load_cases()}
    data = json.loads((ROOT / "tests" / "generation" / "text_inconsistencies_v2.json").read_text(encoding="utf-8"))
    j = judge.Judge(client, model=a.model, timeout=120.0)
    started = time.monotonic()
    flagged = flagged_missed_by_rules = unanswered = n = 0
    missed_by_rules = 0
    for m in data["members"][:a.limit]:
        case = I2.apply(cases[m["case"]], m["line"], m["text"])
        s = j.score(case)
        n += 1
        by_rules = m["line"] in I.baseline_flags(case)
        missed_by_rules += not by_rules
        if s is None:
            unanswered += 1
            continue
        hit = s.lowest() <= FLAG_AT
        flagged += hit
        flagged_missed_by_rules += hit and not by_rules
    clean_flag = clean_n = clean_unanswered = 0
    for cid in data["clean"][:a.clean_limit]:
        s = j.score(cases[cid])
        clean_n += 1
        if s is None:
            clean_unanswered += 1
            continue
        clean_flag += s.lowest() <= FLAG_AT
    out = {"model": a.model, "set": "text_inconsistencies_v2 (seeded defects: an AI-made proxy for poor cases, NOT human ratings)", "seeded": n, "unanswered": unanswered, "flag_at": FLAG_AT, "flagged_share": round(flagged / n, 3),
           "seeded_not_flagged_by_rules": missed_by_rules, "flagged_among_those_the_rules_missed": round(flagged_missed_by_rules / missed_by_rules, 3) if missed_by_rules else None,
           "clean_cases": clean_n, "clean_flagged_share": round(clean_flag / clean_n, 3), "human_calibration": "none: Spearman with human ratings needs the rated set (EX-15); the judge is advisory and unvalidated", "seconds": round(time.monotonic() - started)}
    print(json.dumps(out, indent=1))
    if a.write:
        (ROOT / "reports" / "ai" / "judge-eval.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
