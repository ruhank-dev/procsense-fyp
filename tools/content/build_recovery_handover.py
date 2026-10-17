"""Builds content/handover/recovery_required.json: the SBAR items of the handover to the recovery nurse after the WHO Sign Out (p1-theatre-extension-s03).

    uv run --project tools tools/content/build_recovery_handover.py            # write
    uv run --project tools tools/content/build_recovery_handover.py --check    # fail if the file is out of date

Source: the walkthrough's C.24 'Recovery handover & debrief' (continuity of care; the trainee hands over the key concerns; the named incorrect action is 'omits antibiotic plan'; the recovery nurse is the escalation) and
Step 15 (the recovery bay reuses the patient bay layout, approved in the phase review). The items follow the same SBAR shape as the Step 12 handover (tools/content/build_handover.py) so the same subsystem logic
applies, but ask for what recovery needs: the procedure and findings, the specimen and the counts, drains, the analgesia, antiemetic and antibiotic plans, and the concerns. Which items are required, and how bad an
omission is, are design values of the project team (SR-4130, provisional, a ProcSense educational extension). The trainee can hand over only what they know: each item has a rule over the state that says whether the
fact is known, using the Sign Out flags (signout.*) the safety checklist sets.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "handover" / "recovery_required.json"
REF = "SR-4130"


def has(flag: str) -> dict:
    return {"in": [flag, {"var": "flags"}]}


def item(id, section, label, known, severity="minor"):
    return {"id": id, "section": section, "label": label, "known_if": known, "required_in": ["S1", "S2"], "severity": severity, "ref": REF}


ITEMS = [
    item("identity", "s", "Who the patient is: name, age and date of birth", {"var": "identity_confirmed"}),
    item("procedure", "s", "What was done (the procedure that was recorded at Sign Out)", has("signout.procedure_recorded"), "major"),
    item("allergies", "b", "Allergies (and the reaction)", {"var": "allergies_known"}, "critical"),
    item("findings", "b", "Operative findings and anything unexpected", has("signout.procedure_recorded"), "major"),
    item("specimen", "b", "The specimen: what it is and that it was labelled and read aloud", has("signout.specimen_read")),
    item("counts", "b", "Counts correct (or how a discrepancy was resolved)", has("signout.counts_complete"), "major"),
    item("drains", "b", "Drains and wounds", has("signout.procedure_recorded")),
    item("observations", "a", "Latest observations and NEWS2", has("news2.computed")),
    item("analgesia_plan", "a", "Analgesia plan", has("signout.recovery_briefed"), "major"),
    item("antiemetic_plan", "a", "Antiemetic plan", has("signout.recovery_briefed")),
    item("antibiotic_plan", "a", "Antibiotic plan: what was given and when the next dose is due", has("signout.recovery_briefed"), "major"),
    item("concerns", "r", "Key concerns for recovery and what to watch for", has("signout.recovery_briefed"), "major"),
]
SECTIONS = ["s", "b", "a", "r"]


def build() -> str:
    ids = [i["id"] for i in ITEMS]
    if len(set(ids)) != len(ids):
        raise SystemExit("duplicate item ids")
    doc = {
        "sections": SECTIONS,
        "items": ITEMS,
        "scored": {"complete": "credit.recovery_handover_complete", "critical": "critical.recovery_handover_omitted", "major": "major.recovery_handover_omitted", "minor": "minor.recovery_handover_omitted",
                   "repeat": "major.recovery_handover_repeat_omission"},
        "refs": [REF],
    }
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
            print("FAIL  content/handover/recovery_required.json is out of date; run tools/content/build_recovery_handover.py")
            return 1
        print("content/handover/recovery_required.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
