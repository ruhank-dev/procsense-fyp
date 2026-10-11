"""The twenty P2 scenario cards (SC-1 to SC-20): combinations of problems that can truly coexist, each with the problems found, why they can coexist, the priority order and the simulator
outcome from the book (content/case_seeds.csv). The book gives no patient for these, so the identities are synthetic and authored (origin 'authored'). No doses."""
from lib import D, E, O, P, R, S, mk, person

CASES = {}
T = "P2"


def sc(n, scn, title, who, summary, complaint, hx, vitals, exam, results, scores, problems, decisions, end, correct, wrong, missed, recovery, remember, ver, **kw):
    cid = f"SC-{n}"
    CASES[cid] = mk(cid, f"SCENARIO CARD {n} ({scn}) - {title}", scn, who, summary, complaint, hx, vitals, exam, results, scores, problems, decisions, end, correct, wrong, missed, recovery, remember, ver, tier=T, **kw)


# SC-1
sc(1, "S2", "Sepsis + penicillin anaphylaxis + AKI", person("Mr", "Danish Rehman", "M", 38, "warehouse supervisor", 5, 30, "635001"),
   "Mr Danish Rehman, 38, burst appendix: HR 126, BP 94/58, RR 24, T 39.0, lactate 3.6. Penicillin once caused throat swelling. Creatinine 170 (usual 80).",
   "Terrible pain in my whole tummy for two days and I feel awful.",
   {"red_flags": "Fever and shaking. Vomiting. Very little urine.", "past_history": "Healthy.", "medicines": "None."},
   [O("12:00", 126, 94, 58, 24, 94, 39.0, note="Sepsis: NEWS2 high risk.", origin="authored"), O("13:30", 108, 108, 66, 20, 97, 38.2, cond="after_correct_action", note="After the sepsis bundle and fluids: responding.", origin="authored"),
    O("13:30", 140, 78, 44, 30, 89, 39.6, acvpu="C", cond="if_no_action", note="Untreated: septic shock.", origin="authored")],
   [E("ex.general", "general", "Hot, sweaty, breathing fast, lying very still.", True), E("ex.abdomen", "abdomen", "Rigid abdomen.", True), E("ex.band", "allergy_band", "Red allergy band: penicillin.", True)],
   [R("lab.lactate", "Lactate", 3.6, "mmol/L", "below 2.0", "high", "Hypoperfusion", None, 30), R("lab.creat", "Creatinine", 170, "umol/L", "60-110", "high", "Acute kidney injury (usual 80)", ("80", "usual"), 45),
    R("lab.wbc", "WBC", 21.0, "x10^9/L", "4.0-11.0", "high", "Severe inflammation", None, 45)],
   [S("NEWS2", "10", "High risk", "authored")],
   [P("sc1.sepsis", "life_threat", "Sepsis with poor circulation", 1, "ABCDE, senior and anaesthetist, oxygen if needed, cultures.", ["Call the senior and the anaesthetist", "Cultures and lactate", "Oxygen to target"]),
    P("sc1.allergy", "safety_blocker", "Severe penicillin allergy before the antibiotic", 2, "The antibiotic follows the allergy rule, still within 1 hour.", ["Ask the reaction", "Prescriber chooses a non-penicillin regimen by the hospital rule", "Within 1 hour"]),
    P("sc1.aki", "pathway_changing", "Acute kidney injury caused by poor circulation", 3, "Fluids help; no anti-inflammatories; kidney-adjusted drugs.", ["Fluid bolus by clinician order and re-check", "No anti-inflammatories", "Strict urine output"])],
   [D("sc1.d1", "NEWS2 10 with lactate 3.6. What first?", [("ABCDE, call the senior and anaesthetist, cultures and lactate, start the bundle", True, "Life threat first."), ("Wait for the CT", False, "Delay.")], rule="sepsis_antibiotics"),
    D("sc1.d2", "Penicillin allergy with throat swelling. Which antibiotic?", [("The prescriber chooses by the hospital allergy rule, within 1 hour", True, "Safety blocker before the drug."), ("Standard first-line penicillin", False, "Anaphylaxis.")]),
    D("sc1.d3", "Creatinine 170.", [("Fluids by clinician order and re-check; no anti-inflammatories; kidney-adjusted antibiotic doses by the prescriber", True, "Poor circulation caused it."), ("Ignore the creatinine", False, "AKI worsens.")])],
   "ready", ["Sepsis bundle", "Allergy-safe antibiotic in 1 hour", "Fluids and kidney care"], [("Gives standard antibiotic", "Allergy challenged", "Anaphylaxis", "Stop; allergy rule"), ("Delays for CT", "Shock", "Death", "Bundle now")],
   ["Allergy not asked"], ["Ask, escalate"], "Treat the circulation first, then pick the antibiotic by the allergy, still within the hour.", ["V-02", "V-13", "V-17", "V-21"],
   allergies=[("Penicillin", "Throat swelling")], allergy_answer="Penicillin: my throat swelled.", variant_end={"if_no_action": "failure"})

