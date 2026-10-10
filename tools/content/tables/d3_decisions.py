"""D3 (p0-medical-research-data s05): fasting rules, test indications, decision rules, priority ladder, problem types, priority scale, treatment clashes,
the five decision outcomes and the end states.

Sources: Part 3 (outcomes), Part 9 (fasting), Step 6 (test indications), Part 14B (ladder, problem types, clashes), Part D (priority scale),
Part 16 (end states), Part 17 'Key decision thresholds'. Each threshold's notes say what it triggers ('think / involve whom'), never 'cancel'
(the walkthrough decision principle). Institutional values (fasting policy, escort rule) are Type C and were approved by the owner on 09 Oct 2026.
"""
from framework import Table

# ------------------------------------------------------------------ the five outcomes (Part 3)

outcomes = Table("decision_outcomes", 800, ["code", "v_label", "v_meaning", "v_example"], "Decision outcome", "Part 3 table 'The five possible outcomes of box 9'",
                 evidence="B", size=10)
outcomes.add("safe", "Safe to continue Everything fits and is within acceptable limits. Normal numbers, fasted, consent ready.", "auto", code="SAFE",
             v_label="Safe to continue", v_meaning="Everything fits and is within acceptable limits", v_example="Normal numbers, fasted, consent ready")
outcomes.add("investigate", "Needs more investigation We don't yet know enough to decide. Liver tests abnormal → need a special scan of the bile ducts.", "auto", code="INVESTIGATE",
             v_label="Needs more investigation", v_meaning="We don't yet know enough to decide", v_example="Liver tests abnormal → need a special scan of the bile ducts")
outcomes.add("optimise", "Needs treatment / optimisation A problem can be fixed (now in S2, or before a re-booked date in S1). Dehydrated → give fluids. Diabetes badly controlled → postpone planned surgery to improve it.",
             "auto", code="OPTIMISE", v_label="Needs treatment / optimisation", v_meaning="A problem can be fixed (now in S2, or before a re-booked date in S1)",
             v_example="Dehydrated → give fluids")
outcomes.add("review", "Needs senior or anaesthetic review Decision is beyond the trainee or affects anaesthesia. Patient ate 3 hours ago; heart rhythm irregular.", "auto", code="REVIEW",
             v_label="Needs senior or anaesthetic review", v_meaning="Decision is beyond the trainee or affects anaesthesia", v_example="Patient ate 3 hours ago; heart rhythm irregular")
outcomes.add("emergency", "Emergency The patient may be in danger now. Blood pressure very low, oxygen falling, severe allergic reaction.", "auto", code="EMERGENCY",
             v_label="Emergency", v_meaning="The patient may be in danger now", v_example="Blood pressure very low, oxygen falling, severe allergic reaction")

# ------------------------------------------------------------------ end states (Part 16)

ends = Table("end_states", 810, ["code", "v_label", "v_meaning"], "End state", "Part 16 table 'End states'", evidence="B", size=10)
ends.add("ready", "Ready for incision — safe All blockers resolved; correct pathway.", "auto", code="READY_SAFE", v_label="Ready for incision — safe", v_meaning="All blockers resolved; correct pathway")
ends.add("postponed", "Appropriately postponed (S1) Correct reason found and communicated — counts as SUCCESS.", "auto", code="POSTPONED_APPROPRIATELY",
         v_label="Appropriately postponed (S1)", v_meaning="Correct reason found and communicated — counts as SUCCESS")
ends.add("redirected", "Appropriately redirected Diagnostic diversion (e.g. suspected ectopic pregnancy → gynaecology); cholangitis → biliary drainage pathway; inflamed gallbladder → acute pathway.",
         "auto", code="REDIRECTED_APPROPRIATELY", v_label="Appropriately redirected", v_meaning="Diagnostic diversion")
ends.add("delayed", "Safe but delayed Right outcome with avoidable delays.", "auto", code="SAFE_BUT_DELAYED", v_label="Safe but delayed", v_meaning="Right outcome with avoidable delays")
ends.add("failure", "Safety failure A blocker was violated or a life-threatening event missed.", "auto", code="SAFETY_FAILURE", v_label="Safety failure",
         v_meaning="A blocker was violated or a life-threatening event missed")

