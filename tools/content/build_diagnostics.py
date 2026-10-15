"""Builds content/diagnostics/diagnostics.json: how a sample travels to the laboratory and when the nurse mentions tubes left at the bedside (p0-diagnostic-transitions-s04).

    uv run --project tools tools/content/build_diagnostics.py            # write
    uv run --project tools tools/content/build_diagnostics.py --check    # fail if the file is out of date

Simulator design values of the project team (type D, provisional, tuned with testers; no source gives them), registered as SR-4120 to SR-4122:
- the two ways a labelled sample goes to the laboratory: the pneumatic tube station (it arrives after a short transit) or the porter (who comes after the porter arrival time
  already in content/locations/locations.json, then walks to the laboratory);
- in practice mode at the beginner level, the nurse points out samples taken but not sent after a while (the beginner safety net of the walkthrough's Step 6; never in assessment).
The laboratory's own checks (an unlabelled or mislabelled tube is rejected, or gives someone else's result) stay where they are: content/investigations and the sample-quality table.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "diagnostics" / "diagnostics.json"


def build() -> str:
    doc = {
        "dispatch": [
            {"id": "tube_station", "label": "Pneumatic tube station", "transit_minutes": 2, "porter": False, "ref": "SR-4120"},
            {"id": "porter", "label": "Hand to the porter", "transit_minutes": 5, "porter": True, "ref": "SR-4120"},
        ],
        "waiting_prompt": {
            "after_minutes": 10,
            "practice_beginner_only": True,
            "text": "There are labelled tubes on the bed table that have not gone to the lab yet.",
            "ref": "SR-4121",
        },
        "send_rule": {"default": "now", "later": "later", "ref": "SR-4122"},
        "refs": ["SR-4120", "SR-4121", "SR-4122"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def registered() -> set[str]:
    with open(ROOT / "docs" / "research" / "source_register.csv", encoding="utf-8", newline="") as f:
        return {r["id"] for r in csv.DictReader(f)}


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        missing = sorted(set(json.loads(text)["refs"]) - registered())
        if missing:
            print(f"FAIL  {OUT.relative_to(ROOT).as_posix()} cites register rows that do not exist: {', '.join(missing)}")
            return 1
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/diagnostics/diagnostics.json is out of date; run tools/content/build_diagnostics.py")
            return 1
        print("content/diagnostics/diagnostics.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
