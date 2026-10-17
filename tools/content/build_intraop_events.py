"""Builds content/extension/intra_op_events.json: the intra-operative training events of the ProcSense educational extension (p1-theatre-extension-s02).

    uv run --project tools tools/content/build_intraop_events.py            # write
    uv run --project tools tools/content/build_intraop_events.py --check    # fail if the file is out of date

The operation itself is out of scope (walkthrough 2.3): after the Time Out the procedure is abstracted as a time-lapse, and one labelled event can interrupt it. The walkthrough's C.22 gives the pattern: 'e.g.
conversion to open, bleeding needing suction, normal appendix found (the surgeon's decisions; the trainee observes and communicates)'; trainee action 'observe; respond to team requests'; correct action 'smooth
teamwork'; incorrect action 'freezing / no communication'; missed abnormality goes to the debrief; escalation to the surgeon or anaesthetist; next step Sign Out. Each event is a communication or teamwork moment, never
a surgical decision: the trainee relays, fetches, asks or escalates; what the surgeon decides is not scored. Every event carries the visible label 'ProcSense educational extension', is awaiting a clinician's review
(provisional, practice mode only: it never appears in assessment) and is a design value of the project team (SR-4131).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "extension" / "intra_op_events.json"
REF = "SR-4131"
LABEL = "ProcSense educational extension"


def event(id, title, applies_to, cue, responses):
    return {"id": id, "title": title, "label": LABEL, "applies_to": applies_to, "cue": cue, "deadline_steps": 6, "approval": "awaiting_clinician", "practice_only": True,
            "responses": [{"id": r[0], "label": r[1], "correct": r[2]} for r in responses], "ref": REF}


EVENTS = [
    event("adhesions_open_set", "The surgeon asks for the open set", ["S1"], "The surgeon says dense adhesions make the keyhole view poor and asks for the open set.",
          [("ask_open_set", "Ask the scrub nurse to open the open set and say so aloud", True), ("tell_anaesthetist", "Tell the anaesthetist the operation may be converted to open", True),
           ("carry_on", "Say nothing and carry on", False)]),
    event("bleeding_suction", "Bleeding that needs suction", ["S1", "S2"], "There is bleeding at the operative site and the surgeon asks for suction.",
          [("check_suction", "Check the suction is working and tell the team it is ready", True), ("tell_anaesthetist_bleeding", "Tell the anaesthetist there is bleeding", True),
           ("wait", "Wait to be asked again", False)]),
    event("unexpected_stone", "An unexpected finding", ["S1"], "The surgeon reports an unexpected finding, a stone in the duct, and says the plan may change.",
          [("relay_finding", "Acknowledge it and relay it to the anaesthetist and the circulating nurse", True), ("note_for_handover", "Say you will include it in the Sign Out and recovery handover", True),
           ("ignore", "Carry on without acknowledging it", False)]),
    event("normal_appendix", "A normal-looking appendix", ["S2"], "The surgeon says the appendix looks normal and decides to remove it anyway.",
          [("label_specimen", "Make sure the specimen will be labelled and sent, and say so", True), ("relay_finding", "Relay the finding to the anaesthetist and note it for the handover", True),
           ("ignore", "Carry on without acknowledging it", False)]),
]


def build() -> str:
    ids = [e["id"] for e in EVENTS]
    if len(set(ids)) != len(ids):
        raise SystemExit("duplicate event ids")
    for e in EVENTS:
        if not any(r["correct"] for r in e["responses"]) or all(r["correct"] for r in e["responses"]):
            raise SystemExit(f"{e['id']}: needs at least one correct and one wrong response")
    doc = {"label": LABEL, "events": EVENTS, "scored": {"respond": "credit.intraop_response", "wrong": "minor.intraop_wrong_response", "freeze": "major.intraop_no_response"}, "refs": [REF]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def registered() -> set[str]:
    import csv
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
            print("FAIL  content/extension/intra_op_events.json is out of date; run tools/content/build_intraop_events.py")
            return 1
        print("content/extension/intra_op_events.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
