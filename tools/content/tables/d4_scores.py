"""D4 (p0-medical-research-data s07): score items, points and bands (Alvarado, AIR, AAS), the worked example, TG18, ASGE, ASA, airway/STOP-BANG, VTE.

Source: walkthrough Part 7 (7.1 to 7.7), with the ASGE tiers from the S1 liver-test decision diagram (Part 5.16).
The plan asks for scores.json; the data is kept as CSV rows instead so every row has its quote, page and register id and gets the same checks.
Scores are decision aids: the book's caveat ('scores support judgement; they don't replace it') belongs with each. AIR is the draft default and AAS optional
(owner approved the walkthrough choice on 09 Oct 2026; institutional decision 13).
"""
from framework import Table

# ------------------------------------------------------------------ appendicitis score items (7.1)

items = Table("score_items", 1000, ["score", "item", "v_condition", "v_low", "v_high", "bound", "v_points"], "Score item", "Part 7.1 table 'What is scored'", evidence="A", size=100)
Q = {
    "moved": "Pain moved from belly button to lower right 1 — 2",
    "rlq": "Pain in lower right — 1 2",
    "appetite": "Loss of appetite 1 — —",
    "nausea": "Nausea or vomiting 1 1 (vomiting only) —",
    "tender": "Lower-right tenderness when pressed 2 — Women aged 16–49: 1. Everyone else: 3",
    "rebound": "Rebound (pain when pressure is released) / guarding (tummy muscles tense) Rebound: 1 Light 1, medium 2, strong 3 Guarding: mild 2, moderate/severe 4",
    "temp": "Temperature 37.3 °C or more: 1 38.5 °C or more: 1 —",
    "wbc": "White cell count (×109/L) Above 10.0: 2 10.0–14.9: 1; 15.0 or more: 2 7.2–10.8: 1; 10.9–13.9: 2; 14.0 or more: 3",
    "neut": "Neutrophils (% of white cells) More than 75%: 1 70–84%: 1; 85% or more: 2 62–74.9%: 2; 75–82.9%: 3; 83% or more: 4",
    "crp": "CRP (mg/L) — 10–49: 1; 50 or more: 2 If pain under 24 h: 4–10.9 = 2; 11–24.9 = 3; 25–82.9 = 5; 83 or more = 1. If pain 24 h or more: 12–52.9 = 2; 53–151.9 = 2; 152 or more = 1",
}


def it(score, item, q, points, low="", high="", bound="", cond="", tag=""):
    items.add(f"{score}.{item}.{tag or cond or (str(low) + '-' + str(high))}".replace(" ", "_"), Q[q], "auto", score=score, item=item, v_condition=cond, v_low=low, v_high=high,
              bound=bound, v_points=points)


# Alvarado (0-10)
it("alvarado", "pain_moved", "moved", 1, tag="yes", cond="")
it("alvarado", "loss_of_appetite", "appetite", 1, tag="yes")
it("alvarado", "nausea_or_vomiting", "nausea", 1, tag="yes")
it("alvarado", "rlq_tenderness", "tender", 2, tag="yes")
it("alvarado", "rebound", "rebound", 1, tag="yes", cond="Rebound")
it("alvarado", "temperature_c", "temp", 1, 37.3, "", tag="37.3+")
it("alvarado", "wbc", "wbc", 2, 10.0, "", bound="above", tag="above10")
it("alvarado", "neutrophils_pct", "neut", 1, 75, "", bound="above", tag="above75")
# AIR (0-12)
it("air", "pain_rlq", "rlq", 1, tag="yes")
it("air", "vomiting", "nausea", 1, tag="yes", cond="vomiting only")
it("air", "rebound_light", "rebound", 1, tag="light", cond="Light")
it("air", "rebound_medium", "rebound", 2, tag="medium", cond="medium")
it("air", "rebound_strong", "rebound", 3, tag="strong", cond="strong")
it("air", "temperature_c", "temp", 1, 38.5, "", tag="38.5+")
it("air", "wbc", "wbc", 1, 10.0, 14.9, tag="10.0-14.9")
it("air", "wbc", "wbc", 2, 15.0, "", tag="15.0+")
it("air", "neutrophils_pct", "neut", 1, 70, 84, tag="70-84")
it("air", "neutrophils_pct", "neut", 2, 85, "", tag="85+")
it("air", "crp", "crp", 1, 10, 49, tag="10-49")
it("air", "crp", "crp", 2, 50, "", tag="50+")
# AAS (0-23)
it("aas", "pain_moved", "moved", 2, tag="yes")
it("aas", "pain_rlq", "rlq", 2, tag="yes")
it("aas", "rlq_tenderness", "tender", 1, tag="women16-49", cond="Women aged 16–49")
it("aas", "rlq_tenderness", "tender", 3, tag="everyone_else", cond="Everyone else")
it("aas", "guarding_mild", "rebound", 2, tag="mild", cond="mild")
it("aas", "guarding_moderate_or_severe", "rebound", 4, tag="moderate_severe", cond="moderate/severe")
it("aas", "wbc", "wbc", 1, 7.2, 10.8, tag="7.2-10.8")
it("aas", "wbc", "wbc", 2, 10.9, 13.9, tag="10.9-13.9")
it("aas", "wbc", "wbc", 3, 14.0, "", tag="14.0+")
it("aas", "neutrophils_pct", "neut", 2, 62, 74.9, tag="62-74.9")
it("aas", "neutrophils_pct", "neut", 3, 75, 82.9, tag="75-82.9")
it("aas", "neutrophils_pct", "neut", 4, 83, "", tag="83+")
for lo, hi, p in ((4, 10.9, 2), (11, 24.9, 3), (25, 82.9, 5), (83, "", 1)):
    it("aas", "crp_pain_under_24h", "crp", p, lo, hi, cond="under 24 h", tag=f"u24_{lo}-{hi}")