# ------------------------------------------------------------------ fasting (Part 9)

fast = Table("fasting_rules", 820, ["item", "v_hours", "v_meaning", "applies"], "Fasting rule", "Part 9 table 'Minimum fasting times before PLANNED anaesthesia'",
             evidence="A", size=30)
HEAD = "Minimum fasting times before PLANNED anaesthesia"
fast.add("clear_liquids", "Clear liquids 2 hours Water, pulp-free juice, black tea/coffee WITHOUT milk, fizzy drinks, special carbohydrate drinks.", "auto",
         item="clear_liquids", v_hours=2, v_meaning="Water, pulp-free juice, black tea/coffee WITHOUT milk", applies="planned",
         note="Some hospitals allow sips until 1 hour (context-dependent, institutional protocol).")
fast.add("breast_milk", "Breast milk (babies) 4 hours Listed for completeness; ProcSense is adults", "auto", item="breast_milk", v_hours=4, v_meaning="Listed for completeness", applies="babies (not used: adults only)")
fast.add("milk", "Milk, formula, tea/coffee WITH milk 6 hours Milk behaves like food in the stomach Count as food", "auto", item="milk", v_hours=6,
         v_meaning="Milk behaves like food in the stomach", applies="planned")
fast.add("light_meal", "Light meal (e.g. toast + clear drink) 6 hours Small easily digested meal 6 h minimum", "auto", item="light_meal", v_hours=6,
         v_meaning="Small easily digested meal", applies="planned")
fast.add("heavy_meal", "Heavy, fried or fatty meal, meat (e.g. biryani, paratha, nihari) 8 hours or more Slow to leave the stomach 8 h minimum", "auto",
         item="heavy_meal", v_hours=8, v_meaning="Slow to leave the stomach", applies="planned", note="'8 hours or more'. Classing meat-rich or fried dishes as 'heavy' is clinical interpretation.")
fast.add("chewing_gum", "Chewing gum Don't cancel ASA 2023: gum alone shouldn't delay surgery Remove before sleep", "auto", item="chewing_gum", v_hours="",
         v_meaning="gum alone shouldn't delay surgery", applies="planned")
fast.add("regular_tablets", "Regular tablets With a sip of water Unless told to stop that medicine Follow medicine plan", "auto", item="regular_tablets", v_hours="",
         v_meaning="With a sip of water", applies="planned")
fast.add("breach", "Solid food/milk within 6 h, heavy meal within 8 h, or clear drink within 2 h? Fasting breach → tell the anaesthetist NOW (never 'round up' the time)", "auto",
         item="breach_rule", v_hours="6; 8; 2", v_meaning="tell the anaesthetist NOW", applies="planned (S1)",
         note="A breach means: tell the anaesthetist, who decides (usually later on the list or postpone). The trainee does not decide alone.")
fast.add("emergency_full_stomach", "So the anaesthetist treats every emergency patient as a 'full stomach' and uses RSI", "auto", item="emergency_full_stomach", v_hours="",
         v_meaning="full stomach", applies="emergency (S2)", note="Fasting status is often unknown; the trainee records the exact time and content and declares 'aspiration risk: YES' at Sign In.")

# ------------------------------------------------------------------ test indications (Step 6)

tests = Table("test_indications", 850, ["test", "v_s1", "v_s2", "v_why", "d_s1_kind", "d_s2_kind"], "Test indication", "Step 6 table 'Which tests for which patient?'",
              evidence="A", size=30)
D = "Derived: a one-word reading of the text cell (always / conditional / not_routine / per_policy / not_used); the text itself is kept in the verbatim columns."


def ti(key, quote, s1, s2, why, k1, k2):
    tests.add(key, quote, "auto", test=key, v_s1=s1, v_s2=s2, v_why=why, d_s1_kind=k1, d_s2_kind=k2, note=D)


ti("lft", "Liver tests (LFTs) YES (all) If upper pain or unclear diagnosis Look for bile blockage", "YES (all)", "If upper pain or unclear diagnosis", "Look for bile blockage", "always", "conditional")
ti("pregnancy", "Pregnancy test If pregnancy is possible or uncertain — offered with consent If pregnancy is possible — offered with consent (urgent in abdominal pain) Changes diagnosis, imaging, anaesthesia, drugs and timing",
   "If pregnancy is possible or uncertain — offered with consent", "If pregnancy is possible — offered with consent (urgent in abdominal pain)", "Changes diagnosis, imaging, anaesthesia, drugs and timing", "conditional", "conditional")
