"""Second look at the register rows of evidence type A (a guideline cited by the book) whose original had not been opened (10 October 2026).

Each rule matches register items (the text 'Describe: key') and says what the check found:
  - VERIFIED  the original, or two or more independent published summaries, confirm EVERY number in the row (the level is named: primary = the guideline's own page or paper was opened;
              secondary = independent published summaries). The claim is logged in docs/research/clinical_verification.md under the V-id.
  - DESIGN    the row is a simulator design value or a teaching convention, not a guideline claim (or the book's value deliberately differs from the guideline, with the issue number); it is
              re-typed D and keeps a note saying what the guideline says. Nothing is hidden: the difference is stated.
  - OPEN      the check was tried and did not confirm every number (a source could not be opened, or a number was not found): the row stays UNVERIFIED and says why.
Nothing is VERIFIED on memory alone. The first matching rule wins.
"""
from __future__ import annotations

import re

V, D, O = "VERIFIED", "DESIGN", "OPEN"
RULES: list[tuple[str, str, str, str, str]] = []


def rule(pattern: str, kind: str, vid: str, level: str, text: str) -> None:
    RULES.append((pattern, kind, vid, level, text))


# ---------------------------------------------------------------- vital-sign display bands (Part 17)
rule(r"Vital-sign display band: (hr\.(normal|a\.watch|b\.watch|worry|a\.emergency|b\.emergency)|sbp\.(normal|watch|worry|a\.emergency)|rr\.(normal|a\.watch|b\.watch|a\.emergency|b\.emergency)"
     r"|spo2\.(normal|watch|worry|a\.emergency)|temp\.(a\.watch|worry|emergency)|acvpu\.(normal|emergency)|news2\.(normal|watch|worry|emergency))$",
     V, "V-01", "primary", "The same cells as the RCP NEWS2 chart text kept in docs/research/official_quotes/rcp_news2_2017.txt.")
rule(r"Vital-sign display band: rr\.worry$", V, "V-24", "secondary", "Respiratory rate 22 or more is the qSOFA criterion (Sepsis-3).")
rule(r"Vital-sign display band: sbp\.map\.emergency$", V, "V-34", "secondary", "Surviving Sepsis Campaign 2021: initial mean arterial pressure target 65.")
rule(r"Vital-sign display band: bp_high\.(normal|watch|worry|emergency)$", V, "V-33", "secondary",
     "ESH 2023 grades (140-159/90-99, 160-179/100-109, 180/110 or more) and NICE NG136 stages (140/90, 160/100, severe 180/120).")
rule(r"Vital-sign display band: urine\.normal$", V, "V-34", "secondary", "KDIGO: urine output below 0.5 mL/kg/h is oliguria; stage 1 from 6 hours.")
rule(r"Vital-sign display band: shock\.(normal|watch)$", V, "V-34", "secondary", "Shock index (heart rate / systolic BP): normal 0.5 to 0.7, acceptable up to 0.9 in some studies; above 0.9 abnormal.")
rule(r"Vital-sign display band: (temp\.(normal|b\.watch))$", D, "ISS-0014", "design",
     "Display zone, not a scoring cell: the RCP chart scores 36.1 to 38.0 as 0 (and 38.1 to 39.0 as 1), the book's zones split at 38.0; both are kept on purpose (ISS-0014).")
rule(r"Vital-sign display band: (spo2\.critical\.critical|acvpu\.worry|pain\.|crt\.|urine\.(watch|worry|emergency))", D, "", "design",
     "Simulator display zone (teaching colour), not a guideline cut-off; the scoring cells are in news2_scoring.")

