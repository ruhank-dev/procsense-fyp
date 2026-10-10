"""D5 (p0-medical-research-data s06): medicine groups as plans, perioperative timing, dose policy, the three drug checks and the role convention.

Source: walkthrough Part 8.1 and 8.3 (pages 63-68) and Part 14B.2. The trainee never prescribes, selects a dose for, or administers a patient-specific
medicine: rows whose policy is PATIENT_SPECIFIC keep no dose text at all. EDUCATIONAL_EXAMPLE rows keep the book's example as display text only, with the
source named beside it; the engine never parses it. Antibiotic choice is institutional (owner approved the walkthrough default on 09 Oct 2026).
`d_policy` is derived from the tag printed in the row: EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) -> EDUCATIONAL_EXAMPLE; PATIENT-SPECIFIC DOSE — CLINICIAN
CONTROLLED -> PATIENT_SPECIFIC; CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL -> INSTITUTIONAL; no tag -> NONE (a test checks the tag is in the quote).
"""
from framework import Table

med = Table("medicines", 1300, ["group", "section", "v_for", "v_care", "v_timing", "d_policy", "v_example", "v_source"], "Medicine plan", "Part 8.3 'Medicine groups the trainee deals with'",
            evidence="A", size=100)
EDU = "EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION"
PS = "PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED"
CTX = "CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION"


def m(key, quote, group, section, policy, for_="", care="", timing="", example="", source="", evidence="", note=""):
    med.add(key, quote, "auto", group=group, section=section, v_for=for_, v_care=care, v_timing=timing, d_policy=policy, v_example=example, v_source=source,
            evidence=evidence, note=note)


# ---- core groups (page 64-65)
m("paracetamol", f"Paracetamol (acetaminophen) First painkiller; lowers fever Severe liver disease; very low weight → lower dose Allergy rare Can be given early {EDU} IV paracetamol (SmPC): 1 g as a 15-minute infusion, up to four times a day with at least 4 hours between doses, maximum 4 g/day for adults over 50 kg (3 g/day if additional risk factors such as liver disease, malnutrition, dehydration); under 50 kg weight-based (15 mg/kg per dose, maximum 60 mg/kg/day, not above 3 g); longer interval in severe kidney impairment. Oral dosing: per BNF/SmPC — prescriber decides",
  "paracetamol", "core", "EDUCATIONAL_EXAMPLE", "First painkiller; lowers fever", "Severe liver disease; very low weight → lower dose", "Can be given early",
  "IV paracetamol (SmPC): 1 g as a 15-minute infusion, up to four times a day with at least 4 hours between doses, maximum 4 g/day for adults over 50 kg (3 g/day if additional risk factors such as liver disease, malnutrition, dehydration); under 50 kg weight-based (15 mg/kg per dose, maximum 60 mg/kg/day, not above 3 g); longer interval in severe kidney impairment. Oral dosing: per BNF/SmPC — prescriber decides",
  "SmPC", note="Educational example, display only; never an instruction.")
m("nsaids", f"NSAIDs (non-steroidal anti-inflammatory drugs: ibuprofen, diclofenac, ketorolac) Pain + inflammation; good for gallstone pain Kidney injury/dehydration, stomach ulcers/bleeding, blood thinners, some asthma, pregnancy (avoid), heart failure Some people get asthma at tacks/allergy Avoid in S2 patients with kidney injury {PS}",
  "nsaids", "core", "PATIENT_SPECIFIC", "Pain + inflammation; good for gallstone pain", "Kidney injury/dehydration, stomach ulcers/bleeding, blood thinners, some asthma, pregnancy (avoid), heart failure",
  "Avoid in S2 patients with kidney injury")
m("opioids", f"Opioids (morphine, fentanyl, tramadol) — strong painkillers Moderate–severe pain Can slow breathing, cause drowsiness, sickness; build up in kidney failure True allergy rare; itch common Titrated through the drip in severe pain; watch breathing {PS}",
  "opioids", "core", "PATIENT_SPECIFIC", "Moderate–severe pain", "Can slow breathing, cause drowsiness, sickness; build up in kidney failure", "Titrated through the drip in severe pain; watch breathing")
