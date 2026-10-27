"""Builds content/ui/tablet.json: the tabs, buttons and forms of the hand-held tablet (p0-vr-interaction-ui-s03).

    uv run --project tools tools/content/build_tablet.py            # write
    uv run --project tools tools/content/build_tablet.py --check    # fail if the file is out of date or a P0 correct path needs an action the tablet cannot make

Unity draws the tablet from this file and holds no clinical text: every list (history topics, tests, tubes, regions, ward and WHO checklist items, theatre
stations, consent topics, treatments, outcomes, roles, transport choices, risk and score items) is read here from the content tables, so the values are the
ones the engine accepts. The short button and field labels that no content table holds are written below as interface text (LABELS).

Panel order follows the walkthrough steps, so a trainee meets the tabs in the real sequence. The check replays every action of the P0 correct paths
(tests/golden/*_correct.actions.json) against the tablet: each one must be a button, or a form whose fields accept the action's values.

Field kinds: choice (one option), text (free text; the trainee says or types it), flags (each item yes or no, sent as one parameter "id:yes,id:no"),
booleans (each item a parameter of its own, "true" or "false"), multi (several options sent as "a,b"), state (filled from what the session shows,
for example the investigations that have results). A choice or text field marked optional may be left unset and is then not sent.
"""
from __future__ import annotations

import glob
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "ui" / "tablet.json"
GOLDEN = ROOT / "tests" / "golden"


def load(rel: str):
    return json.loads((CONTENT / rel).read_text(encoding="utf-8"))


# Interface text for values that no content table labels (button words, not clinical facts).
LABELS = {
    "consent": {"diagnosis": "Explain the diagnosis", "operation": "Explain the operation", "benefits": "Explain the benefits", "risks": "Explain the risks",
                "alternatives": "Explain the alternatives", "no_treatment": "Explain having no treatment", "matters": "Ask what matters to the patient"},
    "tubes": {"edta_purple": "Purple (EDTA)", "serum_gold": "Gold (serum)", "citrate_blue": "Blue (citrate)", "grey_fluoride": "Grey (fluoride)", "pink_edta": "Pink (EDTA, blood bank)"},
    "sbar": {"s": "Situation", "b": "Background", "a": "Assessment", "r": "Recommendation"},
    "responses": {"yes": "Yes", "no": "No", "na": "Not applicable"},
    "asa": {"1": "ASA 1", "2": "ASA 2", "3": "ASA 3", "4": "ASA 4", "5": "ASA 5"},
    "yes_no": {"true": "Yes", "false": "No"},
    "modes": {"walk": "Walking", "wheelchair": "Wheelchair", "trolley": "Trolley"},
    "escort": {"porter": "Porter", "nurse": "Nurse", "doctor": "Doctor"},
    "monitoring": {"none": "No monitoring", "portable": "Portable monitor", "full": "Full monitoring"},
    "airway": {"mallampati": "Mallampati (look in the mouth)", "mouth_opening": "Mouth opening", "neck": "Neck movement"},
    "stopbang": {"snoring": "Snores loudly", "tired": "Tired in the day", "observed": "Stops breathing in sleep (observed)", "pressure": "High blood pressure",
                 "bmi_over_35": "BMI over 35", "age_over_50": "Age over 50", "neck_over_40": "Neck over 40 cm", "male": "Male"},
    "interpretations": {"normal": "Normal", "abnormal": "Abnormal", "artefact": "Artefact (repeat the sample)"},
    "imaging_categories": {"normal": "Normal", "positive": "Shows the expected finding", "complicated": "Shows a complication", "other_diagnosis": "Points to another diagnosis",
                           "non_diagnostic": "Not diagnostic (not seen clearly)"},
    "critical_actions": {"repeat_sample": "Repeat the sample", "treat_and_repeat": "Treat and repeat", "escalate_senior": "Escalate to a senior",
                         "order_ecg": "Order an ECG", "review_patient": "Review the patient", "postpone_surgery": "Postpone the operation"},
    "answers": {"approved": "Answer honestly", "false_reassurance": "Reassure that nothing can go wrong", "dismissive": "Say you will tell them later"},
}
SENSORS = [("spo2", "SpO2 probe"), ("nibp_cuff", "BP cuff")]


def opt(value, label):
    return {"value": str(value), "label": label}


def button(label, verb, enabled="always", **params):
    e = {"label": label, "verb": verb, "params": {k: str(v) for k, v in params.items()}, "enabled": enabled}
    return e


def form(label, verb, fields, **params):
    return {"label": label, "verb": verb, "params": {k: str(v) for k, v in params.items()}, "enabled": "always", "fields": fields}


