"""Builds the scoring content: content/scoring/rubric.json, content/assessment/mapping.json and content/debrief/templates.json (p0-scoring-debrief s01, s02 and s04).

    uv run --project tools tools/content/build_scoring.py            # write
    uv run --project tools tools/content/build_scoring.py --check    # fail if the files are out of date

What it holds
  * the 22 assessed behaviours of the guide (section 81, "AI-assisted assessment across the whole workflow"): the steps they belong to, the deterministic actions that are evidence for them, the communication objectives the Gemma semantic evaluator may report for them (ADR-0025), and the rule that decides correctness;
  * the rubric: every score item the engine can record (credit., minor., major., critical., info.) belongs to one behaviour and one item type, with points per severity and a weight per behaviour and scenario;
  * the debrief sentences, one template per item type and severity, whose only placeholders are facts of the item.
The weights, the points and the graded-communication parameters are simulator design values (provisional, SR-2864): the clinical judgement is in the rules that raise the items, not in these numbers.
The builder fails if an item id found in the source code or the content has no rubric rule, so a new item can never score nothing silently.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RUBRIC = ROOT / "content" / "scoring" / "rubric.json"
MAPPING = ROOT / "content" / "assessment" / "mapping.json"
TEMPLATES = ROOT / "content" / "debrief" / "templates.json"
REF = "SR-2864"          # the 22 behaviours and what is evidence for each (guide section 81)
REF_RUBRIC = "SR-2865"   # points, weights, threshold and sentences (design values)

# Graded communication credit (ADR-0025, EXPERIMENTAL design values, SR-2865): a performed objective earns relevance/3 * correctness/3 * (floor + (1 - floor) * completeness/3) of its weight; below credit_floor it earns nothing.
# An ordinary objective weighs 1.0 (the one credit point a behaviour already carries); a critical objective only 0.25, because its physical part is earned by the verified actions below and never by speech.
COMMUNICATION = {"provisional": True, "ref": "SR-2865", "grade_levels": 3, "completeness_floor": 0.4, "credit_floor": 0.1, "ordinary_weight": 1.0, "critical_weight": 0.25,
                 "note": "Experimental graded communication credit (ADR-0025). The model grades relevance, correctness and completeness (0 to 3); the engine converts them with this formula and its own state. Easy to change: these numbers are the only place."}
# The behaviour a critical objective is spoken for and the verified (simulator) actions that earn its physical part, named in the behaviours below: b13 confirm_identity, b02 ask_allergies/ask_allergy_reaction,
# b21 sign_consent, b11 handover/finish_handover. Speech may add the small communication credit of its weight; the physical part is earned only by these verified actions, never by speech.
CRITICAL_ACT_EVIDENCE = {"CONFIRM_IDENTITY": {"behaviour": "b13", "actions": ["confirm_identity"]}, "STATE_ALLERGY": {"behaviour": "b02", "actions": ["ask_allergies", "ask_allergy_reaction"]},
                         "CONSENT_SIGN": {"behaviour": "b21", "actions": ["sign_consent"]}, "HANDOVER_CRITICAL_ELEMENT": {"behaviour": "b11", "actions": ["handover", "finish_handover"]},
                         "SBAR_ELEMENT": {"behaviour": "b11", "actions": ["handover", "finish_handover"]}}

TYPES = ["REQUIRED", "CONTRAINDICATED", "TIMING", "ORDER", "RESPONSE", "APPROPRIATENESS", "INTERPRETATION", "COMMUNICATION", "CRITICAL"]


def b(id, label, steps, actions, acts, critical, rule):
    return {"id": id, "label": label, "steps": steps, "actions": actions, "ai_acts": acts, "critical": critical, "rule": rule, "ref": REF}


BEHAVIOURS = [
    b("b01", "Asks relevant questions", [2], ["ask"], ["ASK_TOPIC"], False, "Required-topic rules per patient"),
    b("b02", "Identifies important patient information", [2, 7], ["open_notes", "read_wristband", "ask_identity", "ask_allergies", "ask_allergy_reaction", "acknowledge_result"], [], False, "Fact-acknowledged rules"),
    b("b03", "Clarifies unclear information", [2, 7], [], ["CLARIFY"], False, "Vague-answer follow-up rule (no P0 action: needs the classifier)"),
    b("b04", "Responds to patient concerns and questions", [2, 9], ["answer_question"], ["ANSWER_QUESTION"], False, "A pending question must be answered within its window"),
    b("b05", "Explains procedures and tests", [6, 9], ["consent_discussion", "explain_delay"], ["EXPLAIN_PROCEDURE", "EXPLAIN_TEST"], False, "Explain-before-action rule and the consent elements"),
    b("b06", "Explains before performing an interaction", [3, 4, 6, 8], [], ["EXPLAIN_ACTION"], False, "Order rule: the explanation comes before the action (needs spoken evidence)"),
    b("b07", "Checks understanding", [9], ["teach_back"], ["CHECK_UNDERSTANDING"], False, "Consent rule element: teach-back"),
    b("b08", "Responds appropriately to anxiety", [2, 8, 9], [], ["ACKNOWLEDGE_EMOTION", "REASSURE"], False, "Response-window rule on an anxiety cue; false reassurance is a minor error"),
    b("b09", "Notices clinically relevant information from the patient", [2, 3, 4], ["tell_anaesthetist"], ["ACKNOWLEDGE_FACT"], False, "Disclosed-fact follow-up rules"),
    b("b10", "Communicates with nurses and staff", [6, 8, 11], ["read_back"], ["CLOSED_LOOP"], False, "Read-back rule"),
    b("b11", "Communicates during handover and transfer", [12, 15], ["handover", "finish_handover", "request_transport", "cancel_transport"], ["SBAR_ELEMENT"], True, "Required-element and critical-omission rules"),
    b("b12", "Gives appropriate instructions", [8, 10, 12], ["request_medication", "request_fluids", "request_oxygen", "request_iv_access", "order_investigation"], ["VOICE_ORDER"], False, "Instruction-validity rules (formulary and state)"),
    b("b13", "Performs required checks", [1, 8, 10, 13, 14], ["confirm_identity", "check_capacity", "check_station", "checklist_item", "tick_ward_item", "check_treatment_timing", "document_treatment", "request_interpreter"], [], True, "Required-check rules"),
    b("b14", "Responds to abnormal findings", [3, 4, 7], ["interpret_result", "interpret_imaging", "decide", "declare_outcome", "postpone", "redirect"], [], False, "Finding-to-action rules"),
    b("b15", "Responds to unexpected events", [3, 4, 6, 8, 11, 12], ["escalate", "answer_phone"], ["ESCALATE_SPEECH"], False, "Event response rules (response windows)"),
    b("b16", "Recognises the need for more information", [5, 7], ["order_investigation", "take_sample", "perform_bedside_test", "view_result", "assess_risk", "assess_vte", "compute_score"], [], False, "Missing-information rules"),
    b("b17", "Follows the workflow and sequence", list(range(1, 15)), [], [], False, "Order and hard-edge rules (readiness blockers)"),
    b("b18", "Notices environmental and safety problems", [11, 13], ["report_fault", "use_substitute_theatre", "confirm_patient_on_call"], ["REPORT_FAULT"], False, "Fault-response rules"),
    b("b19", "Responds when information is missing", [2, 6, 10], ["request_anaesthetic_review"], ["CLARIFY"], False, "Missing-information rules"),
    b("b20", "Escalates or seeks assistance", [3, 4, 7, 8], ["escalate", "say_concern", "tell_anaesthetist"], ["ESCALATE_SPEECH"], False, "Escalation-trigger rules"),
    b("b21", "Completes preparation and safety checks", [8, 9, 10, 11, 12, 13, 14], ["sign_ward_checklist", "declare_or_ready", "complete_time_out", "decide_ready", "sign_consent", "prepare"], [], False, "Readiness blockers"),
    b("b22", "Responds to changing patient and environment state", [3, 4, 8, 12], ["request_oxygen", "request_fluids", "escalate"], [], False, "Time-window rules"),
]

# item id (without its severity prefix) -> (behaviour, type). Ordered; the first match wins. "teaching" items carry no penalty (a moment the anaesthetist or nurse corrects, or an honest stop).
RULES = [
    (r"^consent_signed_without_capacity$", "b13", "CRITICAL", False),
    (r"^(capacity_checked|consent_capacity_not_checked|consent_untrained_interpreter|consent_minor_without_senior)$", "b13", "REQUIRED", False),
    (r"^(consent_element|consent_valid|consent_emergency_route|consent_risks_skipped|consent_alternatives_skipped)$", "b05", "REQUIRED", False),
    (r"^(teach_back|consent_signed_low_understanding)$", "b07", "REQUIRED", False),
    (r"^(consent_pressure|question_answered|question_dismissed|question_unanswered)$", "b04", "COMMUNICATION", False),
    (r"^false_reassurance$", "b08", "COMMUNICATION", False),
    (r"^(readback_wrong)$", "b10", "COMMUNICATION", False),
    (r"^(handover_complete|handover_omitted|handover_repeat_omission|sbar_complete|sbar_incomplete)$", "b11", "COMMUNICATION", False),
    (r"^(recovery_handover_complete|recovery_handover_omitted|recovery_handover_repeat_omission)$", "b11", "COMMUNICATION", False),
    (r"^(intraop_response|intraop_wrong_response|intraop_no_response)$", "b15", "RESPONSE", False),
    (r"^(transfer_escort_inadequate|unsafe_transport_mode)$", "b11", "APPROPRIATENESS", False),
    (r"^(asa_correct)$", "b21", "APPROPRIATENESS", False),
    (r"^(asa_close|asa_far)$", "b21", "APPROPRIATENESS", True),
    (r"^(ignored_critical_result|anaesthetist_not_called|vte_not_assessed)$", "b21", "REQUIRED", False),
    (r"^right_role$", "b20", "APPROPRIATENESS", False),
    (r"^e_[a-z_]+_(not_)?escalated$", "b15", "RESPONSE", False),
    (r"^(reaction_handled|reaction_not_treated)$", "b15", "RESPONSE", False),
    (r"^treatment_allergy_conflict$", "b13", "CONTRAINDICATED", False),
    (r"^sepsis_recognised$", "b14", "INTERPRETATION", False),
    (r"^sepsis_", "b15", "TIMING", False),
    (r"^(interpretation|imaging_interpretation|interpretation_wrong|not_visualised_taken_as_negative)$", "b14", "INTERPRETATION", False),
    (r"^(critical_acknowledged|unacknowledged|result_not_reviewed)$", "b14", "RESPONSE", False),
    (r"^test_appropriate$", "b16", "APPROPRIATENESS", False),
    (r"^(decision_correct|decision_wrong|postponed_appropriately|redirected_appropriately|postponed_unnecessarily|redirected_unnecessarily|postponed_in_emergency|emergency_not_declared)$", "b14", "APPROPRIATENESS", False),
    (r"^(late_fasting_passed|late_fasting_not_passed|hidden_fact_surfaced_late)$", "b02", "REQUIRED", False),
    (r"^(fasting_informed_and_delayed|proceed_despite_fasting_breach)$", "b13", "REQUIRED", False),
    (r"^(or_fault_reported|or_fault_ignored|checklist_stopped_honestly|substitute_theatre_used)$", "b18", "RESPONSE", True),
    (r"^(or_ready|or_declared_ready_not_ready|or_critical_station_unchecked)$", "b21", "REQUIRED", False),
    (r"^(checklist_contradiction|checklist_skipped_repeatedly|checklist_item_skipped|sign_in_complete|time_out_complete|sign_out_complete|specimen_name_wrong|count_discrepancy_handled)$", "b21", "REQUIRED", False),
    (r"^(ward_checklist_complete|ward_signed_with_gaps|ward_tick_false|ward_tick_unverified|razor_used)$", "b21", "REQUIRED", False),
    (r"^(emergency_not_declared|handover_omitted)$", "b11", "COMMUNICATION", False),
    (r"^action_before_identity$", "b13", "CRITICAL", False),
    (r"^identity_asked_closed$", "b13", "REQUIRED", False),
    (r"^unnecessary_mismatch_report$", "b13", "APPROPRIATENESS", False),
    (r"^no_introduction$", "b06", "COMMUNICATION", False),
]

POINTS = {"credit": 1, "info": 0, "minor": -1, "major": -3, "critical": 0}

SENTENCES = {
    "credit": "Credit: {label}. Rule {rule_id} [{source}].",
    "info": "For your information: {label}. Rule {rule_id} [{source}].",
    "minor": "Small slip: {label}. Rule {rule_id} [{source}].",
    "major": "Important miss: {label}. Rule {rule_id} [{source}].",
    "critical": "Critical error (fails the case): {label}. Rule {rule_id} [{source}].",
}


def item_ids() -> set[str]:
    """Every score item id written in the source code, the content and the builders (dynamic ones are caught by their prefix rule)."""
    found: set[str] = set()
    pat = re.compile(r'(?:"|score:)((?:credit|minor|major|critical|info)\.[a-z0-9_]+)')
    for base, exts in ((ROOT / "src", ("*.cs",)), (ROOT / "content", ("*.json",)), (ROOT / "tools" / "content", ("*.py",))):
        for ext in exts:
            for p in base.rglob(ext):
                if p.name in ("rubric.json", "mapping.json", "templates.json") or p.name == "build_scoring.py":
                    continue
                found.update(pat.findall(p.read_text(encoding="utf-8", errors="replace")))
    return {i for i in found if i not in ("critical.json",)}


def label_of(item: str) -> str:
    return item.split(".", 1)[1].replace("_", " ")


def build() -> dict[Path, str]:
    behaviours = {x["id"]: x for x in BEHAVIOURS}
    if len(behaviours) != 22:
        raise SystemExit("there must be exactly 22 behaviours")
    compiled = [(re.compile(p), bid, typ, teaching) for p, bid, typ, teaching in RULES]
    for p, bid, typ, _ in RULES:
        if bid not in behaviours or typ not in TYPES:
            raise SystemExit(f"rubric rule {p} names an unknown behaviour or type")
    unmatched = []
    for item in sorted(item_ids()):
        sev, name = item.split(".", 1)
        if not any(c.search(name) for c, *_ in compiled):
            unmatched.append(item)
    if unmatched:
        raise SystemExit("score items with no rubric rule (add one to RULES in tools/content/build_scoring.py): " + ", ".join(unmatched))
    rubric = {
        "provisional": True,
        "severity_points": POINTS,
        "fail_severity": "critical",
        "types": TYPES,
        "rules": [{"match": p, "behaviour": bid, "type": typ, "teaching": teaching} for p, bid, typ, teaching in RULES],
        "weights": {sc: {x["id"]: 1.0 for x in BEHAVIOURS} for sc in ("S1", "S2")},
        "communication": COMMUNICATION,
        "refs": [REF_RUBRIC],
    }
    mapping = {
        "behaviours": BEHAVIOURS,
        "critical_acts": ["CONFIRM_IDENTITY", "STATE_ALLERGY", "CONSENT_SIGN", "HANDOVER_CRITICAL_ELEMENT", "SBAR_ELEMENT"],
        "critical_act_evidence": CRITICAL_ACT_EVIDENCE,
        "refs": [REF, REF_RUBRIC],
    }
    templates = {"sentences": SENTENCES, "placeholders": ["label", "rule_id", "source"], "refs": [REF_RUBRIC]}
    dump = lambda d: json.dumps(d, indent=1, ensure_ascii=False) + "\n"
    return {RUBRIC: dump(rubric), MAPPING: dump(mapping), TEMPLATES: dump(templates)}


def critical_act_problems(mapping: dict) -> list[str]:
    """The acts the taxonomy flags critical must be exactly `critical_acts`, and every act a critical behaviour lets speech report must be one of them (else speech alone could credit a critical behaviour)."""
    path = ROOT / "content" / "assessment" / "taxonomy.json"
    if not path.exists():
        return []
    flagged = {a["id"] for a in json.loads(path.read_text(encoding="utf-8"))["acts"] if a["critical"]}
    listed = set(mapping["critical_acts"])
    out = [f"taxonomy.json and critical_acts disagree: only in taxonomy {sorted(flagged - listed)}, only in critical_acts {sorted(listed - flagged)}"] if flagged != listed else []
    by_id = {b["id"]: b for b in mapping["behaviours"]}
    ev = mapping.get("critical_act_evidence", {})
    out += [f"critical_act_evidence misses {a}" for a in sorted(listed) if a not in ev]
    out += [f"critical_act_evidence for {a} names an action that behaviour {e['behaviour']} does not have: {x}" for a, e in ev.items() for x in e["actions"] if e["behaviour"] not in by_id or x not in by_id[e["behaviour"]]["actions"]]
    out += [f"critical behaviour {b['id']} lets speech report {a}, which is not in critical_acts" for b in mapping["behaviours"] if b["critical"] for a in b["ai_acts"] if a not in listed]
    return out


def main(argv: list[str]) -> int:
    files = build()
    if "--check" in argv:
        problems = critical_act_problems(json.loads(files[MAPPING]))
        if problems:
            print("FAIL  " + "; ".join(problems))
            return 1
        stale = [str(p.relative_to(ROOT)) for p, t in files.items() if not p.exists() or p.read_text(encoding="utf-8") != t]
        if stale:
            print("FAIL  out of date: " + ", ".join(stale) + "; run tools/content/build_scoring.py")
            return 1
        print("scoring content (rubric, 22 behaviours, debrief sentences) is up to date")
        return 0
    for p, t in files.items():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
