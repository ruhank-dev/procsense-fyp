"""Builds content/treatments.json: the treatment library and its required checks (p0-clinical-logic-s10, Implementation Guide B-M14 steps 1-4).

    uv run --project tools tools/content/build_treatments.py            # write the file and its register rows
    uv run --project tools tools/content/build_treatments.py --check    # fail if either is out of date

A treatment is a class of medicine (never a patient dose), a fluid, oxygen, or a procedure such as IV access. Each one lists, in the file itself, the checks that must pass before it is given
(identity, allergy_known, indication, restriction, timing, order) and after (documentation), the things that must already be in place (IV access), the patient states that restrict it, and the
physiology effect ids it causes. Nothing is implied in code.

Where a fact comes from the book it is copied from content/medicines.csv and checked here: a restriction must be wording that the row's "care" column really contains, a timing check must rest on
timing wording in the row, an IV requirement must rest on wording that names the IV route. A dose is copied only for rows whose dose policy is EDUCATIONAL_EXAMPLE, and always with its source and
the reviewer (rule DOSE_REQUIRES_REVIEWER); PATIENT_SPECIFIC and INSTITUTIONAL rows carry no dose at all.

Which checks a treatment lists, the severities, the effect ids and the IV rule are simulator design values (evidence type D) and the restriction tags are an interpretation of the "care" column
(type B). They have register rows SR-4100..SR-4109, rewritten by this script.
"""
from __future__ import annotations

import csv
import io
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "treatments.json"
REGISTER = ROOT / "docs" / "research" / "source_register.csv"
BLOCK = range(4100, 4120)
TODAY = "2026-10-11"
REVIEWER = "Owner (Ruhan Kamran), who states that the clinician approves everything; the clinician is not named"

REGISTER_HEADER = ["id", "item", "value", "unit", "walkthrough_ref", "source_name", "source_location", "source_url", "evidence_type", "status",
                   "date_checked", "checked_by", "clinician_approval", "notes"]
APPROVAL = ("Simulator design value written by the project team on 11 Oct 2026; covered by the owner's statement that the clinician approves everything (10 Oct 2026); "
            "not individually reviewed by a named clinician")

# The checks: id, phase (before = judged when the nurse is asked to give it, after = judged once it is given) and what happens when it is missing or fails.
CHECKS = [
    ("identity", "before", "critical", "The patient's identity is confirmed"),
    ("allergy_known", "before", "critical", "The patient's allergies are known"),
    ("indication", "before", "major", "There is a recorded reason for the treatment"),
    ("restriction", "before", "critical", "None of the patient's conditions restricts this treatment"),
    ("timing", "before", "major", "The timing wording for this treatment has been checked"),
    ("order", "before", "major", "A clinician has confirmed the order"),
    ("documentation", "after", "minor", "The treatment is recorded"),
]

PREREQUISITES = [("iv_access", "IV_ACCESS_REQUIRED", "IV access is in place")]

# Medicine workflow (s11). The two delays are simulator design values (evidence type D) and are tuned with testers.
WORKFLOW = {"confirm_seconds": 60, "administer_seconds": 60}

# The oxygen devices of equipment item 28 (docs/research/equipment/28-oxygen-supply-and-masks.md): nasal cannula and the simple and non-rebreather masks.
OXYGEN_DEVICES = [("nasal_cannula", "Nasal cannula"), ("simple_mask", "Simple face mask"), ("non_rebreather_mask", "Non-rebreather mask")]
OXYGEN_SHEET = ROOT / "docs" / "research" / "equipment" / "28-oxygen-supply-and-masks.md"

