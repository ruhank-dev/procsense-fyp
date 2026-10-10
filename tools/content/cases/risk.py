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
