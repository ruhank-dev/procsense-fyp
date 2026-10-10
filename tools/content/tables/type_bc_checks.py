"""Second look at the register rows of evidence types B (the book's interpretation) and C (institution-specific values), 10 October 2026.

Same rules as type_a_checks.py (VERIFIED / DESIGN / OPEN, nothing VERIFIED on memory alone), applied to the rows that were still UNVERIFIED:
  C  laboratory normal ranges and bands, critical (telephone) limits: compared with the published UK reference-range database and telephoning limits that were opened;
     a local laboratory may use a different range, which is what type C means, and each row says what the published comparator is.
  B  the book's teaching script (step answers, what-ifs, flow stages, VR cards, player view, ladders): the structure is the simulator's design (typed D); the clinical numbers inside are
     the verified rows, and the statements the book itself marks as source backed were checked one by one (see V-67).
     Glossary entries: each term was looked up (docs/research/glossary_check.csv). Treatment clashes and sample-quality artefacts: each checked against a guideline, SmPC or paper.
"""
from __future__ import annotations

import csv
import re
from pathlib import Path

import type_a_checks as A

V, D, O = A.V, A.D, A.O
ROOT = Path(__file__).resolve().parents[3]
RULES: list[tuple[str, str, str, str, str]] = []


def rule(pattern: str, kind: str, vid: str, level: str, text: str) -> None:
    RULES.append((pattern, kind, vid, level, text))


# ------------------------------------------------------------------------------------------------ glossary (looked up one by one)
_GLOSSARY: dict[str, tuple[str, str]] = {}
_path = ROOT / "docs" / "research" / "glossary_check.csv"
if _path.exists():
    with open(_path, encoding="utf-8", newline="") as f:
        for r in csv.DictReader(f):
            _GLOSSARY[r["glossary_id"]] = (r["checked_against"], r["result"])


def glossary(item: str) -> tuple[str, str, str, str, str] | None:
    m = re.match(r"Glossary entry: (g\d{3})$", item)
    if not m or m.group(1) not in _GLOSSARY:
        return None
    src, res = _GLOSSARY[m.group(1)]
    return V, "V-61", "secondary", f"Looked up on 10 Oct 2026 in {src}: {res} (docs/research/glossary_check.csv).", "glossary"


# ------------------------------------------------------------------------------------------------ C: laboratory normal ranges
LEEDS = "Leeds Teaching Hospitals Blood Sciences Reference Range Database v3.20 (Pathology Harmony and WYAAT ranges, opened and read)"
NORMAL = {
    "hb": "adult women 120-160 and men 130-180 g/L; the book's 120-150 and 130-170 sit inside the spread of published adult ranges (the WHO anaemia limits are 120 and 130, V-18)",
    "wbc": "above 11 years 3.5-10.0; the book's 4.0-11.0 is a common local variant",
    "neut": "1.5-7.0 (Dacie and Lewis lists 2.0-7.5, the book's range)",
    "plt": "150-400, identical",
    "crp": "below 10 (Siemens stated range); the book's below 5 is a stricter local limit",
    "na": "133-146 (Pathology Harmony); the book's 135-145 is the narrower textbook range",
    "k": "above 17 years 3.5-5.3, identical",
    "urea": "above 17 years 2.5-7.8, identical",
    "creat": "18 years and over women 49-90, men 62-115 (Leeds); the book's 45-90 and 60-110 are within the usual variation",
    "bili": "below 21 umol/L (Pathology Harmony), the same upper limit as the book's 3-21",
    "alt": "above 19 years 40 U/L or less, identical",
    "alp": "adult 30-130 U/L, identical",
    "ggt": "women 6-40, men 12-68 U/L; the book's below 40 and below 60 are close local limits",
    "alb": "17 years and over 35-50 g/L, identical",
    "lac": "0.5-2.0 mmol/L (group consensus based on Pathology Harmony and NICE NG51), the same upper limit as the book's below 2.0",
    "hcg": "below 5 U/L, identical",
}
for an, text in NORMAL.items():
    rule(rf"Lab band: {an}\.normal\.", V, "V-59", "primary", f"{LEEDS}: {text}. Local laboratories may differ, which is why this is a type C row.")
