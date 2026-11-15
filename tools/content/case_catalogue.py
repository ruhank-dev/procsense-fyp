"""Generates content/ui/case_catalogue.json from content/case_files (P3-SCN-02): what the headset's scenario picker reads.

    uv run --project tools tools/content/case_catalogue.py            # write
    uv run --project tools tools/content/case_catalogue.py --check    # exit 1 when the file is not what the case files give

Procedure: S1 = laparoscopic cholecystectomy preparation, S2 = appendicectomy preparation (the case files' `scenario`).
`stacked` marks the stacked cases (ids STK-*), which a beginner is not given. `avatar_profile` is the key `<sex>-<age band>` that
P3-AST-03 maps to a patient avatar; it is a key, not a claim that the avatar exists. The status is copied as it is: nothing here approves a case.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CASES = ROOT / "content" / "case_files"
OUT = ROOT / "content" / "ui" / "case_catalogue.json"
PROCEDURE = {"S1": "cholecystectomy", "S2": "appendicectomy"}


def age_band(age: int) -> str:
    return "under-18" if age < 18 else "18-39" if age < 40 else "40-64" if age < 65 else "65-plus"


def label(text: str) -> str:
    text = re.sub(r"^SCENARIO CARD \d+ \(S\d\)\s*-\s*", "", text.strip())
    return re.sub(r"^Example Patient [A-Z]\s*-\s*", "", text)


def build() -> str:
    rows = []
    for path in sorted(CASES.glob("*.json"), key=lambda p: p.stem):
        d = json.loads(path.read_text(encoding="utf-8"))
        sex, age = d["identity"]["sex"], int(d["identity"]["age"])
        rows.append({
            "id": d["id"], "procedure": PROCEDURE[d["scenario"]], "scenario": d["scenario"], "tier": d["tier"], "status": d["status"],
            "stacked": d["id"].startswith("STK-"), "setting": d["presentation"]["setting"], "sex": sex, "age_band": age_band(age),
            "label": label(d["walkthrough_label"]), "avatar_profile": f"{sex}-{age_band(age)}",
        })
    rows.sort(key=lambda r: r["id"])
    return json.dumps({"schema": "procsense.case_catalogue/1", "cases": rows}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        have = OUT.read_text(encoding="utf-8").replace("\r\n", "\n") if OUT.exists() else ""
        if have != text:
            print("FAIL  content/ui/case_catalogue.json is not what the case files give; run tools/content/case_catalogue.py")
            return 1
        print(f"ok    case catalogue up to date ({text.count(chr(34) + 'id' + chr(34))} cases)")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