# ---------------------------------------------------------------- NEWS2 and sepsis tiers
rule(r"NEWS2 response tier: (0|1-4)$", V, "V-01", "primary", "RCP chart 2: aggregate 0 to 4 is the low band with a ward-based response; the finer split of 0 is the book's.")
rule(r"Sepsis tier \(NICE NG253\): (high|moderate)\.single3$", D, "ISS-0005", "design",
     "The book upgrades the tier when one parameter scores 3; NICE NG253 1.6.2 asks for a high-priority review for a definite decision. The safer simulator rule is kept by decision (ISS-0005).")
rule(r"Sepsis tier \(NICE NG253\): (high|moderate|low)\.antibiotics$", V, "V-40", "primary",
     "NICE NG253 (page opened): antibiotics within 1 hour for high risk (1.8.3), deferral for up to 3 hours at medium risk (1.8.17) and up to 6 hours at low risk; the AoMRC statement v2.0 gives the same hours.")
rule(r"Sepsis tier \(NICE NG253\): moderate\.review$", V, "V-44", "primary", "NICE NG253 (page opened): a clinician with core competencies (FY2 or above) reviews the person and the venous lactate within 1 hour of being assessed as medium risk.")
rule(r"Sepsis tier \(NICE NG253\): high\.review$", D, "V-44", "design", "NG253 1.8.2 asks for an urgent assessment by an FY2-level clinician but gives no hour for it; the book's 1 hour is kept as the simulator's review clock.")

# ---------------------------------------------------------------- fasting
rule(r"Fasting rule: (clear_liquids|milk|light_meal|heavy_meal)$", V, "V-09", "secondary", "ASA practice guideline (2017, clear liquids reaffirmed 2023), ANZCA PG07: clear liquids 2 h, light meal and non-human milk 6 h, fried or fatty meal 8 h or more.")
rule(r"Fasting rule: breast_milk$", V, "V-32", "secondary", "ASA fasting table: breast milk 4 hours.")
rule(r"Fasting rule: chewing_gum$", V, "V-32", "secondary", "ASA 2023: no need to delay the procedure after confirming the gum is removed.")
rule(r"Fasting rule: (regular_tablets|breach|emergency_full_stomach)$", D, "", "design", "Simulator rule for what the trainee does (tell the anaesthetist, record the time); not a guideline cut-off.")

# ---------------------------------------------------------------- decision thresholds (Part 17)
for key, vid, level, text in [
    ("fasting_planned", "V-09", "secondary", "ASA/ANZCA: 2 h clear liquids, 6 h light meal, 8 h fried or fatty meal."),
    ("s1_bp_postpone", "V-07", "secondary", "CPOC/AAGBI and British Hypertension Society: below 180/110 in clinic does not delay elective surgery; the GP target 160/100 is NICE NG136 stage 2."),
    ("s1_diabetes_postpone", "V-07", "secondary", "JBDS perioperative guidance: HbA1c below 69 mmol/mol (8.5%) for elective surgery."),
    ("periop_glucose_target", "V-20", "secondary", "JBDS: 6 to 10 mmol/L, 6 to 12 acceptable."),
    ("duct_stone_high_risk", "V-03", "secondary", "ASGE 2019: high risk if a stone is seen, cholangitis, or bilirubin above 4 mg/dL (68 micromol/L) with a dilated duct."),
    ("gallbladder_moderate_grade", "V-06", "secondary", "TG18 Grade II: white count above 18, a tender mass, or symptoms longer than 72 hours."),
    ("appendix_abnormal_width", "V-35", "secondary", "Ultrasound: a non-compressible appendix with an outer diameter above 6 mm."),
    ("air_bands", "V-26", "secondary", "AIR score: 0-4 low, 5-8 indeterminate, 9-12 high."),
    ("aas_bands", "V-25", "primary", "Adult Appendicitis Score paper (Sammalkorpi 2014): low 0-10, intermediate 11-15, high 16 or more."),
    ("sepsis_antibiotics", "V-40", "secondary", "AoMRC v2.0 and NICE NG253: 1, 3 and 6 hour maximums by risk level."),
    ("antibiotic_before_incision", "V-31", "secondary", "WHO checklist asks if prophylaxis was given within the last 60 minutes. Note: the WHO 2018 prophylaxis guideline says within 120 minutes and NICE NG125 says on starting anaesthesia; the checklist wording is the teaching rule."),
    ("theatre_temperature", "V-22", "primary", "NICE CG65: warm a patient below 36.0 degC before anaesthesia."),
    ("sign_in_blood_loss", "V-31", "secondary", "WHO checklist: risk of more than 500 mL blood loss (7 mL/kg in children)."),
    ("appendectomy_timing", "V-12", "secondary", "WSES Jerusalem 2025: laparoscopic appendectomy within 24 hours of admission when indicated."),
    ("acute_cholecystitis_surgery", "V-39", "secondary", "NICE CG188: laparoscopic cholecystectomy within 1 week of diagnosis; Tokyo guidelines: early surgery (within 72 hours in TG13)."),
]:
    rule(rf"Decision threshold: {key}$", V, vid, level, text)