rule(r"Lab band: egfr\.(normal|mild|significant|severe)\.", V, "V-62", "primary", "NICE NG203 (page opened): GFR category G1 90 or over, G2 60 to 89, G3 30 to 59 (G3a 45-59, G3b 30-44), G4 and G5 below 30.")
rule(r"Lab band: glu\.(normal|acceptable)\.", V, "V-49", "primary", "CPOC diabetes guideline 2023 (PDF read): keep capillary glucose 6-10 mmol/L (up to 12 acceptable).")
rule(r"Lab band: hba1c\.normal\.", V, "V-59", "primary", f"{LEEDS}: HbA1c below 42 normal and above 48 diagnostic (WHO), as in V-23.")
rule(r"Lab band: ket\.", V, "V-20", "secondary", "JBDS and CPOC (PDF read) ketone bands: below 0.6 normal, 0.6-1.4, 1.5-2.9, 3.0 or more ketoacidosis likely.")
rule(r"Lab band: bili\.severe\.", V, "V-03", "secondary", "Bilirubin above 68 micromol/L (4 mg/dL) is the ASGE high-risk limit for a duct stone.")
rule(r"Lab band: lip\.normal\.", V, "V-59", "primary", "The upper limit of the local lipase range; the table has no numeric value to check.")
rule(r"Lab band: lip\.significant\.", V, "V-63", "secondary", "Revised Atlanta classification: a lipase at least three times the upper limit of normal supports the diagnosis of acute pancreatitis.")
rule(r"Lab band: inr\.normal\.", V, "V-63", "secondary", "NHS laboratory pages: INR 0.8-1.2 in people not taking warfarin.")
rule(r"Lab band: lac\.(significant|severe)\.", V, "V-21", "secondary", "Sepsis-3 and the Surviving Sepsis Campaign: lactate 2 mmol/L or more marks hypoperfusion; 4 or more is the severe band used in sepsis tables (Newcastle phones at 5.0).")
rule(r"Lab band: qtc\.", V, "V-23", "secondary", "QTc prolonged above 450 ms (men) and 460 ms (women), above 500 ms high risk.")
rule(r"Lab band: hba1c\.(mild|significant)\.", V, "V-23", "secondary", "HbA1c 42-47 high risk, 48 or more diabetic (NICE); above 69 is the elective-surgery limit (V-07, CPOC).")
rule(r"Lab band: (k|na|hb|creat)\.(mild|significant|severe|critical)", V, "V-64", "secondary",
     "The severity bands follow the guideline bands already logged: potassium UKKA (V-15, V-51), sodium ESICM (V-19), haemoglobin WHO (V-18), creatinine KDIGO and the critical limits (V-17).")
rule(r"Lab band: neut\.(significant|severe)\.", V, "V-60", "primary", "Neutrophils below 1.5 is the lower limit of the adult range (Leeds) and below 0.5 is the telephoning limit (Newcastle haematology, RCPath-based).")
rule(r"Lab band: plt\.critical\.", V, "V-60", "primary", "Platelets below 30 is the telephoning limit (Newcastle haematology, RCPath-based).")
rule(r"Lab band: (crp|plt|wbc|neut|urea|alt|alp|ggt|alb|lip|inr|hcg|bili|glu|hb)\.", D, "", "design",
     "The mild, significant and severe cut-offs are the book's teaching bands (they set the colour and the urgency the simulator shows), not guideline limits; the normal range of this test is verified in its own row.")

# ------------------------------------------------------------------------------------------------ C: critical (telephone) limits
NEWC = "Newcastle Hospitals biochemistry and haematology telephoning limits (documents BS-CTG-Biochem-67 and BS-CTG-Haem-1, based on the Royal College of Pathologists guidance on critical results, opened and read)"
for key, text in [
    ("k.low", "potassium 2.5 or below"), ("k.high", "potassium 6.5 or above"), ("na.low", "sodium 120 or below"), ("na.high", "sodium 160 or above"), ("glu.low", "glucose 2.5 or below"),
    ("ca.low", "adjusted calcium 1.80 or below"), ("ca.high", "adjusted calcium 3.50 or above"), ("creat.high", "creatinine 354 or above"), ("crp.high", "CRP 300 or above"),
    ("hb.low", "haemoglobin below 70 g/L (normocytic) or below 50 (micro- or macrocytic)"), ("plt.low", "platelets below 30"), ("neut.low", "neutrophils below 0.5"),
    ("inr.high_on_warfarin", "INR above 5.0 is phoned to the requestor"),
]:
    rule(rf"Critical result \(the lab phones the doctor\): {re.escape(key)}$", V, "V-60", "primary", f"{NEWC}: {text}.")
rule(r"Critical result \(the lab phones the doctor\): lac\.high$", V, "V-21", "secondary", "Lactate 4 mmol/L or more is the severe sepsis band (Surviving Sepsis Campaign); Newcastle phones at 5.0, so local laboratories differ.")
rule(r"Critical result \(the lab phones the doctor\): (hb\.fall|inr\.high_unexpected)$", D, "", "design",
     "The book's own trigger: a fall of 20 g/L in haemoglobin and an unexpected INR of 1.5 are not in the telephoning limits read (Newcastle asks for a call about an unexpected grossly abnormal clotting result in a patient on no anticoagulant). The simulator keeps them as teaching triggers.")

