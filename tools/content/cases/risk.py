"""The ASA grade each P0 case expects and the anaesthetic plan (p0-simulation-workflow-s08).

The grade is never worked out by the engine: the case file states it (walkthrough Part 7.4: "Expected grades for ProcSense patients", a clinical interpretation derived from the ASA 2020 examples).
A range (min to max) is used where the book gives one or where more than one grade is defensible; a proposal inside the range is credited. 'book' = the walkthrough states it for this patient;
'authored' = chosen here by applying the book's rule to the case, to be confirmed by the anaesthetist/clinician. Plans: S1 patients standard general anaesthetic, S2 emergencies rapid sequence induction.
"""

# The rules from the book, quoted so each 'why' can name one:
R1 = "healthy = ASA 1"
R2 = "mild disease with no real limits (smoker, pregnant, controlled blood pressure or diabetes) = ASA 2"
R3 = "poorly controlled diabetes or blood pressure, or atrial fibrillation on a blood thinner = ASA 3"
RS = "sepsis = ASA 3 to 4 and in an emergency the book gives septic S2 patients 2E to 4E"


def asa(lo, hi, why, origin="authored", emergency=False):
    return {"asa": {"min": lo, "max": hi, "emergency": emergency, "why": why, "origin": origin}}


def risk(lo, hi, why, plan, origin="authored", emergency=False):
    return {**asa(lo, hi, why, origin, emergency), "plan": plan}


RISK = {
    "VAR-S1-A": risk(1, 1, "She is fit, a non-smoker, drinks little and has no illness: healthy is ASA 1 (walkthrough Scene 5).", "standard_ga", "book"),
    "VAR-S1-B": risk(2, 3, "Treated high blood pressure: controlled is ASA 2 and poorly controlled is ASA 3; readings of 168/102 sit between the two.", "standard_ga"),
    "VAR-S1-C": risk(2, 3, "Type 2 diabetes: well controlled is ASA 2 and poorly controlled is ASA 3; an HbA1c of 58 sits between the two, and the poorly controlled variant leans to 3.", "standard_ga"),
    "VAR-S1-D": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list of expected grades.", "standard_ga", "book"),
    "VAR-S1-E": risk(2, 3, "A planned patient now unwell with an inflamed gallbladder, no organ failure: mild systemic illness (ASA 2) to severe (ASA 3).", "standard_ga"),
    "VAR-S1-G": risk(1, 1, "She is healthy with no illness: ASA 1; the fasting breach changes the timing, not the grade.", "standard_ga"),
    "VAR-S1-H": risk(2, 2, "Pregnancy is ASA 2 in the book's list of examples.", "standard_ga", "book"),
    "VAR-S1-J": risk(1, 1, "He is well: ASA 1; the haemolysed potassium was a sample problem, not a disease.", "standard_ga"),
    "VAR-S1-PB": risk(2, 3, "A stone that may have moved into the bile duct, with jaundice: a mild to severe systemic problem (ASA 2 to 3).", "standard_ga"),
    "STK-S1-D": risk(3, 4, "Cholangitis with sepsis (NEWS2 7) needs urgent drainage: sepsis is ASA 3 to 4, and the operation is now an emergency.", "standard_ga", emergency=True),
    "STK-S2-B": risk(2, 4, "Septic perforation with a rigid abdomen: the book gives septic S2 patients 2E to 4E.", "rsi", "book", True),
    "VAR-S2-B": risk(2, 3, "Pregnant with suspected ectopic pregnancy and dizzy: pregnancy is ASA 2, and the risk of bleeding makes ASA 3 defensible; the E is for emergency.", "rsi", emergency=True),
    "VAR-S2-D": risk(1, 1, "A healthy young adult in an emergency: healthy S2 is 1E in the book.", "rsi", "book", True),
    "VAR-S2-F": risk(1, 1, "A healthy young adult in an emergency: healthy S2 is 1E; the full stomach is handled by the rapid sequence plan.", "rsi", "book", True),
    "VAR-S2-G": risk(2, 3, "Vomiting for two days with kidney injury stage 1 and a fast pulse: mild to severe systemic disturbance (2E to 3E).", "rsi", emergency=True),
    "VAR-S2-PC": risk(1, 1, "A healthy 19-year-old with classic appendicitis: healthy S2 is 1E (walkthrough, 'S2 typical ASA 1E').", "rsi", "book", True),
    "VAR-S2-PD": risk(2, 4, "A patient who is deteriorating into sepsis: the book gives septic S2 patients 2E to 4E, so the right grade rises as the numbers worsen.", "rsi", "book", True),
}