# ---------------------------------------------------------------- scores
rule(r"Score item: alvarado\.", V, "V-27", "secondary", "Alvarado 1986: migration 1, anorexia 1, nausea or vomiting 1, tenderness 2, rebound 1, temperature 1, leucocytosis 2, left shift (neutrophils above 75%) 1.")
rule(r"Score item: air\.", V, "V-26", "secondary", "Andersson 2008 AIR score table (vomiting 1; rebound 1/2/3; temperature 38.5 1; WBC 10-14.9 1, 15 or more 2; neutrophils 70-84 1, 85 or more 2; CRP 10-49 1, 50 or more 2).")
rule(r"Score item: aas\.", V, "V-25", "primary", "Sammalkorpi 2014 Table 2 (opened): every point value and range matches.")
rule(r"Score band: alvarado\.", V, "V-27", "secondary", "Alvarado bands: 0-4 unlikely, 5-6 compatible, 7-8 probable, 9-10 very probable.")
rule(r"Score band: air\.", V, "V-26", "secondary", "AIR bands 0-4, 5-8, 9-12; maximum 12.")
rule(r"Score band: aas\.(low|intermediate|high)$", V, "V-25", "primary", "Sammalkorpi 2014: low 0-10, intermediate 11-15, high 16 or more.")
rule(r"Score band: aas\.max$", V, "V-25", "primary", "Maximum 23 follows from the table (RLQ pain 2 + relocation 2 + tenderness 3 + guarding 4 + WBC 3 + neutrophils 4 + CRP 5).")
rule(r"Score worked example: (input|air\.|aas\.)", V, "V-25", "primary",
     "Recomputed from the verified item points: AIR 1+1+2+0+1+1+1 = 7 (indeterminate); AAS 2+2+3+2+2+3+5 = 19 (high). The book's 'aas.rebound' row is the mild-guarding item (2 points).")
rule(r"Score fact: tg18_cholecystitis\.(grade_ii|grade_i)$", V, "V-06", "secondary", "TG18 Grade II (white count above 18, tender mass, symptoms over 72 hours, marked local inflammation); Grade I otherwise.")
rule(r"Score fact: tg18_cholangitis\.(group_a|group_b|group_c)$", V, "V-04", "secondary", "TG18 cholangitis criteria A (inflammation), B (cholestasis), C (imaging).")
rule(r"Score fact: tg18_cholangitis\.diagnosis$", D, "", "design", "Simulator rule: cholangitis is handled as an emergency.")
rule(r"Score fact: asge\.", V, "V-03", "secondary", "ASGE 2019 choledocholithiasis risk classes.")
rule(r"Score fact: asa\.", V, "V-28", "secondary", "ASA Physical Status classification I to VI and the E modifier.")
rule(r"Score fact: airway\.mallampati$", V, "V-30", "secondary", "Mallampati classes I to IV; III and IV raise the chance of a difficult intubation.")
rule(r"Score fact: stopbang\.", V, "V-29", "secondary", "STOP-Bang: 8 items (snoring, tired, observed apnoea, pressure, BMI above 35, age above 50, neck above 40 cm, male); 0-2 low, 3-4 intermediate, 5-8 high.")