# ------------------------------------------------------------------------------------------------ the remaining C rows
rule(r"Score fact: sort\.draft_choice$", V, "V-65", "primary", "NICE QS216 Perioperative care in adults, statement 2 (page opened): adults have an assessment of perioperative risk with a validated risk stratification tool; the standard names no tool, so SORT is the book's draft choice and the hospital decides.")
rule(r"Drug rule: policy_institutional$", D, "", "design", "Label the simulator uses for choices a hospital protocol decides; not a guideline claim.")
rule(r"WHO checklist item: sign_in\.02$", V, "V-31", "secondary", "WHO Surgical Safety Checklist sign-in item 'Is the site marked? Yes / Not applicable'; whether a site is marked is a hospital decision (type C).")

# ------------------------------------------------------------------------------------------------ B: treatment clashes
for key, vid, level, text in [
    ("fluid_heart_kidney", "V-44", "primary", "NICE NG253 (page opened): give the fluid bolus unless contraindicated (for example cardiac or renal failure), 250 ml boluses with reassessment after each, advice from a senior clinical decision maker after 1,000 ml."),
    ("pain_kidney", "V-64", "primary", "NICE NG148 (page opened) lists NSAIDs among drugs that can cause or worsen acute kidney injury; the UKCPA Handbook (morphine page) lists renal disease as a risk factor for opioid-induced ventilatory impairment."),
    ("pain_pregnancy", "V-64", "secondary", "Ibuprofen SmPCs (electronic Medicines Compendium): contraindicated in the last three months of pregnancy and to be avoided earlier if possible; paracetamol is the usual choice."),
    ("antibiotic_penicillin_allergy", "V-53", "primary", "BSACI beta-lactam allergy guideline (PDF read): the choice after a penicillin allergy label follows allergy assessment and local policy."),
    ("ct_pregnancy", "V-55", "primary", "WSES Jerusalem appendicitis guideline 2020 update (PDF read): ultrasound is the preferred initial imaging in pregnancy and MRI is sensitive and specific when it is unclear."),
    ("ct_dye_kidney", "V-64", "secondary", "NICE NG148: adults with an eGFR below 40 (below 30 carries clearly increased risk) are at particular risk of contrast-induced kidney injury; volume assessment and expansion are advised."),
    ("ct_dye_allergy", "V-64", "secondary", "Contrast-reaction guidance (ACR 2024 and RANZCR): for a previous mild reaction premedication is not recommended and an alternative agent is used; the policy is local."),
    ("antiemetic_qtc", "V-52", "primary", "Ondansetron SmPC (read): prolongs the QT interval in a dose-dependent manner."),
    ("metoclopramide_obstruction", "V-52", "primary", "Metoclopramide SmPC (read): contraindicated in gastrointestinal obstruction, perforation or haemorrhage."),
    ("blood_thinner_urgent_surgery", "V-50", "primary", "UKCPA Handbook (warfarin and dabigatran pages read): emergency surgery needs haematology advice and reversal or prohaemostatic agents."),
    ("antiplatelet_stent", "V-50", "primary", "UKCPA Handbook (clopidogrel page read): stopping raises the risk of major adverse cardiovascular events; cardiac decisions go to the multidisciplinary team."),
    ("insulin_nbm", "V-49", "primary", "CPOC diabetes guideline 2023 (PDF read): VRIII when fasting causes more than one missed meal; frequent glucose checks."),
    ("sglt2_sepsis", "V-49", "primary", "CPOC diabetes guideline 2023 (PDF read): measure capillary ketones when unwell or on an SGLT2 inhibitor even if glucose is normal."),
    ("steroid_sepsis", "V-48", "primary", "NICE NG243 (page opened): glucocorticoid cover for surgery and for physiological stress; adrenal crisis is the risk."),
    ("high_potassium_suxamethonium", "V-52", "primary", "Suxamethonium SmPC (electronic Medicines Compendium): contraindicated in pre-existing hyperkalaemia."),
    ("full_stomach_emergency", "V-64", "secondary", "NAP4 and rapid sequence induction literature: emergency patients who are not fully fasted should have a rapid sequence induction; aspiration was the commonest cause of anaesthetic airway death."),
    ("low_oxygen_copd", "V-64", "primary", "BTS guideline for oxygen use (summary read): target 94-98% for most patients and 88-92% for those at risk of hypercapnic respiratory failure."),
    ("confused_consent_emergency", "V-64", "primary", "Mental Capacity Act 2005, section 4 (legislation.gov.uk): decisions for a person who lacks capacity are taken in their best interests."),
]:
    rule(rf"Treatment clash: {key}$", V, vid, level, text)