m("antiemetics", f"Antiemetics (anti-sickness: ondansetron, cyclizine, metoclopramide) Nausea/vomiting Ondansetron: certain heart-rhythm (long QTc) patients; metoclopramide: not with bowel blockage — Whenever needed {EDU} ondansetron (SmPC, post-operative nausea/vomiting): 4 mg by slow IV injection (not less than 30 s), at induction for prevention or as a single dose for established PONV; use for other causes of nausea is the prescriber's decision; others {PS}",
  "antiemetics", "core", "EDUCATIONAL_EXAMPLE", "Nausea/vomiting", "Ondansetron: certain heart-rhythm (long QTc) patients; metoclopramide: not with bowel blockage", "Whenever needed",
  "ondansetron (SmPC, post-operative nausea/vomiting): 4 mg by slow IV injection (not less than 30 s), at induction for prevention or as a single dose for established PONV; use for other causes of nausea is the prescriber's decision",
  "SmPC", note="Other antiemetics are patient-specific (clinician controlled).")
m("iv_fluids", f"IV fluids (salt-water solutions such as Hartmann's / Ringer's lactate, or 0.9% saline) Replace lost fluid; keep hydrated while fasting; treat low BP from dehyd ration/sepsis Heart failure or kidney failure (fluid overload — lungs fill with water) — Quick 'bolus' to resuscitate; slower 'maintenance' otherwise {EDU} resuscitation (NICE CG174 rec 1.3.1): crystalloid with sodium 130–154 mmol/L as a 500 mL bolus over less than 15 minutes, then reassess. CG174 does not cover pregnancy, severe liver or kidney disease, diabetes or burns, and heart failure needs smaller boluses — in those groups, and for the overall plan, {PS}",
  "iv_fluids", "core", "EDUCATIONAL_EXAMPLE", "Replace lost fluid; keep hydrated while fasting", "Heart failure or kidney failure (fluid overload — lungs fill with water)", "Quick 'bolus' to resuscitate; slower 'maintenance' otherwise",
  "resuscitation (NICE CG174 rec 1.3.1): crystalloid with sodium 130–154 mmol/L as a 500 mL bolus over less than 15 minutes, then reassess",
  "NICE CG174 rec 1.3.1", note="NICE NG253 resources state 250 mL (WT-I13, ISS-0015); the book's value is kept. In pregnancy, severe liver or kidney disease, diabetes, burns and heart failure the plan is patient-specific.")

# ---- antibiotics (page 65)
m("abx_s1_low_risk", "S1, low-risk planned keyhole gallbladder Preventive ('prophylactic') antibiotic NOT routinely needed CLINICAL REFERENCE — SOURCE BACKED",
  "antibiotics_s1_low_risk", "antibiotics", "NONE", "Preventive ('prophylactic') antibiotic NOT routinely needed")
m("abx_s1_higher_risk", f"S1, higher risk: age over 70, inflamed gallbladder, jaundice/duct stones, diabetes, ASA 3+, weak immunity, pregnancy, likely conversion to open One preventive dose; drug chosen by local list {CTX} Within 60 minutes before the first cut CLINICAL REFERENCE — SOURCE BACKED (WHO; NICE NG125 says at the start of anaesthesia); repeat if operation is long {EDU} cefazolin (ASHP/IDSA/SIS/SHEA 2013): 2 g IV, 3 g if body weight is 120 kg or more; choice of agent and any redose depend on institutional policy, allergy history and kidney function",
  "antibiotics_s1_higher_risk", "antibiotics", "EDUCATIONAL_EXAMPLE", "One preventive dose; drug chosen by local list", "age over 70, inflamed gallbladder, jaundice/duct stones, diabetes, ASA 3+, weak immunity, pregnancy, likely conversion to open",
  "Within 60 minutes before the first cut", "cefazolin (ASHP/IDSA/SIS/SHEA 2013): 2 g IV, 3 g if body weight is 120 kg or more; choice of agent and any redose depend on institutional policy, allergy history and kidney function",
  "ASHP/IDSA/SIS/SHEA 2013", note="Drug choice is institutional (local list); the timing is source-backed (WHO; NICE NG125).")
