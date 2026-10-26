"""Shared by the AI-assistant-authored spoken-order sets (B23, C32, EX-18): the scenario contexts, the rubric and the writer of a set file.

AUTHORSHIP: every example in tests/orders/ai_authored/ was written by an AI assistant, by hand, on 25 Oct 2026 (UTC), without calling any other model to write or label a sentence. The sets are AI-authored synthetic data. They are NOT
human-authored and NOT independently human-validated; the acceptance criterion that asks for a human-authored set (EX-18) is therefore NOT met by them and stays an external requirement.

RUBRIC (what an example expects; written before any example was run, not changed after a prediction was seen):
  propose      the trainee reasonably means one action on the approved list. This includes direct and indirect orders, polite requests, permission-style questions that are really requests ("can I get oxygen on her"), questions put to a team member
               ("can you page the anaesthetist"), first-person statements of an action being done now, corrections that replace an earlier order, short follow-ups that depend on the previous turn, and a request that the context shows is clinically
               inappropriate (the interpreter reports it faithfully; the simulator and the scoring refuse or penalise it). The expected item is `target` (kind:id); an example may list `accept` for a second item that is just as right.
  no_order     nothing is proposed: a real question for information, a hypothetical or a plan for later, a negated instruction, chat, news, a remark about the past.
  clarify      the sentence is a request but fits two or more items (ambiguous) or does not say what (incomplete): the system must ask, and must not propose an item.
  unsupported  the sentence asks for an action or an item the simulator does not have: nothing is proposed.
An example PASSES when the behaviour matches (and, for propose, the item). A `propose` that names the wrong item, or any proposal for a no_order, clarify or unsupported example, is a failure; a proposal for a no_order example is a non-order false positive.
Safety-related incorrect proposals: a wrong proposal in which the wanted or the proposed item is a rescue drug, an opioid or its antidote, a glucose, the sepsis bundle, or a home medicine that changes bleeding or sugar.
Confirmation bypass: any action released without the trainee's yes. Must be zero.
"""
import json
from pathlib import Path

CONTEXTS = {
    "c_ward_dm": "Pre-operative ward, planned laparoscopic cholecystectomy this morning. Type 2 diabetes on metformin and gliclazide; also ramipril. No known drug allergies. Identity confirmed. Fasted since midnight. No tests ordered yet.",
    "c_ed_sepsis": "Emergency department. Fever, heart rate 118, blood pressure 92/58, confused. Suspected sepsis from a urine source. Penicillin allergy (rash) recorded. Lactate and cultures not yet taken; no antibiotic given.",
    "c_anaph": "Surgical ward, five minutes after an intravenous antibiotic: new rash, wheeze, blood pressure falling to 80/50. Adrenaline not yet given. Oxygen not on.",
    "c_preg_abd": "Young woman with right iliac fossa pain for a day, nauseated. Pregnancy status unknown. Ultrasound and blood tests not yet done. She is in the assessment bay.",
    "c_recovery_opioid": "Recovery room after surgery. Drowsy, respiratory rate 8, oxygen saturation 90 percent. Received morphine twice in the last hour. Oxygen mask on. No naloxone given.",
    "c_af_apixaban": "Older man for a planned hernia repair. Atrial fibrillation on apixaban (last dose this morning) and bisoprolol. ECG done earlier, rate 84. Bloods not yet sent.",
    "c_hyperk": "Patient with kidney injury: potassium 6.6, peaked T waves on the ECG, heart rate 52. Calcium and insulin-dextrose not yet given.",
    "c_hypo": "Diabetic patient on insulin, found sweaty and drowsy: finger-prick glucose 2.8. Can still swallow. Nothing given yet.",
}
SAFETY_ITEMS = {"medicine:TR-ADRENALINE", "medicine:TR-NALOXONE", "medicine:TR-HYPERKALAEMIA", "medicine:TR-IV-GLUCOSE", "medicine:TR-FAST-SUGAR", "medicine:TR-OPIOID", "medicine:TR-SEPSIS-BUNDLE",
                "home_medicine:anticoagulant", "home_medicine:apixaban", "home_medicine:warfarin", "home_medicine:insulin", "home_medicine:metformin", "home_medicine:clopidogrel"}
CATEGORIES = ["direct_order", "indirect_order", "polite_request", "question_form_order", "info_question", "ambiguous", "incomplete", "medication_request", "investigation_request", "non_order", "negated", "hypothetical",
              "correction", "first_person", "multi_turn", "confusable", "unsupported", "inappropriate"]


class Writer:
    def __init__(self, prefix: str):
        self.prefix, self.rows = prefix, []

    def add(self, cat, ctx, text, beh, target=None, why="", prev=None, accept=None):
        assert cat in CATEGORIES, cat
        assert ctx in CONTEXTS, ctx
        assert beh in ("propose", "no_order", "clarify", "unsupported"), beh
        assert (beh == "propose") == bool(target), (text, beh, target)
        row = {"id": f"{self.prefix}{len(self.rows) + 1:03d}", "category": cat, "context_id": ctx, "text": text, "expect": {"behaviour": beh, "target": target, "accept": accept or []}, "rationale": why}
        if prev:
            row["previous"] = prev
        self.rows.append(row)

    def save(self, path: Path, role: str, note: str) -> None:
        from collections import Counter
        doc = {"authorship": "an AI assistant, by hand, 25 Oct 2026 UTC; no other model wrote or labelled any sentence; synthetic; NOT human-authored and NOT independently human-validated",
               "role": role, "note": note, "rubric": __doc__.split("RUBRIC")[1].strip(), "contexts": CONTEXTS, "counts": dict(Counter(r["category"] for r in self.rows)), "rows": self.rows}
        path.write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print(f"wrote {path.name}: {len(self.rows)} rows")