# ------------------------------------------------------------------------------------------------ B: sample-quality artefacts
rule(r"Sample-quality artefact: haemolysed$", V, "V-51", "primary", "UKKA hyperkalaemia guideline (PDF read): in-vitro haemolysis raises the measured potassium and can misclassify patients.")
rule(r"Sample-quality artefact: drip_arm$", V, "V-66", "secondary", "Laboratory medicine references (ADLM Pearls of Laboratory Medicine; LabCE): a sample taken from the arm with the infusion is diluted by the fluid.")
rule(r"Sample-quality artefact: wrong_tube_edta$", V, "V-66", "secondary", "Annals of Clinical Biochemistry and Biochemia Medica: EDTA contamination of a serum sample raises potassium and lowers calcium, magnesium and ALP.")
rule(r"Sample-quality artefact: clumped$", V, "V-66", "secondary", "EDTA-dependent platelet clumping falsely lowers the platelet count (pseudothrombocytopenia); a citrate tube corrects it.")
rule(r"Sample-quality artefact: (mislabelled|rejected)$", D, "", "design", "Definition used by the simulator (a mislabelled sample belongs to someone else; the laboratory rejects an unusable sample); there is no number to check.")

# ------------------------------------------------------------------------------------------------ B: the book's teaching script and structure
rule(r"Score fact: asa\.expected_grades$", D, "V-28", "design", "The book's expected ASA grade for each example patient; the ASA classes and examples are verified (V-28) and the anaesthetist confirms the grade.")
rule(r"Priority ladder step: ", D, "", "design", "The book's own prioritisation ladder; its first step is the ABCDE approach (Resuscitation Council UK) and its time limits are the verified rows (NG253 V-44, potassium V-15).")
for fam in ("Flow stage", "Player-view stage", "VR reasoning card", "Priority scale \\(Part D\\)", "Decision outcome", "End state", "Problem type"):
    rule(rf"{fam}: ", D, "", "design", "Structure of the simulator (scenario flow, screens, scoring outcomes); the thresholds it uses are the verified rows.")
rule(r"Step answer: ", D, "V-67", "design",
     "The book's teaching script for this step: the simulator's design content. Its clinical thresholds are verified rows (NEWS2 V-01, NG253 V-44, fasting V-09, potassium V-15, WHO checklist V-31, diabetes V-49) and the statements the book marks as source backed were checked: NG45 tests V-41, hair removal with clippers (NICE NG125), warming (NICE CG65 V-22), NEWS2 ranges V-01.")
rule(r"What-if: ", D, "V-67", "design",
     "The book's what-if script: the simulator's design content. The clinical thresholds it uses are verified rows; checked individually: warming below 36.0 (NICE CG65 V-22), potassium fall after insulin-glucose of about 0.65 to 1.0 mmol/L at 30 to 60 minutes (UKKA 2026 guideline), anaphylaxis treatment (UK Resuscitation Council, V-38), duct-stone risk bands (V-03), sepsis timing (V-44).")


def check(item: str) -> tuple[str, str, str, str, str] | None:
    g = glossary(item)
    if g:
        return g
    for pattern, kind, vid, level, text in RULES:
        if re.search(pattern, item):
            return kind, vid, level, text, pattern
    return None


URLS = {
    "V-59": "https://www.leedsth.nhs.uk/wp-content/uploads/2026/08/BSHC-REC-317b_Blood-Sciences-Reference-Range-Database_v3.20.pdf",
    "V-60": "https://laboratories.newcastle-hospitals.nhs.uk/wp-content/uploads/2024/06/BS-CTG-Biochem-67-telephone-limits.pdf",
    "V-61": "https://en.wikipedia.org/wiki/Glossary_of_medicine", "V-62": "https://www.nice.org.uk/guidance/ng203/chapter/Recommendations",
    "V-63": "https://gut.bmj.com/content/62/1/102", "V-64": "https://www.nice.org.uk/guidance/ng148/chapter/Recommendations",
    "V-65": "https://www.nice.org.uk/guidance/qs216", "V-67": "https://www.nice.org.uk/guidance/ng125/chapter/Recommendations", "V-66": "https://myadlm.org/Science-and-Research/Clinical-Chemistry/Clinical-Chemistry-Trainee-Council/Pearls-of-Laboratory-Medicine-in-English/2016/Collecting-Blood-from-Patients-with-Vascular-Lines",
}