m("abx_s2_all", f"S2 appendicitis (all patients) Broad antibiotics covering gut germs before surgery CLINICAL REFERENCE — SOURCE BACKED (WSES) As soon as decided; if sepsis is suspected follow the NICE NG253 risk tier (within 1 hour for high risk); confirm timing at Time Out Local formulary {CTX} (common examples: cefazolin + metronidazole; co-amoxiclav)",
  "antibiotics_s2_appendicitis", "antibiotics", "INSTITUTIONAL", "Broad antibiotics covering gut germs before surgery", "",
  "As soon as decided; if sepsis is suspected follow the NICE NG253 risk tier (within 1 hour for high risk); confirm timing at Time Out",
  note="Agent from the local formulary; common examples named in the book: cefazolin + metronidazole; co-amoxiclav.")
m("abx_s2_simple_after", "S2 simple appendicitis after removal Usually no more antibiotics", "antibiotics_s2_simple_after_removal", "antibiotics", "NONE", "Usually no more antibiotics")
m("abx_s2_complicated", f"S2 burst/complicated Short course after source control: 2–3 days (WSES 2025 edition; the 2020 edition said up to 3–5 days) — {PS}",
  "antibiotics_s2_complicated", "antibiotics", "PATIENT_SPECIFIC", "", "", "Short course after source control: 2–3 days",
  note="2–3 days is the plan's duration (WSES 2025; the 2020 edition said up to 3–5 days), not a dose. The drug and dose are clinician controlled.")
m("abx_allergy_mild", "Mild delayed rash as a child → most guidelines allow related antibiotics like cefazolin.", "antibiotic_allergy_mild_rash", "antibiotics", "NONE", "",
  "", "", note="Ask the patient what happened. 'Mild delayed rash as a child': most guidelines allow related antibiotics like cefazolin.")
m("abx_allergy_severe", f"Immediate severe reaction (throat swelling, collapse = anaphylaxis) → some guidelines still allow cefazolin, others require a completely different antibiotic (e.g. clindamycin or vancomycin plus a gut-germ antibiotic). This is an important hospital decision {CTX}",
  "antibiotic_allergy_immediate_severe", "antibiotics", "INSTITUTIONAL", "", "", "",
  note="Immediate severe reaction (anaphylaxis): a hospital decision; some guidelines still allow cefazolin, others need a completely different antibiotic.")
m("abx_allergy_sjs", "Severe skin peeling reaction (SJS/TEN) → avoid this whole antibiotic family CLINICAL REFERENCE — SOURCE BACKED", "antibiotic_allergy_sjs_ten", "antibiotics", "NONE", "",
  "avoid this whole antibiotic family")

# ---- blood thinners (page 65-66)
m("warfarin", f"Warfarin 5 days; check INR the day before (aim 1.5 or less) 'Bridging' with injections only for very high clot risk — specialist decision {CTX}",
  "warfarin", "blood_thinners", "INSTITUTIONAL", "", "", "5 days; check INR the day before (aim 1.5 or less)", note="Usual stop time before planned surgery. Bridging only for very high clot risk: a specialist decision.")
m("doacs", f"Apixaban / rivaroxaban / edoxaban 1 day (low-moderate bleeding risk surgery) or 2 days (high bleeding risk), if kidneys OK Whether lap chole counts as low or high bleeding risk {CTX} (PAUSE trial)",
  "apixaban_rivaroxaban_edoxaban", "blood_thinners", "INSTITUTIONAL", "", "", "1 day (low-moderate bleeding risk surgery) or 2 days (high bleeding risk), if kidneys OK",
  note="Whether laparoscopic cholecystectomy counts as low or high bleeding risk is institutional (PAUSE trial).")
