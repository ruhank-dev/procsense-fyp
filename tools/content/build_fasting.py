"""Builds content/fasting/fasting.json: the fasting times the rule uses (p0-clinical-logic-s15).

    uv run --project tools tools/content/build_fasting.py            # write
    uv run --project tools tools/content/build_fasting.py --check    # fail if the file is out of date

The hours come from content/fasting_rules.csv (walkthrough Part 9, ASA 2017 and 2023 update): clear liquids 2, milk 6, light meal 6, heavy meal 8. The script checks that each hour it copies is the
number the table holds, so a changed table fails the build. When what the patient ate is not known (kind "unknown"), the rule asks for the longest fast in the table, which is derived here, not typed.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "fasting" / "fasting.json"

CLASSES = {"clear_liquids": "clear_liquids", "milk": "milk", "light_meal": "light_meal", "heavy_meal": "heavy_meal"}   # kind in the case files -> row id in fasting_rules.csv
EXPECTED = {"clear_liquids": 2, "milk": 6, "light_meal": 6, "heavy_meal": 8}


def build() -> str:
    with open(ROOT / "content" / "fasting_rules.csv", encoding="utf-8", newline="") as f:
        rows = {r["id"]: r for r in csv.DictReader(f)}
    classes = {}
    for kind, rid in CLASSES.items():
        hours = float(rows[rid]["v_hours"])
        if hours != EXPECTED[kind]:
            raise SystemExit(f"{rid}: fasting_rules.csv now says {hours} hours, this script was written for {EXPECTED[kind]}")
        classes[kind] = {"hours": hours, "ref": rows[rid]["ref"]}
    doc = {
        "classes": classes,
        "unknown_kind_hours": max(c["hours"] for c in classes.values()),
        "refs": [rows["breach"]["ref"], rows["emergency_full_stomach"]["ref"], rows["chewing_gum"]["ref"], rows["regular_tablets"]["ref"]],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/fasting/fasting.json is out of date; run tools/content/build_fasting.py")
            return 1
        print("content/fasting/fasting.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