# SC-2
sc(2, "S2", "Positive pregnancy test + lower-right pain + low BP", person("Ms", "Nimra Javed", "F", 24, "salon assistant", 4, 8, "635002"),
   "Ms Nimra Javed, 24: HR 118, BP 92/56, pregnancy test positive, Hb 98, pain low on one side.",
   "Sharp pain low in my tummy on one side and I feel faint.",
   {"red_flags": "Light vaginal bleeding since yesterday. Shoulder-tip pain.", "past_history": "Healthy.", "pregnancy": "Late period; not sure if pregnant.", "medicines": "None."},
   [O("12:00", 118, 92, 56, 20, 98, 36.9, pain=8, note="Shocked: ruptured ectopic pregnancy suspected.", origin="authored"), O("12:45", 100, 106, 64, 18, 98, 36.9, cond="after_correct_action", note="After fluids and urgent gynaecology: stabilising.", origin="authored"),
    O("12:45", 136, 72, 40, 28, 94, 36.7, acvpu="V", cond="if_no_action", note="Bleeding continues: collapse.", origin="authored")],
   [E("ex.general", "general", "Pale and clammy.", True), E("ex.abdomen", "abdomen", "Tender low on one side with guarding.", True)],
   [R("lab.preg", "Pregnancy test", "Positive", "", "negative", "positive", "hCG positive", None, 5, origin="book"), R("lab.hb", "Hb", 98, "g/L", "120-150", "low", "Low: possible blood loss", None, 45, origin="book")],
   [S("NEWS2", "7", "High risk", "authored")],
   [P("sc2.bleed", "life_threat", "Possible internal bleeding", 1, "Two large drips, crossmatch, fluids or blood.", ["Call for help", "Two large IV lines", "Crossmatch"]),
    P("sc2.dx", "pathway_changing", "Ectopic pregnancy until proven otherwise", 2, "Gynaecology now; the appendicitis pathway stops.", ["Gynaecology now", "Pelvic ultrasound", "Avoid CT"])],
   [D("sc2.d1", "Positive pregnancy test with pain and low BP.", [("Possible ectopic: call for help, two drips, crossmatch, gynaecology now", True, "Matches NICE NG126."), ("Continue the appendicitis pathway", False, "Misses the ectopic.")]),
    D("sc2.d2", "What imaging?", [("Pelvic ultrasound by gynaecology; avoid CT", True, "Matches the book."), ("CT abdomen first", False, "Radiation and delay.")])],
   "redirected", ["Pregnancy test", "Bleed recognised", "Gynaecology now"], [("Misses the pregnancy test", "Safety failure", "Death", "Test now")],
   ["Pregnancy test missed"], ["Test and escalate"], "Always do a pregnancy test in a woman with lower tummy pain.", ["V-10"], variant_end={"if_no_action": "failure"})

# SC-3
sc(3, "S2", "Pregnant (20 weeks) + appendicitis + fever", person("Mrs", "Rabia Hussain", "F", 32, "primary school teacher", 12, 1, "635003"),
   "Mrs Rabia Hussain, 32, 20 weeks pregnant: T 38.2, HR 108, WBC 15, ultrasound non-diagnostic.",
   "Pain in my right side and a fever.", {"red_flags": "Baby moving. No bleeding.", "past_history": "Healthy.", "pregnancy": "20 weeks, second pregnancy.", "medicines": "Pregnancy vitamins."},
   [O("12:00", 108, 110, 66, 18, 98, 38.2, pain=6, note="Maternity score used; NEWS2 reference only.", origin="book")],
   [E("ex.general", "general", "Uncomfortable, alert.", True), E("ex.abdomen", "abdomen", "Tender on the right, higher than usual.", True)],
   [R("lab.wbc", "WBC", 15.0, "x10^9/L", "4.0-11.0", "high", "Partly pregnancy", None, 45, origin="book"), R("lab.us", "Ultrasound appendix", "Appendix not visualised", "", "normal", "abnormal", "Non-diagnostic (picture result)", None, 45, origin="book"),
    R("lab.mri", "MRI appendix", "Findings of appendicitis", "", "normal", "abnormal", "MRI after non-diagnostic ultrasound (picture result)", None, 90)],
   [S("NEWS2", "3", "Not validated in pregnancy", "authored")],
   [P("sc3.preg", "pathway_changing", "Appendicitis in pregnancy with fever", 1, "Obstetrics early, MRI not CT first, analgesia avoiding anti-inflammatories, antibiotics from the safe local list, left tilt.", ["Obstetrics early", "MRI", "Left tilt", "Maternity score"])],
   [D("sc3.d1", "Fever and appendicitis in pregnancy.", [("Involve obstetrics early; MRI after the ultrasound; left tilt; maternity score", True, "Matches the book."), ("CT first", False, "Avoid ionising imaging first.")]),
    D("sc3.d2", "Pain relief?", [("Paracetamol with an opioid if needed, by the clinician; avoid anti-inflammatories", True, "Matches the book."), ("Anti-inflammatory tablets", False, "Avoid in pregnancy.")])],
   "ready", ["Obstetrics early", "MRI", "Left tilt"], [("Requests CT first", "Radiology challenges", "Avoidable radiation", "MRI")], ["Obstetrics late"], ["Call obstetrics"], "Appendicitis in pregnancy needs the obstetric team and MRI.", ["V-14", "V-16"])

# SC-4
sc(4, "S1", "High BP + poorly controlled diabetes", person("Mr", "Tahir Mehmood", "M", 55, "bus conductor", 6, 6, "635004"),
   "Mr Tahir Mehmood, 55: BP 176/104 repeated, HbA1c 78, sugar 11.2.", "Pain under my right ribs after meals.",
   {"red_flags": "No fever.", "past_history": "Type 2 diabetes and high blood pressure.", "medicines": "Metformin and amlodipine.", "smoking": "Never.", "previous_anaesthetic": "None."},
   [O("08:00", 82, 176, 104, 16, 97, 36.6, note="Repeat 174/102; below 180/110; NEWS2 0.", origin="book")],
   [E("ex.general", "general", "Well, overweight.", False), E("ex.abdomen", "abdomen", "Mild tenderness under the right ribs.", False)],
   [R("lab.hba1c", "HbA1c", 78, "mmol/mol", "below 48", "high", "Above 69: poor control", None, 120, origin="book"), R("lab.glu", "Capillary glucose", 11.2, "mmol/L", "6-10", "high", "Above target", None, 2, origin="book")],
   [S("NEWS2", "0", "Well", "authored")],
   [P("sc4.dm", "optimisable", "HbA1c 78 above the planned-surgery limit of 69", 1, "Optimisation is recommended; one clear reason to postpone.", ["Postpone", "Refer to the GP and diabetes team"]),
    P("sc4.bp", "information_only", "BP 176/104 (below 180/110)", 2, "On its own BP would allow surgery; the GP reviews.", ["Repeat BP", "Refer the GP"])],
   [D("sc4.d1", "HbA1c 78. What now?", [("Postpone for optimisation; refer to the GP for diabetes and BP", True, "Matches the HbA1c limit."), ("Proceed because the BP is below 180/110", False, "Diabetes is the reason.")], rule="s1_diabetes_postpone")],
   "postponed", ["HbA1c checked", "Postponed appropriately"], [("Proceeds", "Anaesthetist stops it", "Poor outcome", "Postpone")], ["HbA1c missed"], ["Check it"], "One clear reason is enough to postpone.", ["V-07"])