# ---- the 36 P1 and P2 cases (DT-065): graded by the same rule, using the ASA's own examples (V-69): current smoker, pregnancy, BMI 30 to 40 and well-controlled diabetes or blood pressure are ASA 2;
# poorly controlled diabetes or blood pressure, morbid obesity (BMI 40 or more), a pacemaker, a stroke or stent more than 3 months ago and moderately reduced heart function are ASA 3;
# sepsis, recent heart or brain events, severe heart failure and kidney failure without dialysis are ASA 4. 'book' = the walkthrough's own expected-grade rule names it; 'authored' = applied here.
RISK.update({
    "SC-1": risk(3, 4, "Sepsis from a burst appendix with low blood pressure and a raised lactate: sepsis is ASA 4 in the ASA's examples; early sepsis can be called 3.", "rsi", emergency=True),
    "SC-10": risk(1, 1, "A healthy 17-year-old in an emergency: healthy S2 is 1E in the book.", "rsi", "book", True),
    "SC-11": risk(1, 4, "Healthy when he first refuses, so 1E; four hours later he is septic and confused, which is ASA 3 to 4: the right grade rises as he worsens.", "rsi", emergency=True),
    "SC-12": risk(2, 3, "Treated high blood pressure is ASA 2 when well controlled; the low sodium is a further problem that can make it ASA 3.", "standard_ga"),
    "SC-13": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list of expected grades.", "standard_ga", "book"),
    "SC-14": risk(2, 3, "Appendix not seen and the pulse and temperature rising: early sepsis, ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "SC-15": risk(2, 3, "Anaphylaxis just treated, now stable, with an infection still to treat: ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "SC-16": risk(3, 3, "BMI 42 is morbid obesity (BMI 40 or more), which is ASA 3 in the ASA's examples and the book's list; type 2 diabetes and likely sleep apnoea add to it.", "rsi", "book"),
    "SC-17": risk(1, 2, "Thalassaemia trait is mild and does not limit her (ASA 1 to 2); dehydration from vomiting is a mild illness in an emergency.", "rsi", emergency=True),
    "SC-18": risk(1, 1, "He is healthy with gallstones only: ASA 1; the missing consent and the faulty equipment are not a health problem.", "standard_ga"),
    "SC-19": risk(3, 4, "Type 2 diabetes with a low glucose, confusion, fever and ketones of 3.5 (acidosis risk on an SGLT2 inhibitor): a threat to life, ASA 3 to 4 in an emergency.", "rsi", emergency=True),
    "SC-2": risk(3, 4, "Suspected ectopic pregnancy with a fast pulse, low blood pressure and a low haemoglobin: pregnancy is ASA 2, and shock from bleeding makes it ASA 3 to 4 in an emergency.", "rsi", emergency=True),
    "SC-20": risk(3, 3, "A stroke some years ago (more than 3 months) is ASA 3 in the ASA's examples; the operation is an emergency.", "rsi", emergency=True),
    "SC-3": risk(2, 3, "Pregnancy is ASA 2 in the book's list; the fever and the raised white cells may be early sepsis, which makes it ASA 3 in an emergency.", "rsi", emergency=True),
    "SC-4": risk(3, 3, "Poorly controlled diabetes (HbA1c 78) and high blood pressure (176/104) are ASA 3 in the book's list.", "standard_ga", "book"),
    "SC-5": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; the fast irregular pulse today adds to it.", "standard_ga", "book"),
    "SC-6": risk(2, 3, "Type 1 diabetes on insulin: well controlled is ASA 2 and poorly controlled is ASA 3; a glucose of 4.6 today sits at the well-controlled end.", "standard_ga"),
    "SC-7": risk(2, 3, "A healthy man made drowsy and slow-breathing by morphine, and vomiting: a temporary threat to the airway, ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "SC-8": risk(4, 4, "Kidney disease with a potassium of 6.6, lactate 4.2 and blood pressure 88/50: shock and sepsis are ASA 4 in the ASA's examples.", "rsi", emergency=True),
    "SC-9": risk(3, 4, "A stent more than 3 months ago is ASA 3; cholangitis with sepsis (fever, fast pulse, bilirubin 85) is ASA 4; urgent drainage makes it an emergency.", "standard_ga", emergency=True),
    "STK-S1-A": risk(3, 3, "Type 2 diabetes with a low glucose today and blood pressure 174/102: poorly controlled diabetes or blood pressure is ASA 3 in the book's list.", "standard_ga", "book"),
    "STK-S1-B": risk(3, 3, "Atrial fibrillation on a blood thinner is ASA 3 in the book's list; early kidney disease and anaemia add to it.", "standard_ga", "book"),
    "STK-S1-C": risk(3, 4, "Heart failure with a weak pumping function and kidney disease: moderately reduced heart function is ASA 3 and severely reduced is ASA 4; the slow pulse and low blood pressure today lean to 4.", "standard_ga"),
    "STK-S2-A": risk(2, 3, "Two days of vomiting with kidney injury and early sepsis signs: ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "STK-S2-C": risk(2, 3, "A healthy man made drowsy by morphine with slow shallow breathing and a low oxygen level: a temporary threat to the airway, ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "STK-S2-D": risk(2, 3, "Pregnancy is ASA 2 in the book's list; appendicitis with a fast pulse and a raised white count can make it ASA 3 in an emergency.", "rsi", emergency=True),
    "VAR-S1-I": risk(1, 1, "She is well, so ASA 1; a penicillin allergy changes the antibiotic, not the grade.", "standard_ga"),
    "VAR-S1-K": risk(2, 2, "Anaemia (haemoglobin 98) from iron deficiency is a mild systemic disease without real limits: ASA 2.", "standard_ga"),
    "VAR-S1-L": risk(3, 3, "A fast irregular pulse today (new atrial fibrillation) with high blood pressure: not controlled, ASA 3.", "standard_ga"),
    "VAR-S1-M": risk(3, 3, "BMI 41 is morbid obesity (BMI 40 or more), which is ASA 3 in the ASA's examples and the book's list; likely sleep apnoea and high blood pressure add to it.", "standard_ga", "book"),
    "VAR-S2-E": risk(2, 3, "An older man with weight loss, anaemia and a raised CRP: a mild to severe systemic problem, ASA 2 to 3 in an emergency.", "rsi", emergency=True),
    "VAR-S2-H": risk(2, 2, "Pregnancy is ASA 2 in the book's list; the E is for emergency.", "rsi", "book", True),
    "VAR-S2-I": risk(1, 1, "A healthy 16-year-old in an emergency: healthy S2 is 1E in the book.", "rsi", "book", True),
    "VAR-S2-J": risk(1, 1, "A healthy young woman in an emergency; the contraceptive pill is not a disease: healthy S2 is 1E in the book.", "rsi", "book", True),
    "VAR-S2-K": risk(1, 1, "A healthy young man in an emergency: healthy S2 is 1E in the book; the penicillin label changes the antibiotic, not the grade.", "rsi", "book", True),
    "VAR-S2-L": risk(1, 1, "A healthy man in an emergency: healthy S2 is 1E in the book.", "rsi", "book", True),
})