for lo, hi, p in ((12, 52.9, 2), (53, 151.9, 2), (152, "", 1)):
    it("aas", "crp_pain_24h_or_more", "crp", p, lo, hi, cond="24 h or more", tag=f"o24_{lo}-{hi}")

# ------------------------------------------------------------------ bands and maxima

bands = Table("score_bands", 1100, ["score", "band", "v_low", "v_high"], "Score band", "Part 7.1 table 'What is scored' (Maximum and Meaning rows)", evidence="A", size=30)
MAXQ = "Maximum 10 12 23"
MEAN = "Meaning 0–4 unlikely; 5–6 unsure; 7–8 probable; 9–10 very probable 0–4 low; 5–8 unsure; 9–12 high 0–10 low; 11–15 intermediate; 16+ high"
for score, mx in (("alvarado", 10), ("air", 12), ("aas", 23)):
    bands.add(f"{score}.max", MAXQ, "auto", score=score, band="max", v_low="", v_high=mx)
for score, band, lo, hi in (("alvarado", "unlikely", 0, 4), ("alvarado", "unsure", 5, 6), ("alvarado", "probable", 7, 8), ("alvarado", "very_probable", 9, 10),
                            ("air", "low", 0, 4), ("air", "unsure", 5, 8), ("air", "high", 9, 12),
                            ("aas", "low", 0, 10), ("aas", "intermediate", 11, 15), ("aas", "high", 16, "")):
    bands.add(f"{score}.{band}", MEAN, "auto", score=score, band=band, v_low=lo, v_high=hi)

# ------------------------------------------------------------------ the worked example (7.1, patient S2-A)

ex = Table("score_examples", 1150, ["score", "patient", "item", "v_points", "v_total", "v_band"], "Score worked example", "Part 7.1 'Worked example — calculate both for the same patient'",
           evidence="A", size=40)
EXQ = {
    "air_total": "TOTAL 7 → 'unsure' (indeterminate) 19 → 'high'",
    "vomiting": "Vomiting 1 —", "rlq": "Lower-right pain 1 2", "moved": "Pain moved — 2", "tender": "Tenderness (male) — 3",
    "rebound": "Rebound medium / guarding mild 2 2", "temp": "Temperature 37.9 0 (needs 38.5) —", "wbc": "WBC 13.6 1 2",
    "neut": "Neutrophils 82% 1 3", "crp": "CRP 38 (under 24 h) 1 5",
}
PATIENT = "S2-A: man, 19; pain moved to lower right 20 hours ago; vomited once; lower-right tenderness; mild guarding; medium rebound; temperature 37.9; WBC 13.6; neutrophils 82%; CRP 38"
ex.add("input", PATIENT, "auto", score="both", patient="S2-A", item="inputs", v_points="", v_total="", v_band="",
       note="Inputs of the worked example: man, 19; pain moved 20 hours ago; vomited once; tenderness; mild guarding; medium rebound; 37.9; WBC 13.6; neutrophils 82%; CRP 38.")
