"""Builds content/ai/lexicon.json: the words an AI-written line may not use unless the case allows them (p1-ai-language s01, guide B-R2).

    uv run --project tools tools/content/build_ai_lexicon.py            # write
    uv run --project tools tools/content/build_ai_lexicon.py --check    # fail if the file is out of date

Categories: drug (medicine names and classes), test (investigations and their analytes), condition, allergen, equipment, and the events the Event Director can fire with the words that name them.
A character's line may mention an entity only if the case itself mentions it (the "allowed set" the validator is given); a model that invents a drug, a test or a condition is rejected. Medicine names carry no dose.
The curated lists are design tables (SR-2870, provisional); the test names and event ids are read from the content, so a new test or event is covered the day it is added.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "ai" / "lexicon.json"
REF = "SR-2870"

DRUGS = sorted(set("""paracetamol ibuprofen diclofenac naproxen aspirin morphine codeine tramadol fentanyl oxycodone ondansetron metoclopramide cyclizine amoxicillin co-amoxiclav flucloxacillin cefuroxime ceftriaxone
gentamicin metronidazole piperacillin meropenem vancomycin clindamycin penicillin warfarin apixaban rivaroxaban edoxaban dabigatran clopidogrel ticagrelor prasugrel heparin enoxaparin metformin gliclazide insulin
dapagliflozin empagliflozin semaglutide liraglutide sitagliptin ramipril lisinopril losartan candesartan bisoprolol atenolol metoprolol amlodipine furosemide bendroflumethiazide spironolactone atorvastatin simvastatin
prednisolone hydrocortisone dexamethasone adrenaline epinephrine naloxone salbutamol levothyroxine propofol rocuronium suxamethonium sugammadex atropine glucose dextrose antibiotic antibiotics anticoagulant
painkiller opioid steroid""".split()))
CONDITIONS = sorted(set("""appendicitis cholecystitis cholangitis pancreatitis gallstones sepsis septic diabetes hypertension fibrillation pregnancy ectopic anaphylaxis asthma copd stroke cancer pneumonia peritonitis
perforation obstruction jaundice dehydration hypoglycaemia hyperkalaemia hypotension tachycardia bradycardia hypoxia arrhythmia infarction thrombosis embolism haemorrhage bleeding ketoacidosis""".split()))
ALLERGENS = sorted(set("penicillin latex iodine egg nuts shellfish cephalosporin sulfa aspirin nsaid".split()))
EQUIPMENT = sorted(set("cannula catheter ventilator defibrillator laparoscope insufflator diathermy monitor pulse-oximeter ecg-leads suction oxygen-mask trolley wheelchair".split()))
# words that name each event, so a debrief cannot describe an event that never happened
EVENT_WORDS = {"E-HYPERSENSITIVITY": ["allergic reaction", "anaphylaxis", "hypersensitivity", "swollen lips"]}
EVENT_WORDS_DEFAULT = {"E-LOW-BP": ["low blood pressure", "hypotension"], "E-HIGH-HR": ["fast heart rate", "tachycardia"], "E-LOW-HR": ["slow heart rate", "bradycardia"], "E-LOW-OXYGEN": ["low oxygen", "desaturation"],
                       "E-FAST-BREATHING": ["fast breathing", "tachypnoea"], "E-SLOW-BREATHING": ["slow breathing", "respiratory depression"]}


def build() -> str:
    inv = json.loads((ROOT / "content" / "investigations" / "investigations.json").read_text(encoding="utf-8"))["tests"]
    tests = sorted({t["label"].lower() for t in inv} | {a.lower() for t in inv for a in t.get("analytes", [])})
    events = json.loads((ROOT / "content" / "events" / "events.json").read_text(encoding="utf-8"))["events"]
    ev = {e["id"]: (EVENT_WORDS | EVENT_WORDS_DEFAULT).get(e["id"], [e["id"].lower()]) for e in events}
    doc = {"categories": {"drug": DRUGS, "condition": CONDITIONS, "allergen": ALLERGENS, "equipment": EQUIPMENT, "test": tests}, "events": ev, "refs": [REF]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/ai/lexicon.json is out of date; run tools/content/build_ai_lexicon.py")
            return 1
        print("content/ai/lexicon.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