# ---------------------------------------------------------------- medicines
rule(r"Medicine plan: sglt2$", V, "V-37", "secondary", "CPOC and the international consensus: withhold before surgery and check ketones; the US label's longer hold is noted in the row.")
rule(r"Medicine plan: doacs$", V, "V-08", "secondary", "PAUSE: apixaban omitted 1 day (low bleeding risk) or 2 days (high) before surgery with normal kidney function.")
rule(r"Medicine plan: adrenaline$", V, "V-38", "secondary", "The educational example matches the UK Resuscitation Council anaphylaxis guidance and the adrenaline SmPC for adults and children over 12.")
rule(r"Medicine plan: fast_sugar$", V, "V-38", "secondary", "The educational example matches the JBDS hospital hypoglycaemia guideline for a conscious patient.")
rule(r"Medicine plan: abx_s2_complicated$", V, "V-12", "secondary", "WSES 2025: 2 to 3 days of antibiotics after source control in complicated appendicitis.")
rule(r"Medicine plan: reversal$", D, "", "design", "Reversal is a senior and haematology decision; the simulator only flags it.")
rule(r"Medicine plan: .*", O, "", "open", "Teaching summary of medicine cautions. The relevant NICE and CPOC pages were not reachable (HTTP 403) or did not state every number; the openFDA label wording for the group is in drug_label_safety.csv.")

# ---------------------------------------------------------------- drug rules, WHO checklist, ultrasound finding codes, tests
rule(r"Drug rule: ", D, "", "design", "Role convention and safety questions of the simulator (the trainee never prescribes); not a guideline claim.")
rule(r"WHO checklist item: extension\.", D, "", "design", "A ProcSense teaching extension, shown apart from the WHO items; the book says it is not a WHO item.")
rule(r"WHO checklist item: ", V, "V-31", "secondary", "WHO Surgical Safety Checklist (first edition 2008): the item and its wording match the published checklist (three published copies and the WHO-based hospital copies).")
rule(r"Ultrasound finding code: US-AP-01$", V, "V-35", "secondary", "Non-compressible, blind-ending appendix with outer diameter above 6 mm and inflamed fat.")
rule(r"Ultrasound finding code: US-GB-03$", V, "V-03", "secondary", "Common bile duct above 6 mm counts as dilated (ASGE).")
rule(r"Ultrasound finding code: ", D, "", "design", "ProcSense finding code that groups the book's wording into one machine value for the picture library; the pictures are chosen by the code.")
rule(r"Test indication: ", O, "", "open", "NICE NG45 sets tests by ASA grade and grade of surgery; its page returned HTTP 403, so the table was not read and the book's one-line rule is not confirmed.")
rule(r"Score fact: (vte\.|airway\.(mouth_opening|thyromental|neck_movement))", O, "", "open", "NICE NG89 and the airway measurements were not read in full; not every number was confirmed.")
rule(r"Score fact: tg18_cholecystitis\.", O, "", "open", "Only Grade I and II were checked (V-06); the diagnostic groups and Grade III organ-failure limits were not re-read.")


# ---------------------------------------------------------------- second pass (10 Oct 2026, later): NICE NG45 text opened (BJUI reproduction), NG89 summaries, openFDA labels
_FIRST: list[tuple[str, str, str, str, str]] = []


def first(pattern: str, kind: str, vid: str, level: str, text: str) -> None:
    _FIRST.append((pattern, kind, vid, level, text))