def choice(key, label, options):
    return {"key": key, "label": label, "kind": "choice", "options": options}


def text(key, label):
    return {"key": key, "label": label, "kind": "text"}


def region_label(r: str) -> str:
    return {"abdomen_later": "Abdomen (again, later)", "allergy_band": "Allergy band", "ecg": "Heart tracing on the monitor", "crt": "Capillary refill"}.get(r, r.replace("_", " ").capitalize())


def build() -> dict:
    spoken = {a["id"]: a["label"] for a in load("ai/spoken_actions.json")["actions"]}
    topics = load("history_topics.json")["topics"]["ids"]
    inv = load("investigations/investigations.json")
    tests = [(t["id"], t["label"], t["kind"]) for t in inv["tests"]]
    ward = sorted(load("ward/ward_checklist.json")["items"], key=lambda i: i["order"])
    stations = sorted(load("or/or_requirements.json")["stations"], key=lambda s: s["order"])
    who = load("checklist/safety_checklist.json")
    consent = load("consent/consent.json")
    decision = load("decision/decision.json")
    risk = load("risk/risk.json")
    scores = load("scores/scores.json")
    treatments = [t for t in load("treatments.json")["treatments"]]
    loc = load("locations/locations.json")

    regions, airway = set(), set()
    for f in sorted((CONTENT / "case_files").glob("*.json")):
        s = f.read_text(encoding="utf-8")
        for part in s.split('"examine:')[1:]:
            regions.add(part.split('"', 1)[0])
        for part in s.split('"airway_check:')[1:]:
            airway.add(part.split('"', 1)[0])
    regions = sorted(regions, key=lambda r: (r not in ("general", "abdomen", "chest", "heart"), r))

    tabs = []
    tabs.append({"id": "admission", "label": "Admission", "step": 1, "entries": [
        button(spoken["introduce_self"], "introduce_self"),
        button(spoken["ask_identity"], "ask_identity"),
        button("Read name to patient", "read_name_to_patient"),
        button("Read wristband", "read_wristband"),
        button("Open notes", "open_notes"),
        button("Confirm identity", "confirm_identity", enabled="identity_ready"),
        button("Report mismatch", "report_mismatch", enabled="has_record"),
        button("Ask nurse for a wristband", "request_wristband"),
        button(spoken["ask_allergies"], "ask_allergies"),
        button(spoken["ask_allergy_reaction"], "ask_allergy_reaction"),
        button(spoken["ask_reason_for_admission"], "ask_reason_for_admission"),
        button(spoken["ask_responsible_adult"], "ask_responsible_adult"),
    ]})
    questions = [q for q in consent["questions"]]
    answer_ids = []
    for q in questions:
        for a in q["answers"]:
            if a["id"] not in answer_ids:
                answer_ids.append(a["id"])
    tabs.append({"id": "ask", "label": "Ask", "step": 2, "entries":
                 [button(spoken.get("ask:" + t, t.replace("_", " ").capitalize()), "ask", topic=t) for t in topics] +
                 [button("Summarise", "summarise"),
                  form("Answer a question", "answer_question", [choice("q", "Question", [opt(q["id"], q["text"]) for q in questions]),
                                                                choice("a", "Answer", [opt(a, LABELS["answers"].get(a, a.replace("_", " "))) for a in answer_ids])])]})
    tabs.append({"id": "known", "label": "Known", "step": 2, "list": "known", "entries": []})
    bedside = [(i, l) for i, l, k in tests if k == "bedside"]
    tabs.append({"id": "assess", "label": "Assess", "step": 3, "entries":
                 [button("Attach " + l, "attach_sensor", sensor=s) for s, l in SENSORS] +
                 [button("Measure BP", "measure_nibp"), button("Take temperature", "measure_temperature"), button("Count breathing rate", "count_rr"),
                  button("Check alertness", "assess_acvpu"), button("Ask pain score", "ask_pain_score")] +
                 [button("Remove " + l, "detach_sensor", sensor=s) for s, l in SENSORS] +
                 [button("Ask permission to examine", "ask_exam_permission"),
                  form("Examine", "examine", [choice("region", "Region", [opt(r, region_label(r)) for r in regions])]),
                  button("Murphy's test", "murphy_test"),
                  form("Airway check", "airway_check", [choice("item", "Check", [opt(a, LABELS["airway"].get(a, a)) for a in sorted(airway)])]),
                  form("Bedside test", "perform_bedside_test", [choice("test", "Test", [opt(i, l) for i, l in bedside])]),
                  form("ASA grade", "assess_risk", [choice("asa", "Grade", [opt(k, v) for k, v in LABELS["asa"].items()]),
                                                    dict(choice("emergency", "Emergency", [opt("true", "Yes")]), optional=True)]),
                  form("VTE risk", "assess_vte", [{"key": "items", "label": "Risk and bleeding factors", "kind": "flags",
                                                   "items": [opt(f["id"], f["label"]) for f in risk["vte"]["risk_factors"] + risk["vte"]["bleeding_factors"]]}]),
                  form("STOP-BANG", "compute_score", [{"key": "", "label": "Questions", "kind": "booleans",
                                                       "items": [opt(i, LABELS["stopbang"].get(i, i)) for i in scores["stopbang"]["items"]]}], score="stopbang"),
                  button("Ask the anaesthetist to review", "request_anaesthetic_review"),
                  form("Ready for theatre?", "decide_ready", [choice("option", "Decision", [opt(o["id"], o["label"]) for o in risk["readiness_options"]])])]})
    blood = [(i, l) for i, l, k in tests if k == "blood"]
    medicines = [t for t in treatments if t.get("kind") == "medicine"]
    places = [l for l in loc["locations"] if l["id"] not in ("patient_bay", "corridor")]
    tabs.append({"id": "orders", "label": "Orders", "step": 6, "entries": [
        form("Order a test", "order_investigation", [choice("test", "Test", [opt(i, l) for i, l, k in tests])]),
        form("Take a blood sample", "take_sample", [choice("test", "Test", [opt(i, l) for i, l in blood]),
                                                     choice("tube", "Tube", [opt(t, LABELS["tubes"].get(t, t)) for t in inv["tubes"]])]),
        form("Request a medicine", "request_medication", [choice("class", "Medicine", [opt(t["id"], t["label"]) for t in medicines]), text("indication", "For")]),
        form("Request transport", "request_transport", [choice("to", "To", [opt(l["id"], l["label"]) for l in places]),
                                                        choice("mode", "How", [opt(m["id"], LABELS["modes"].get(m["id"], m["id"])) for m in loc["transport"]["modes"]]),
                                                        {"key": "escort", "label": "Escort", "kind": "multi", "options": [opt(e, LABELS["escort"].get(e, e)) for e in loc["transport"]["escort_roles"]]},
                                                        choice("monitoring", "Monitoring", [opt(m, LABELS["monitoring"].get(m, m)) for m in loc["transport"]["monitoring_levels"]]),
                                                        choice("accompany", "You go too", [opt("true", "Yes"), opt("false", "No")]),
                                                        choice("oxygen", "Oxygen", [opt("true", "Yes"), opt("false", "No")])]),
    ]})
    ordered = {"key": "test", "label": "Test", "kind": "state", "source": "investigations"}
    critical = load("investigations/critical.json")
    imaging = load("imaging/imaging.json")
    tabs.append({"id": "results", "label": "Results", "step": 7, "list": "investigations", "entries": [
        form("View a result", "view_result", [ordered]),
        form("Interpret a result", "interpret_result", [ordered, choice("interpretation", "Interpretation", [opt(i, LABELS["interpretations"][i]) for i in ("normal", "abnormal", "artefact")])]),
        form("Interpret imaging", "interpret_imaging", [ordered, choice("category", "The picture is", [opt(c, LABELS["imaging_categories"].get(c, c.replace("_", " "))) for c in imaging["categories"]])]),
        form("Acknowledge a result", "acknowledge_result", [ordered,
                                                             dict(choice("action", "Action (critical result)", [opt(a, LABELS["critical_actions"].get(a, a.replace("_", " "))) for a in critical["actions"]]), optional=True),
                                                             dict(text("note", "Note (critical result)"), optional=True)]),
        button("Answer the phone", "answer_phone"),
        form("Confirm the patient with the caller", "confirm_patient_on_call", [text("name", "Name"), text("dob", "Date of birth")]),
        form("Read the values back", "read_back", [text("values", "Values (analyte=value, one per value)")]),
    ]})
    tabs.append({"id": "treat", "label": "Treat", "step": 8, "list": "orders", "entries": [
        form("Check the timing of a treatment", "check_treatment_timing", [choice("treatment", "Treatment", [opt(t["id"], t["label"]) for t in treatments])]),
        button("Let a moment pass", "wait"),
        form("Let time pass", "time_skip", [text("minutes", "Minutes")]),
    ]})
    tabs.append({"id": "consent", "label": "Consent", "step": 9, "entries":
                 [button(LABELS["consent"].get(t, t), "consent_discussion", topic=t) for t in consent["topics"]] +
                 [button("Check understanding (teach back)", "teach_back"), button("Check capacity", "check_capacity"),
                  button("Sign the consent form", "sign_consent"), button("Use the emergency route", "use_emergency_route")]})
    who_items = sorted(who["items"], key=lambda i: (who["phases"].index(i["phase"]), i["order"]))
    responses = [opt(r, LABELS["responses"][r]) for r in ("yes", "no", "na")]
    tabs.append({"id": "checklist", "label": "Checklist", "step": 10, "entries":
                 [button("Tick: " + i["label"], "tick_ward_item", item=i["id"]) for i in ward] +
                 [form("Prepare: " + i["label"], "prepare", [choice("detail", "Done", [opt(a, a.replace("_", " ")) for a in i["action"]])], item=i["id"]) for i in ward if i.get("action")] +
                 [button("Sign the ward checklist", "sign_ward_checklist")] +
                 [button("Check: " + s["label"], "check_station", station=s["id"]) for s in stations] +
                 [button("Theatre is ready", "declare_or_ready")] +
                 [form(i["prompt"], "checklist_item", [choice("response", "Answer", responses)], phase=i["phase"], item=i["id"]) for i in who_items] +
                 [button("Complete the Time Out", "complete_time_out")]})
    outcomes = [opt(o["code"], o["label"]) for o in decision["outcomes"]]
    tabs.append({"id": "handover", "label": "Handover", "step": 11, "entries": [
        form("Declare the decision", "declare_outcome", [choice("outcome", "Decision", outcomes), text("reason", "Reason")]),
        form("Escalate (SBAR)", "escalate", [choice("role", "To", [opt(r["id"], r["label"]) for r in decision["roles"]])] +
             [text(p, LABELS["sbar"].get(p, p)) for p in decision["sbar"]]),
        form("Postpone", "postpone", [text("reason", "Reason")]),
        form("Redirect to another team", "redirect", [text("service", "Team")]),
    ]})
    tabs.append({"id": "debrief", "label": "Debrief", "step": 15, "list": "goals", "entries": []})
    return {"version": 1, "note": "Generated by tools/content/build_tablet.py from the content tables; do not edit by hand. Unity draws the tablet from this file (p0-vr-interaction-ui-s03).",
            "enabled_rules": ["always", "identity_ready", "has_record"], "tabs": tabs}


