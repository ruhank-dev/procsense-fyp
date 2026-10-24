"""The twelve new multi-problem combination cases (SC-21 to SC-32), DT-220 / p2-content-extensions-s02, authored 25 Oct 2026.

Each case is built from one fixture of tests/composition/compositions.json (the composition engine, tools/composer/multiproblem.py, chose the problems by the walkthrough's 14B.5 recipe from
problems of approved cases). The fixtures hold no patient, so everything here is AUTHORED: synthetic, invented patients, values written to be internally consistent and flagged against the
verified laboratory bands (docs/research/clinical_verification.md V-nn rows cited per case). No doses. Practice material only: not clinician-reviewed, not for assessment (ADR-0008).
The end state follows the fixture unless the case says otherwise in its recovery notes.
"""
from lib import D, E, O, P, R, S, mk, person
from risk import risk

CASES = {}
RISK = {}        # ASA range and anaesthetic plan per case, with the reason (same shape as risk.py)
FIXTURE = {}      # case id -> composition id of tests/composition/compositions.json
STORY_REF = "SR-4135"       # register row: what a combination case is and where it comes from
AUTHORING_REF = "SR-4136"   # register row: the authored parts of the combination cases


def cb(n, mp, scn, title, who, summary, complaint, hx, vitals, exam, results, scores, problems, decisions, end, correct, wrong, missed, recovery, remember, ver, **kw):
    cid = f"SC-{n}"
    FIXTURE[cid] = mp
    CASES[cid] = mk(cid, f"COMBINATION CARD {n} ({scn}) - {title}", scn, who, summary, complaint, hx, vitals, exam, results, scores, problems, decisions, end, correct, wrong, missed, recovery,
                    remember, ver, tier="P2", **kw)