m("dabigatran", "Dabigatran 1–2 days with good kidneys; 2–4 days if kidneys weaker Cleared by kidneys", "dabigatran", "blood_thinners", "NONE", "", "", "1–2 days with good kidneys; 2–4 days if kidneys weaker")
m("aspirin", "Aspirin Usually continue if for heart/stroke protection; may stop 7 days if only 'just in case' Surgeon decides", "aspirin", "blood_thinners", "NONE", "", "",
  "Usually continue if for heart/stroke protection; may stop 7 days if only 'just in case'")
m("p2y12", "Clopidogrel / ticagrelor / prasugrel 5 / 3–5 / 7 days Stopping after a recent heart stent is a cardiology decision (stent-clot risk): do not stop without the heart team; planned surgery is usually deferred instead",
  "clopidogrel_ticagrelor_prasugrel", "blood_thinners", "NONE", "", "do not stop without the heart team", "5 / 3–5 / 7 days")
m("reversal", f"In emergencies (S2), reversing blood thinners is a senior/haematology decision {PS}", "blood_thinner_reversal", "blood_thinners", "PATIENT_SPECIFIC", "", "", "",
  note="In emergencies (S2) reversal is a senior/haematology decision.")

# ---- diabetes medicines (page 66)
m("metformin", f"Metformin Normal Normal if only one meal missed (some hospitals stop if kidneys weak or CT dye) {CTX} Low risk of low sugar",
  "metformin", "diabetes", "INSTITUTIONAL", "", "", "Normal if only one meal missed",
  note="Day before: normal. Day of surgery: normal if only one meal missed; some hospitals stop if kidneys are weak or CT dye is used.")
m("sulfonylureas", "Sulfonylureas (gliclazide, glimepiride) Normal Omit Cause low sugar when fasting", "sulfonylureas", "diabetes", "NONE", "", "Cause low sugar when fasting",
  "Omit", note="Day before: normal. Day of surgery: omit.")
m("sglt2", f"SGLT2 inhibitors (dapagliflozin, empagliflozin, canagliflozin) Sources disagree: UK 2025 consensus (Association of Anaesthetists, CPOC, JBDS, RCoA) = omit the day before AND the day of surgery; US FDA labelling = stop 3 days before (ertugliflozin 4). ProcSense default: UK consensus {CTX} Omit; check ketones if unwell Can cause DKA even with normal sugar",
  "sglt2_inhibitors", "diabetes", "INSTITUTIONAL", "", "Can cause DKA even with normal sugar", "Omit; check ketones if unwell",
  note="Sources disagree: UK 2025 consensus omits the day before and the day of surgery; US FDA labelling says stop 3 days before (ertugliflozin 4). ProcSense default: UK consensus.")
m("glp1", "GLP-1 agonists (semaglutide/Ozempic, liraglutide, tirzepatide) See fasting Part 9 See Part 9 Slow stomach emptying → aspiration risk", "glp1_agonists", "diabetes", "NONE", "",
  "Slow stomach emptying → aspiration risk", "See fasting Part 9", note="The plan is in the fasting rules (Part 9).")
m("dpp4", f"DPP-4 inhibitors (sitagliptin) Normal Omit or take per local {CTX} Low risk", "dpp4_inhibitors", "diabetes", "INSTITUTIONAL", "", "", "Omit or take per local",
  note="Day before: normal. Day of surgery: omit or take per local protocol.")
m("insulin_long", f"Long-acting insulin Reduce by about 20% Reduce by about 20% — {PS} Avoid low sugar while fasting", "long_acting_insulin", "diabetes", "PATIENT_SPECIFIC", "", "Avoid low sugar while fasting", "",
  note="The book says 'reduce by about 20%' for the day before and the day of surgery. That number is not kept as content because the dose is patient-specific (clinician controlled): the trainee sees 'adjusted per protocol', not a figure.")
m("insulin_other", f"Other insulin Normal Adjust/omit per protocol; insulin drip if more than one meal missed or poor control {PS}", "other_insulin", "diabetes", "PATIENT_SPECIFIC", "", "", "",
  note="Day before: normal. Day of surgery: adjust or omit per protocol; an insulin drip if more than one meal is missed or control is poor (clinician controlled).")