# SC-5
sc(5, "S1", "On apixaban (taken this morning) + new fast AF", person("Mr", "Sajid Mahmood", "M", 72, "retired clerk", 2, 19, "635005"),
   "Mr Sajid Mahmood, 72: HR 124 irregular, BP 128/80; took his apixaban this morning out of habit.", "My heart is racing and I have gallstone pain.",
   {"red_flags": "Heart racing since yesterday. No chest pain.", "past_history": "Atrial fibrillation, high blood pressure.", "medicines": "Apixaban and bisoprolol. I took my apixaban this morning as I always do."},
   [O("08:00", 124, 128, 80, 18, 96, 36.8, rhythm="irregular", note="New fast AF; NEWS2 2.", origin="book")],
   [E("ex.general", "general", "Comfortable at rest.", False), E("ex.pulse", "pulse", "Fast and irregular.", True)],
   [R("lab.ecg", "ECG", "Atrial fibrillation, fast ventricular rate", "", "sinus rhythm", "abnormal", "Fast AF (picture result)", None, 10, origin="book")],
   [S("NEWS2", "2", "Heart rate 111 to 130 scores 2", "authored")],
   [P("sc5.af", "pathway_changing", "New fast AF", 1, "Medical review for rate control.", ["12-lead ECG", "Medical review"]), P("sc5.doac", "safety_blocker", "Apixaban not stopped: bleeding risk", 2, "Two independent reasons to postpone.", ["Confirm last dose", "Tell the anaesthetist"])],
   [D("sc5.d1", "Fast AF and apixaban taken today.", [("Two independent reasons to postpone: medical review for rate control and a proper anticoagulation stop plan", True, "Matches the book."), ("Proceed because it is keyhole surgery", False, "Bleeding and rhythm risk.")])],
   "postponed", ["AF recognised", "Dose time confirmed", "Postponed"], [("Proceeds", "List stopped", "Bleeding or arrhythmia", "Stop")], ["Dose time not asked"], ["Ask"], "Two independent reasons strengthen the case to postpone.", ["V-08", "V-23"])

# SC-6
sc(6, "S1", "Fasting breach + diabetes on insulin", person("Mrs", "Uzma Aziz", "F", 49, "bookkeeper", 10, 22, "635006"),
   "Mrs Uzma Aziz, 49, ate toast at 07:00 because her sugar felt low; she is on basal insulin; glucose 4.6.", "I felt shaky and had some toast at seven.",
   {"red_flags": "No fever.", "past_history": "Type 1 diabetes; gallstones.", "medicines": "Basal insulin.", "last_food": "Toast at 07:00, a light meal.", "last_drink": "Water at 07:30."},
   [O("08:00", 80, 124, 76, 16, 98, 36.6, note="NEWS2 0.", origin="authored")],
   [E("ex.general", "general", "Well, slightly anxious.", False)],
   [R("lab.glu", "Capillary glucose", 4.6, "mmol/L", "6-10", "low", "Looming low (4.0 to 5.9)", None, 2, origin="book"), R("lab.glu2", "Capillary glucose (repeat in 30 minutes)", 6.2, "mmol/L", "6-10", "normal", "In range", None, 32)],
   [S("NEWS2", "0", "Well", "authored")],
   [P("sc6.glu", "optimisable", "Glucose 4.6: treat if below 4.0; recheck in 30 minutes", 1, "4.0 to 5.9 is a looming low.", ["Recheck in 30 minutes"]), P("sc6.fast", "safety_blocker", "Fasting breach: a light meal 1 hour ago", 2, "Light meals need 6 hours; the anaesthetist decides.", ["Tell the anaesthetist"]),
    P("sc6.plan", "pathway_changing", "Insulin plan likely too much for fasting", 3, "Review the plan.", ["Diabetes team to review"])],
   [D("sc6.d1", "She ate toast at 07:00.", [("Tell the anaesthetist of the breach and recheck the glucose; the list is re-timed later or postponed", True, "Matches the book."), ("Hide the breach to keep the list", False, "Aspiration risk.")], rule="fasting_planned")],
   "delayed", ["Breach told", "Glucose rechecked", "Plan reviewed"], [("Hides the toast", "Anaesthetist finds out", "Aspiration risk", "Tell now")], ["Breach not mentioned"], ["Tell"], "Hypoglycaemia caused the fasting breach.", ["V-09", "V-20"])

# SC-7
sc(7, "S2", "Unknown fasting + vomiting + opioid given + sleepy", person("Mr", "Adnan Qureshi", "M", 27, "mobile phone repairer", 9, 9, "635007"),
   "Mr Adnan Qureshi, 27: RR 10, SpO2 93, drowsy after morphine, vomiting.", "Tummy pain... so sleepy.", {"red_flags": "Vomiting.", "past_history": "Healthy.", "medicines": "The morphine just given.", "last_food": "Unknown; probably a few hours ago."},
   [O("12:00", 98, 116, 70, 10, 93, 37.6, acvpu="V", note="Opioid effect.", origin="authored"), O("12:15", 88, 118, 72, 14, 97, 37.5, oxygen="supplemental", cond="after_correct_action", note="Stabilised.", origin="authored"),
    O("12:15", 110, 104, 60, 6, 82, 37.8, acvpu="P", cond="if_no_action", note="Aspiration.", origin="authored")],
   [E("ex.general", "general", "Drowsy and retching.", True), E("ex.chest", "chest", "Reduced air entry.", True)],
   [R("lab.cxr", "Chest X-ray", "Right lower zone shadowing", "", "clear", "abnormal", "Possible aspiration (picture result)", None, 30)],
   [S("NEWS2", "8", "High risk", "authored")],
   [P("sc7.airway", "life_threat", "Airway at risk and slow breathing", 1, "Side-lying, suction, oxygen, anaesthetist.", ["Side-lying", "Suction", "Oxygen", "Anaesthetist"]), P("sc7.aspiration", "time_critical", "Aspiration risk: full stomach", 2, "Rapid sequence plan; NG tube per senior.", ["Anaesthetist told"]),
    P("sc7.opioid", "safety_blocker", "Stop further opioid", 3, "Alternative pain plan; clinician considers the antidote.", ["Clinician decides"])],
   [D("sc7.d1", "Drowsy, RR 10, vomiting.", [("Side-lying, suction, oxygen, call the anaesthetist, stop opioids; clinician considers the antidote", True, "Matches the book."), ("More morphine", False, "Further sedation.")], rule="low_spo2_trigger")],
   "ready", ["Airway first", "Opioid stopped", "Anaesthetist called"], [("More morphine", "RR 6", "Arrest", "Stop")], ["Airway missed"], ["Rescue"], "A drowsy vomiting patient is an airway problem first.", ["V-09"], variant_end={"if_no_action": "failure"})