# ----------------------------------------------------------------------------------------------------------------------------- S1
# SC-21: apixaban + acute cholecystitis + probable bile-duct stone  (MP-S1-da2ee785, redirected)
cb(21, "MP-S1-da2ee785", "S1", "Apixaban not stopped + acute cholecystitis + probable bile-duct stone", person("Mr", "Haroon Siddique", "M", 66, "pharmacy driver", 3, 8, "635021"),
   "Mr Haroon Siddique, 66, is booked for keyhole gallbladder removal at 10:00. His eyes look slightly yellow, he has right-sided pain, temperature 37.9, and he took apixaban last night at 20:00.",
   "I came for my gallbladder, but the pain has been worse since Tuesday and my wife says my eyes are yellow.",
   {"red_flags": "Right-sided pain for three days, dark urine, pale stools since yesterday. A shiver last night.", "past_history": "Atrial fibrillation (rate controlled), gallstones.",
    "medicines": "Apixaban twice a day and bisoprolol. I took the apixaban last night at eight, as usual; nobody told me to stop it.", "last_food": "Dinner at 19:30 yesterday.", "last_drink": "Water at 06:30."},
   [O("08:00", 88, 138, 82, 18, 97, 37.9, rhythm="irregular", pain=5, note="NEWS2 1 for temperature and pulse together; the numbers hide the three problems.", origin="authored"),
    O("09:30", 84, 136, 80, 17, 97, 37.6, rhythm="irregular", pain=4, cond="after_correct_action", note="After postponement and referral: stable.", origin="authored"),
    O("09:30", 102, 118, 70, 22, 95, 38.7, rhythm="irregular", pain=7, cond="if_no_action", note="Proceeding: cholangitis picture develops.", origin="authored")],
   [E("ex.general", "general", "Slightly yellow sclerae, comfortable at rest.", True), E("ex.abdomen", "abdomen", "Tender under the right ribs with a catch on deep breath in.", True, painful=True),
    E("ex.pulse", "pulse", "Irregular, rate controlled.", True)],
   [R("lab.bili", "Bilirubin", 48, "umol/L", "3-21", "high", "Raised: obstruction of the bile duct likely (above 4 mg/dL, 68 umol/L, with a dilated duct is high risk; this value alone is not)", None, 45),
    R("lab.alp", "ALP", 210, "U/L", "30-130", "high", "Raised, in keeping with a blocked duct", None, 45), R("lab.alt", "ALT", 96, "U/L", "7-40", "high", "Raised", None, 45),
    R("lab.wbc", "WBC", 14.2, "x10^9/L", "4.0-11.0", "high", "Raised: inflammation", None, 45), R("lab.crp", "CRP", 62, "mg/L", "below 5", "high", "Raised", None, 45),
    R("lab.creat", "Creatinine", 98, "umol/L", "60-110", "normal", "Normal: apixaban clearance is not reduced", None, 45),
    R("lab.us", "Ultrasound abdomen", "Gallstones with a thick wall; common bile duct 9 mm", "", "normal", "abnormal", "Dilated duct (above 6 mm) plus raised bilirubin: a stone in the duct is probable", None, 60)],
   [S("NEWS2", "1", "Temperature 37.9 scores 0 and pulse 88 scores 0; irregular rhythm is not scored, so the total understates the risk", "authored")],
   [P("sc21.doac", "safety_blocker", "Apixaban taken last night: bleeding risk at surgery", 1, "Apixaban is omitted for 2 days before a high-bleeding-risk operation with normal kidney function.", ["Confirm the time of the last dose", "Check kidney function", "Tell the surgeon and the anaesthetist"]),
    P("sc21.chole", "time_critical", "Acute cholecystitis with early systemic signs", 2, "Infection needs treatment and a surgical plan within days, not today's list.", ["Look for systemic signs", "Request inflammatory markers", "Surgical review"]),
    P("sc21.cbd", "pathway_changing", "Probable stone in the main bile duct: not what he was booked for", 3, "The duct must be cleared or imaged first; routine gallbladder removal today would miss it.", ["Ask the red-flag questions and look at the eyes", "Liver tests and ultrasound", "Do not proceed; call the senior surgeon"])],
   [D("sc21.d1", "He took apixaban last night at 20:00. The list is at 10:00.", [("Postpone: apixaban is omitted for 2 days before this operation; re-list after the duct problem is dealt with", True, "Bleeding risk avoided."), ("Proceed because the operation is keyhole", False, "Bleeding risk with an anticoagulant on board.")]),
    D("sc21.d2", "Bilirubin 48, ALP 210 and a duct of 9 mm on ultrasound.", [("Do not proceed with routine gallbladder removal: a duct stone is probable, so call the senior surgeon about duct imaging and drainage", True, "Duct stone pathway."), ("Proceed and look at the duct during the operation", False, "The stone is missed and cholangitis follows.")], rule="duct_stone_high_risk"),
    D("sc21.d3", "Temperature 37.9, WBC 14.2, CRP 62 with right-sided tenderness.", [("Treat as acute cholecystitis with a possible duct problem: surgical review, fluids and antibiotics by the clinician's order", True, "Source control is planned."), ("Treat as a normal pre-assessment", False, "Infection is missed.")], rule="acute_cholecystitis_surgery")],
   "redirected", ["Last apixaban dose asked", "Yellow eyes and dark urine noticed", "Liver tests and ultrasound read", "Postponed and redirected to the duct pathway"], [("Proceeds with routine surgery", "Duct stone missed; anticoagulant on board", "Bleeding and cholangitis", "Stop; postpone; call the senior")],
   ["Dose time not asked", "Yellow eyes not seen"], ["Ask about the dose and the red flags", "Read the results together"], "Three problems, one decision: not today, and not the operation he was booked for.", ["V-03", "V-08", "V-39", "V-54"],
   trigger="Bilirubin raised with a dilated duct, or any anticoagulant taken inside its stop window")