first(r"Test indication: (pregnancy|fbc|u_and_e|hba1c|clotting|urine_dipstick|ecg|chest_xray)$", V, "V-41", "primary",
      "The planned-surgery (S1) side matches NICE NG45 (text opened): ask about pregnancy and test if in doubt; HbA1c if diabetic and none in 3 months; no routine urine dipstick or chest X-ray; FBC, kidney function, ECG and clotting by ASA grade, surgery grade and comorbidity. The emergency (S2) side is outside NG45, which covers elective surgery only, and is the book's practice.")
first(r"Test indication: ", D, "", "design", "The book's diagnostic work-up for the surgical problem (liver tests, inflammation, glucose, group and save, imaging, sepsis tests); NICE NG45 does not cover these, so there is no guideline row to match.")
first(r"Score fact: vte\.risk_factors_old$", V, "V-42", "secondary", "NICE NG89 surgical risk factors: age over 60, BMI over 30, anaesthetic and surgical time over 90 minutes (60 for pelvic or lower limb surgery).")
first(r"Medicine plan: vte_prophylaxis$", V, "V-42", "secondary", "NICE NG89: pharmacological prophylaxis from 6 to 12 hours after surgery and for at least 7 days after abdominal surgery when the clot risk outweighs the bleeding risk.")
first(r"Medicine plan: (paracetamol|opioids)$", V, "V-43", "primary", "openFDA label wording kept in content/drug_label_safety.csv supports the caution (severe liver disease for paracetamol; respiratory depression for opioids); the dose example is the book's own and is not used by the simulator.")
first(r"Score fact: vte\.(risk_factors|bleeding_risk_factors)$", V, "V-45", "primary", "NICE NG89 VTE risk assessment tool (opened): age over 60, BMI over 30, anaesthetic and surgical time over 90 minutes; bleeding risk includes platelets below 75.")
first(r"Medicine plan: cocp_hrt$", V, "V-45", "primary", "NICE NG89 1.3.13 (page opened): consider stopping oestrogen-containing contraceptives or HRT 4 weeks before elective surgery.")
first(r"Medicine plan: (iv_fluids|abx_s1_low_risk)$", V, "V-46", "primary", "NICE CG174 1.3.1 (page opened): crystalloid bolus for resuscitation, reassess; NICE NG125 1.2.13 (page opened): no routine antibiotic prophylaxis for clean uncomplicated surgery.")
first(r"Medicine plan: sepsis_emergency$", D, "ISS-0015", "design", "The book's example is the CG174 bolus; the current NICE NG253 (page opened) gives a smaller bolus with reassessment. The difference is logged (ISS-0015) and the debrief names the source of each; the simulator stores no volume.")
first(r"Medicine plan: (metformin|sulfonylureas)$", V, "V-47", "secondary", "CPOC and hospital peri-operative diabetes guidance: sulfonylureas are omitted while fasting; metformin is continued in most patients when at most one meal is missed.")
first(r"Medicine plan: steroids$", V, "V-48", "primary", "NICE NG243 1.4.8 (page opened): people having planned or emergency surgery are offered glucocorticoid cover; adrenal crisis is the risk of missing it.")
first(r"Medicine plan: (sglt2|metformin|sulfonylureas|insulin_long|insulin_other)$", V, "V-49", "primary", "CPOC Guideline for Perioperative Care for People with Diabetes (2023, PDF read), Practical Resource 1.2: SGLT2 inhibitors omitted the day before and the day of surgery with daily ketones; metformin as normal (lunchtime dose omitted if taken three times a day); sulfonylureas omitted on the morning of surgery; insulin modified when more than one meal is missed.")
first(r"Medicine plan: glp1$", V, "V-49", "primary", "CPOC 2023 (PDF read): GLP-1 agonists continued with precautions against pulmonary aspiration, noting that the ASA advises withholding them; the book's aspiration warning agrees.")
first(r"Medicine plan: dpp4$", D, "V-49", "design", "CPOC 2023 says take DPP4 inhibitors as normal; the book allows omitting them per local protocol. Both are acceptable teaching positions and the local protocol decides.")
RULES[:0] = _FIRST