# SC-8
sc(8, "S2", "High potassium + AKI + sepsis", person("Mr", "Waqas Nadeem", "M", 63, "retired mechanic", 3, 3, "635008"),
   "Mr Waqas Nadeem, 63: K 6.6, creatinine 210, lactate 4.2, BP 88/50.", "Awful tummy pain and I feel faint.",
   {"red_flags": "Fever and shaking. Hardly any urine.", "past_history": "Kidney disease.", "medicines": "Ramipril."},
   [O("12:00", 122, 88, 50, 26, 93, 38.9, note="Septic shock.", origin="authored"), O("13:00", 104, 106, 62, 20, 97, 38.0, cond="after_correct_action", note="Responding.", origin="authored"), O("13:00", 38, 70, 40, 30, 88, 38.6, acvpu="P", cond="if_no_action", note="Hyperkalaemic arrhythmia.", origin="authored")],
   [E("ex.general", "general", "Hot, sweaty and drowsy.", True), E("ex.abdomen", "abdomen", "Rigid abdomen.", True)],
   [R("lab.k", "Potassium", 6.6, "mmol/L", "3.5-5.3", "critical_high", "Severe hyperkalaemia", None, 20, origin="book"), R("lab.creat", "Creatinine", 210, "umol/L", "60-110", "high", "Acute kidney injury", ("95", "usual"), 45, origin="book"),
    R("lab.lactate", "Lactate", 4.2, "mmol/L", "below 2.0", "high", "Hypoperfusion", None, 30, origin="book"), R("lab.ecg", "ECG", "Peaked T waves", "", "normal", "abnormal", "Hyperkalaemia (picture result)", None, 10)],
   [S("NEWS2", "11", "High risk", "authored")],
   [P("sc8.k", "life_threat", "Dangerous potassium", 1, "Emergency team, monitor, ECG, treatment by the clinician.", ["Emergency team", "Monitor", "ECG"]), P("sc8.shock", "life_threat", "Septic shock", 2, "Bundle within 1 hour; fluids.", ["Cultures", "Antibiotics in 1 hour", "Fluids by clinician"]),
    P("sc8.anaes", "safety_blocker", "No suxamethonium; correct potassium first", 3, "Anaesthetist decides the plan.", ["Tell the anaesthetist"])],
   [D("sc8.d1", "K 6.6, BP 88/50, lactate 4.2.", [("Emergency team, monitor and ECG, start the sepsis bundle, critical care involved", True, "Matches the book."), ("Wait for a repeat sample", False, "Delay.")], rule="sepsis_antibiotics")],
   "ready", ["Two life threats", "Emergency team", "Critical care"], [("Waits", "Arrhythmia", "Arrest", "Treat")], ["Potassium ignored"], ["Rescue"], "Treat the potassium and the shock together.", ["V-15", "V-17", "V-21"], variant_end={"if_no_action": "failure"})

# SC-9
sc(9, "S1", "Jaundice + fever + shaking (cholangitis) + on clopidogrel", person("Mr", "Zahid Latif", "M", 73, "retired postmaster", 7, 15, "635009"),
   "Mr Zahid Latif, 73: bilirubin 85, T 39.1, HR 118, BP 100/62; clopidogrel for a stent two years ago.", "Yellow eyes and shaking since last night.",
   {"red_flags": "Dark urine, pale stools, rigors.", "past_history": "A coronary stent two years ago; gallstones.", "medicines": "Clopidogrel and aspirin."},
   [O("08:00", 118, 100, 62, 22, 96, 39.1, note="Sepsis from cholangitis.", origin="book"), O("09:30", 100, 116, 70, 18, 98, 38.2, cond="after_correct_action", note="Responding.", origin="authored"), O("09:30", 132, 80, 46, 28, 92, 39.6, acvpu="C", cond="if_no_action", note="Septic shock.", origin="authored")],
   [E("ex.general", "general", "Flushed and shivering.", True), E("ex.eyes", "eyes", "Yellow whites of the eyes.", True), E("ex.abdomen", "abdomen", "Tender under the right ribs.", True)],
   [R("lab.bili", "Bilirubin", 85, "umol/L", "3-21", "high", "Jaundice", None, 45, origin="book"), R("lab.wbc", "WBC", 18.0, "x10^9/L", "4.0-11.0", "high", "Raised", None, 45), R("lab.crp", "CRP", 190, "mg/L", "below 5", "high", "Raised", None, 45)],
   [S("NEWS2", "7", "High risk", "authored")],
   [P("sc9.sepsis", "life_threat", "Cholangitis with sepsis", 1, "Sepsis bundle, antibiotics within 1 hour.", ["Senior now", "Cultures", "Antibiotics in 1 hour"]), P("sc9.dx", "pathway_changing", "Emergency biliary drainage (ERCP) instead of gallbladder surgery", 2, "Source control is the treatment.", ["Gastroenterology urgent ERCP"]),
    P("sc9.antiplatelet", "safety_blocker", "Antiplatelet affects ERCP bleeding", 3, "Gastroenterology and cardiology decide; a stent older than 12 months lowers the risk of stopping.", ["Tell gastroenterology and cardiology"])],
   [D("sc9.d1", "Jaundice, fever and shaking.", [("Cholangitis: sepsis bundle in 1 hour; urgent ERCP; the elective cholecystectomy is cancelled", True, "Matches TG18."), ("Continue to cholecystectomy", False, "Wrong pathway.")], rule="duct_stone_high_risk")],
   "redirected", ["Sepsis recognised", "ERCP pathway", "Antiplatelet flagged"], [("Continues the list", "Septic shock", "Death", "Stop")], ["Fever missed"], ["Escalate"], "Cholangitis needs drainage, not a gallbladder operation.", ["V-03", "V-04", "V-05"], variant_end={"if_no_action": "failure"})