for key, air, aas in (("vomiting", 1, ""), ("rlq", 1, 2), ("moved", "", 2), ("tender", "", 3), ("rebound", 2, 2), ("temp", 0, ""), ("wbc", 1, 2), ("neut", 1, 3), ("crp", 1, 5)):
    if air != "":
        ex.add(f"air.{key}", EXQ[key], "auto", score="air", patient="S2-A", item=key, v_points=air, v_total="", v_band="")
    if aas != "":
        ex.add(f"aas.{key}", EXQ[key], "auto", score="aas", patient="S2-A", item=key, v_points=aas, v_total="", v_band="")
ex.add("air.total", EXQ["air_total"], "auto", score="air", patient="S2-A", item="total", v_points="", v_total=7, v_band="unsure")
ex.add("aas.total", EXQ["air_total"], "auto", score="aas", patient="S2-A", item="total", v_points="", v_total=19, v_band="high")

# ------------------------------------------------------------------ TG18, ASGE, ASA, airway, VTE, SORT

facts = Table("score_facts", 1200, ["group", "item", "v_text", "v_number"], "Score fact", "Part 7.2 to 7.7 and the S1 liver-test decision diagram", evidence="A", size=100)


def fact(group, key, quote, text, number="", evidence="", note=""):
    facts.add(f"{group}.{key}", quote, "auto", group=group, item=key, v_text=text, v_number=number, evidence=evidence, note=note)


G1 = "A — local signs Murphy's sign (patient catches their breath in pain when the examiner presses under the right ribs while they breathe in); lump/pain/tenderness in the right upper tummy"
fact("tg18_cholecystitis", "group_a", G1, "Murphy's sign")
fact("tg18_cholecystitis", "group_b", "B — whole-body signs Fever; raised CRP; raised WBC", "Fever; raised CRP; raised WBC")
fact("tg18_cholecystitis", "group_c", "C — scan Ultrasound/CT shows inflamed gallbladder", "Ultrasound/CT shows inflamed gallbladder")
fact("tg18_cholecystitis", "diagnosis", "Diagnosis Suspected = one from A + one from B. Definite = A + B + C.", "Suspected = one from A + one from B")
fact("tg18_cholecystitis", "grade_iii", "Grade III (severe) Any organ failing: BP needs medicine support; reduced consciousness; poor oxygen; low urine or creatinine above 2.0 mg/dL (~177 µmol/L); INR above 1.5; platelets below 100",
     "Any organ failing", "2.0; 177; 1.5; 100")
fact("tg18_cholecystitis", "grade_ii", "Grade II (moderate) Any of: WBC above 18; tender lump in right upper tummy; symptoms more than 72 hours; severe local inflammation on scan",
     "WBC above 18", "18; 72")
fact("tg18_cholecystitis", "grade_i", "Grade I (mild) Neither of the above Early keyhole operation", "Neither of the above")
fact("tg18_cholangitis", "group_a", "A — inflammation Fever above 38 °C and/or shaking chills; WBC below 4 or above 10; CRP 10 mg/L or more", "Fever above 38", "38; 4; 10; 10")
fact("tg18_cholangitis", "group_b", "B — bile blocked Jaundice (bilirubin 2 mg/dL ≈ 34 µmol/L or more); liver tests above 1.5 × upper limit", "Jaundice", "2; 34; 1.5")
fact("tg18_cholangitis", "group_c", "C — scan Widened bile duct; cause seen (stone, narrowing)", "Widened bile duct")
fact("tg18_cholangitis", "diagnosis", "Diagnosis Definite = A + B + C; suspected = A + (B or C) → EMERGENCY: sepsis care + urgent drainage (ERCP)", "EMERGENCY",
     note="Cholangitis is an emergency: sepsis care and urgent drainage.")
fact("asge", "cholangitis_first", "Fever/shivers AND yellow skin/eyes AND right upper pain? Cholangitis — EMERGENCY: sepsis bundle, senior, urgent ERCP", "Cholangitis — EMERGENCY")
fact("asge", "high", "Stone seen in duct on scan, OR bilirubin above 68 with duct wider than 6 mm? HIGH risk of duct stone: planned operation is usually deferred; senior; ERCP pathway",
     "HIGH risk of duct stone", "68; 6")
