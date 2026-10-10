"""Builds content/scores/scores.json: the numbers and groups the clinical calculators use (p0-clinical-logic-s05).

    uv run --project tools tools/content/build_scores.py            # write
    uv run --project tools tools/content/build_scores.py --check    # fail if the file is out of date

Every number comes from a verified content table, never from this script:
  * AIR, AAS and Alvarado: content/score_items.csv (points per item) and content/score_bands.csv (what a total means)
  * TG18 cholecystitis and cholangitis, ASGE, STOP-Bang: content/score_facts.csv (the numbers in the v_number column)
  * AKI stage: content/lab_bands.csv (the creatinine rows measured against the patient's usual value) and content/score_facts.csv (the 26.5 micromol/L rise in 48 hours)
The script checks that each number it copies is the number the table holds, so a changed table fails the build instead of silently disagreeing.
What this script adds is structure only: which inputs a score needs (the "required" groups: any one item of a group counts; a group of several items is mutually exclusive, such as light,
medium and strong rebound) and the ranges worked out from the table's wording ('above 10.0' excludes 10.0; '10.0-14.9' runs up to but not including 15.0).
"""
from __future__ import annotations

import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "scores" / "scores.json"

TITLES = {"air": "Appendicitis Inflammatory Response score", "aas": "Adult Appendicitis Score", "alvarado": "Alvarado score"}

# Inputs each point score needs. A list inside the list is an exclusive group (one answer among several items).
REQUIRED = {
    "air": [["pain_rlq"], ["vomiting"], ["rebound_light", "rebound_medium", "rebound_strong"], ["temperature_c"], ["wbc"], ["neutrophils_pct"], ["crp"]],
    "aas": [["pain_moved"], ["pain_rlq"], ["rlq_tenderness"], ["guarding_mild", "guarding_moderate_or_severe"], ["wbc"], ["neutrophils_pct"], ["crp_pain_under_24h", "crp_pain_24h_or_more"]],
    "alvarado": [["pain_moved"], ["loss_of_appetite"], ["nausea_or_vomiting"], ["rlq_tenderness"], ["rebound"], ["temperature_c"], ["wbc"], ["neutrophils_pct"]],
}


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def number(text: str):
    return float(text) if text.strip() else None


def step_of(text: str) -> float:
    """The smallest step the table's number is written in: '14.9' -> 0.1, '49' -> 1."""
    text = text.strip()
    return 10.0 ** -len(text.split(".")[1]) if "." in text else 1.0


def item_row(r: dict) -> dict:
    low, high = r["v_low"].strip(), r["v_high"].strip()
    numeric = bool(low or high)
    out = {"id": r["id"], "item": r["item"], "option": r["id"].split(".", 2)[2] if r["id"].count(".") >= 2 else "yes", "points": int(r["v_points"]), "ref": r["ref"]}
    if numeric:
        out["min"] = number(low)   # a row without an upper limit (or, for a flag, without a lower one) simply leaves that key out
        out["min_inclusive"] = r["bound"] != "above"
        if high:
            out["max_exclusive"] = round(float(high) + step_of(high), 6)
    return out


def point_scores() -> list[dict]:
    items, bands = rows("score_items"), rows("score_bands")
    out = []
    for sid in ("air", "aas", "alvarado"):
        rs = [item_row(r) for r in items if r["score"] == sid]
        known = {r["item"] for r in rs}
        for group in REQUIRED[sid]:
            missing = [i for i in group if i not in known]
            if missing:
                raise SystemExit(f"{sid}: required item(s) {missing} are not rows of score_items.csv")
        used = {i for g in REQUIRED[sid] for i in g}
        extra = known - used
        if extra:
            raise SystemExit(f"{sid}: rows for {sorted(extra)} are in score_items.csv but not in the required list")
        bs = [{"band": r["band"], "min": int(r["v_low"]), **({"max": int(r["v_high"])} if r["v_high"].strip() else {}), "ref": r["ref"]} for r in bands if r["score"] == sid and r["band"] != "max"]
        mx = next(int(r["v_high"]) for r in bands if r["score"] == sid and r["band"] == "max")
        out.append({"id": sid, "title": TITLES[sid], "max": mx, "required": REQUIRED[sid], "items": rs, "bands": bs})
    return out