# SC-10
sc(10, "S2", "Language barrier + 17 years old + needs urgent surgery", person("Master", "Shahzaib Gul", "M", 17, "school student", 4, 25, "635010"),
   "Shahzaib Gul, 17, speaks Pashto only; his father is present; appendicitis needing urgent surgery.", "(Through an interpreter) Pain in my right side.",
   {"red_flags": "Fever, vomiting.", "past_history": "Healthy.", "medicines": "None."},
   [O("12:00", 102, 118, 72, 20, 98, 38.3, pain=7, note="NEWS2 per table.", origin="authored")],
   [E("ex.general", "general", "Flushed and anxious; father at the bedside.", True), E("ex.abdomen", "abdomen", "Tender and guarded low on the right.", True)],
   [R("lab.wbc", "WBC", 16.0, "x10^9/L", "4.0-11.0", "high", "Raised", None, 45), R("lab.crp", "CRP", 92, "mg/L", "below 5", "high", "Raised", None, 45)],
   [S("AIR score", "10", "High", "authored")],
   [P("sc10.int", "safety_blocker", "Language barrier", 1, "A trained interpreter, not a sibling or child.", ["Book a trained interpreter"]), P("sc10.consent", "pathway_changing", "A minor: consent per law", 2, "Parent or guardian consents; involve the young person.", ["Follow hospital policy"]),
    P("sc10.pain", "time_critical", "Pain relief first", 3, "Analgesia by the clinician.", ["Request analgesia"])],
   [D("sc10.d1", "Pashto-only 17-year-old needs consent.", [("Trained interpreter; parent or guardian consent per the law; involve the patient; pain relief first", True, "Matches the book."), ("Use the 10-year-old brother as interpreter", False, "Inappropriate.")])],
   "ready", ["Interpreter", "Valid consent", "Pain relief"], [("Uses a child interpreter", "Consent invalid", "Harm", "Use a trained interpreter")], ["Interpreter not arranged"], ["Book one"], "Consent needs understanding in a language the patient knows.", ["V-12"])

# SC-11
sc(11, "S2", "Refuses surgery + becomes septic later", person("Mr", "Kashif Ali", "M", 41, "plumber", 1, 28, "635011"),
   "Mr Kashif Ali, 41, initially refuses surgery with capacity; four hours later NEWS2 8 and confused.", "Pain low on the right; I do not want surgery.",
   {"red_flags": "Mild fever.", "past_history": "Healthy.", "medicines": "None."},
   [O("12:00", 90, 124, 76, 16, 98, 37.8, pain=5, note="Initial: capable refusal.", origin="authored"), O("16:00", 122, 94, 56, 24, 94, 39.2, acvpu="C", cond="variant", note="Four hours later: NEWS2 8 and confused.", origin="authored")],
   [E("ex.general", "general", "Initially comfortable and clear.", False), E("ex.abdomen", "abdomen", "Tender low on the right.", True)],
   [R("lab.lactate", "Lactate", 3.0, "mmol/L", "below 2.0", "high", "Hypoperfusion (at 16:00)", None, 30), R("lab.ct", "CT appendix", "Perforated appendix with free fluid", "", "normal", "abnormal", "At 16:00 (picture result)", None, 60)],
   [S("NEWS2", "8", "At 16:00", "authored")],
   [P("sc11.sepsis", "life_threat", "Sepsis now", 1, "Bundle.", ["Senior now", "Cultures", "Antibiotics in 1 hour"]), P("sc11.capacity", "pathway_changing", "Capacity now lost from sepsis", 2, "Treat the cause and reassess capacity.", ["Senior-led best-interests decision"]),
    P("sc11.refusal", "safety_blocker", "Earlier capable refusal is respected where it clearly applied", 3, "Senior, legal and ethics guidance.", ["Document fully"])],
   [D("sc11.d1", "Four hours later: NEWS2 8 and confused.", [("Sepsis bundle; senior-led decision on capacity and best interests; document everything", True, "Matches the book."), ("Take the earlier refusal as final and do nothing", False, "Treatable cause.")], rule="sepsis_antibiotics")],
   "ready", ["Sepsis recognised", "Senior-led decision", "Documented"], [("Does nothing", "Shock", "Death", "Bundle")], ["Late recognition"], ["Escalate"], "Capacity can change when the patient becomes unwell.", ["V-02", "V-21"], variant_end={"if_no_action": "failure"})

