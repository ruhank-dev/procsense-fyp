"""Builds content/investigations/critical.json: the results the lab phones about, and what the trainee must do on the call (p0-clinical-logic-s08).

    uv run --project tools tools/content/build_critical.py            # write
    uv run --project tools tools/content/build_critical.py --check    # fail if the file is out of date

The limits come from the verified table content/critical_results.csv (walkthrough 5.27, modelled on the Royal College of Pathologists' guidance; haematology values provisional). The script
adds structure only: which result names count as each analyte (the case files write 'Hb', 'Capillary glucose'), which fact a condition needs ('without known kidney failure' needs the case's
known_kidney_failure fact, 'warfarin' needs on_warfarin, 'unexpectedly' means not on warfarin), and the actions a trainee can choose when acknowledging a critical result.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "investigations" / "critical.json"

ALIASES = {
    "potassium": ["Potassium"], "sodium": ["Sodium"], "glucose": ["Capillary glucose", "Finger-prick glucose"], "calcium": ["Calcium"], "creatinine": ["Creatinine"], "crp": ["CRP"],
    "haemoglobin": ["Hb", "Haemoglobin"], "platelets": ["Platelets"], "neutrophils": ["Neutrophils"], "inr": ["INR"], "lactate": ["Lactate"],
}
CONDITIONS = {"": "always", "without known kidney failure": "no_known_kidney_failure", "warfarin": "on_warfarin", "unexpectedly": "not_on_warfarin"}
ACTIONS = ["repeat_sample", "treat_and_repeat", "escalate_senior", "order_ecg", "review_patient", "postpone_surgery"]


def build() -> str:
    with open(CONTENT / "critical_results.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    rules = []
    for r in rows:
        if r["analyte"] not in ALIASES:
            raise SystemExit(f"critical_results.csv: the analyte '{r['analyte']}' has no result names in this script")
        if r["v_condition"] not in CONDITIONS:
            raise SystemExit(f"critical_results.csv: the condition '{r['v_condition']}' is not one this script knows")
        rules.append({"id": r["id"], "analyte": r["analyte"], "direction": r["direction"], "inclusive": r["inclusive"] == "yes", "value": float(r["v_value"]), "unit": r["d_unit"],
                      "condition": CONDITIONS[r["v_condition"]], "ref": r["ref"]})
    doc = {"rules": rules, "aliases": ALIASES, "actions": ACTIONS, "refs": ["SR-0076", "SR-0077"]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/investigations/critical.json is out of date; run tools/content/build_critical.py")
            return 1
        print("content/investigations/critical.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