# SC-22: cholangitis with sepsis + GLP-1 delayed emptying + beta-blocked slow heart and low BP + acute cholecystitis  (MP-S1-30aaa2fa, redirected)
cb(22, "MP-S1-30aaa2fa", "S1", "Cholangitis with sepsis + weekly GLP-1 injection + beta-blocker masking + acute cholecystitis", person("Mrs", "Rukhsana Baig", "F", 63, "primary school secretary", 11, 2, "635022"),
   "Mrs Rukhsana Baig, 63, listed for gallbladder removal at 10:00: temperature 38.9, heart rate only 56 and BP 92/58, jaundiced. She injects semaglutide weekly (last dose 3 days ago) and takes bisoprolol.",
   "I feel shaky and cold, the pain is under my ribs, and my husband says I look yellow.",
   {"red_flags": "Fever and shaking since the night, right-sided pain, dark urine, very bloated and nauseated for two days.", "past_history": "Type 2 diabetes, high blood pressure, obesity (BMI 38).",
    "medicines": "Semaglutide injection once a week (the last one was on Sunday), bisoprolol, metformin.", "last_food": "Dry toast yesterday at 18:00.", "last_drink": "Water at 06:30."},
   [O("08:00", 56, 92, 58, 22, 94, 38.9, pain=6, note="Heart rate 56 on a beta-blocker looks reassuring but is not: the usual tachycardia of sepsis is blocked.", origin="authored"),
    O("09:30", 74, 108, 66, 19, 97, 37.9, oxygen="supplemental", cond="after_correct_action", note="After the sepsis bundle and escalation.", origin="authored"),
    O("09:30", 50, 78, 46, 28, 90, 39.6, acvpu="V", pain=8, cond="if_no_action", note="Untreated: septic shock.", origin="authored")],
   [E("ex.general", "general", "Flushed, shivering, yellow eyes, breathing fast.", True), E("ex.abdomen", "abdomen", "Tender under the right ribs, bloated.", True, painful=True), E("ex.cap", "perfusion", "Cool hands, slow capillary refill.", True)],
   [R("lab.bili", "Bilirubin", 78, "umol/L", "3-21", "high", "Raised", None, 45), R("lab.alp", "ALP", 260, "U/L", "30-130", "high", "Raised: cholestasis", None, 45), R("lab.wbc", "WBC", 19.4, "x10^9/L", "4.0-11.0", "high", "Systemic inflammation", None, 45),
    R("lab.crp", "CRP", 180, "mg/L", "below 5", "high", "Markedly raised", None, 45), R("lab.lactate", "Lactate", 3.1, "mmol/L", "below 2.0", "high", "Hypoperfusion (2.0 or more)", None, 30),
    R("lab.creat", "Creatinine", 132, "umol/L", "45-90", "high", "Raised: early kidney injury", ("72", "usual"), 45), R("lab.glu", "Capillary glucose", 11.8, "mmol/L", "6-10", "high", "Above target; a stress response", None, 2)],
   [S("NEWS2", "7", "Respiratory rate, saturation, systolic pressure and temperature score; the slow pulse scores nothing because of the beta-blocker", "authored")],
   [P("sc22.sepsis", "life_threat", "Cholangitis with sepsis (NEWS2 7, lactate 3.1)", 1, "High-risk sepsis: antibiotics within 1 hour, fluids, senior and anaesthetic call, urgent drainage plan.", ["Call the senior and the anaesthetist", "Cultures and lactate", "Antibiotic by the prescriber within 1 hour"]),
    P("sc22.bb", "time_critical", "Slow heart and low BP: tachycardia masked by the beta-blocker", 2, "A heart rate of 56 does not mean she is well; treat the pressure and the lactate.", ["Do not rely on the pulse", "Fluids by clinician order and reassess", "Medicines review by the anaesthetist"]),
    P("sc22.glp1", "safety_blocker", "Weekly GLP-1 injection 3 days ago with nausea and bloating: stomach may still hold food", 3, "Delayed gastric emptying raises aspiration risk; a full-stomach plan is needed if she needs urgent anaesthesia.", ["Tell the anaesthetist about the injection date", "Ask about nausea, bloating and vomiting"]),
    P("sc22.chole", "pathway_changing", "Acute cholecystitis behind the picture", 4, "The gallbladder is not today's operation: drain and treat first.", ["Surgical and radiology review"])],
   [D("sc22.d1", "NEWS2 7, lactate 3.1, jaundice and fever.", [("Escalate now as high-risk sepsis: senior and anaesthetist, cultures, fluids and an antibiotic within 1 hour; cancel the elective list", True, "Life threat first."), ("Wait for the liver ultrasound", False, "Delay in antibiotics.")], rule="sepsis_antibiotics"),
    D("sc22.d2", "Her pulse is 56 and the BP 92/58.", [("Treat the low pressure and lactate despite the slow pulse: the beta-blocker hides the tachycardia", True, "The anaesthetist reviews the beta-blocker."), ("Reassure: a slow pulse is a good sign", False, "Shock is missed.")]),
    D("sc22.d3", "Semaglutide last Sunday, nauseated and bloated.", [("Tell the anaesthetist: treat as a full stomach if she needs anaesthesia", True, "Aspiration risk planned for."), ("She has fasted since 18:00, so there is no risk", False, "GLP-1 drugs delay emptying despite fasting.")], rule="fasting_planned")],
   "redirected", ["Sepsis recognised despite a slow pulse", "Senior and anaesthetist called", "GLP-1 date given", "Redirected to urgent drainage and treatment"], [("Keeps her on the elective list", "She deteriorates on the table", "Septic shock", "Stop; escalate")],
   ["Beta-blocker effect not considered", "GLP-1 not asked about"], ["Read the pressure and lactate, not the pulse"], "A normal pulse on a beta-blocker proves nothing in sepsis.", ["V-02", "V-04", "V-05", "V-21", "V-40", "V-49", "V-75"],
   trigger="NEWS2 7 or more or lactate 2.0 or more", if_ignored="Septic shock before any operation")