# SC-12
sc(12, "S1", "Haemolysed high potassium + genuinely low sodium + water tablets", person("Mrs", "Parveen Akhtar", "F", 66, "retired nurse", 5, 12, "635012"),
   "Mrs Parveen Akhtar, 66: K 6.1 haemolysed, Na 127, on bendroflumethiazide.", "Gallstone pain under the right ribs.",
   {"red_flags": "Tired and a bit confused at times.", "past_history": "High blood pressure; gallstones.", "medicines": "Bendroflumethiazide."},
   [O("08:00", 74, 134, 80, 15, 97, 36.6, note="NEWS2 0.", origin="authored")],
   [E("ex.general", "general", "Well but tired.", False)],
   [R("lab.k", "Potassium", 6.1, "mmol/L", "3.5-5.3", "high", "Haemolysed sample: repeat urgently", None, 20, quality="haemolysed", origin="book"), R("lab.k2", "Potassium (repeat)", 4.2, "mmol/L", "3.5-5.3", "normal", "Normal: the first result was false", None, 60),
    R("lab.na", "Sodium", 127, "mmol/L", "135-145", "low", "Moderate hyponatraemia (125 to 129)", None, 45, origin="book")],
   [S("NEWS2", "0", "Well", "authored")],
   [P("sc12.na", "pathway_changing", "Real low sodium from the water tablet", 1, "Moderate hyponatraemia: postpone; GP or medical review.", ["Postpone", "Medical review"]), P("sc12.k", "information_only", "Haemolysed potassium: repeat", 2, "Probably false; do not let it distract.", ["Repeat the potassium"])],
   [D("sc12.d1", "K 6.1 haemolysed and Na 127.", [("Repeat the potassium; the sodium is real: postpone and ask for medical review", True, "Matches the book."), ("Treat the potassium and ignore the sodium", False, "Distracted by the false alarm.")])],
   "postponed", ["Potassium repeated", "Sodium acted on"], [("Chases the potassium", "Sodium missed", "Harm", "Repeat; postpone")], ["Sodium missed"], ["Look again"], "A false alarm must not hide a real abnormality.", ["V-15", "V-19"])

# SC-13
sc(13, "S1", "Anaemia + anticoagulant + needs G&S", person("Mrs", "Tasneem Bibi", "F", 68, "retired seamstress", 8, 8, "635013"),
   "Mrs Tasneem Bibi, 68: Hb 96, on warfarin, INR 2.6 today (should have stopped).", "Gallstone pain and tiredness.",
   {"red_flags": "Black stools sometimes.", "past_history": "Atrial fibrillation (old mechanical valve is not present); gallstones.", "medicines": "Warfarin; I did not stop it."},
   [O("08:00", 86, 128, 78, 16, 97, 36.6, rhythm="irregular", note="NEWS2 1.", origin="authored")],
   [E("ex.general", "general", "Pale, comfortable.", True)],
   [R("lab.hb", "Hb", 96, "g/L", "120-150", "low", "Moderate anaemia", None, 45, origin="book"), R("lab.inr", "INR", 2.6, "ratio", "0.8-1.2", "high", "Not stopped", None, 30, origin="book")],
   [S("NEWS2", "1", "HR 86", "authored")],
   [P("sc13.inr", "safety_blocker", "Warfarin not stopped: INR 2.6", 1, "Too high for surgery.", ["Tell the anaesthetist", "Anticoagulation clinic plan"]), P("sc13.anaemia", "pathway_changing", "Unexplained anaemia: bleeding on warfarin", 2, "Ask about black stools; group and save.", ["Group and save", "Investigate"])],
   [D("sc13.d1", "INR 2.6 and Hb 96.", [("Two reasons: warfarin not stopped and unexplained anaemia; ask about bleeding, group and save, tell the senior", True, "Matches the book."), ("Proceed after checking the INR only", False, "Anaemia unexplained.")])],
   "postponed", ["INR flagged", "Anaemia investigated"], [("Proceeds", "Bleeding", "Harm", "Stop")], ["Bleeding not asked"], ["Ask"], "Anticoagulants can hide bleeding.", ["V-08", "V-18"])

# SC-14
sc(14, "S2", "Equivocal ultrasound + deteriorating vital signs", person("Ms", "Mahnoor Raza", "F", 28, "graphic designer", 11, 16, "635014"),
   "Ms Mahnoor Raza, 28: appendix not seen; HR 104 rising to 122; T 38.9; CRP 160.", "Pain low on the right.",
   {"red_flags": "Fever; vomiting.", "past_history": "Healthy.", "medicines": "None.", "pregnancy": "Not pregnant."},
   [O("12:00", 104, 118, 72, 18, 98, 38.0, pain=6, note="NEWS2 per table.", origin="authored"), O("14:00", 122, 106, 64, 22, 96, 38.9, cond="if_no_action", note="Deteriorating.", origin="authored"), O("14:30", 98, 120, 74, 18, 98, 37.8, cond="after_correct_action", note="After senior review and the faster pathway.", origin="authored")],
   [E("ex.abdomen", "abdomen", "Tender and guarded low on the right.", True)],
   [R("lab.crp", "CRP", 160, "mg/L", "below 5", "high", "Rising", None, 45, origin="book"), R("lab.us", "Ultrasound appendix", "Appendix not visualised", "", "normal", "abnormal", "Equivocal (picture result)", None, 45, origin="book"),
    R("lab.ct", "CT appendix", "Complicated appendicitis with abscess", "", "normal", "abnormal", "Picture result", None, 60)],
   [S("NEWS2", "5", "Rising", "authored")],
   [P("sc14.det", "time_critical", "Deterioration overrides wait-and-see", 1, "Senior urgently; sepsis screen.", ["Senior now", "Sepsis screen"]), P("sc14.imaging", "pathway_changing", "CT now with escort and monitoring, or straight to surgery", 2, "The senior decides.", ["CT with escort", "Or theatre"])],
   [D("sc14.d1", "Equivocal ultrasound and a rising heart rate.", [("Escalate to the senior now, sepsis screen, CT with escort or straight to surgery", True, "Matches the book."), ("Keep observing", False, "Observation option withdrawn.")], rule="deterioration_clock")],
   "ready", ["Deterioration recognised", "Senior called"], [("Keeps observing", "Septic", "Harm", "Escalate")], ["Trend missed"], ["Escalate"], "A worsening trend ends wait-and-see.", ["V-02", "V-11"], variant_end={"if_no_action": "failure"})