def facts() -> dict[str, dict]:
    return {r["id"]: r for r in rows("score_facts")}


def expect(table: dict[str, dict], fid: str, numbers: str, number: bool = False) -> str:
    """Return the register id of a fact row after checking its number list is exactly the one this script uses (or, with number=True, return the number itself)."""
    if table[fid]["v_number"].strip() != numbers:
        raise SystemExit(f"{fid}: score_facts.csv now says '{table[fid]['v_number']}', this script was written for '{numbers}'")
    return numbers if number else table[fid]["ref"]


def build() -> str:
    t = facts()
    lab = {r["id"]: r for r in rows("lab_bands")}
    for rid, low in (("creat.mild.all.1.5-1.9", "1.5"), ("creat.significant.all.2-2.9", "2"), ("creat.severe.x3.3-", "3"), ("creat.severe.abs.354-", "354")):
        if lab[rid]["v_low"].strip() != low:
            raise SystemExit(f"{rid}: lab_bands.csv now says {lab[rid]['v_low']}")
    doc = {
        "point_scores": point_scores(),
        "tg18_cholecystitis": {
            "grade_iii": {"creatinine_umol": 177, "inr": 1.5, "platelets": 100, "ref": expect(t, "tg18_cholecystitis.grade_iii", "2.0; 177; 1.5; 100")},
            "grade_ii": {"wbc": 18, "symptom_hours": 72, "ref": expect(t, "tg18_cholecystitis.grade_ii", "18; 72")},
            "refs": [t["tg18_cholecystitis.diagnosis"]["ref"], t["tg18_cholecystitis.grade_i"]["ref"]],
        },
        "tg18_cholangitis": {
            "inflammation": {"temperature_c": 38, "wbc_low": 4, "wbc_high": 10, "crp": 10, "ref": expect(t, "tg18_cholangitis.group_a", "38; 4; 10; 10")},
            "bile_blocked": {"bilirubin_umol": 34, "liver_tests_x_uln": 1.5, "ref": expect(t, "tg18_cholangitis.group_b", "2; 34; 1.5")},
            "refs": [t["tg18_cholangitis.diagnosis"]["ref"]],
        },
        "asge": {
            "high": {"bilirubin_umol": 68, "duct_mm": 6, "ref": expect(t, "asge.high", "68; 6")},
            "intermediate": {"age": 55, "ref": expect(t, "asge.intermediate", "55")},
            "refs": [t["asge.cholangitis_first"]["ref"], t["asge.low"]["ref"]],
        },
        "stopbang": {
            "items": ["snoring", "tired", "observed", "pressure", "bmi_over_35", "age_over_50", "neck_over_40", "male"],
            "bands": [{"band": "low", "min": 0, "max": 2}, {"band": "intermediate", "min": 3, "max": 4}, {"band": "high", "min": 5, "max": 8}],
            "ref": expect(t, "stopbang.bands", "0; 2; 3; 4; 5; 8"),
        },
        "aki": {
            "stage1_ratio": 1.5, "stage2_ratio": 2.0, "stage3_ratio": 3.0, "stage3_absolute_umol": 354,
            "rise_48h_umol": float(expect(t, "aki.rise_48h", "26.5", number=True)),
            "refs": [t["aki.rise_48h"]["ref"], lab["creat.mild.all.1.5-1.9"]["ref"], lab["creat.significant.all.2-2.9"]["ref"], lab["creat.severe.x3.3-"]["ref"], lab["creat.severe.abs.354-"]["ref"]],
        },
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/scores/scores.json is out of date; run tools/content/build_scores.py")
            return 1
        print("content/scores/scores.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