URLS = {
    "V-01": "https://www.rcp.ac.uk/media/a4ibkkbf/news2-final-report_0_0.pdf", "V-03": "https://pmc.ncbi.nlm.nih.gov/articles/PMC8039746/", "V-04": "https://pubmed.ncbi.nlm.nih.gov/29032610/",
    "V-06": "https://pure.fujita-hu.ac.jp/en/publications/tokyo-guidelines-2018-diagnostic-criteria-and-severity-grading-of/", "V-07": "https://www.cpoc.org.uk/", "V-08": "https://pmc.ncbi.nlm.nih.gov/articles/PMC6686768/",
    "V-09": "https://asa2.silverchair.com/anesthesiology/article-split/138/2/132/137508/2023-American-Society-of-Anesthesiologists", "V-12": "https://wjes.biomedcentral.com/articles/10.1186/s13017-025-00582-x",
    "V-20": "https://abcd.care/", "V-22": "https://www.nice.org.uk/guidance/cg65", "V-24": "https://pubmed.ncbi.nlm.nih.gov/26903335/", "V-25": "https://pmc.ncbi.nlm.nih.gov/articles/PMC4087125/",
    "V-26": "https://link.springer.com/doi/10.1007/s00268-008-9649-y", "V-27": "https://litfl.com/alvarado-score/", "V-28": "https://pmc.ncbi.nlm.nih.gov/articles/PMC6319155/table/T1",
    "V-29": "https://pubmed.ncbi.nlm.nih.gov/26378880/", "V-30": "https://wikianesthesia.org/wiki/Mallampati_score", "V-31": "https://en.wikipedia.org/wiki/WHO_Surgical_Safety_Checklist",
    "V-32": "https://asa2.silverchair.com/anesthesiology/article-split/138/2/132/137508/2023-American-Society-of-Anesthesiologists",
    "V-33": "https://www.acc.org/Latest-in-Cardiology/Articles/2024/02/05/11/43/2023-ESH-Hypertension-Guideline-Update", "V-34": "https://sccm.org/sepsisguidelines",
    "V-35": "https://pmc.ncbi.nlm.nih.gov/articles/PMC4925633", "V-37": "https://www.ncbi.nlm.nih.gov/pmc/articles/PMC12464960/", "V-38": "https://www.medicines.org.uk/emc/product/3449/smpc",
    "V-39": "https://www.nice.org.uk/guidance/QS104/chapter/quality-statement-1-acute-cholecystitis", "V-40": "https://aomrc.org.uk/publication/statement-on-the-initial-antimicrobial-treatment-of-sepsis-v2-0",
    "V-41": "https://www.bjuinternational.com/?p=33086", "V-42": "https://gpnotebook.com/pages/respiratory-and-chest-medicine/deep-vein-thrombosis/risk-factors-and-associated-increased-risk-for-development-of-venous-thromboembolism-in-surgical-patients",
    "V-43": "https://open.fda.gov/apis/drug/label/", "V-44": "https://www.nice.org.uk/guidance/ng253/chapter/Managing-suspected-sepsis", "V-45": "https://www.nice.org.uk/guidance/ng89/chapter/Recommendations", "V-46": "https://www.nice.org.uk/guidance/cg174/chapter/1-Recommendations", "V-49": "https://cpoc.org.uk/sites/cpoc/files/documents/2024-05/CPOC-DiabetesGuideline2023.pdf", "V-48": "https://www.nice.org.uk/guidance/ng243/chapter/Recommendations", "V-47": "https://pharmaceutical-journal.com/article/ld/perioperative-management-of-type-2-diabetes-mellitus",
}


def check(item: str) -> tuple[str, str, str, str, str] | None:
    for pattern, kind, vid, level, text in RULES:
        if re.search(pattern, item):
            return kind, vid, level, text, pattern
    return None