ti("fbc", "FBC (blood count) If sicker (ASA 3+), anaemia symptoms, heart/kidney disease, or local rule YES Infection, bleeding, anaemia",
   "If sicker (ASA 3+), anaemia symptoms, heart/kidney disease, or local rule", "YES", "Infection, bleeding, anaemia", "conditional", "always")
ti("crp", "CRP Only if inflammation suspected YES Inflammation; score", "Only if inflammation suspected", "YES", "Inflammation; score", "conditional", "always")
ti("u_and_e", "U&E (kidneys, salts) If 65+, diabetes, kidney disease, water tablets/ACE-i, ASA 3+ YES Kidneys, potassium, dehydration",
   "If 65+, diabetes, kidney disease, water tablets/ACE-i, ASA 3+", "YES", "Kidneys, potassium, dehydration", "conditional", "always")
ti("glucose_fingerprick", "Blood sugar (finger-prick) If diabetic If diabetic or vomiting Low/high sugar", "If diabetic", "If diabetic or vomiting", "Low/high sugar", "conditional", "conditional")
ti("hba1c", "HbA1c If diabetic and none in 3 months No Diabetes control", "If diabetic and none in 3 months", "No", "Diabetes control", "conditional", "not_used")
ti("clotting", "Clotting (INR/APTT) If on blood thinners or liver disease If on blood thinners, septic, liver disease Bleeding risk",
   "If on blood thinners or liver disease", "If on blood thinners, septic, liver disease", "Bleeding risk", "conditional", "conditional")
ti("group_and_save", "Group & save Per hospital rule Per rule; always if bleeding suspected Blood ready if needed", "Per hospital rule", "Per rule; always if bleeding suspected", "Blood ready if needed", "per_policy", "per_policy")
ti("urine_dipstick", "Urine dipstick No (not routine) YES Urine infection/stone look-alikes", "No (not routine)", "YES", "Urine infection/stone look-alikes", "not_routine", "always")
ti("ecg", "ECG If heart/kidney/diabetes disease, irregular pulse, 65+ (some) If 65+, heart disease, HR above 130, irregular, salt problems Heart rhythm",
   "If heart/kidney/diabetes disease, irregular pulse, 65+ (some)", "If 65+, heart disease, HR above 130, irregular, salt problems", "Heart rhythm", "conditional", "conditional")
ti("lactate_cultures", "Lactate + blood cultures If septic If sepsis suspected Sepsis", "If septic", "If sepsis suspected", "Sepsis", "conditional", "conditional")
ti("lipase", "Lipase/amylase If severe upper pain / back pain If diagnosis unclear Pancreatitis", "If severe upper pain / back pain", "If diagnosis unclear", "Pancreatitis", "conditional", "conditional")
ti("ultrasound", "Ultrasound Already done before booking; repeat if acute Young/female/pregnant first See inside", "Already done before booking; repeat if acute", "Young/female/pregnant first", "See inside",
   "conditional", "conditional")
ti("ct", "CT Only for complications Adults when unclear; older patients Detailed scan", "Only for complications", "Adults when unclear; older patients", "Detailed scan", "conditional", "conditional")
ti("mrcp", "MRCP If liver tests abnormal / duct wide No Look for duct stones", "If liver tests abnormal / duct wide", "No", "Look for duct stones", "conditional", "not_used")
ti("chest_xray", "Chest X-ray No (not routine) Only if breathing problems / suspected burst gut —", "No (not routine)", "Only if breathing problems / suspected burst gut", "", "not_routine", "conditional")

# ------------------------------------------------------------------ decision rules (Part 17 key decision thresholds; inventory D187-D205)

rules = Table("decision_rules", 900, ["inventory", "rule", "v_number", "kind", "triggers"], "Decision threshold", "Part 17 table 'Key decision thresholds'", evidence="A", size=40)
SRC = "Part 17 'Key decision thresholds'"


