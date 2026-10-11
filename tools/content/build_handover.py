"""Builds content/handover/handover_required.json: the SBAR items the trainee can hand over, which a planned or an emergency case must include, and how an omission is scored (p0-simulation-workflow-s15).

    uv run --project tools tools/content/build_handover.py            # write
    uv run --project tools tools/content/build_handover.py --check    # fail if the file is out of date

Source: the walkthrough's Step 12 (SBAR = Situation, Background, Assessment, Recommendation; the planned example 'S: name, age, for lap chole. B: no medical problems, no allergies. A: obs normal, fasting times, LFTs,
pregnancy test, consent. R: ready for theatre'; the emergency case 'must include aspiration risk, antibiotic time, fluids given, latest NEWS2, lactate, kidney status, pregnancy status, scan result'; the abnormal list
'missing item (allergy, fasting time, antibiotic time, blood thinner)'), registered as SR-2834. The severity of an omission is a design value (SR-2835). The trainee can only hand over what they know:
each item has a rule over the state (JsonLogic over the keys the engine gives every rule) that says whether the fact is known.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "handover" / "handover_required.json"


def has(flag: str) -> dict:
    return {"in": [flag, {"var": "flags"}]}


def topic(t: str) -> dict:
    return {"in": [t, {"var": "topics"}]}


def item(id, section, label, known, required_in, severity="minor"):
    return {"id": id, "section": section, "label": label, "known_if": known, "required_in": required_in, "severity": severity, "ref": "SR-2834"}


BOTH = ["S1", "S2"]
ITEMS = [
    item("identity", "s", "Who the patient is: name, age and date of birth", {"var": "identity_confirmed"}, BOTH),
    item("reason", "s", "Why the patient is here and the operation planned", topic("complaint"), []),
    item("allergies", "b", "Allergies (and the reaction)", {"var": "allergies_known"}, BOTH, "critical"),
    item("history", "b", "Relevant medical history", topic("past_history"), ["S1"]),
    item("blood_thinner", "b", "Medicines, especially any blood thinner and when it was last taken", topic("medicines"), [], "major"),
    item("observations", "a", "Latest observations and NEWS2", has("news2.computed"), BOTH),
    item("fasting", "a", "Fasting times (or that they are uncertain)", {"!=": [{"var": "fasting"}, "unknown"]}, BOTH, "critical"),
    item("results", "a", "Results: blood tests, lactate, kidney status and the scan", has("results.any_viewed"), BOTH),
    item("pregnancy", "a", "Pregnancy status", {"!": [has("pregnancy.status_required")]}, BOTH),
    item("consent", "a", "Consent signed", has("consent.signed"), ["S1"]),
    item("aspiration_risk", "a", "Aspiration risk (full stomach, uncertain fasting)", {"!=": [{"var": "fasting"}, "unknown"]}, ["S2"], "critical"),
    item("antibiotic", "a", "Antibiotic given: name and time", has("treatment.antibiotic_given"), ["S2"], "major"),
    item("fluids", "a", "Fluids given", has("treatment.fluids_given"), ["S2"]),
    item("recommendation", "r", "What you need or the plan: ready for theatre, antibiotic per protocol", True, BOTH),
]
SECTIONS = ["s", "b", "a", "r"]


def build() -> str:
    ids = [i["id"] for i in ITEMS]
    if len(set(ids)) != len(ids):
        raise SystemExit("duplicate item ids")
    bad = [i["id"] for i in ITEMS if i["section"] not in SECTIONS]
    if bad:
        raise SystemExit(f"items in a section that does not exist: {bad}")
    doc = {
        "sections": SECTIONS,
        "items": ITEMS,
        "scored": {"complete": "credit.handover_complete", "critical": "critical.handover_omitted", "major": "major.handover_omitted", "minor": "minor.handover_omitted", "repeat": "major.handover_repeat_omission"},
        "refs": ["SR-2834", "SR-2835"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/handover/handover_required.json is out of date; run tools/content/build_handover.py")
            return 1
        print("content/handover/handover_required.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