# An allergy to a whole group of medicines (walkthrough 14.4): the words in the allergy record that name the group, and the words that mean an immediate severe reaction.
ALLERGY_GROUPS = {"antibiotic": ["penicillin", "antibiotic"]}
SEVERE_REACTION_WORDS = ["throat swelling", "collapse", "anaphylaxis"]
# What the nurse does about a critical problem: practice mode (the beginner) challenges and does not give; assessment mode gives it, marks it critical and, for an allergen, makes the reaction eligible.
ALLERGY_OUTCOME = {"practice": "challenge", "advanced": "reaction", "assessment": "reaction"}
HYPERSENSITIVITY_EVENT = "E-HYPERSENSITIVITY"

# The approved lines the clinician and the nurse say (the trainee never sees a line that is not here).
LINES = {
    "confirm": "I confirm the order for {treatment}.",
    "confirm_allergy_caution": "I have noted the allergy on record: {allergy}. I will take that into account.",
    "challenge_allergy_conflict": "Before I give this, I have to stop: the chart records an allergy to {allergy}. Please ask the clinician to review the order.",
    "challenge_allergy_unknown": "Before I give this, can you confirm the patient's allergy status?",
    "challenge_restriction": "Before I give this, I am worried about {condition}. Please ask the clinician to review the order.",
    "given": "{treatment} has been given.",
    "iv_in_place": "The IV access is in.",
}

# Effect ids the physiology step (p0-physiology-events-behaviour-s05) will define; names only, no numbers.
EFFECTS = [
    ("EFF-ANALGESIA", "Pain falls"), ("EFF-ANTIEMETIC", "Nausea settles"), ("EFF-FLUID-BOLUS", "Circulating volume rises: blood pressure up, heart rate down"),
    ("EFF-OXYGEN", "Oxygen saturation rises"), ("EFF-ANTIBIOTIC", "Infection treated: slows the deterioration clock"), ("EFF-ADRENALINE", "Allergic reaction reverses"),
    ("EFF-GLUCOSE-RISE", "Low blood sugar corrects"), ("EFF-POTASSIUM-LOWER", "Potassium falls"), ("EFF-OPIOID-REVERSAL", "Slow breathing from an opioid reverses"),
]


def T(id, kind, label, medicine, *, checks=None, requires=(), iv_phrase="", restricted=(), timing_phrase="", effects=(), allergen_group=""):
    return dict(id=id, kind=kind, label=label, medicine=medicine, checks=checks, requires=list(requires), iv_phrase=iv_phrase, restricted=list(restricted), timing_phrase=timing_phrase, effects=list(effects),
                allergen_group=allergen_group)


