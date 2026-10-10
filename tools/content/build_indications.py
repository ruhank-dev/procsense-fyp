"""Builds content/investigations/indications.json: when each test is indicated for a planned (S1) or an emergency (S2) patient (p0-clinical-logic-s06).

    uv run --project tools tools/content/build_indications.py            # write
    uv run --project tools tools/content/build_indications.py --check    # fail if the file is out of date

The kinds come from the verified table content/test_indications.csv (walkthrough Step 6): always, conditional, not_routine, per_policy, not_used. The words of each condition
('If 65+, diabetes, kidney disease...') stay in the table for the debrief; the engine uses the kind. The script refuses a kind it does not know and a test that content/investigations/investigations.json
does not define.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "investigations" / "indications.json"
KINDS = {"always", "conditional", "not_routine", "per_policy", "not_used"}


def build() -> str:
    with open(CONTENT / "test_indications.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    tests = {t["id"] for t in json.loads((CONTENT / "investigations" / "investigations.json").read_text(encoding="utf-8"))["tests"]}
    out = []
    for r in rows:
        for k in ("d_s1_kind", "d_s2_kind"):
            if r[k] not in KINDS:
                raise SystemExit(f"test_indications.csv: {r['id']} has the kind '{r[k]}', which this script does not know")
        if r["test"] not in tests:
            raise SystemExit(f"test_indications.csv: the test '{r['test']}' is not defined in content/investigations/investigations.json")
        out.append({"test": r["test"], "s1": r["d_s1_kind"], "s2": r["d_s2_kind"], "why": r["v_why"], "when_s1": r["v_s1"], "when_s2": r["v_s2"], "ref": r["ref"]})
    return json.dumps({"indications": out}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/investigations/indications.json is out of date; run tools/content/build_indications.py")
            return 1
        print("content/investigations/indications.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