# SC-23: apixaban + sleep apnoea + iron-deficiency anaemia + high HbA1c  (MP-S1-9665f015, postponed)
cb(23, "MP-S1-9665f015", "S1", "Apixaban not stopped + high OSA risk + unexplained iron-deficiency anaemia + HbA1c above the limit", person("Mr", "Ghulam Nabi", "M", 71, "retired tailor", 7, 14, "635023"),
   "Mr Ghulam Nabi, 71, listed for gallbladder removal: apixaban taken yesterday, STOP-Bang 6 with BMI 37, haemoglobin 98 with a low MCV, HbA1c 76.",
   "I am here for my gallbladder. I am tired a lot, but I put it down to my age.",
   {"red_flags": "Tired and short of breath on stairs for months. Dark stools sometimes (he thought it was the iron tablets he bought).", "past_history": "Type 2 diabetes, atrial fibrillation, snores loudly and stops breathing in sleep according to his wife.",
    "medicines": "Apixaban, metformin, gliclazide, over-the-counter iron. Last apixaban dose yesterday evening.", "last_food": "Dinner at 20:00 yesterday.", "last_drink": "Water at 06:30."},
   [O("08:00", 82, 146, 88, 18, 95, 36.7, rhythm="irregular", note="Stable numbers; the problems are in the history and bloods.", origin="authored"), O("09:30", 80, 142, 86, 17, 96, 36.7, rhythm="irregular", cond="after_correct_action", note="After postponement.", origin="authored")],
   [E("ex.general", "general", "Pale, comfortable at rest, thick neck.", True), E("ex.airway", "airway", "Mallampati III, neck 46 cm.", True), E("ex.abdomen", "abdomen", "Mild tenderness under the right ribs.", False)],
   [R("lab.hb", "Haemoglobin", 98, "g/L", "130-170", "low", "Moderate anaemia (80 to 109 g/L)", None, 45), R("lab.mcv", "MCV", 72, "fL", "80-100", "low", "Small cells: iron deficiency likely", None, 45),
    R("lab.ferr", "Ferritin", 9, "ug/L", "30-300", "low", "Low iron stores: a source of bleeding must be looked for in a man of his age", None, 60),
    R("lab.hba1c", "HbA1c", 76, "mmol/mol", "below 48 (below 69 is acceptable for planned surgery)", "high", "Above the 69 limit for planned surgery", None, 120), R("lab.creat", "Creatinine", 92, "umol/L", "60-110", "normal", "Normal", None, 45),
    R("lab.stop", "STOP-BANG", 6, "points", "below 3", "high", "High risk of obstructive sleep apnoea (5 to 8)", None, 5)],
   [S("STOP-Bang", "6", "Snoring, tiredness, observed apnoea, high BP, BMI over 35, age over 50, neck over 40 cm, male", "authored")],
   [P("sc23.doac", "safety_blocker", "Apixaban not stopped: bleeding risk", 1, "Omit 2 days before this operation with normal kidney function.", ["Confirm the last dose", "Tell the surgeon and the anaesthetist"]),
    P("sc23.anaemia", "pathway_changing", "Unexplained iron-deficiency anaemia in an older man", 2, "A bleeding source (often bowel) must be sought before an elective operation.", ["Repeat the blood count", "Refer for investigation of the source", "Do not give iron as the only answer"]),
    P("sc23.hba1c", "pathway_changing", "HbA1c 76 above the limit of 69 for planned surgery", 3, "Optimise glucose control first and re-list.", ["Tell the diabetes team", "Re-list when below 69"]),
    P("sc23.osa", "optimisable", "High risk of obstructive sleep apnoea with BMI 37", 4, "Needs an anaesthetic plan for airway and opioid sparing.", ["Tell the anaesthetist", "Plan post-operative monitoring"])],
   [D("sc23.d1", "Apixaban yesterday, haemoglobin 98, HbA1c 76.", [("Postpone and re-list: apixaban stop window, anaemia investigation and glucose control each need action first", True, "Three independent reasons."), ("Proceed and fix the anaemia afterwards", False, "Unsafe bleeding and unsafe glucose.")], rule="s1_diabetes_postpone"),
    D("sc23.d2", "Low iron, small cells, dark stools in a 71-year-old man.", [("Refer for investigation of a bleeding source and tell his GP", True, "Possible bowel cancer is not missed."), ("Prescribe iron and re-check later", False, "Cancer is missed.")]),
    D("sc23.d3", "STOP-Bang 6 and BMI 37.", [("Tell the anaesthetist for an airway, opioid-sparing and monitoring plan", True, "Risk planned for."), ("No action: the operation is short", False, "Respiratory risk after anaesthesia.")])],
   "postponed", ["Dose time confirmed", "Anaemia and HbA1c read", "Postponed with a plan"], [("Proceeds because only keyhole surgery", "Bleeding and respiratory events", "Harm", "Stop; postpone")],
   ["Iron-deficiency source not considered"], ["Ask about stools and weight loss"], "Each abnormal result belongs to a different action; none is solved by proceeding.", ["V-08", "V-18", "V-29", "V-69", "V-73"],
   trigger="Anticoagulant in its stop window, anaemia without a source, or HbA1c above 69")