# (tag, phrase that must appear in the medicine row's care column)
TREATMENTS = [
    T("TR-PARACETAMOL", "medicine", "Paracetamol", "paracetamol", restricted=[("severe_liver_disease", "Severe liver disease")], effects=["EFF-ANALGESIA"]),
    T("TR-NSAID", "medicine", "NSAIDs", "nsaids",
      restricted=[("kidney_injury", "Kidney injury"), ("dehydration", "dehydration"), ("peptic_ulcer_bleeding", "stomach ulcers/bleeding"), ("blood_thinner", "blood thinners"),
                  ("asthma", "asthma"), ("pregnancy", "pregnancy"), ("heart_failure", "heart failure")], effects=["EFF-ANALGESIA"]),
    T("TR-OPIOID", "medicine", "Opioids", "opioids", requires=["iv_access"], iv_phrase="drip", restricted=[("kidney_failure", "kidney failure")], effects=["EFF-ANALGESIA"]),
    T("TR-ANTIEMETIC", "medicine", "Antiemetics", "antiemetics", restricted=[("long_qtc", "long QTc"), ("bowel_obstruction", "bowel blockage")], effects=["EFF-ANTIEMETIC"]),
    T("TR-IV-FLUIDS", "fluid", "IV fluids", "iv_fluids", requires=["iv_access"], iv_phrase="iv_fluids", restricted=[("heart_failure", "Heart failure"), ("kidney_failure", "kidney failure")],
      effects=["EFF-FLUID-BOLUS"]),
    T("TR-ABX-PROPHYLAXIS", "medicine", "Preventive antibiotic (planned surgery, higher risk)", "abx_s1_higher_risk", requires=["iv_access"], iv_phrase="2 g IV",
      timing_phrase="Within 60 minutes", allergen_group="antibiotic"),
    T("TR-ABX-S2", "medicine", "Broad antibiotics (emergency appendicitis)", "abx_s2_all", timing_phrase="As soon as decided", effects=["EFF-ANTIBIOTIC"], allergen_group="antibiotic"),
    T("TR-STEROID-COVER", "medicine", "Steroid cover at induction", "steroids", requires=["iv_access"], iv_phrase="IV at induction", timing_phrase="at induction"),
    T("TR-VTE-PROPHYLAXIS", "medicine", "Clot prevention (VTE prophylaxis)", "vte_prophylaxis", timing_phrase="6-12 hours after surgery"),
    T("TR-ADRENALINE", "medicine", "Adrenaline (anaphylaxis)", "adrenaline", effects=["EFF-ADRENALINE"]),
    T("TR-FAST-SUGAR", "medicine", "Fast-acting sugar (low blood sugar, can swallow)", "fast_sugar", effects=["EFF-GLUCOSE-RISE"]),
    T("TR-IV-GLUCOSE", "medicine", "IV glucose (low blood sugar, cannot swallow)", "iv_glucose", requires=["iv_access"], iv_phrase="IV 10% glucose", effects=["EFF-GLUCOSE-RISE"]),
    T("TR-HYPERKALAEMIA", "medicine", "Hyperkalaemia treatment", "hyperkalaemia", requires=["iv_access"], iv_phrase="calcium gluconate IV", effects=["EFF-POTASSIUM-LOWER"]),
    T("TR-NALOXONE", "medicine", "Naloxone (opioid overdose)", "naloxone", effects=["EFF-OPIOID-REVERSAL"]),
    T("TR-SEPSIS-BUNDLE", "medicine", "Sepsis emergency treatment", "sepsis_emergency", requires=["iv_access"], iv_phrase="bolus", effects=["EFF-FLUID-BOLUS", "EFF-ANTIBIOTIC"], allergen_group="antibiotic"),
    T("TR-OXYGEN", "oxygen", "Oxygen", None, checks=["identity", "indication", "restriction", "order", "documentation"], effects=["EFF-OXYGEN"]),
    T("TR-IV-ACCESS", "procedure", "IV access (cannula)", None, checks=["identity", "order", "documentation"]),
]

# Treatments with no medicines.csv row: where the book states what they rest on.
NON_MEDICINE = {
    "TR-OXYGEN": ("SR-0986", [("co2_retainer", "SR-0986")]),
    "TR-IV-ACCESS": ("SR-4103", []),
}


DOSE_LABEL_ROWS = {"EDUCATIONAL_EXAMPLE": ("policy_educational", "EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION"),
                   "PATIENT_SPECIFIC": ("policy_patient_specific", "PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED"),
                   "INSTITUTIONAL": ("policy_institutional", "CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION")}


def read_csv(name: str) -> dict[str, dict]:
    with open(ROOT / "content" / name, encoding="utf-8", newline="") as f:
        return {r["id"]: r for r in csv.DictReader(f)}


def contains(text: str, phrase: str) -> bool:
    return phrase.lower().replace("–", "-") in text.lower().replace("–", "-")


