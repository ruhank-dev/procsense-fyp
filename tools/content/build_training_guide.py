"""Generates content/ui/training_guide.json (P3-TRN-01) and its goal-inactive reasons (the content half of P3-TRN-02).

    uv run --project tools tools/content/build_training_guide.py            # write
    uv run --project tools tools/content/build_training_guide.py --check    # exit 1 when the file is not what this builds

The bulletin board reads one entry per engine goal (content/workflow/workflow.json), per flow stage (content/flow/flow_maps.json, S1 and S2) and per ward
checklist item (content/ward/ward_checklist.json). The ids, step numbers, labels and source ids are copied from those files; the only authored text is the
how-to for each input mode, a short explanation and the reason a conditional goal is "not needed". It contains no clinical value, dose or threshold: the board
never decides completion, it only explains. Tablet tab names are those of content/ui/tablet.json.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "ui" / "training_guide.json"

SECTIONS = [
    ("identity", "Identity and arrival"), ("history", "History and observations"), ("examination", "Examination"), ("assessment", "Risk and anaesthetic assessment"),
    ("investigations", "Investigations and results"), ("consent", "Consent"), ("fasting_medicines", "Fasting and medicines"), ("ward_checklist", "Ward checklist"),
    ("theatre", "Theatre and WHO checklist"), ("handover", "Recovery and handover"), ("debrief", "Debrief"),
]
STAGE_SECTION = {1: "identity", 2: "identity", 3: "history", 4: "history", 5: "investigations", 6: "investigations", 7: "assessment", 8: "assessment", 9: "consent", 10: "assessment",
                 11: "theatre", 12: "theatre", 13: "theatre", 14: "theatre", 15: "theatre", 16: "theatre", 17: "handover", 18: "handover", 19: "handover", 20: "debrief"}
GOAL_SECTION = {"G-S1-IDENTITY": "identity", "G-S1-ALLERGY": "identity", "G-S1-REASON": "identity", "G-S1-ADULT": "identity", "G-FASTING": "fasting_medicines",
                "G-EXAM-PERMISSION": "examination", "G-EXAM-REGIONS": "examination", "G-EXAM-AIRWAY": "examination", "G-SENIOR-NOW": "examination",
                "G-RISK-ASA": "assessment", "G-RISK-VTE": "assessment", "G-ANAES-REVIEW": "assessment", "G-ANAES-PLAN": "assessment", "G-READY": "assessment",
                "G-TESTS": "investigations", "G-RESULTS": "investigations"}

# goal id -> (menu how-to, voice example sentence, explanation)
GOALS = {
    "G-S1-IDENTITY": ("Admission tab: Ask name and date of birth, Read wristband (tablet), Open notes, then Confirm identity.", "Can you tell me your full name and date of birth?", "Two identifiers must agree before anything else is done."),
    "G-S1-ALLERGY": ("Admission tab: Ask about allergies.", "Do you have any allergies, and what happened when you had a reaction?", "An unrecorded allergy is a common avoidable harm."),
    "G-S1-REASON": ("Admission tab: Ask why she is here.", "What operation are you here for today?", "The patient's own account must match the booking."),
    "G-S1-ADULT": ("Admission tab: Ask about home support.", "Is there an adult who can take you home and stay with you tonight?", "Day-case discharge needs a responsible adult."),
    "G-FASTING": ("Ask tab: Last food, then Last drink.", "When did you last eat, and when did you last drink?", "Fasting times are compared with the time of anaesthesia."),
    "G-EXAM-PERMISSION": ("Assess tab: Ask permission to examine (and Offer a chaperone).", "May I examine you now? I can ask a chaperone to be present.", "Permission is asked before touching the patient."),
    "G-EXAM-REGIONS": ("Assess tab: Examine, region by region, starting away from the painful area.", "I would like to examine your abdomen now.", "The examination findings feed the problem list."),
    "G-EXAM-AIRWAY": ("Assess tab: Airway check.", "Please open your mouth wide and move your neck for me.", "The airway is checked before anaesthesia."),
    "G-RISK-ASA": ("Assess tab: ASA grade.", "I propose an ASA grade for this patient.", "The grade summarises fitness for anaesthesia."),
    "G-RISK-VTE": ("Assess tab: VTE risk, every item of the form.", "Let me complete the clot and bleeding risk form.", "The form is complete only when every item is answered."),
    "G-ANAES-REVIEW": ("Assess tab: Ask the anaesthetist to review, then present the summary.", "Please call the anaesthetist; I will present the summary.", "The anaesthetist reviews a clear summary."),
    "G-ANAES-PLAN": ("Assess tab: Ask the anaesthetist to review, then agree the plan.", "Let us agree the anaesthetic plan.", "The plan is agreed with the anaesthetist, not decided alone."),
    "G-READY": ("Assess tab: Ready for theatre?, then choose the decision.", "I have made my readiness decision.", "The decision follows the problems found."),
    "G-TESTS": ("Orders tab: Order a test, only those this patient needs.", "Please order the tests this patient needs.", "Needed tests only: extra tests are not free."),
    "G-RESULTS": ("Results tab: View a result, for every result you ordered.", "Show me the results I ordered.", "An ordered result is only useful once it is read."),
    "G-SENIOR-NOW": ("Handover tab: Escalate (SBAR) to the senior doctor now.", "I am calling the senior doctor now.", "An emergency finding needs a senior at once."),
}

# goal id -> (reason id, why it is not needed)
INACTIVE = {
    "G-S1-ADULT": ("NOT_SCENARIO_S1", "This step belongs to the cholecystectomy day-case pathway; this case is not on it."),
    "G-EXAM-REGIONS": ("NO_EXAM_REGIONS", "This case has no examination regions to examine."),
    "G-EXAM-AIRWAY": ("NO_AIRWAY_ITEMS", "This case has no airway items to check."),
}

# ward item id -> (menu, voice, explanation, reason it can be not needed or None)
WARD = {
    "fasting": ("Checklist tab: Tick: Fasting times re-confirmed.", "Confirming fasting times with the patient.", "A breach goes to the anaesthetist.", None),
    "gown": ("Checklist tab: Tick: Hospital gown.", "Please put on the hospital gown.", "Clothing is changed before theatre.", None),
    "jewellery": ("Checklist tab: Tick: Jewellery and make-up.", "Please take off or tape your jewellery.", "Jewellery is removed or taped.", None),
    "aids": ("Checklist tab: Tick: Dentures, hearing aids, lenses and implants.", "Do you wear dentures, hearing aids or contact lenses?", "These are noted before theatre.", None),
    "hair": ("Checklist tab: Tick: Hair removal.", "Hair is removed only if needed, with clippers.", "Clippers, not a razor.", None),
    "site_marking": ("Checklist tab: Tick: Site marking.", "Is the site marked, or not applicable?", "Marked per policy where it applies.", "SITE_MARKING_NOT_APPLICABLE"),
    "stockings": ("Checklist tab: Tick: Stockings.", "Are stockings prescribed?", "Only if prescribed.", "STOCKINGS_NOT_PRESCRIBED"),
    "pregnancy": ("Checklist tab: Tick: Pregnancy status.", "Could you be pregnant?", "The status is recorded.", None),
    "consent": ("Checklist tab: Tick: Consent present and correct.", "Checking the consent form is present and correct.", "No consent, no theatre.", None),
    "bands": ("Checklist tab: Tick: Identity band.", "Checking your identity band.", "The band must be on.", None),
    "cbg": ("Checklist tab: Tick: Finger-prick sugar.", "Recording the finger-prick sugar.", "Needed for a diabetic patient only.", "NOT_DIABETIC"),
    "temperature": ("Checklist tab: Tick: Temperature.", "Checking your temperature.", "A cold patient is warmed first.", None),
    "documents": ("Checklist tab: Tick: All documents together.", "Checking all the documents are together.", "Notes, consent, results and drug chart travel with the patient.", None),
}
WARD_REASON_TEXT = {
    "SITE_MARKING_NOT_APPLICABLE": "Site marking is usually not applicable for a midline abdominal operation.",
    "STOCKINGS_NOT_PRESCRIBED": "Stockings are only needed when prescribed; this patient has none prescribed.",
    "NOT_DIABETIC": "This patient is not diabetic, so no finger-prick sugar is needed.",
}

# stage number -> (menu, voice, explanation)
STAGES = {
    1: ("Admission tab: Introduce yourself, then Read wristband (tablet).", "Hello, I am the doctor looking after you today.", "A calm start sets up the identity check."),
    2: ("Admission tab: identity and allergy checks.", "Can you tell me your name, date of birth and any allergies?", "Identity and allergy checks come before everything else."),
    3: ("Assess tab: Attach SpO2 probe and BP cuff, then measure.", "Let me check your observations.", "Observations show whether the patient is well today."),
    4: ("Ask tab: go through the history topics one by one.", "Tell me about your symptoms and your medicines.", "A hidden fact is only found by asking."),
    5: ("Orders tab: order the tests the patient needs.", "Please order the tests this patient needs.", "Only needed tests."),
    6: ("Results tab: open each result.", "Show me the results.", "Results are read before a decision."),
    7: ("Handover tab: Escalate (SBAR) or Say what worries you.", "This result needs action: I will escalate it.", "Abnormal findings change the plan."),
    8: ("Treat tab: start the optimisation.", "I will start optimising this problem.", "Fixable risks are treated before surgery."),
    9: ("Consent tab: explain, check understanding, then Sign the consent form.", "Do you understand the operation and agree to it?", "Consent is informed and signed."),
    10: ("Assess tab: Ask the anaesthetist to review.", "Please review the patient.", "The anaesthetist plans the anaesthetic."),
    11: ("Checklist tab: WHO Sign In.", "Starting the sign-in checklist.", "The sign-in checklist is done before anaesthesia."),
    12: ("Checklist tab: the Check: items for theatre preparation.", "Is theatre ready?", "Theatre is prepared in parallel."),
    13: ("Checklist tab: WHO Time Out.", "Starting the time out.", "The team pauses before the first cut."),
    14: ("Checklist tab: confirm theatre is ready (Theatre is ready).", "We are ready to start.", "The procedure follows a clean time out."),
    15: ("Treat tab: choose the response the event needs.", "I will deal with this event.", "A training event, not part of the real checklist."),
    16: ("Checklist tab: WHO Sign Out.", "Starting the sign-out checklist.", "Counts and specimens are confirmed."),
    17: ("Handover tab: hand over to recovery.", "Handing over to recovery.", "A structured handover keeps the information."),
    18: ("Handover tab: Escalate (SBAR).", "The patient is deteriorating: I will escalate.", "Early escalation saves time."),
    19: ("Handover tab: Escalate (SBAR) at once.", "This is an emergency: calling for help.", "Emergencies need immediate help."),
    20: ("Debrief tab: read the goals and the debrief.", "Show me the debrief.", "The debrief explains what was done and missed."),
}


def build() -> str:
    wf = json.loads((ROOT / "content/workflow/workflow.json").read_text(encoding="utf-8"))
    fm = json.loads((ROOT / "content/flow/flow_maps.json").read_text(encoding="utf-8"))
    wd = json.loads((ROOT / "content/ward/ward_checklist.json").read_text(encoding="utf-8"))
    entries = []
    for g in wf["goals"]:
        menu, voice, why = GOALS[g["id"]]
        entries.append({"id": g["id"], "kind": "goal", "section": GOAL_SECTION[g["id"]], "step": g["step"], "label": g["label"], "scenarios": ["S1", "S2"],
                        "how_to": {"menu": menu, "voice": {"text": g["hint"], "example": voice}}, "explanation": why, "source": g["refs"][0]})
    for m in fm["maps"]:
        for s in m["stages"]:
            menu, voice, why = STAGES[s["stage"]]
            entries.append({"id": s["id"], "kind": "stage", "section": STAGE_SECTION[s["stage"]], "step": s["stage"], "label": s["title"], "scenarios": [m["scenario"]],
                            "how_to": {"menu": menu, "voice": {"text": menu, "example": voice}}, "explanation": why, "source": s["refs"][0]})
    for i, it in enumerate(wd["items"], 1):
        menu, voice, why, _ = WARD[it["id"]]
        entries.append({"id": "ward." + it["id"], "kind": "ward_item", "section": "ward_checklist", "step": i, "label": it["label"], "scenarios": ["S1", "S2"],
                        "how_to": {"menu": menu, "voice": {"text": menu, "example": voice}}, "explanation": why, "source": it["ref"]})
    for n, e in enumerate(entries, 1):
        e["order"] = n
    inactive = [{"id": gid, "kind": "goal", "reason_id": r, "text": t, "section": GOAL_SECTION[gid]} for gid, (r, t) in INACTIVE.items()]
    inactive += [{"id": "ward." + k, "kind": "ward_item", "reason_id": v[3], "text": WARD_REASON_TEXT[v[3]], "section": "ward_checklist"} for k, v in WARD.items() if v[3]]
    doc = {"schema": "procsense.training_guide/1", "note": "Generated by tools/content/build_training_guide.py; do not edit by hand. The board reads the engine's goals, stages and checklist evidence and only explains them (P3-TRN-01); inactive_reasons are the wording for goals the engine lists as not needed (P3-TRN-02, content half).",
           "input_modes": ["menu", "voice"], "sections": [{"id": a, "order": i, "title": b} for i, (a, b) in enumerate(SECTIONS, 1)], "entries": entries, "inactive_reasons": inactive}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            print("content/ui/training_guide.json is missing or out of date: run `uv run --project tools tools/content/build_training_guide.py`")
            return 1
        print("training guide up to date")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