def accepts(entry: dict, verb: str, params: dict) -> bool:
    """True when this entry can produce exactly this action."""
    if entry["verb"] != verb:
        return False
    rest = dict(params)
    for k, v in entry["params"].items():
        if rest.pop(k, None) != v:
            return False
    for f in entry.get("fields", []):
        kind = f["kind"]
        if kind == "booleans":
            for i in f["items"]:
                if rest.pop(i["value"], None) not in ("true", "false"):
                    return False
            continue
        if f["key"] not in rest:
            if f.get("optional"):
                continue
            return False
        v = rest.pop(f["key"])
        if kind == "choice" and v not in {o["value"] for o in f["options"]}:
            return False
        if kind == "multi" and not set(v.split(",")) <= {o["value"] for o in f["options"]}:
            return False
        if kind == "flags":
            ids = [i["value"] for i in f["items"]]
            pairs = [p.split(":") for p in v.split(",")]
            if [p[0] for p in pairs] != ids or any(len(p) != 2 or p[1] not in ("yes", "no") for p in pairs):
                return False
    return not rest


def uncovered(layout: dict) -> list[str]:
    entries = [e for t in layout["tabs"] for e in t["entries"]]
    missing = []
    for f in sorted(glob.glob(str(GOLDEN / "*_correct.actions.json"))):
        if "skeleton" in f:
            continue
        for a in json.loads(Path(f).read_text(encoding="utf-8")):
            if not any(accepts(e, a["verb"], {k: str(v) for k, v in a.get("params", {}).items()}) for e in entries):
                missing.append(f"{Path(f).name}: {a['verb']} {a.get('params', {})}")
    return missing


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    layout = build()
    text_out = json.dumps(layout, ensure_ascii=False, indent=1) + "\n"
    missing = uncovered(layout)
    n = sum(len(t["entries"]) for t in layout["tabs"])
    if missing:
        print(f"{len(missing)} P0 correct-path action(s) the tablet cannot make:")
        for m in sorted(set(missing))[:40]:
            print("  - " + m)
        return 1
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != text_out:
            print("content/ui/tablet.json is out of date: run tools/content/build_tablet.py and commit it")
            return 1
        print(f"tablet layout up to date: {len(layout['tabs'])} tabs, {n} entries; every P0 correct-path action can be made from the tablet")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text_out, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}: {len(layout['tabs'])} tabs, {n} entries; every P0 correct-path action can be made from the tablet")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