def build_doc() -> dict:
    meds = read_csv("medicines.csv")
    labels = read_csv("drug_label_safety.csv")
    clashes = read_csv("treatment_clashes.csv")
    if "SR-0986" != clashes["low_oxygen_copd"]["ref"]:
        raise SystemExit("treatment_clashes.csv: the oxygen row is no longer SR-0986; update NON_MEDICINE")
    treatments = []
    for t in TREATMENTS:
        entry: dict = {"id": t["id"], "kind": t["kind"], "label": t["label"]}
        if t["medicine"]:
            row = meds[t["medicine"]]
            text = " ".join(row[c] for c in ("group", "v_for", "v_care", "v_timing", "v_example"))
            entry["medicine"] = {"id": row["id"], "ref": row["ref"]}
            policy = row["d_policy"] or "NONE"
            entry["dose_policy"] = policy
            if policy == "EDUCATIONAL_EXAMPLE":
                if not row["v_example"] or not row["v_source"]:
                    raise SystemExit(f"{t['id']}: an educational example needs its text and its source in medicines.csv")
                entry["dose_display"] = {"text": row["v_example"], "source": row["v_source"], "reviewer": REVIEWER}
            else:
                entry["dose_display"] = None
            restricted = []
            for tag, phrase in t["restricted"]:
                if not contains(row["v_care"], phrase):
                    raise SystemExit(f"{t['id']}: the care column of medicines.csv/{row['id']} does not contain '{phrase}'")
                restricted.append({"tag": tag, "ref": row["ref"]})
            if t["iv_phrase"] and not contains(text, t["iv_phrase"]):
                raise SystemExit(f"{t['id']}: medicines.csv/{row['id']} does not contain the IV wording '{t['iv_phrase']}'")
            if t["timing_phrase"] and not contains(row["v_timing"] + " " + row["v_example"], t["timing_phrase"]):
                raise SystemExit(f"{t['id']}: medicines.csv/{row['id']} does not contain the timing wording '{t['timing_phrase']}'")
            label_row = labels.get(row["id"])
            entry["label_row"] = {"id": label_row["id"], "ref": label_row["ref"]} if label_row else None
            refs = [row["ref"], "SR-4105"]
        else:
            ref, restricted_pairs = NON_MEDICINE[t["id"]]
            entry["medicine"] = None
            entry["dose_policy"] = "NONE"
            entry["dose_display"] = None
            restricted = [{"tag": tag, "ref": r} for tag, r in restricted_pairs]
            entry["label_row"] = None
            refs = [ref, "SR-4105"]
        entry["allergen_group"] = t["allergen_group"] or None
        if t["allergen_group"] and t["medicine"] and not contains(" ".join(meds[t["medicine"]][c] for c in ("group", "v_for", "quote")), "antibiotic"):
            raise SystemExit(f"{t['id']}: medicines.csv/{t['medicine']} does not mention antibiotics")
        entry["requires"] = t["requires"]
        if t["checks"] is not None:
            entry["required_checks"] = list(t["checks"])
        else:   # the standard list for a medicine; restriction and timing only where the book gives a reason for them
            entry["required_checks"] = (["identity", "allergy_known", "indication"] + (["restriction"] if restricted else []) + (["timing"] if t["timing_phrase"] else [])
                                        + ["order", "documentation"])
        entry["restricted_by"] = restricted
        entry["effects"] = t["effects"]
        entry["refs"] = sorted(set(refs))
        if "restriction" in entry["required_checks"] and not restricted:
            raise SystemExit(f"{t['id']}: lists the restriction check but names no restriction")
        if "restriction" not in entry["required_checks"] and restricted:
            raise SystemExit(f"{t['id']}: names restrictions but does not list the restriction check")
        if ("timing" in entry["required_checks"]) != bool(t["timing_phrase"]):
            raise SystemExit(f"{t['id']}: the timing check and the timing wording must go together")
        treatments.append(entry)
    checks_csv = read_csv("drug_checks.csv")
    dose_labels = {}
    for policy, (rid, text) in DOSE_LABEL_ROWS.items():
        if not contains(checks_csv[rid]["quote"], text):
            raise SystemExit(f"drug_checks.csv/{rid} no longer contains '{text}'")
        dose_labels[policy] = {"text": text, "ref": checks_csv[rid]["ref"]}
    dose_labels["NONE"] = {"text": "", "ref": checks_csv["policy_educational"]["ref"]}
    severe_quote = meds["abx_allergy_severe"]["quote"]
    for word in SEVERE_REACTION_WORDS:
        if not contains(severe_quote, word):
            raise SystemExit(f"medicines.csv/abx_allergy_severe does not contain '{word}'")
    clash_quote = clashes["antibiotic_penicillin_allergy"]["v_clash"]
    for word in ALLERGY_GROUPS["antibiotic"]:
        if not contains(clash_quote, word) and not contains(meds["abx_allergy_severe"]["quote"] + " " + meds["abx_allergy_mild"]["quote"] + " " + "antibiotic", word):
            raise SystemExit(f"the antibiotic-allergy rows do not contain '{word}'")
    sheet = OXYGEN_SHEET.read_text(encoding="utf-8").lower()
    for word in ("nasal cannula", "non-rebreather", "simple"):
        if word not in sheet:
            raise SystemExit(f"{OXYGEN_SHEET.name} does not mention '{word}'")
    return {
        "workflow": {"confirm_seconds": WORKFLOW["confirm_seconds"], "administer_seconds": WORKFLOW["administer_seconds"], "ref": "SR-4107"},
        "oxygen_devices": [{"id": i, "label": l} for i, l in OXYGEN_DEVICES],
        "allergy": {"groups": [{"id": g, "keywords": k} for g, k in ALLERGY_GROUPS.items()], "severe_reaction_words": SEVERE_REACTION_WORDS,
                    "outcome": ALLERGY_OUTCOME, "event": HYPERSENSITIVITY_EVENT, "refs": ["SR-0973", "SR-1311", "SR-4108"]},
        "dose_labels": dose_labels,
        "lines": [{"id": i, "text": t} for i, t in LINES.items()],
        "checks": [{"id": c, "phase": p, "severity": s, "meaning": m} for c, p, s, m in CHECKS],
        "prerequisites": [{"id": i, "reject": r, "meaning": m} for i, r, m in PREREQUISITES],
        "effects": [{"id": i, "note": n} for i, n in EFFECTS],
        "refs": ["SR-4100", "SR-4101", "SR-4102", "SR-4103", "SR-4104", "SR-4106", "SR-4109"],
        "treatments": treatments,
    }


