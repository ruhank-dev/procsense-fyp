"""Builds content/clashes/clashes.json: the treatment clash table in a form the engine can check, how the patient's factors are read from a case, and the perioperative medicine plans (p0-clinical-logic-s12).

    uv run --project tools tools/content/build_clashes.py            # write
    uv run --project tools tools/content/build_clashes.py --check    # fail if the file is out of date

Sources (verified in earlier steps): content/treatment_clashes.csv (walkthrough 14B.3), content/drug_checks.csv (the three checks plus blood thinner, 14B.2) and content/medicines.csv (the perioperative plans).
What this script adds is the project's reading that turns those rows into machine-checkable form (SR-0092): each clash is the set of tags that must all be present (what is being asked for plus the patient's factors),
each tag has the words that show it in a case's wording, and each medicine plan has the words that name the medicine. No clinical value is typed here: the text shown comes from the table rows.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "clashes" / "clashes.json"

# What is being asked for, from the treatment or the test.
NEEDS = {
    "TR-PARACETAMOL": ["need_pain_relief"], "TR-NSAID": ["need_pain_relief"], "TR-OPIOID": ["need_pain_relief"],
    "TR-IV-FLUIDS": ["need_fluid"], "TR-SEPSIS-BUNDLE": ["need_fluid", "need_antibiotic"],
    "TR-ABX-PROPHYLAXIS": ["need_antibiotic"], "TR-ABX-S2": ["need_antibiotic"],
    "TR-ANTIEMETIC": ["need_antiemetic"],
    "ct": ["need_ct", "need_ct_dye"],
}
# The patient's factors: the words that show each one in a case's wording (history answers, the presentation summary, the problems). 'pregnancy' comes from the case's own pregnant flag.
FACTORS = {
    "kidney_injury": ["acute kidney injury", "kidney injury"],
    "kidney_failure": ["kidney failure", "renal failure", "known_kidney_failure"],
    "heart_failure": ["heart failure"],
    "severe_liver_disease": ["cirrhosis", "severe liver disease", "liver failure"],
    "dehydration": ["dehydrat"],
    "peptic_ulcer_bleeding": ["ulcer"],
    "asthma": ["asthma"],
    "blood_thinner": ["warfarin", "apixaban", "rivaroxaban", "dabigatran", "edoxaban", "clopidogrel", "ticagrelor", "prasugrel", "aspirin", "heparin", "blood thinner"],
    "long_qtc": ["long qtc", "prolonged qtc"],
    "bowel_obstruction": ["bowel obstruction"],
    "co2_retainer": ["copd", "co2 retain", "long-term lung disease"],
    "penicillin_allergy": ["penicillin"],
    "dye_allergy": ["contrast allergy", "dye allergy"],
    "diabetes": ["diabetes", "diabetic"],
    "insulin_user": ["insulin"],
    "sglt2_user": ["dapagliflozin", "empagliflozin", "canagliflozin", "sglt2"],
    "steroid_user": ["prednisolone", "hydrocortisone", "long-term steroid", "steroid tablets"],
    "recent_stent": ["stent"],
}
# The context the engine knows: S2 means urgent surgery, a suspected infection with a sepsis tier of moderate or higher means sepsis.
# the situation tags the engine now works out from the session (DT-072): see ClashSubsystem.Context
CONTEXT = ["urgent_surgery", "sepsis", "nil_by_mouth", "high_potassium", "need_suxamethonium", "full_stomach", "low_oxygen", "confused", "consent_needed"]
# the two numbers those tags use, from the verified bands: potassium 6.0 and above is the "significant" band (SR-0536), oxygen 91% or less is the emergency sign (SR-0124)
LIMITS = {"high_potassium_from": 6.0, "low_oxygen_to": 91, "refs": ["SR-0536", "SR-0124"]}
# Each clash row of treatment_clashes.csv: the tags that must all be present.
WHEN = {
    "fluid_heart_kidney": [["need_fluid", "heart_failure"], ["need_fluid", "kidney_failure"]],
    "pain_kidney": [["need_pain_relief", "kidney_injury"]],
    "pain_pregnancy": [["need_pain_relief", "pregnancy"]],
    "antibiotic_penicillin_allergy": [["need_antibiotic", "penicillin_allergy"]],
    "ct_pregnancy": [["need_ct", "pregnancy"]],
    "ct_dye_kidney": [["need_ct_dye", "kidney_injury"], ["need_ct_dye", "kidney_failure"]],
    "ct_dye_allergy": [["need_ct_dye", "dye_allergy"]],
    "antiemetic_qtc": [["need_antiemetic", "long_qtc"]],
    "metoclopramide_obstruction": [["need_antiemetic", "bowel_obstruction"]],
    "blood_thinner_urgent_surgery": [["blood_thinner", "urgent_surgery"]],
    "antiplatelet_stent": [["blood_thinner", "recent_stent"]],
    "sglt2_sepsis": [["sglt2_user", "sepsis"]],
    "steroid_sepsis": [["steroid_user", "sepsis"]],
    # these need facts the engine does not know yet (nil by mouth state, a result, consent state): the rows are kept, nothing can raise them until DT-072 is done
    "insulin_nbm": [["insulin_user", "nil_by_mouth"]],
    "high_potassium_suxamethonium": [["high_potassium", "need_suxamethonium"]],
    "full_stomach_emergency": [["full_stomach", "urgent_surgery"]],
    "low_oxygen_copd": [["low_oxygen", "co2_retainer"]],
    "confused_consent_emergency": [["confused", "consent_needed", "urgent_surgery"]],
}
PENDING_TAGS: list[str] = []         # every tag a clash row names is now worked out by the engine

# medicines.csv plan rows that become Step 8 decisions, with the words that name the medicine in a case's history
PLANS = {
    "warfarin": ["warfarin"], "doacs": ["apixaban", "rivaroxaban", "edoxaban"], "dabigatran": ["dabigatran"], "aspirin": ["aspirin"], "p2y12": ["clopidogrel", "ticagrelor", "prasugrel"],
    "metformin": ["metformin"], "sulfonylureas": ["gliclazide", "glimepiride", "glipizide"], "sglt2": ["dapagliflozin", "empagliflozin", "canagliflozin"], "glp1": ["semaglutide", "liraglutide", "dulaglutide"],
    "dpp4": ["sitagliptin", "linagliptin", "vildagliptin"], "insulin_long": ["glargine", "detemir", "degludec", "basal insulin"],
    "ace_arb": ["ramipril", "lisinopril", "losartan", "candesartan", "enalapril"], "beta_blockers": ["bisoprolol", "atenolol", "metoprolol", "propranolol"], "diuretics": ["furosemide", "bendroflumethiazide", "indapamide", "water tablet"],
    "statins_ccb": ["atorvastatin", "simvastatin", "amlodipine"], "steroids": ["prednisolone", "hydrocortisone"], "cocp_hrt": ["combined pill", "hrt", "contraceptive pill"], "herbal": ["herbal", "ginkgo", "garlic", "ginseng"],
    "epilepsy_parkinsons": ["levodopa", "carbamazepine", "phenytoin", "lamotrigine", "levetiracetam", "valproate"],
}


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def build() -> str:
    clashes_rows = rows("treatment_clashes")
    keys = [r["clash_key"] for r in clashes_rows]
    if sorted(keys) != sorted(WHEN):
        raise SystemExit(f"treatment_clashes.csv and WHEN disagree: {sorted(set(keys) ^ set(WHEN))}")
    known = set(FACTORS) | {t for tags in NEEDS.values() for t in tags} | set(CONTEXT) | set(PENDING_TAGS) | {"pregnancy"}
    for k, alts in WHEN.items():
        for alt in alts:
            unknown = [t for t in alt if t not in known]
            if unknown:
                raise SystemExit(f"{k}: tags that nothing defines: {unknown}")
    clashes = [{"id": r["clash_key"], "when": WHEN[r["clash_key"]], "clash": r["v_clash"], "why": r["v_why"], "safer": r["v_safer"], "decided_by": r["v_decided_by"], "tag": r["v_tag"], "problem_type": "safety_blocker", "ref": r["ref"]} for r in clashes_rows]

    checks = [{"id": r["name"], "text": r["v_text"], "ref": r["ref"]} for r in rows("drug_checks") if r["kind"] == "check"]
    meds = {r["id"]: r for r in rows("medicines")}
    plans = []
    for mid, words in PLANS.items():
        if mid not in meds:
            raise SystemExit(f"PLANS names {mid}, which medicines.csv does not have")
        r = meds[mid]
        if not (r["v_timing"].strip() or r["v_care"].strip()):
            raise SystemExit(f"{mid} has no timing or care wording in medicines.csv")
        plans.append({"id": mid, "words": words, "plan": r["v_timing"], "care": r["v_care"], "ref": r["ref"]})
    doc = {
        "checks": checks,
        "needs": NEEDS,
        "factors": [{"tag": t, "words": w} for t, w in FACTORS.items()],
        "context": CONTEXT,
        "pending_tags": PENDING_TAGS,
        "limits": LIMITS,
        "clashes": clashes,
        "plans": plans,
        "refs": ["SR-0092", "SR-0093"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/clashes/clashes.json is out of date; run tools/content/build_clashes.py")
            return 1
        print("content/clashes/clashes.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