# SC-24: unsigned consent + slow heart and low BP + otherwise fit + known AF  (MP-S1-c63a728a, redirected; female)
cb(24, "MP-S1-c63a728a", "S1", "Consent form not signed + slow heart and low BP + known AF on rate control", person("Mrs", "Shahida Parveen", "F", 68, "retired dressmaker", 4, 21, "635024"),
   "Mrs Shahida Parveen, 68, arrives otherwise fit: heart rate 46, BP 98/60, known atrial fibrillation on a rate-limiting tablet, and the consent form is not signed.",
   "I feel perfectly well. I just came for my operation; nobody gave me a form to sign.",
   {"red_flags": "Slightly dizzy when she stands quickly, no chest pain, no fainting.", "past_history": "Atrial fibrillation for six years, otherwise well; she walks 3 km most days.",
    "medicines": "Bisoprolol and apixaban (last dose 3 days ago, stopped as instructed).", "last_food": "Dinner at 19:00 yesterday.", "last_drink": "Water at 06:30."},
   [O("08:00", 46, 98, 60, 16, 97, 36.5, rhythm="irregular", note="Slow rate and low-normal pressure on a beta-blocker; asymptomatic.", origin="authored"), O("09:30", 58, 112, 68, 16, 98, 36.5, rhythm="irregular", cond="after_correct_action", note="After medicines review and rest.", origin="authored")],
   [E("ex.general", "general", "Well, alert, comfortable.", False), E("ex.pulse", "pulse", "Slow and irregular.", True), E("ex.chest", "chest", "Clear.", False)],
   [R("lab.ecg", "ECG", "Atrial fibrillation, slow ventricular rate 46, no pauses", "", "sinus rhythm", "abnormal", "Slow AF: rate control may be too strong; anaesthetic review", None, 10), R("lab.k", "Potassium", 4.3, "mmol/L", "3.5-5.3", "normal", "Normal", None, 45),
    R("lab.creat", "Creatinine", 71, "umol/L", "45-90", "normal", "Normal", None, 45), R("lab.hb", "Haemoglobin", 131, "g/L", "120-150", "normal", "Normal", None, 45)],
   [S("NEWS2", "1", "Heart rate 46 scores 3 only when 40 or below; 41 to 50 scores 1", "authored")],
   [P("sc24.consent", "safety_blocker", "Consent form not signed", 1, "No operation without valid consent; the surgeon takes it with the patient, not the trainee.", ["Do not ask her to sign without the surgeon", "Tell the surgeon the form is missing", "Check she understands the operation"]),
    P("sc24.slow", "pathway_changing", "Slow heart and low BP in a patient with AF on a beta-blocker", 2, "Needs an anaesthetic and medical review before any list; the plan for the beta-blocker is not the trainee's to change.", ["Repeat the observations after sitting", "12-lead ECG", "Anaesthetic review"]),
    P("sc24.fit", "information_only", "Otherwise fit and fasted correctly", 3, "Fitness does not remove the two blockers above.", ["Record in the handover"])],
   [D("sc24.d1", "The consent form is blank.", [("Tell the surgeon; consent is taken with her first, and the list waits for it", True, "Consent valid."), ("Give her the form to sign now so the list can start", False, "Consent without information is not valid.")]),
    D("sc24.d2", "Heart rate 46, BP 98/60 and a slow AF ECG.", [("Anaesthetic and medical review before she goes to theatre; do not change the beta-blocker yourself", True, "Safe plan."), ("Proceed: she feels well", False, "Bradycardia under anaesthesia.")])],
   "redirected", ["Blank form noticed", "ECG read", "Anaesthetic review requested"], [("Asks her to sign without the surgeon", "Invalid consent", "Harm", "Stop; surgeon first")], ["Slow pulse not escalated"], ["Repeat pulse and ECG"], "Feeling well does not make a patient ready: consent and the pulse both need an owner.", ["V-01", "V-50", "V-65"],
   trigger="Consent not valid or heart rate 40 to 50 with low pressure")