def register_rows(doc: dict) -> list[list[str]]:
    n = len(doc["treatments"])
    src = ["ProcSense project team", "content/treatments.json (built by tools/content/build_treatments.py)"]

    def row(i, item, value, evidence, note):
        return [f"SR-{i}", item, value, "", "", src[0], src[1], "", evidence, "VERIFIED", TODAY, "Project team", APPROVAL, note]

    return [
        row(4100, "Treatment library: which treatments exist", f"{n} treatments: {', '.join(t['id'] for t in doc['treatments'])}", "B",
            "Each medicine treatment is one administrable group from content/medicines.csv (its own register row is cited by the treatment); oxygen and IV access are added because the book's cases and clashes use them. Doses are never stored; see the dose policy on each treatment."),
        row(4101, "Treatment checks: vocabulary and phases", "; ".join(f"{c['id']} ({c['phase']})" for c in doc["checks"]), "D",
            "The checks come from Implementation Guide B-M14 (identity, allergy_known, indication, restriction, timing, order, documentation). 'before' checks are judged when the nurse is asked to give the treatment, 'after' when it is given."),
        row(4102, "Treatment checks: what a missing or failed check costs", "; ".join(f"{c['id']}={c['severity']}" for c in doc["checks"]), "D",
            "Severity is a simulator design value used by the scoring step. Identity, allergy and a restriction that applies are critical because the book's role convention says the trainee never gives a patient-specific medicine without these checks (SR-1400, SR-1405)."),
        row(4103, "Treatment prerequisite: IV access", "IV_ACCESS_REQUIRED", "D",
            "An IV treatment is refused until IV access is in place (Implementation Guide test T-TR5; walkthrough Part E item 25, IV cannula kit). Which treatments need it rests on wording in their medicines.csv row (IV, drip, bolus); where the row only implies the route (bolus) this is a design inference."),
        row(4104, "Treatment effect ids", ", ".join(e["id"] for e in doc["effects"]), "D",
            "Names only. The physiology step (p0-physiology-events-behaviour-s05) defines what each effect does and owns content/physio/effects.json; until it exists this file is the list of ids treatments may use."),
        row(4105, "Treatment required checks per treatment", "; ".join(f"{t['id']}: {' '.join(t['required_checks'])}" for t in doc["treatments"]), "D",
            "Each treatment lists its checks explicitly (no implicit checks in code). Timing is listed only where the medicines.csv row states timing wording; restriction only where the care column names a condition; emergency treatments keep identity, allergy, order and documentation."),
        row(4107, "Medicine workflow: confirmation and administration delays", f"clinician confirms after {WORKFLOW['confirm_seconds']} s; nurse gives {WORKFLOW['administer_seconds']} s later", "D",
            "Simulator design values (p0-clinical-logic-s11): how long the clinician and the nurse take. No source gives them; they are tuned with testers and kept in content, never in code."),
        row(4108, "Medicine workflow: what happens at an allergen or another critical problem", f"practice (beginner): {ALLERGY_OUTCOME['practice']}; practice (advanced): {ALLERGY_OUTCOME['advanced']}; assessment: {ALLERGY_OUTCOME['assessment']} ({HYPERSENSITIVITY_EVENT} eligible)", "D",
            "Walkthrough 14.4 and Implementation Guide test T-TR3: a beginner has the nurse challenge the request, an advanced trainee has it given and the reaction happens. Until the difficulty setting exists (DT-056) practice mode is the beginner and assessment mode the advanced setting. The allergy group words come from the antibiotic-allergy rows (SR-0973, SR-1311); the severe-reaction words are those of the book's severe row (throat swelling, collapse, anaphylaxis)."),
        row(4109, "Medicine workflow: approved lines and dose labels", "; ".join(f"{i}" for i, _ in LINES.items()), "D",
            "The clinician's and the nurse's lines are wording the project team wrote; they state no clinical fact and no dose. A dose is shown only under the label of its dose policy (SR-1406, SR-1407, SR-1408): an educational example with its source, or the words 'the clinician decides'."),
        row(4106, "Treatment restriction tags", "; ".join(sorted({r['tag'] for t in doc['treatments'] for r in t['restricted_by']})), "B",
            "Each tag restates a condition named in the care column of the treatment's medicines.csv row (or the treatment-clash row for oxygen); the generator refuses a tag whose wording is not in that column."),
    ]


