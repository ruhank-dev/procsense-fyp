"""Builds content/decision/decision.json: the five outcomes and the ordered checks of the master decision (p0-clinical-logic-s13).

    uv run --project tools tools/content/build_decision.py            # write
    uv run --project tools tools/content/build_decision.py --check    # fail if the file is out of date

Source: content/decision_outcomes.csv (the five outcomes, verified). The walkthrough's Step 8 diagram ('Is it safe to continue?') gives the order of the checks and the emergency signs (NEWS2 7+, top BP 90 or less,
oxygen 91% or less, new confusion, anaphylaxis, chest pain, positive pregnancy test with shock): SR-0094. How they are turned into an expected outcome (SR-0095) is the project's reading, which the clinician confirms.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "decision" / "decision.json"

CHECKS = [
    {"id": "emergency", "outcome": "EMERGENCY", "text": "Any emergency sign, or a life-threatening problem still open?", "ref": "SR-0094"},
    {"id": "information_missing", "outcome": "INVESTIGATE", "text": "Is a result the case needs still unopened?", "ref": "SR-0095"},
    {"id": "diagnosis_changed", "outcome": "REVIEW", "text": "Does the diagnosis look different or complicated (a problem that changes the pathway)?", "ref": "SR-0095"},
    {"id": "fixable_problem", "outcome": "OPTIMISE", "text": "Is there a fixable problem still open (safety blocker, time-critical or optimisable)?", "ref": "SR-0095"},
    {"id": "affects_anaesthesia", "outcome": "REVIEW", "text": "Is anything affecting the anaesthetic (a fasting breach, ASA 3 or more)?", "ref": "SR-0095"},
    {"id": "safe", "outcome": "SAFE", "text": "Nothing left: safe to continue to consent and preparation", "ref": "SR-0095"},
]
EMERGENCY_SIGNS = {"news2_min": 7, "sbp_max": 90, "spo2_max": 91, "not_alert": True, "flags": ["event.anaphylaxis", "symptom.chest_pain"], "positive_pregnancy_with_shock": True, "ref": "SR-0094"}
PACE = {"S1": "postpone", "S2": "parallel"}


def build() -> str:
    with open(CONTENT / "decision_outcomes.csv", encoding="utf-8", newline="") as f:
        outcomes = [{"id": r["id"], "code": r["code"], "label": r["v_label"], "meaning": r["v_meaning"], "ref": r["ref"]} for r in csv.DictReader(f)]
    codes = {o["code"] for o in outcomes}
    bad = [c["id"] for c in CHECKS if c["outcome"] not in codes]
    if bad:
        raise SystemExit(f"checks that name an outcome the table does not have: {bad}")
    doc = {"outcomes": outcomes, "checks": CHECKS, "emergency_signs": EMERGENCY_SIGNS, "pace": PACE, "refs": ["SR-0094", "SR-0095"]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/decision/decision.json is out of date; run tools/content/build_decision.py")
            return 1
        print("content/decision/decision.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
