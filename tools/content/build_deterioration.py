"""Builds content/physiology/deterioration.json: the deterioration clock and the Usman (VAR-S2-PD) timeline (p0-physiology-events-behaviour-s04).

    uv run --project tools tools/content/build_deterioration.py            # write
    uv run --project tools tools/content/build_deterioration.py --check    # fail if the file is out of date

Sources: content/decision_rules.csv row deterioration_clock (HR +5-10 and NEWS2 +1 about every 30 minutes untreated; SR-0916) and content/case_timelines.csv (the four timed readings of Part 13 Example Patient D).
What this script adds: which treatments stop the clock and how long recovery takes (design values, SR-0096) and the treated values the walkthrough states after the correct action (BP 108/64, HR 112; SR-0097).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "physiology" / "deterioration.json"

# design values (PROVISIONAL): the treatments that stop the clock, and the minutes the patient takes to reach the treated values
TREATED_BY_EFFECTS = ["EFF-FLUID-BOLUS", "EFF-ANTIBIOTIC"]
RECOVERY_MINUTES = 30
# the walkthrough's 'After the correct action': BP rises to 108/64 after fluids, HR 112 (Part 13 Example Patient D)
TREATED = {"VAR-S2-PD": {"sbp": 108, "dbp": 64, "hr": 112, "ref": "SR-0097"}}
ACVPU = {"A": "A", "Confused": "C", "Voice": "V", "Pain": "P", "Unresponsive": "U"}


def num(text: str) -> float:
    return float(text)


def build() -> str:
    with open(CONTENT / "decision_rules.csv", encoding="utf-8", newline="") as f:
        row = next(r for r in csv.DictReader(f) if r["id"] == "deterioration_clock")
    lo, hi = (int(x) for x in row["v_number"].split(";")[0].replace("–", "-").split("-"))
    news2_step, interval = (int(x) for x in row["v_number"].split(";")[1:3])
    timelines: dict[str, dict] = {}
    with open(CONTENT / "case_timelines.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    for case in sorted({r["case_id"] for r in rows}):
        pts = [r for r in rows if r["case_id"] == case]
        start = None
        points = []
        for r in pts:
            h, m = (int(x) for x in r["time"].split(":"))
            minutes = h * 60 + m
            start = minutes if start is None else start
            sbp, dbp = (int(x) for x in r["v_bp"].split("/"))
            points.append({"minutes": minutes - start, "time": r["time"], "hr": int(r["v_hr"]), "sbp": sbp, "dbp": dbp, "rr": int(r["v_rr"]), "spo2": int(r["v_spo2"]), "temp": num(r["v_temp"]),
                           "acvpu": ACVPU[r["v_acvpu"]], "news2_book": int(r["v_news2_book"]), "ref": r["ref"]})
        if case not in TREATED:
            raise SystemExit(f"{case} has a timeline but no treated values")
        timelines[case] = {"points": points, "treated": TREATED[case]}
    doc = {
        "clock": {"hr_step_min": lo, "hr_step_max": hi, "news2_step": news2_step, "interval_minutes": interval, "ref": row["ref"]},
        "treated_by_effects": TREATED_BY_EFFECTS,
        "recovery_minutes": RECOVERY_MINUTES,
        "timelines": timelines,
        "refs": ["SR-0096", "SR-0097"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/physiology/deterioration.json is out of date; run tools/content/build_deterioration.py")
            return 1
        print("content/physiology/deterioration.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