def merged_register(rows: list[list[str]]) -> str:
    with open(REGISTER, encoding="utf-8", newline="") as f:
        reader = list(csv.reader(f))
    header, existing = reader[0], reader[1:]
    if header != REGISTER_HEADER:
        raise SystemExit(f"register header changed: {header}")
    keep = [r for r in existing if int(r[0][3:]) not in BLOCK]
    merged = sorted(keep + rows, key=lambda r: r[0])
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(header)
    w.writerows(merged)
    return buf.getvalue()


def main(argv: list[str]) -> int:
    doc = build_doc()
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    register = merged_register(register_rows(doc))
    if "--check" in argv:
        bad = []
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            bad.append("content/treatments.json")
        if REGISTER.read_text(encoding="utf-8") != register:
            bad.append("the SR-4100..SR-4109 rows of docs/research/source_register.csv")
        if bad:
            print("FAIL  out of date: " + ", ".join(bad) + "; run tools/content/build_treatments.py")
            return 1
        print(f"content/treatments.json is up to date ({len(doc['treatments'])} treatments)")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    REGISTER.write_text(register, encoding="utf-8", newline="")
    print(f"wrote {OUT.relative_to(ROOT)} ({len(doc['treatments'])} treatments) and the SR-4100..SR-4109 register rows")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
