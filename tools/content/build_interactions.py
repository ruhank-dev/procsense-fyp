"""Builds content/ui/interactions.json: the interaction catalogue behind the contextual panel (P3-CTX-01).

    uv run --project tools tools/content/build_interactions.py            # write
    uv run --project tools tools/content/build_interactions.py --check    # fail if the file is out of date or does not validate

Reads content/ui/tablet.json (written by build_tablet.py) and content/ai/spoken_actions.json. Every tablet entry gets a stable id
`<tab>.<verb>[.<param values>]`, an icon key, a confirm flag for consequential actions and voice examples (only for actions the spoken-action
list allows; physical and safety-critical actions are never spoken). Entries are then grouped per entity (patient, nurse, doctor, ...) following the
first version of the entity map in docs/plans/p3/p3-04-contextual-interaction-menus.md. No clinical rule lives here: availability still comes from the engine.
The validator (validate_interactions.py) requires every tablet entry to belong to at least one entity or to the global group.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / "content" / "ui"
OUT = UI / "interactions.json"
SPOKEN = ROOT / "content" / "ai" / "spoken_actions.json"

# Consequential actions: the panel asks "are you sure" before sending (p3-04).
CONFIRM = {"sign_consent", "sign_ward_checklist", "postpone", "declare_outcome", "redirect", "complete_time_out", "decide_ready"}

# Icon keys (names for the UI kit, P3-UIX; the artwork is not part of this file). First matching prefix wins.
ICONS = [("ask", "speech"), ("introduce", "wave"), ("read_back", "phone"), ("read_", "eye"), ("view_", "eye"), ("open_", "folder"), ("confirm", "check"),
         ("report", "alert"), ("request", "hand"), ("attach", "sensor"), ("detach", "sensor"), ("measure", "gauge"), ("count", "gauge"),
         ("assess", "clipboard"), ("compute", "clipboard"), ("examine", "hand-exam"), ("murphy", "hand-exam"), ("airway", "hand-exam"),
         ("offer", "person"), ("perform", "flask"), ("order", "flask"), ("take_sample", "flask"), ("send", "flask"), ("interpret", "magnifier"),
         ("acknowledge", "check"), ("answer", "phone"), ("consent", "document"), ("sign", "pen"), ("teach", "speech"), ("check", "check"),
         ("tick", "check"), ("prepare", "check"), ("declare", "flag"), ("escalate", "alert"), ("say_concern", "alert"), ("postpone", "pause"),
         ("redirect", "arrow"), ("tell", "speech"), ("hold", "pause"), ("document", "pen"), ("wait", "clock"), ("time_skip", "clock"),
         ("use_emergency", "alert"), ("decide", "flag"), ("summarise", "speech"), ("complete", "check")]

PATIENT_ADMISSION = ["introduce_self", "ask_identity", "read_name_to_patient", "read_wristband", "open_notes", "confirm_identity", "report_mismatch",
                     "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission", "ask_responsible_adult"]
PATIENT_ASSESS = ["attach_sensor", "detach_sensor", "measure_nibp", "measure_temperature", "count_rr", "assess_acvpu", "ask_pain_score", "ask_exam_permission",
                  "offer_chaperone", "examine", "murphy_test", "airway_check"]
# Entity map: (entity id, label, [(group id, group label, tablet tab or None for any tab, verbs or None for every verb of the tab)])
ENTITIES = [
    ("patient", "Patient", [
        ("admission", "Admission", "admission", PATIENT_ADMISSION),
        ("ask", "Ask", "ask", None),
        ("assess", "Assess", "assess", PATIENT_ASSESS),
        ("consent", "Consent", "consent", ["consent_discussion", "teach_back", "check_capacity", "sign_consent", "request_interpreter"])]),
    ("nurse", "Ward or ED nurse", [
        ("requests", "Requests and samples", None, ["request_wristband", "take_sample", "send_samples", "request_fluids", "request_oxygen", "request_iv_access",
                                                     "hold_medicine", "document_treatment", "check_treatment_timing"]),
        ("ward_checklist", "Ward checklist", "checklist", ["prepare", "tick_ward_item", "sign_ward_checklist"])]),
    ("doctor", "Doctor (registrar or surgeon)", [
        ("decisions", "Escalation and decisions", None, ["escalate", "say_concern", "declare_outcome", "postpone", "redirect", "decide_ready", "order_investigation",
                                                         "request_medication"])]),
    ("anaesthetist", "Anaesthetist", [
        ("review", "Review and risk", None, ["request_anaesthetic_review", "tell_anaesthetist", "assess_risk", "assess_vte", "compute_score"])]),
    ("porter", "Receptionist or porter", [("transport", "Transport", None, ["request_transport", "use_emergency_route"])]),
    ("monitor", "Monitor and bedside equipment", [
        ("equipment", "Sensors and tests", None, ["attach_sensor", "detach_sensor", "measure_nibp", "perform_bedside_test"])]),
    ("records", "Records (notes, results, phone)", [
        ("records", "Notes, results and phone", None, ["open_notes", "view_result", "interpret_result", "interpret_imaging", "acknowledge_result", "answer_phone",
                                                       "confirm_patient_on_call", "read_back"])]),
    ("theatre_team", "Theatre team", [
        ("who_checks", "Stations and WHO checklist", "checklist", ["check_station", "checklist_item", "complete_time_out", "declare_or_ready"])]),
]
GLOBAL_VERBS = ["wait", "time_skip"]


def entry_id(tab: str, e: dict) -> str:
    return ".".join([tab, e["verb"], *[e["params"][k] for k in sorted(e["params"])]])


def icon(verb: str) -> str:
    return next((i for p, i in ICONS if verb.startswith(p)), "dot")


def build() -> str:
    tablet = json.loads((UI / "tablet.json").read_text(encoding="utf-8"))
    spoken = {a["verb"] for a in json.loads(SPOKEN.read_text(encoding="utf-8"))["actions"]}
    entries = []
    for tab in tablet["tabs"]:
        for e in tab["entries"]:
            label = e["label"]
            low = label[0].lower() + label[1:]
            entries.append({"id": entry_id(tab["id"], e), "tab": tab["id"], "verb": e["verb"], "params": e["params"], "label": label, "icon": icon(e["verb"]),
                            "confirm": e["verb"] in CONFIRM, "has_form": "fields" in e,
                            "voice": {"spoken": e["verb"] in spoken, "examples": [label, f"Please {low}"] if e["verb"] in spoken else []}})
    entities = []
    for eid, elabel, groups in ENTITIES:
        gs = []
        for gid, glabel, tab, verbs in groups:
            ids = [x["id"] for x in entries if (tab is None or x["tab"] == tab) and (verbs is None or x["verb"] in verbs)]
            gs.append({"id": gid, "label": glabel, "entries": ids})
        entities.append({"id": eid, "label": elabel, "groups": gs})
    glob = [x["id"] for x in entries if x["verb"] in GLOBAL_VERBS]
    doc = {"schema": "procsense.interactions/1", "note": "Generated by tools/content/build_interactions.py from content/ui/tablet.json; do not edit by hand. No clinical rule lives here.",
           "entities": entities, "global": {"id": "wrist_menu", "label": "Wrist menu", "entries": glob}, "entries": entries}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import validate_interactions as v
    text = build()
    if "--check" in argv:
        have = OUT.read_text(encoding="utf-8").replace("\r\n", "\n") if OUT.exists() else ""
        if have != text:
            print("FAIL  content/ui/interactions.json is out of date; run tools/content/build_interactions.py")
            return 1
        bad = v.problems()
        for b in bad:
            print("FAIL ", b)
        if bad:
            return 1
        print(f"ok    interaction catalogue up to date ({len(json.loads(text)['entries'])} entries)")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