# SC-25: AKI + iron-deficiency anaemia + infected obstructed bile duct  (MP-S1-f00253c9, redirected)
cb(25, "MP-S1-f00253c9", "S1", "Acute kidney injury + unexplained anaemia + obstructed infected bile duct", person("Mr", "Zafar Iqbal", "M", 74, "retired school caretaker", 9, 30, "635025"),
   "Mr Zafar Iqbal, 74, listed for gallbladder removal: creatinine 178 (usually 82), haemoglobin 104 with low iron, fever 38.4 and jaundice.",
   "I have not been passing much water and the pain under my ribs came back last night.",
   {"red_flags": "Fever, shaking last night, dark urine, passing little urine since yesterday.", "past_history": "High blood pressure, gallstones, knee pain for which he takes ibuprofen most days.",
    "medicines": "Ramipril, amlodipine and ibuprofen most days for the knee.", "last_food": "Dinner at 19:00 yesterday.", "last_drink": "Water at 06:30."},
   [O("08:00", 96, 112, 70, 20, 96, 38.4, pain=5, note="Fever with a low-normal pressure on two blood pressure tablets.", origin="authored"), O("09:30", 84, 122, 74, 18, 97, 37.6, cond="after_correct_action", note="After fluids and the antibiotic.", origin="authored"),
    O("09:30", 112, 92, 52, 26, 93, 39.2, acvpu="V", cond="if_no_action", note="Untreated cholangitis with kidney failure.", origin="authored")],
   [E("ex.general", "general", "Yellow, dry mouth, feverish.", True), E("ex.abdomen", "abdomen", "Tender under the right ribs.", True, painful=True)],
   [R("lab.creat", "Creatinine", 178, "umol/L", "60-110", "high", "Acute kidney injury: stage 1 or 2 against his usual 82 (about 2.2 times, so stage 2)", ("82", "usual"), 45), R("lab.hb", "Haemoglobin", 104, "g/L", "130-170", "low", "Moderate anaemia", None, 45),
    R("lab.ferr", "Ferritin", 12, "ug/L", "30-300", "low", "Iron deficient: bleeding source to be sought", None, 60), R("lab.bili", "Bilirubin", 84, "umol/L", "3-21", "high", "Obstructive pattern", None, 45),
    R("lab.wbc", "WBC", 16.8, "x10^9/L", "4.0-11.0", "high", "Inflammation", None, 45), R("lab.crp", "CRP", 140, "mg/L", "below 5", "high", "Markedly raised", None, 45), R("lab.k", "Potassium", 5.2, "mmol/L", "3.5-5.3", "normal", "Upper normal; recheck", None, 45)],
   [S("NEWS2", "3", "Respiratory rate, temperature and heart rate score", "authored")],
   [P("sc25.chol", "life_threat", "Obstructed infected bile duct (acute cholangitis)", 1, "Antibiotics and urgent drainage; not a list case.", ["Call the senior surgeon", "Cultures and lactate", "Antibiotic by the prescriber"]),
    P("sc25.aki", "time_critical", "Acute kidney injury: creatinine 178 against a usual 82", 2, "Stop kidney-harming medicines (ibuprofen, ramipril) on clinician order and give fluids; no contrast without a plan.", ["Medicines review by the clinician", "Fluids and urine output", "Repeat creatinine"]),
    P("sc25.anaemia", "pathway_changing", "Unexplained iron-deficiency anaemia in an older man", 3, "A source must be sought after he is stable; ibuprofen may be a clue.", ["Tell the team and the GP", "Investigate the source"])],
   [D("sc25.d1", "Fever, jaundice, bilirubin 84 and WBC 16.8.", [("Treat as acute cholangitis: senior surgeon, antibiotics within the hour and drainage planning", True, "Life threat first."), ("Wait for the gallbladder list", False, "Sepsis.")], rule="sepsis_antibiotics"),
    D("sc25.d2", "Creatinine 178 (usual 82) and he takes ibuprofen and ramipril.", [("Report the kidney injury and ask the clinician to review the medicines; fluids by order; recheck creatinine", True, "AKI managed."), ("Give another dose of his usual tablets", False, "Kidney injury worsens.")]),
    D("sc25.d3", "Haemoglobin 104 with ferritin 12.", [("Record it and refer for the cause after he is stable", True, "Source sought."), ("Ignore it because the kidney is the priority", False, "Source missed.")])],
   "redirected", ["Cholangitis recognised", "Kidney injury named", "Medicines reviewed by the clinician"], [("Gives ibuprofen for the pain", "Worsens the kidney injury", "Harm", "Stop; report")], ["Ibuprofen use not asked"], ["Ask about painkillers"], "A raised creatinine is a new problem to fix, not a background number.", ["V-03", "V-04", "V-17", "V-18", "V-64", "V-68"],
   trigger="Creatinine rise of 26 umol/L or more within 48 hours, or fever with jaundice")