def dr(inv, key, quote, number, triggers, evidence="", note=""):
    rules.add(key, quote, "auto", inventory=inv, rule=key, v_number=number, kind="threshold", triggers=triggers, evidence=evidence, note=note)


dr("D187", "fasting_planned", "Fasting: clear / milk & light meal / heavy meal 2 h / 6 h / 8 h", "2 h / 6 h / 8 h", "think: tell the anaesthetist if breached; the anaesthetist decides")
dr("D188", "s1_bp_postpone", "S1 BP postpone ≥180/110 on repeat (GP target <160/100)", "180/110; 160/100", "think: senior/anaesthetic review; S1 usually deferred after review")
dr("D189", "s1_diabetes_postpone", "S1 diabetes postpone HbA1c >69 mmol/mol (8.5%)", "69; 8.5", "think: optimise before planned surgery; senior/anaesthetic review")
dr("D190", "periop_glucose_target", "Peri-op sugar target 6–10 (6–12 acceptable) mmol/L", "6–10; 6–12", "target range; outside it, think and tell the team")
dr("D191", "duct_stone_high_risk", "Bile-duct stone high risk Bilirubin >68 µmol/L (4 mg/dL) + duct >6 mm; or stone seen; or cholangitis", "68; 4; 6", "involve: senior; ERCP pathway before cholecystectomy")
dr("D192", "gallbladder_moderate_grade", "Gallbladder 'moderate' grade WBC >18; lump; >72 h; severe local inflammation", "18; 72", "involve: senior; acute pathway")
dr("D193", "appendix_abnormal_width", "Appendix abnormal width >6 mm", "6", "think: supports appendicitis; senior decision")
dr("D194", "air_bands", "AIR bands 0–4 low, 5–8 unsure, 9–12 high", "0–4; 5–8; 9–12", "think: guides scan or senior decision")
dr("D195", "aas_bands", "AAS bands 0–10 low, 11–15 intermediate, ≥16 high", "0–10; 11–15; 16", "think: guides scan or senior decision")
dr("D196", "sepsis_antibiotics", "Sepsis antibiotics Within 1 h if NEWS2 ≥7 (or 5–6 with a single 3); 3 h if 5–6; 6 h if 1–4 (NICE NG253, 2025)", "1; 3; 6", "think: flag antibiotics and fluids; a clinician prescribes",
   note="The same times are in sepsis_ng253_tiers.csv (with the NICE page); this row is the Part 17 summary.")
dr("D197", "antibiotic_before_incision", "Antibiotic before incision Within 60 min", "60", "think: Time Out checks the antibiotic was given in time")
dr("D198", "theatre_temperature", "Theatre temperature ≥36.0 °C", "36.0", "think: warming equipment")
dr("D199", "sign_in_blood_loss", "Blood loss question (Sign In) >500 mL (7 mL/kg children)", "500; 7", "think: blood ready and access at Sign In")
dr("D200", "appendectomy_timing", "Appendectomy timing Generally prompt; WSES 2025: laparoscopic appendectomy within 24 h of admission when surgery is indicated", "24",
   "think: the surgeon sets the timing; context-dependent", note="Context-dependent: severity, sepsis, anaesthetic assessment, resources.")
dr("D201", "acute_cholecystitis_surgery", "Acute cholecystitis surgery Early (ideally ≤72 h; NICE ≤1 week)", "72 h; 1 week", "involve: senior; acute pathway")
rules.add("transfer_escort", "Transfer escort NEWS2 ≤4 porter+nurse; 5–6 nurse+monitor; ≥7 doctor+nurse+full monitoring", "auto", inventory="D202", rule="transfer_escort",
          v_number="4; 5–6; 7", kind="threshold", triggers="think: who escorts the patient", evidence="D",
          note="Simulator design value (provisional) and institution-dependent (Type D + C); the book says there is no numeric guideline.")
rules.add("deterioration_clock", "Deterioration clock (untreated S2) HR +5–10 and NEWS2 +1 about every 30 min", "auto", inventory="D203", rule="deterioration_clock",
          v_number="5–10; 1; 30", kind="simulator_design", triggers="the clock the S2 deterioration runs on", evidence="D",
          note="Simulator design value (provisional): HR +5 to 10 and NEWS2 +1 about every 30 minutes if untreated.")
