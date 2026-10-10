"""Builds content/investigations/normal_results.json: the normal results the engine may supply when a case states none (p0-clinical-logic-s07, DT-067).

    uv run --project tools tools/content/build_normal_results.py            # write
    uv run --project tools tools/content/build_normal_results.py --check    # fail if the file is out of date

Only where that is safe. A planned (S1) patient with no jaundice has normal liver tests, so the liver tests are supplied for S1 cases that state none (the cases that do state liver tests keep their own).
The emergency (S2) blood tests are never supplied: a patient with appendicitis or sepsis does not have a normal CRP or white cell count, so a normal result would contradict the story; those results
have to be written into the case (DT-067). Each value is the middle of the verified normal range in content/lab_bands.csv for the patient's sex (rounded to a whole number), labelled generated.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "investigations" / "normal_results.json"

# test -> (scenario it is supplied for, [(name shown, lab_bands analyte)])
SUPPLY = {"lft": ("S1", [("Bilirubin", "bilirubin"), ("ALT", "alt"), ("ALP", "alp"), ("GGT", "ggt"), ("Albumin", "albumin")])}


def build() -> str:
    with open(CONTENT / "lab_bands.csv", encoding="utf-8", newline="") as f:
        bands = [r for r in csv.DictReader(f) if r["zone"] == "normal" and not r["basis"] and not r["v_state"]]
    tests = {}
    for test, (scenario, analytes) in SUPPLY.items():
        rows = []
        for shown, analyte in analytes:
            for sex in ("F", "M"):
                hits = [b for b in bands if b["analyte"] == analyte and b["sex"] in ("", sex)]
                if len(hits) != 1:
                    raise SystemExit(f"lab_bands.csv: {analyte} for sex {sex} has {len(hits)} normal rows (it must be exactly 1)")
                b = hits[0]
                low = float(b["v_low"]) if b["v_low"].strip() else 0.0
                high = float(b["v_high"])
                value = int(round((low + high) / 2))
                rng = f"{b['v_low'].strip() or '0'}-{b['v_high'].strip()}" if b["bound"] != "below" else f"below {b['v_high'].strip()}"
                rows.append({"id": f"gen.{test}.{analyte}", "analyte": shown, "sex": sex, "value": str(value), "unit": b["unit"], "range": rng, "ref": b["ref"]})
        tests[test] = {"scenario": scenario, "results": rows}
    return json.dumps({"tests": tests}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/investigations/normal_results.json is out of date; run tools/content/build_normal_results.py")
            return 1
        print("content/investigations/normal_results.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