# SC-26: apixaban + duct stone + anaesthetist decides  (MP-S1-0a9f54f6, postponed)
cb(26, "MP-S1-0a9f54f6", "S1", "Apixaban three days ago + probable duct stone + anaesthetic plan outstanding", person("Mrs", "Farida Hussain", "F", 59, "librarian", 1, 17, "635026"),
   "Mrs Farida Hussain, 59, apixaban stopped 3 days ago as instructed, but yellow eyes and a bilirubin of 71 with a duct of 10 mm; the anaesthetist has not yet seen her.",
   "I did what I was told with my tablets, but my family says I look yellow.",
   {"red_flags": "Pale stools and itching for a week; no fever.", "past_history": "Atrial fibrillation (paroxysmal), gallstones.", "medicines": "Apixaban, stopped 3 days ago as instructed.", "last_food": "Dinner at 19:30 yesterday.", "last_drink": "Water at 06:30."},
   [O("08:00", 78, 130, 78, 16, 98, 36.9, note="Stable.", origin="authored"), O("09:30", 76, 128, 76, 16, 98, 36.9, cond="after_correct_action", note="After the surgeon's review.", origin="authored")],
   [E("ex.general", "general", "Jaundiced, itchy scratch marks, well.", True), E("ex.abdomen", "abdomen", "Mild tenderness under the right ribs.", False)],
   [R("lab.bili", "Bilirubin", 71, "umol/L", "3-21", "high", "Above 68 umol/L (4 mg/dL)", None, 45), R("lab.alp", "ALP", 240, "U/L", "30-130", "high", "Raised", None, 45), R("lab.alt", "ALT", 110, "U/L", "7-40", "high", "Raised", None, 45),
    R("lab.creat", "Creatinine", 80, "umol/L", "45-90", "normal", "Normal", None, 45), R("lab.us", "Ultrasound abdomen", "Gallstones; common bile duct 10 mm", "", "normal", "abnormal", "Dilated duct with bilirubin above 68: high risk of a duct stone", None, 60)],
   [S("NEWS2", "0", "Well", "authored")],
   [P("sc26.cbd", "pathway_changing", "Probable stone in the main bile duct: not what she was booked for", 1, "Bilirubin above 68 umol/L with a dilated duct is the high-risk group for a duct stone.", ["Ask the red-flag questions and look at her eyes", "Request liver tests and ultrasound", "Do not proceed today; call the senior surgeon"]),
    P("sc26.doac", "information_only", "Apixaban correctly stopped 3 days ago", 2, "No bleeding blocker, but the restart plan depends on the duct procedure.", ["Record the dates", "Tell the anaesthetist"]),
    P("sc26.anaes", "pathway_changing", "Anaesthetist decides", 3, "The anaesthetist must see her before any list.", ["Request anaesthetic review"])],
   [D("sc26.d1", "Bilirubin 71 and a duct of 10 mm.", [("Do not proceed today: call the senior surgeon about duct imaging and clearance", True, "High-risk duct stone pathway."), ("Proceed as the apixaban is already stopped", False, "Duct stone is missed.")], rule="duct_stone_high_risk"),
    D("sc26.d2", "The anaesthetist has not seen her.", [("Request the review before any decision to list her again", True, "Plan agreed."), ("Skip the review because the problem is the surgeon's", False, "Anaesthetic plan missing.")])],
   "postponed", ["Stop date confirmed", "Duct stone risk recognised", "Postponed"], [("Proceeds because the tablets were stopped", "Duct stone missed", "Retained stone", "Stop; call the senior")], ["Red flags not asked"], ["Look at the eyes"], "A correct medicines plan does not rescue the wrong operation.", ["V-03", "V-08"], trigger="Bilirubin above 68 umol/L with a dilated duct")


RISK.update({
    "SC-21": risk(3, 4, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; a probable infected duct stone with fever and jaundice raises it towards 4 if it progresses.", "standard_ga"),
    "SC-22": risk(4, 4, "Sepsis with hypotension and lactate 3.1 is ASA 4 in the ASA's examples; obesity (BMI 38) and type 2 diabetes add to it; urgent drainage makes it an emergency, and the weekly GLP-1 injection means a full-stomach plan.", "rsi", emergency=True),
    "SC-23": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; poorly controlled diabetes (HbA1c 76), BMI 37 and anaemia add to it.", "standard_ga"),
    "SC-24": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; she is otherwise fit.", "standard_ga"),
    "SC-25": risk(3, 4, "Acute kidney injury with cholangitis is ASA 3 to 4; urgent drainage makes it an emergency.", "standard_ga", emergency=True),
    "SC-26": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; a probable duct stone does not change the grade today.", "standard_ga"),
})


import p2comb_s2  # noqa: E402,F401  (the six emergency cases register themselves in CASES and RISK)