# ---- other regular medicines (page 67)
m("ace_arb", f"ACE inhibitors / ARBs (BP tablets: ramipril, lisinopril, losartan…) Often omit for 24 h before surgery {CTX} Can cause low BP when going to sleep",
  "ace_inhibitors_arbs", "regular_medicines", "INSTITUTIONAL", "", "Can cause low BP when going to sleep", "Often omit for 24 h before surgery")
m("beta_blockers", "Beta-blockers (bisoprolol, atenolol…) Usually continued; avoid stopping suddenly; not usually started on the day Stopping suddenly can cause fast heart/heart attack",
  "beta_blockers", "regular_medicines", "NONE", "", "Stopping suddenly can cause fast heart/heart attack", "Usually continued; avoid stopping suddenly; not usually started on the day")
m("diuretics", f"Water tablets (diuretics) Usually omit on the morning {CTX} Dehydration, low potassium", "diuretics", "regular_medicines", "INSTITUTIONAL", "", "Dehydration, low potassium", "Usually omit on the morning")
m("statins_ccb", "Statins, calcium-channel blockers Continue Safe", "statins_calcium_channel_blockers", "regular_medicines", "NONE", "", "", "Continue")
m("steroids", f"Steroid tablets (e.g. prednisolone 5 mg+ for over 4 weeks) Continue + extra steroid cover if the patient has adrenal insufficiency or long-term glucocorticoid use (prednisolone 5 mg/day or more for about a month). {EDU} hydrocortisone 100 mg IV at induction (Association of Anaesthetists / RCP / Society for Endocrinology, Anaesthesia 2020); the anaesthetist decides who needs it and what follows The body can't make its own stress steroids → collapse risk ('adrenal crisis')",
  "steroid_tablets", "regular_medicines", "EDUCATIONAL_EXAMPLE", "", "The body can't make its own stress steroids → collapse risk ('adrenal crisis')",
  "Continue + extra steroid cover if the patient has adrenal insufficiency or long-term glucocorticoid use",
  "hydrocortisone 100 mg IV at induction (Association of Anaesthetists / RCP / Society for Endocrinology, Anaesthesia 2020); the anaesthetist decides who needs it and what follows",
  "Association of Anaesthetists / RCP / Society for Endocrinology, Anaesthesia 2020")
m("cocp_hrt", f"Combined contraceptive pill / HRT Clot risk; for major surgery may stop 4 weeks before (NICE NG89); lap chole usually continue with clot prevention {CTX} Oestrogen raises clot risk",
  "combined_contraceptive_pill_hrt", "regular_medicines", "INSTITUTIONAL", "", "Oestrogen raises clot risk", "for major surgery may stop 4 weeks before (NICE NG89); lap chole usually continue with clot prevention")
m("herbal", f"Herbal remedies (ginkgo, garlic, ginseng, fish oil) Often stop 1–2 weeks before {CTX} Bleeding risk", "herbal_remedies", "regular_medicines", "INSTITUTIONAL", "", "Bleeding risk", "Often stop 1–2 weeks before")
m("epilepsy_parkinsons", "Epilepsy and Parkinson's medicines Usually continued on time; specialist advice if any dose will be missed Fits / severe stiffness",
  "epilepsy_parkinsons_medicines", "regular_medicines", "NONE", "", "Fits / severe stiffness", "Usually continued on time; specialist advice if any dose will be missed")
m("vte_prophylaxis", f"usually starting 6–12 hours after surgery and continuing at least 7 days for tummy surgery if clot risk outweighs bleeding risk CLINICAL REFERENCE — SOURCE BACKED (NICE NG89). {EDU} enoxaparin (SmPC): 20 mg once daily SC for moderate VTE risk, 40 mg once daily SC for high risk; 20 mg daily if creatinine clearance is 15–30 mL/min; the SmPC advises about 7–10 days minimum in moderate-risk patients; the risk category, start time (including timing relative to spinal/epidural anaesthesia) and duration {PS}",
  "vte_prophylaxis", "regular_medicines", "EDUCATIONAL_EXAMPLE", "", "", "usually starting 6–12 hours after surgery and continuing at least 7 days for tummy surgery if clot risk outweighs bleeding risk",
  "enoxaparin (SmPC): 20 mg once daily SC for moderate VTE risk, 40 mg once daily SC for high risk; 20 mg daily if creatinine clearance is 15–30 mL/min; the SmPC advises about 7–10 days minimum in moderate-risk patients",
  "SmPC", note="Mechanical: stockings or leg sleeves (not if poor leg circulation, leg ulcers, severe nerve damage). The risk category, start time and duration are patient-specific.")