# SC-15
sc(15, "S2", "Anaphylaxis to antibiotic + still needs antibiotics", person("Mr", "Yasir Hameed", "M", 35, "driver", 6, 18, "635015"),
   "Mr Yasir Hameed, 35: reaction to the first antibiotic; treated with adrenaline; now stable; the infection still needs treatment.", "Pain low on the right, and a rash and wheeze after the drip.",
   {"red_flags": "Fever.", "past_history": "Healthy.", "medicines": "None."},
   [O("12:00", 112, 100, 60, 22, 94, 38.4, note="Reaction under treatment by the clinician.", origin="authored"), O("12:30", 90, 118, 72, 16, 98, 37.6, cond="after_correct_action", note="Stable.", origin="authored")],
   [E("ex.general", "general", "Flushed with hives, wheezy before treatment; now settled.", True), E("ex.band", "allergy_band", "New allergy band placed.", True)],
   [R("lab.tryp", "Tryptase", "Raised", "", "normal", "abnormal", "Supports anaphylaxis", None, 120)],
   [S("NEWS2", "6", "Before treatment", "authored")],
   [P("sc15.record", "safety_blocker", "Record the new allergy", 1, "Alternative antibiotic from a different family per the policy.", ["Record the allergy", "Allergy band", "Prescriber chooses"]), P("sc15.anaes", "pathway_changing", "Anaesthetist informed", 2, "Reaction may recur.", ["Tell the anaesthetist"]), P("sc15.time", "pathway_changing", "Surgery timing per senior", 3, "Once stable.", ["Senior decides"])],
   [D("sc15.d1", "He reacted to the first antibiotic.", [("Record the new allergy, tell the anaesthetist, alternative antibiotic from a different family per the policy", True, "Matches the book."), ("Give the same antibiotic again", False, "Anaphylaxis.")])],
   "ready", ["Allergy recorded", "Alternative chosen"], [("Repeats the antibiotic", "Anaphylaxis", "Death", "Stop")], ["Allergy not recorded"], ["Record"], "A new allergy must be recorded immediately.", ["V-13"], allergies=[("Antibiotic given today", "Anaphylaxis, treated with adrenaline")], allergy_answer="I reacted to the drip today.")

# SC-16
sc(16, "S1", "GLP-1 injection + nausea + high BMI + sleep apnoea", person("Mrs", "Saima Tufail", "F", 47, "dental receptionist", 3, 14, "635016"),
   "Mrs Saima Tufail, 47: weekly semaglutide taken 2 days ago; bloated; BMI 42; STOP-BANG 6.", "Gallstone pain; I feel bloated and sick.",
   {"red_flags": "Nausea and bloating since the injection.", "past_history": "Type 2 diabetes; obesity; snoring.", "medicines": "Semaglutide weekly.", "last_food": "Dinner at 19:00 yesterday.", "pregnancy": "Not pregnant."},
   [O("08:00", 84, 136, 84, 16, 95, 36.7, note="NEWS2 1.", origin="authored")],
   [E("ex.general", "general", "Obese, comfortable, bloated.", True), E("ex.airway", "airway", "Large neck; to be assessed.", True)],
   [R("lab.bmi", "BMI", 42, "kg/m2", "18.5-24.9", "high", "Class III obesity", None, 5, origin="book"), R("lab.stopbang", "STOP-BANG", 6, "points", "below 3", "high", "High risk", None, 5, origin="book")],
   [S("STOP-BANG", "6", "High", "book")],
   [P("sc16.glp", "safety_blocker", "Delayed stomach emptying likely: aspiration risk even if fasted", 1, "Symptoms point to a full stomach.", ["Tell the anaesthetist"]), P("sc16.osa", "pathway_changing", "Airway and sleep apnoea risk", 2, "Extra monitoring.", ["Tell the anaesthetist"]), P("sc16.plan", "pathway_changing", "Anaesthetist decides", 3, "Postpone with a 24-hour liquid diet plan, or proceed with a rapid sequence and stomach ultrasound.", ["Anaesthetist decides"])],
   [D("sc16.d1", "Semaglutide taken 2 days ago and she is bloated.", [("Tell the anaesthetist: aspiration risk; they decide to postpone with a plan or proceed with precautions", True, "Matches the book."), ("Assume she is safe because she has fasted", False, "Delayed emptying.")], rule="fasting_planned")],
   "delayed", ["Aspiration risk recognised", "Anaesthetist told"], [("Assumes fasting is enough", "Aspiration", "Harm", "Tell the anaesthetist")], ["GLP-1 not asked"], ["Tell"], "A GLP-1 injection can slow stomach emptying despite fasting.", ["V-09"])

# SC-17
sc(17, "S2", "Thalassaemia trait + dehydration", person("Ms", "Iqra Saleem", "F", 25, "dental student", 10, 10, "635017"),
   "Ms Iqra Saleem, 25: known thalassaemia trait; Hb 108 with small cells; dehydrated from vomiting.", "Pain low on the right and I keep vomiting.",
   {"red_flags": "Vomiting; poor intake.", "past_history": "Thalassaemia trait.", "medicines": "None.", "pregnancy": "Not pregnant."},
   [O("12:00", 106, 112, 68, 18, 98, 37.8, pain=6, note="Dehydrated; NEWS2 per table.", origin="authored")],
   [E("ex.general", "general", "Dry lips, comfortable.", True), E("ex.abdomen", "abdomen", "Tender low on the right.", True)],
   [R("lab.hb", "Hb", 108, "g/L", "120-150", "low", "Chronic: explained by the trait", ("110", "last year"), 45, origin="book"), R("lab.mcv", "MCV", 68, "fL", "80-100", "low", "Small cells", None, 45)],
   [S("AIR score", "8", "Indeterminate", "authored")],
   [P("sc17.hb", "information_only", "Anaemia explained by thalassaemia trait", 1, "Chronic; no new bleeding; HbA1c unreliable (irrelevant in S2).", ["Tell the anaesthetist"]), P("sc17.fluids", "time_critical", "Dehydration", 2, "Fluids by the clinician.", ["Fluids", "Re-check"])],
   [D("sc17.d1", "Hb 108 with small cells.", [("Explained by her known trait; treat the dehydration; tell the anaesthetist", True, "Matches the book."), ("Call it acute bleeding", False, "It is chronic.")])],
   "ready", ["Trait recognised", "Fluids"], [("Calls it acute bleeding", "Wrong pathway", "Delay", "Check the history")], ["Trait not asked"], ["Ask"], "Know the baseline before you call a result abnormal.", ["V-18"])

