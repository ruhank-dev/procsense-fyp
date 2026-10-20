"""Builds tests/safety_challenge/regression_cases.json (p1-ai-language-s16): the must-not-credit sentences that the UNCHANGED 4a2ed00 implementation credited (its engine-accepted failures on the development and challenge sets), with the recorded view that credited them.
`tools/.venv/Scripts/python.exe tools/bench/safety_regression.py [--check]`. These are the permanent regression cases of the text layer: tools/ci/test_safety_hardening.py requires the assertion layer to name each of them as not performed. The challenge rows entered this file after the first look at the
challenge split and were then used for development (so they are regression cases, no longer independent evidence); the final split never enters it."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / "reports" / "ai" / "safety"
OUT = ROOT / "tests" / "safety_challenge" / "regression_cases.json"


def build() -> dict:
    rows = []
    for split in ("dev", "challenge"):
        data = json.loads((REPORTS / f"baseline-4a2ed00-{split}.json").read_text(encoding="utf-8"))
        for f in data["failures"]:
            rows.append({"id": f["id"], "split": split, "category": f["category"], "template": f["template"], "text": f["text"], "act": f["act"], "credited_by_4a2ed00_in": [v["view"] for v in f["views"]]})
    return {"note": "must-not-credit sentences that 4a2ed00 credited through the engine (tools/bench/safety_eval.py baseline run); each must now be named as not performed by the assertion layer. Synthetic, builder-written, provisional labels.", "baseline_commit": "4a2ed00", "rows": rows}


def main(argv):
    doc = build()
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("regression_cases.json is out of date; run tools/bench/safety_regression.py")
            return 1
        print("regression_cases.json is current")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix(), len(doc["rows"]), "rows")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