# ---- emergency medicines (page 67-68)
m("adrenaline", f"Anaphylaxis (severe allergy) Adrenaline (epinephrine) injected into the thigh muscle {EDU} adults and children over 12: 500 micrograms (0.5 mL of 1 mg/mL) IM into the anterolateral thigh; repeat after 5 minutes if airway, breathing or circulation problems persist; IV adrenaline only by experienced specialists (Resuscitation Council UK 2021; this is a standard algorithm dose — the responsible clinician confirms)",
  "adrenaline_anaphylaxis", "emergency", "EDUCATIONAL_EXAMPLE", "Anaphylaxis (severe allergy)", "", "",
  "adults and children over 12: 500 micrograms (0.5 mL of 1 mg/mL) IM into the anterolateral thigh; repeat after 5 minutes if airway, breathing or circulation problems persist; IV adrenaline only by experienced specialists (Resuscitation Council UK 2021; this is a standard algorithm dose — the responsible clinician confirms)",
  "Resuscitation Council UK 2021")
m("fast_sugar", f"Low blood sugar, can swallow Fast sugar (e.g. glucose drink/tablets 15–20 g) {EDU} JBDS hospital hypoglycaemia guideline: 15–20 g quick-acting carbohydrate if the patient is conscious and able to swallow (and the protocol allows oral treatment — fasting status matters), recheck glucose after 10–15 minutes and repeat as the protocol directs",
  "fast_sugar_hypoglycaemia", "emergency", "EDUCATIONAL_EXAMPLE", "Low blood sugar, can swallow", "", "",
  "JBDS hospital hypoglycaemia guideline: 15–20 g quick-acting carbohydrate if the patient is conscious and able to swallow (and the protocol allows oral treatment — fasting status matters), recheck glucose after 10–15 minutes and repeat as the protocol directs",
  "JBDS hospital hypoglycaemia guideline")
m("iv_glucose", f"Low blood sugar, nil by mouth/drowsy/unable to swallow Glucose through the drip {EDU} JBDS: IV 10% glucose 150–200 mL (or 20% glucose 75–100 mL) over about 15 minutes, recheck after 10 minutes; prescriber and hospital protocol confirm",
  "iv_glucose_hypoglycaemia", "emergency", "EDUCATIONAL_EXAMPLE", "Low blood sugar, nil by mouth/drowsy/unable to swallow", "", "",
  "JBDS: IV 10% glucose 150–200 mL (or 20% glucose 75–100 mL) over about 15 minutes, recheck after 10 minutes; prescriber and hospital protocol confirm", "JBDS")
m("hyperkalaemia", f"Very high potassium IV calcium (protects the heart without lowering potassium); insulin with glucose (moves potassium into cells) {EDU} UK Kidney Association algorithm: with ECG changes, 30 mL of 10% calcium gluconate IV over 10 min (or 10 mL of 10% calcium chloride); insulin–glucose 10 units soluble insulin in 25 g glucose IV over 15–30 min, followed by 10% glucose 50 mL/h for 5 h if pre-treatment glucose is below 7 mmol/L; repeat ECG and monitor potassium and glucose. Emergency prescription by the responsible clinician",
  "hyperkalaemia_treatment", "emergency", "EDUCATIONAL_EXAMPLE", "Very high potassium", "", "",
  "UK Kidney Association algorithm: with ECG changes, 30 mL of 10% calcium gluconate IV over 10 min (or 10 mL of 10% calcium chloride); insulin–glucose 10 units soluble insulin in 25 g glucose IV over 15–30 min, followed by 10% glucose 50 mL/h for 5 h if pre-treatment glucose is below 7 mmol/L; repeat ECG and monitor potassium and glucose. Emergency prescription by the responsible clinician",
  "UK Kidney Association algorithm", note="UKKA guideline has a July 2026 version; the book cites 2023 (ISS-0013).")