fact("asge", "intermediate", "Any liver test abnormal, OR age over 55, OR duct wide? INTERMEDIATE risk: senior; MRCP (or EUS) to look for a stone", "INTERMEDIATE risk", "55",
     note="The book notes that age over 55 alone would push some patients into 'intermediate'; many surgeons do not act on age alone (ASGE 2019; the European version does not use age).")
fact("asge", "low", "LOW risk → continue with planned operation", "LOW risk")
for g, quote, meaning in (
    ("1", "ASA 1 Healthy Fit, non-smoker, little alcohol", "Healthy"),
    ("2", "ASA 2 Mild disease, no real limits Smoker, social drinker, pregnant, BMI 30–40, well-controlled diabetes or BP", "Mild disease, no real limits"),
    ("3", "ASA 3 Severe disease, limits activity Poorly controlled diabetes/BP, lung disease (COPD), BMI 40+, dialysis, old heart attack/stroke (over 3 months ago), pacemaker", "Severe disease, limits activity"),
    ("4", "ASA 4 Severe disease, constant threat to life Recent heart attack/stroke (under 3 months), sepsis, severe heart valve disease", "Severe disease, constant threat to life"),
    ("5", "ASA 5 Not expected to survive without the operation Ruptured major artery, massive trauma", "Not expected to survive without the operation"),
    ("6", "ASA 6 Brain-dead organ donor —", "Brain-dead organ donor"),
):
    fact("asa", f"asa{g}", quote, meaning, g if g in ("30", "40") else g)
fact("asa", "emergency_e", "+ E Emergency operation All S2 patients", "Emergency operation", note="'E' is added for emergencies: all S2 patients.")
fact("asa", "expected_grades", "Expected grades for ProcSense patients CLINICAL INTERPRETATION (derived from the ASA 2020 examples): healthy S1 = ASA 1; S1 with controlled BP or diabetes = 2; poorly controlled diabetes, AF on blood thinners, BMI 40+ = 3; healthy S2 = 1E; septic S2 = 2E–4E.",
     "healthy S1 = ASA 1", "1; 2; 3; 1; 2; 4", evidence="B", note="Clinical interpretation derived from the ASA 2020 examples; the anaesthetist confirms.")
fact("airway", "mallampati", "Mallampati class Patient opens mouth wide and sticks tongue out; what can you see? Class 1–2 (uvula — the dangly bit at the back — visible) Class 3–4 (only base of uvula or only roof of mouth)",
     "Class 1–2", "1; 2; 3; 4")
fact("airway", "mouth_opening", "Mouth opening Gap between front teeth 3 cm or more (~2–3 fingers) Less than 3 cm", "3 cm or more", "3")
fact("airway", "thyromental", "Chin-to-neck distance (thyromental) Chin tip to Adam's apple, head tilted back 6.5 cm or more Less than 6 cm", "6.5 cm or more", "6.5; 6")
fact("airway", "neck_movement", "Neck movement Look up / down Full Stiff", "Full")
fact("stopbang", "questions", "8 yes/no questions: Snoring, Tired, Observed stopping breathing, high blood Pressure, BMI above 35, Age above 50, Neck above 40 cm, male Gender", "Snoring", "8; 35; 50; 40")
fact("stopbang", "bands", "0–2 low risk 3–4 intermediate; 5–8 high risk", "low risk", "0; 2; 3; 4; 5; 8")
fact("vte", "risk_factors", "Risk factors include: age over 60, BMI over 30, cancer, previous clot, family history of clots, oestrogen pill or HRT, pregnancy, dehydration, reduced mobility, surgery longer than 90 minutes, acute tummy inflammation.",
     "age over 60", "60; 30; 90")
fact("vte", "bleeding_risk_factors", "Bleeding risk factors: active bleeding, blood thinners, platelets below 75, very high BP.", "platelets below 75", "75")
fact("sort", "draft_choice", "NICE quality standard QS216 asks hospitals to use a validated risk tool. Options: SORT (Surgical Outcome Risk Tool, simple), P-POSSUM, ACS NSQIP online calculator.", "SORT", "216",
     evidence="C", note="Draft choice: SORT; institution-dependent (QS216 requires a validated tool but does not name one).")

TABLES = [items, bands, ex, facts]
OFFICIAL_FILES = {}