rules.add("low_spo2_trigger", "Obvious respiratory distress, cyanosis, falling consciousness, or SpO2 very low (e.g. ≤85% — a ProcSense simulator trigger, not a guideline cut-off)?",
          "auto", inventory="D205", rule="low_spo2_trigger", v_number="85", kind="simulator_trigger", triggers="EMERGENCY: call for help now", evidence="D",
          note="Simulator trigger, not a guideline cut-off.")

# ------------------------------------------------------------------ priority ladder, problem types, Part D scale (Part 14B and Part D)

ladder = Table("priority_ladder", 940, ["rank", "v_question", "v_action"], "Priority ladder step", "Part 14B.1 'Universal priority ladder'", evidence="B", size=10)
L = [
    (1, "Is anything immediately life-threatening? (Airway, Breathing, Circulation, low sugar, anaphylaxis, NEWS2 7+)", "Fix that FIRST using ABCDE and call for help — everything else waits"),
    (2, "Is there a hard safety blocker? (identity doubt, allergy unknown before a drug, consent invalid, positive pregnancy test with pain)", "Resolve before any non-emergency treatment or transfer"),
    (3, "Is there a time-critical treatment? (antibiotics within 1 h in sepsis, pain relief, fluids for shock, potassium 6.5+)", "Do it now — in parallel with the remaining work"),
    (4, "Does any finding change the diagnosis or pathway? (ectopic, cholangitis, burst appendix, duct stone)", "Senior/specialist; change pathway"),
    (5, "Do treatments conflict? (e.g. fluids vs heart failure; best antibiotic vs allergy; painkiller vs kidneys)", "Choose the safest alternative (table 14B.3); ask senior/anaesthetist"),
    (6, "Is it S1 with any fixable problem left?", "Senior/anaesthetic review → usually optimise first (defer / re-time / rebook)"),
]
for rank, q, a in L:
    ladder.add(f"rank{rank}", f"{rank}. {q} {a}", "auto", rank=rank, v_question=q, v_action=a)
ladder.add("rank7", "7. Prepare, consent, hand over — listing EVERY problem and its plan in the SBAR", "auto", rank=7, v_question="Prepare, consent, hand over", v_action="listing EVERY problem and its plan in the SBAR")

ptypes = Table("problem_types", 950, ["type", "v_examples", "v_effect"], "Problem type", "Part 14B.5 table 'Problem type'", evidence="B", size=10)
ptypes.add("life_threat", "Life threat Shock, low oxygen, anaphylaxis, low sugar, K 6.5+, NEWS2 7+ Stops everything; emergency mode", "auto", type="life_threat",
           v_examples="Shock, low oxygen, anaphylaxis, low sugar, K 6.5+, NEWS2 7+", v_effect="Stops everything; emergency mode")
ptypes.add("safety_blocker", "Safety blocker Identity doubt, unknown allergy, invalid consent, unknown pregnancy status, unacknowledged critical result, incomplete checklist Cannot pass checkpoints until resolved",
           "auto", type="safety_blocker", v_examples="Identity doubt, unknown allergy, invalid consent, unknown pregnancy status, unacknowledged critical result, incomplete checklist",
           v_effect="Cannot pass checkpoints until resolved")
ptypes.add("time_critical", "Time-critical Sepsis antibiotics, analgesia, fluids for dehydration Must be done within a clock", "auto", type="time_critical",
           v_examples="Sepsis antibiotics, analgesia, fluids for dehydration", v_effect="Must be done within a clock")
ptypes.add("pathway_changing", "Pathway-changing Ectopic, cholangitis, perforation, duct stone, new AF, MI Switches to a different branch/team", "auto", type="pathway_changing",
           v_examples="Ectopic, cholangitis, perforation, duct stone, new AF, MI", v_effect="Switches to a different branch/team")
ptypes.add("optimisable", "Optimisable BP 180/110+, HbA1c above 69, anaemia, salts, anticoagulant timing, fasting S1 usually deferred after review/re-time; S2 correct in parallel", "auto",
           type="optimisable", v_examples="BP 180/110+, HbA1c above 69, anaemia, salts, anticoagulant timing, fasting", v_effect="S1 usually deferred after review/re-time; S2 correct in parallel")
