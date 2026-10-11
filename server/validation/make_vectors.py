"""Writes tests/vectors/grounding_vectors.json: the cases both validators (Python and C#) must answer identically (plan test T-GV5).

    uv run --project tools python server/validation/make_vectors.py            # write
    uv run --project tools python server/validation/make_vectors.py --check    # fail if the file is out of date

Each vector is an output a model might write, the context it was given, and the codes the validator must return. The expectations are written by hand here, not computed from the validator, so a validator that
drifts fails against them. A context is named once and reused.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "vectors" / "grounding_vectors.json"

PATIENT = {
    "allowed_intents": ["answer_question", "express_symptom", "ask_clarification"],
    "facts": [
        {"id": "f.complaint", "value": "Attacks under my right ribs after oily food, settle in an hour or two.", "disclosed": True, "disclosable_now": False},
        {"id": "f.medicines", "value": "Metformin and gliclazide. She has not taken them this morning.", "disclosed": False, "disclosable_now": False},
        {"id": "f.sugar", "value": "My sugar was 13.4 last night.", "disclosed": False, "disclosable_now": False},
        {"id": "f.allergy", "value": "Penicillin gave me a rash as a child.", "disclosed": False, "disclosable_now": True},
    ],
    "allowed_entities": ["gallstones", "diabetes", "metformin", "gliclazide", "penicillin", "ecg", "paracetamol"],
    "numbers": ["42", "98", "13.4", "1", "15"],
    "dose_text": ["IV paracetamol (SmPC): 1 g as a 15-minute infusion"],
}
DEBRIEF = {
    "debrief_facts": [
        {"id": "d.1", "verdict": "credit", "numbers": []},
        {"id": "d.2", "verdict": "penalty", "numbers": ["70"]},
        {"id": "d.3", "verdict": "critical", "numbers": []},
    ],
    "allowed_entities": ["sepsis", "antibiotics"],
    "numbers": ["15"],
    "log_events": ["E-LOW-BP"],
}


def u(text, intent="answer_question", facts=None, role="patient"):
    return {"kind": "utterance", "role": role, "intent": intent, "text": text, "fact_ids": facts if facts is not None else []}


def d(*sections):
    return {"kind": "debrief", "sections": [{"title": t, "text": x, "fact_ids": f} for t, x, f in sections]}


VECTORS = [
    # accepted
    ("a plain approved answer", "patient", u("It hurts under my ribs after I eat oily food.", facts=["f.complaint"]), []),
    ("a disclosable fact told", "patient", u("I had a rash from penicillin when I was little.", facts=["f.allergy"]), []),
    ("a number of the context", "patient", u("I am 42 and my oxygen was 98."), []),
    ("a drug the case mentions", "patient", u("I take metformin for my diabetes.", facts=[]), []),
    ("an exact approved dose example", "patient", u("The sheet says IV paracetamol (SmPC): 1 g as a 15-minute infusion"), []),
    ("a hidden number mentioned alone still leaks it", "patient", u("It was 13.4 once but I was ill."), ["DISCLOSURE_LEAK"]),
    # schema
    ("not an object", "patient", "hello", ["SCHEMA"]),
    ("an extra key", "patient", {**u("Hello."), "extra": 1}, ["SCHEMA"]),
    ("a missing key", "patient", {"kind": "utterance", "role": "patient", "intent": "answer_question", "text": "Hi"}, ["SCHEMA"]),
    ("an empty text", "patient", u(""), ["SCHEMA"]),
    ("a text that is too long", "patient", u("a " * 250), ["SCHEMA"]),
    ("fact ids that are not text", "patient", {**u("Hi."), "fact_ids": [1]}, ["SCHEMA"]),
    ("an unknown kind", "patient", {"kind": "poem", "text": "x"}, ["SCHEMA"]),
    # intent and facts
    ("an intent the character may not use", "patient", u("Hello.", intent="diagnose"), ["UNKNOWN_INTENT"]),
    ("a fact id that does not exist", "patient", u("Hello.", facts=["f.nope"]), ["UNKNOWN_FACT"]),
    # off-card entities
    ("a drug the case does not mention", "patient", u("I take warfarin every day."), ["OFF_CARD_ENTITY"]),
    ("a plural drug", "patient", u("I take two apixabans."), ["OFF_CARD_ENTITY"]),
    ("a condition the case does not mention", "patient", u("My doctor said I might have pancreatitis."), ["OFF_CARD_ENTITY"]),
    ("a test the case does not mention", "patient", u("They did my lactate yesterday."), ["OFF_CARD_ENTITY"]),
    ("equipment the case does not mention", "patient", u("They used a defibrillator on me."), ["OFF_CARD_ENTITY"]),
    ("a word that only contains a lexicon word", "patient", u("Insulinoma is a long word."), []),
    ("an allowed entity in capitals", "patient", u("METFORMIN is my tablet."), []),
    # numbers
    ("a number not in the context", "patient", u("I weigh 81 and I am 42."), ["UNGROUNDED_NUMBER"]),
    ("a time is a number too", "patient", u("It started at 14:30."), ["UNGROUNDED_NUMBER"]),
    # disclosure
    ("a hidden fact told in its own words", "patient", u("She has not taken metformin and gliclazide this morning."), ["DISCLOSURE_LEAK"]),
    ("a hidden number told", "patient", u("My sugar was 13.4 last night."), ["DISCLOSURE_LEAK"]),
    ("a paraphrase of a hidden fact (the known limit of a word check: the model is never given hidden facts)", "patient", u("I have not had my tablets today, metformin and gliclazide."), []),
    ("a hint that is not the fact", "patient", u("I take some tablets for my diabetes."), []),
    # doses and prescribing
    ("a dose that is not the approved text", "patient", u("Take 500 mg of it."), ["DOSE_STATEMENT", "UNGROUNDED_NUMBER"]),
    ("a dose in micrograms", "patient", u("About 500 micrograms I think."), ["DOSE_STATEMENT", "UNGROUNDED_NUMBER"]),
    ("a volume of fluid", "patient", u("They gave me 500 ml."), ["DOSE_STATEMENT", "UNGROUNDED_NUMBER"]),
    ("grams per litre is a result not a dose", "patient", u("The number was 98 g/L."), []),
    ("telling the trainee to give", "patient", u("You should give me something for the pain."), ["PRESCRIBE_PHRASING"]),
    ("telling the trainee to prescribe", "patient", u("Please prescribe me something."), ["PRESCRIBE_PHRASING"]),
    # debriefs
    ("a faithful debrief", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), []),
    ("a debrief that drops a fact", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"])), ["DEBRIEF_MISSING_FACT"]),
    ("a debrief that changes a number", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1"]), ("Missed", "You did not give the antibiotics for 60 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["DEBRIEF_NUMBER_CHANGED", "UNGROUNDED_NUMBER"]),
    ("a debrief that praises a missed item", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1"]), ("Missed", "Well done on the antibiotics at 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["DEBRIEF_VERDICT_CHANGED"]),
    ("a debrief that blames a credited item", "debrief", d(("Done well", "You missed the identity check.", ["d.1"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["DEBRIEF_VERDICT_CHANGED"]),
    ("a debrief that invents an event", "debrief", d(("Done well", "You confirmed the identity well and handled the allergic reaction.", ["d.1"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["DEBRIEF_NEW_EVENT"]),
    ("a debrief that mentions a logged event", "debrief", d(("Done well", "You confirmed the identity well and called about the low blood pressure.", ["d.1"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), []),
    ("a debrief that names a new drug", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1"]), ("Missed", "You did not give the antibiotics or morphine for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["OFF_CARD_ENTITY"]),
    ("a debrief with an unknown fact", "debrief", d(("Done well", "You confirmed the identity well.", ["d.1", "d.9"]), ("Missed", "You did not give the antibiotics for 70 minutes.", ["d.2"]), ("Critical", "The handover omitted the allergy.", ["d.3"])), ["UNKNOWN_FACT"]),
    ("a debrief with no sections", "debrief", {"kind": "debrief", "sections": []}, ["SCHEMA"]),
]


def build() -> str:
    doc = {"lexicon": "content/ai/lexicon.json", "contexts": {"patient": PATIENT, "debrief": DEBRIEF}, "vectors": [{"name": n, "context": c, "output": o, "expect": e} for n, c, o, e in VECTORS]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  tests/vectors/grounding_vectors.json is out of date; run server/validation/make_vectors.py")
            return 1
        print(f"tests/vectors/grounding_vectors.json is up to date ({len(VECTORS)} vectors)")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)} ({len(VECTORS)} vectors)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
