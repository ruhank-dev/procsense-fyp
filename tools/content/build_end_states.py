"""Builds content/endstate/end_states.json: the five end states, what counts as an avoidable delay and as a safety failure, and the facts that surface later (p0-simulation-workflow-s17).

    uv run --project tools tools/content/build_end_states.py            # write
    uv run --project tools tools/content/build_end_states.py --check    # fail if the file is out of date

Source: content/end_states.csv (the five end states and their meaning, walkthrough Part 16, verified). What this script adds: which score items are an avoidable delay and that every critical item is a safety
failure (SR-2838), and the hidden facts that surface at a later moment if the trainee never asked, with their consequence (SR-2839): VAR-S1-D variant D2 (apixaban taken this morning) surfaces when the
anaesthetist reaches Sign In (walkthrough T02-WT1).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "endstate" / "end_states.json"

# score items that make a right outcome 'safe but delayed' (an avoidable delay)
DELAY_ITEMS = [
    "major.or_declared_ready_not_ready", "major.or_fault_ignored", "major.postponed_unnecessarily", "major.redirected_unnecessarily", "major.late_fasting_not_passed",
    "major.handover_repeat_omission", "major.hidden_fact_surfaced_late", "major.ward_signed_with_gaps", "major.checklist_item_skipped",
]
SURFACE_LATER = [
    {"id": "s1d.apixaban_this_morning", "case": "VAR-S1-D", "variant": "D2", "topic": "medicines", "fact": "He took his apixaban this morning, although he was told to stop it three days ago",
     "moment": "sign_in", "by": "anaesthetist", "effect": "stop", "ref": "SR-2839"},
]


def build() -> str:
    with open(CONTENT / "end_states.csv", encoding="utf-8", newline="") as f:
        states = [{"id": r["id"], "code": r["code"], "label": r["v_label"], "meaning": r["v_meaning"], "ref": r["ref"]} for r in csv.DictReader(f)]
    codes = [s["code"] for s in states]
    if codes != ["READY_SAFE", "POSTPONED_APPROPRIATELY", "REDIRECTED_APPROPRIATELY", "SAFE_BUT_DELAYED", "SAFETY_FAILURE"]:
        raise SystemExit(f"unexpected end states: {codes}")
    have = {p.stem for p in (CONTENT / "case_files").glob("*.json")}
    missing = sorted({e["case"] for e in SURFACE_LATER} - have)
    if missing:
        raise SystemExit(f"surface-later facts for cases that are not case files: {missing}")
    topics = json.loads((CONTENT / "history_topics.json").read_text(encoding="utf-8"))["topics"]["ids"]
    bad = [e["id"] for e in SURFACE_LATER if e["topic"] not in topics]
    if bad:
        raise SystemExit(f"surface-later facts on a topic the history does not have: {bad}")
    doc = {
        "states": states,
        "delay_items": DELAY_ITEMS,
        "failure_prefix": "critical.",
        "surface_later": SURFACE_LATER,
        "items": {"surfaced_late": "major.hidden_fact_surfaced_late"},
        "refs": ["SR-2838", "SR-2839"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/endstate/end_states.json is out of date; run tools/content/build_end_states.py")
            return 1
        print("content/endstate/end_states.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