m("naloxone", f"Too much opioid (slow breathing) Naloxone (antidote) {PS}", "naloxone_opioid_overdose", "emergency", "PATIENT_SPECIFIC", "Too much opioid (slow breathing)", "", "")
m("sepsis_emergency", f"Sepsis IV antibiotics + fluids Antibiotic per institutional policy {CTX} ; fluid {EDU} 500 mL crystalloid bolus over less than 15 minutes then reassess (NICE CG174; scope exclusions above)",
  "sepsis_emergency", "emergency", "EDUCATIONAL_EXAMPLE", "Sepsis", "", "", "500 mL crystalloid bolus over less than 15 minutes then reassess (NICE CG174; scope exclusions above)", "NICE CG174",
  note="Antibiotic per institutional policy. NICE NG253 resources state 250 mL (ISS-0015); the book's value is kept as a display-only example.")

# ------------------------------------------------------------------ the three drug checks, the dose policies and the role convention (Parts 8.1 and 14B.2)

checks = Table("drug_checks", 1400, ["kind", "name", "v_text"], "Drug rule", "Part 8.1 role convention and Part 14B.2 golden rules", evidence="A", size=20)
checks.add("check_allergy", "Every drug decision must pass three checks: allergy? kidneys/liver? pregnancy? (plus 'blood thinner?' for anything that might cause bleeding).", "auto",
           kind="check", name="allergy", v_text="allergy?")
checks.add("check_kidney_liver", "Every drug decision must pass three checks: allergy? kidneys/liver? pregnancy? (plus 'blood thinner?' for anything that might cause bleeding).", "auto",
           kind="check", name="kidneys_liver", v_text="kidneys/liver?")
checks.add("check_pregnancy", "Every drug decision must pass three checks: allergy? kidneys/liver? pregnancy? (plus 'blood thinner?' for anything that might cause bleeding).", "auto",
           kind="check", name="pregnancy", v_text="pregnancy?")
checks.add("check_blood_thinner", "Every drug decision must pass three checks: allergy? kidneys/liver? pregnancy? (plus 'blood thinner?' for anything that might cause bleeding).", "auto",
           kind="check", name="blood_thinner", v_text="blood thinner?", note="Extra check for anything that might cause bleeding.")
checks.add("role_convention",
           "The trainee identifies the clinical problem, recognises that a medicine may be needed, communicates it, and initiates the appropriate workflow (e.g. asks the senior/anaesthetist, flags it on the drug chart for confirmation). A qualified clinician confirms the final treatment decision and dose; a nurse or clinician administers.",
           "auto", kind="role", name="role_convention", v_text="initiates the appropriate workflow")
checks.add("never_prescribe",
           "The trainee never independently prescribes, selects a dose for, or administers a patient-specific medicine.", "auto", kind="role", name="never_prescribe",
           v_text="never independently prescribes")
checks.add("policy_patient_specific", "PATIENT-SPECIFIC DOSE — CLINICIAN CONTROLLED — the simulator shows the decision (e.g. 'a strong painkiller titrated by the clinician') but not a number.",
           "auto", kind="dose_policy", name="PATIENT_SPECIFIC", v_text="but not a number")
checks.add("policy_educational", "EDUCATIONAL EXAMPLE DOSE (SOURCE-BACKED) — NOT A TRAINEE PRESCRIPTION — shown only where an authoritative source gives a standard adult dose (named beside it)",
           "auto", kind="dose_policy", name="EDUCATIONAL_EXAMPLE", v_text="named beside it")
checks.add("policy_institutional", "CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION", 65, kind="dose_policy", name="INSTITUTIONAL", v_text="INSTITUTIONAL PROTOCOL",
           evidence="C")

TABLES = [med, checks]
OFFICIAL_FILES = {}