# SC-18
sc(18, "S1", "Missing consent + equipment fault + late list", person("Mr", "Bilal Hashmi", "M", 52, "estate agent", 12, 5, "635018"),
   "Mr Bilal Hashmi, 52: consent form not signed; insufflator fault; list running late.", "Booked for gallbladder removal.",
   {"red_flags": "No fever.", "past_history": "Gallstones.", "medicines": "None."},
   [O("08:00", 76, 126, 78, 14, 98, 36.6, note="Well; NEWS2 0.", origin="authored")],
   [E("ex.general", "general", "Well and a little irritated by the delay.", False)],
   [R("lab.hb", "Hb", 142, "g/L", "130-170", "normal", "Normal", None, 45)],
   [S("NEWS2", "0", "Well", "authored")],
   [P("sc18.consent", "safety_blocker", "Consent form not signed", 1, "Re-consent properly, not rushed.", ["Re-consent"]), P("sc18.equip", "safety_blocker", "Insufflator fault", 2, "Report the equipment.", ["Report the fault"]), P("sc18.time", "pathway_changing", "Late list", 3, "If time runs out, postpone rather than rush.", ["Postpone if needed"])],
   [D("sc18.d1", "Form unsigned, equipment faulty, list late.", [("Neither is a medical emergency; re-consent properly, report the equipment; postpone if time runs out", True, "Matches the book."), ("Rush through to save time", False, "Unsafe.")])],
   "postponed", ["Consent redone", "Equipment reported"], [("Rushes", "Unsafe", "Harm", "Slow down")], ["Consent missed"], ["Re-consent"], "System failures cluster on busy lists; do not rush.", ["V-22"], variant_end={"fixed": "ready"})

# SC-19
sc(19, "S2", "Low sugar + confusion + sepsis in a diabetic on an SGLT2 inhibitor", person("Mr", "Rashid Mahmood", "M", 60, "retired engineer", 2, 2, "635019"),
   "Mr Rashid Mahmood, 60: glucose 3.4, confused, T 38.8, HR 120, ketones 3.5.", "I do not feel right; confused.",
   {"red_flags": "Fever; not eating for two days.", "past_history": "Type 2 diabetes.", "medicines": "An SGLT2 inhibitor and metformin."},
   [O("12:00", 120, 104, 62, 24, 95, 38.8, acvpu="C", note="Confused; NEWS2 high.", origin="authored"), O("13:00", 100, 118, 72, 18, 98, 37.9, cond="after_correct_action", note="After treatment.", origin="authored")],
   [E("ex.general", "general", "Confused, flushed, deep breathing.", True)],
   [R("lab.glu", "Capillary glucose", 3.4, "mmol/L", "6-10", "low", "Hypoglycaemia", None, 2, origin="book"), R("lab.ket", "Capillary ketones", 3.5, "mmol/L", "below 0.6", "high", "Ketoacidosis range (3.0 or more)", None, 2, origin="book"),
    R("lab.dip", "Urine dipstick", "Glucose and ketones positive", "", "negative", "abnormal", "Written result", None, 5)],
   [S("NEWS2", "9", "High risk", "authored")],
   [P("sc19.glu", "life_threat", "Low sugar", 1, "Treat immediately.", ["Treat and recheck"]), P("sc19.sepsis", "life_threat", "Sepsis", 2, "Bundle.", ["Cultures", "Antibiotics in 1 hour"]), P("sc19.dka", "life_threat", "Ketoacidosis on an SGLT2 inhibitor", 3, "DKA protocol by the clinician.", ["DKA protocol"])],
   [D("sc19.d1", "Glucose 3.4, ketones 3.5, T 38.8.", [("Treat the low sugar immediately, sepsis bundle, DKA protocol; diabetes team, senior and anaesthetist", True, "Matches the book."), ("Treat only the sepsis", False, "Other threats missed.")], rule="sepsis_antibiotics")],
   "ready", ["Three life threats", "Diabetes team"], [("Treats only the sepsis", "Hypoglycaemia", "Harm", "Treat all")], ["Ketones missed"], ["Check"], "Three conditions can cause the same confusion.", ["V-02", "V-20"], variant_end={"if_no_action": "failure"})

# SC-20
sc(20, "S2", "Older patient + appendicitis picture + weight loss + anaemia + on aspirin", person("Mr", "Anwar Baig", "M", 70, "retired postman", 9, 27, "635020"),
   "Mr Anwar Baig, 70: CRP 80, Hb 102, aspirin for stroke prevention; weight loss.", "Pain low on the right for a week.",
   {"red_flags": "Lost some weight; tired.", "past_history": "A minor stroke some years ago.", "medicines": "Aspirin."},
   [O("12:00", 94, 140, 84, 16, 97, 37.5, pain=4, note="NEWS2 per table.", origin="authored")],
   [E("ex.abdomen", "abdomen", "Tender low on the right with a fullness.", True)],
   [R("lab.hb", "Hb", 102, "g/L", "130-170", "low", "Mild-moderate anaemia", None, 45, origin="book"), R("lab.crp", "CRP", 80, "mg/L", "below 5", "high", "Raised", None, 45, origin="book"),
    R("lab.ct", "CT appendix", "Caecal mass with inflammation", "", "normal", "abnormal", "Possible tumour (picture result)", None, 60)],
   [S("NEWS2", "1", "HR 94", "authored")],
   [P("sc20.ct", "pathway_changing", "Possible caecal tumour presenting like appendicitis", 1, "CT before deciding.", ["CT", "Senior"]), P("sc20.asp", "optimisable", "Aspirin usually continues", 2, "Senior decides.", ["Senior decides"]), P("sc20.gs", "information_only", "Group and save", 3, "Prepare.", ["Group and save"])],
   [D("sc20.d1", "70-year-old with anaemia and weight loss.", [("CT before deciding; continue aspirin usually; group and save; the CT decides surgery or colorectal team", True, "Matches the book."), ("Operate for appendicitis immediately", False, "Possible tumour.")])],
   "delayed", ["CT first", "Aspirin considered"], [("Operates immediately", "Tumour missed", "Harm", "CT")], ["Anaemia ignored"], ["CT"], "Older patients with this picture need a scan first.", ["V-11", "V-18"])