ptypes.add("information_only", "Information-only Smoker, mild obesity, controlled AF Recorded and handed over", "auto", type="information_only",
           v_examples="Smoker, mild obesity, controlled AF", v_effect="Recorded and handed over")

scale = Table("priority_scale_part_d", 960, ["priority", "v_meaning"], "Priority scale (Part D)", "Part D introduction 'How to prioritise'", evidence="B", size=10)
PD = ("Priority 1 — immediate threat to airway, breathing or circulation (or brain: glucose). Priority 2 — potentially serious physiological abnormality. "
      "Priority 3 — condition needing optimisation before proceeding. Priority 4 — important but non-urgent finding.")
scale.add("p1", PD, "auto", priority=1, v_meaning="immediate threat to airway, breathing or circulation (or brain: glucose)")
scale.add("p2", PD, "auto", priority=2, v_meaning="potentially serious physiological abnormality")
scale.add("p3", PD, "auto", priority=3, v_meaning="condition needing optimisation before proceeding")
scale.add("p4", PD, "auto", priority=4, v_meaning="important but non-urgent finding")

# ------------------------------------------------------------------ treatment clashes (Part 14B.3)

clash = Table("treatment_clashes", 970, ["clash_key", "v_clash", "v_why", "v_safer", "v_decided_by", "v_tag"], "Treatment clash", "Part 14B.3 table 'Common treatment clashes'",
              evidence="B", size=40)
PSD = "PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED"
CTX = "CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION"


def cl(key, quote, clash_text, why, safer, decided, tag=""):
    clash.add(key, quote, "auto", clash_key=key, v_clash=clash_text, v_why=why, v_safer=safer, v_decided_by=decided, v_tag=tag,
              note="The book says 'Safer approach (draft)'; doses are never shown to the trainee.")


cl("fluid_heart_kidney", "Needs fluid (sepsis/dehydration) + has heart failure or severe kidney failure Fluid may flood the lungs Smaller boluses (e.g. 250 mL) with re-check of breathing/oxygen after each; earlier senior/critical care Senior/anaesthetist PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Needs fluid (sepsis/dehydration) + has heart failure or severe kidney failure", "Fluid may flood the lungs", "Smaller boluses (e.g. 250 mL) with re-check of breathing/oxygen after each; earlier senior/critical care", "Senior/anaesthetist", PSD)
cl("pain_kidney", "Needs pain relief + kidney injury NSAIDs (ibuprofen) worsen kidneys; morphine builds up Paracetamol + carefully titrated opioid (often not morphine in severe kidney injury); no NSAIDs PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Needs pain relief + kidney injury", "NSAIDs (ibuprofen) worsen kidneys; morphine builds up", "Paracetamol + carefully titrated opioid (often not morphine in severe kidney injury); no NSAIDs", "", PSD)
cl("pain_pregnancy", "Needs pain relief + pregnancy NSAIDs avoided in pregnancy Paracetamol; opioid if needed per senior PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Needs pain relief + pregnancy", "NSAIDs avoided in pregnancy", "Paracetamol; opioid if needed per senior", "", PSD)
cl("antibiotic_penicillin_allergy", "Needs antibiotic + penicillin allergy Usual drug may be unsafe Use hospital allergy-alternative rule CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION Microbiology/policy",
   "Needs antibiotic + penicillin allergy", "Usual drug may be unsafe", "Use hospital allergy-alternative rule", "Microbiology/policy", CTX)
cl("ct_pregnancy", "Needs CT + pregnancy Radiation Ultrasound → MRI; CT only if senior decides benefit outweighs risk Senior/radiology",
   "Needs CT + pregnancy", "Radiation", "Ultrasound → MRI; CT only if senior decides benefit outweighs risk", "Senior/radiology")
cl("ct_dye_kidney", "Needs CT with dye + kidney injury Dye can worsen kidneys Fluids first; senior weighs benefit; non-contrast or ultrasound alternatives Senior/radiology",
   "Needs CT with dye + kidney injury", "Dye can worsen kidneys", "Fluids first; senior weighs benefit; non-contrast or ultrasound alternatives", "Senior/radiology")
