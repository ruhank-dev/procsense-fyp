"""Builds content/sepsis/sepsis.json: the NICE NG253 risk tiers, their timers and the cases where infection is suspected (p0-clinical-logic-s04).

    uv run --project tools tools/content/build_sepsis.py            # write
    uv run --project tools tools/content/build_sepsis.py --check    # fail if the file is out of date

Source: content/sepsis_ng253_tiers.csv (verified against NICE NG253 and the walkthrough 4.10). The criteria and the timers are read from its rows; nothing is typed here except
(SR-0090) the project's reading of how they combine, and (SR-0091) which cases have an infection as the working diagnosis (the walkthrough says: S2 appendicitis, especially perforated, an abscess;
S1 inflamed gallbladder, infected bile duct, urine or chest infection), which the clinician confirms.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "sepsis" / "sepsis.json"

RANK = {"very_low": 0, "low": 1, "moderate": 2, "high": 3}

# Cases where an infection is the working diagnosis, each with the reason (the project's reading; the clinician confirms).
INFECTION_CASES = {
    "SC-1": "perforated appendix with sepsis",
    "SC-3": "appendicitis in pregnancy with fever",
    "SC-8": "septic shock",
    "SC-9": "cholangitis with sepsis",
    "SC-11": "sepsis from appendicitis",
    "SC-14": "appendicitis",
    "SC-19": "sepsis",
    "STK-S1-D": "high-risk sepsis from an infected bile duct",
    "STK-S2-A": "appendicitis needing source control",
    "STK-S2-B": "sepsis with a perforated appendix",
    "STK-S2-D": "appendicitis in pregnancy",
    "VAR-S1-E": "acute cholecystitis",
    "VAR-S2-G": "appendicitis with dehydration",
    "VAR-S2-H": "appendicitis in pregnancy",
    "VAR-S2-I": "appendicitis in a minor",
    "VAR-S2-J": "appendicitis behind a urine-infection story",
    "VAR-S2-L": "appendicitis (antibiotics-only is an option)",
    "VAR-S2-PC": "probable appendicitis",
    "VAR-S2-PD": "perforated appendix with sepsis (Mr Usman)",
}


def rows() -> list[dict]:
    with open(CONTENT / "sepsis_ng253_tiers.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def num(text: str):
    return int(text) if text.strip() != "" else None


def build() -> str:
    table = rows()
    criteria, timers = [], {}
    for r in table:
        tier = r["tier"]
        if r["kind"] == "criteria":
            criteria.append({"id": r["id"], "tier": tier, "news2_min": num(r["v_news2_min"]), "news2_max": num(r["v_news2_max"]), "single3": r["single3"] == "yes", "ref": r["ref"]})
        else:
            t = timers.setdefault(tier, {"ref": r["ref"]})
            if r["kind"] in ("antibiotics", "review"):
                t[r["kind"] + "_hours"] = num(r["v_hours"])
            elif r["kind"] == "reassess":
                if r["d_minutes"].strip():
                    lo, _, hi = r["d_minutes"].partition("-")
                    t["reassess_minutes_min"] = int(lo)
                    t["reassess_minutes_max"] = int(hi or lo)
    # NG253 high risk: IV fluid bolus within 1 hour as well (the row for antibiotics carries both)
    fluids_row = next(r for r in table if r["id"] == "high.antibiotics")
    assert "fluid bolus within 1 hour" in fluids_row["text"]
    timers["high"]["fluids_hours"] = timers["high"]["antibiotics_hours"]
    if "lactate" in next(r for r in table if r["id"] == "moderate.review")["text"]:
        timers["moderate"]["lactate"] = True
    # the cases must exist
    have = {p.stem for p in (CONTENT / "case_files").glob("*.json")}
    missing = sorted(set(INFECTION_CASES) - have)
    if missing:
        raise SystemExit(f"infection cases that are not case files: {missing}")
    doc = {
        "tiers": [{"id": t, "rank": RANK[t]} for t in sorted(RANK, key=RANK.get, reverse=True)],
        "criteria": criteria,
        "timers": dict(sorted(timers.items())),
        "concern_raises_by": 1,
        "infection_cases": dict(sorted(INFECTION_CASES.items())),
        "refs": ["SR-0090", "SR-0091"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/sepsis/sepsis.json is out of date; run tools/content/build_sepsis.py")
            return 1
        print("content/sepsis/sepsis.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