cl("ct_dye_allergy", "Needs CT with dye + dye allergy Allergic reaction Alternative imaging or pre-medication per radiology policy Radiology CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION",
   "Needs CT with dye + dye allergy", "Allergic reaction", "Alternative imaging or pre-medication per radiology policy", "Radiology", CTX)
cl("antiemetic_qtc", "Needs anti-sickness + long QTc on ECG Ondansetron can worsen QT Alternative antiemetic; fix potassium/magnesium Anaesthetist PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Needs anti-sickness + long QTc on ECG", "Ondansetron can worsen QT", "Alternative antiemetic; fix potassium/magnesium", "Anaesthetist", PSD)
cl("metoclopramide_obstruction", "Needs metoclopramide + bowel obstruction Can worsen obstruction Different antiemetic; NG tube per senior PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Needs metoclopramide + bowel obstruction", "Can worsen obstruction", "Different antiemetic; NG tube per senior", "", PSD)
cl("blood_thinner_urgent_surgery", "On blood thinner + needs urgent surgery Bleeding vs clot risk Senior/haematology decide reversal; blood products ready PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "On blood thinner + needs urgent surgery", "Bleeding vs clot risk", "Senior/haematology decide reversal; blood products ready", "", PSD)
cl("antiplatelet_stent", "On antiplatelets after a recent heart stent + needs surgery Stopping risks stent clot (heart attack) S1 usually deferred after review; S2 operate on antiplatelets with heart-team advice Cardiology + senior",
   "On antiplatelets after a recent heart stent + needs surgery", "Stopping risks stent clot (heart attack)", "S1 usually deferred after review; S2 operate on antiplatelets with heart-team advice", "Cardiology + senior")
cl("insulin_nbm", "Diabetic on insulin + nil by mouth Low sugar risk Insulin adjusted, sugar hourly, glucose-containing fluid or VRIII per protocol PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Diabetic on insulin + nil by mouth", "Low sugar risk", "Insulin adjusted, sugar hourly, glucose-containing fluid or VRIII per protocol", "", PSD)
cl("sglt2_sepsis", "SGLT2 user + sepsis/vomiting Euglycaemic DKA Check ketones even if sugar near normal; diabetes team Diabetes team",
   "SGLT2 user + sepsis/vomiting", "Euglycaemic DKA", "Check ketones even if sugar near normal; diabetes team", "Diabetes team")
cl("steroid_sepsis", "Steroid user + sepsis/surgery Adrenal crisis (collapse) Extra steroid cover per anaesthetist PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED",
   "Steroid user + sepsis/surgery", "Adrenal crisis (collapse)", "Extra steroid cover per anaesthetist", "", PSD)
cl("high_potassium_suxamethonium", "High potassium + needs suxamethonium (an anaesthetic relaxant) Raises potassium further Anaesthetist chooses a different drug; correct potassium first Anaesthetist",
   "High potassium + needs suxamethonium (an anaesthetic relaxant)", "Raises potassium further", "Anaesthetist chooses a different drug; correct potassium first", "Anaesthetist")
cl("full_stomach_emergency", "Full stomach + emergency surgery Aspiration vs delay of surgery RSI; don't wait unless safe to Anaesthetist + surgeon",
   "Full stomach + emergency surgery", "Aspiration vs delay of surgery", "RSI; don't wait unless safe to", "Anaesthetist + surgeon")
cl("low_oxygen_copd", "Low oxygen + long-term lung disease (CO2 retainer) Too much oxygen can be harmful Target 88–92%; blood gas Senior",
   "Low oxygen + long-term lung disease (CO2 retainer)", "Too much oxygen can be harmful", "Target 88–92%; blood gas", "Senior")
cl("confused_consent_emergency", "Confused patient + consent needed + emergency No capacity Treat cause; if still lacking and urgent → emergency/best-interests route CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION Senior",
   "Confused patient + consent needed + emergency", "No capacity", "Treat cause; if still lacking and urgent → emergency/best-interests route", "Senior", CTX)

TABLES = [outcomes, ends, fast, tests, rules, ladder, ptypes, scale, clash]
OFFICIAL_FILES = {}
