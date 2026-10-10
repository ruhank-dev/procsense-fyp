"""D2 (p0-medical-research-data s04): laboratory bands, critical results, sample-quality artefacts, turnaround times.

Bands: walkthrough Part 17 (pages 146-147), as the Change Report decides for the WT-I05..I08 disagreements.
Critical results: Part 5.27. Artefacts: Part 5 introduction point 4 and 5.28. Turnaround: Part H ('FBC 45 min, imaging 60-120 min, etc.').
Lab ranges are institution-dependent (Type C); the owner approved the walkthrough values on 09 Oct 2026 (institutional decision 1 closed).
"""
from framework import Table

PART17 = "Part 17 table 'Blood tests'"

bands = Table("lab_bands", 500, ["analyte", "unit", "zone", "sex", "basis", "v_low", "v_high", "bound", "v_state", "v_condition"],
              "Lab band", PART17, evidence="C", size=150)
ROWS = {
    "hb": ("Haemoglobin g/L M 130–170; F 120–150 110–129/119 80–109 below 80 (crit. below 70)", 146),
    "wbc": ("WBC ×109/L 4.0–11.0 11.1–15.0 15.1–20.0 >20 or <4", 146),
    "neut": ("Neutrophils 2.0–7.5 — <1.5 <0.5", 146),
    "plt": ("Platelets 150–400 100–149 50–99 <50 (crit. <30)", 146),
    "crp": ("CRP mg/L <5 10–49 50–149 ≥150 (≥300 critical)", 146),
    "na": ("Sodium mmol/L 135–145 130–134 / 146–150 125–129 / 151–155 <125 / >155", 146),
    "k": ("Potassium mmol/L 3.5–5.3 3.0–3.4 / 5.5–5.9 2.5–2.9 / 6.0–6.4 <2.5 / ≥6.5", 146),
    "urea": ("Urea mmol/L 2.5–7.8 7.9–12 >12 —", 146),
    "creat": ("Creatinine µmol/L M 60–110; F 45–90 ×1.5–1.9 baseline ×2–2.9 ×3 or ≥354", 146),
    "egfr": ("eGFR ≥90 60–89 30–59 <30", 146),
    "glu": ("Glucose (diabetic, peri-op) 6–10 (to 12 OK) 12.1–14 >14 <4.0 (severe <3.0)", 146),
    "hba1c": ("HbA1c mmol/mol <42 42–47 / 48–69 >69 (postpone S1) —", 146),
    "ket": ("Ketones mmol/L <0.6 0.6–1.4 1.5–2.9 ≥3.0", 146),
    "bili": ("Bilirubin µmol/L 3–21 22–30 31–68 >68", 146),
    "alt": ("ALT U/L ≤40 41–120 121–400 >400", 146),
    "alp": ("ALP U/L 30–130 ≤2× upper 2–5× upper >5× upper", 146),
    "ggt": ("GGT U/L M<60; F<40 ≤3× upper >3× upper —", 146),
    "alb": ("Albumin g/L 35–50 30–34 25–29 <25", 146),
    "lip": ("Lipase <upper limit 1–3× >3× (pancreatitis) —", 146),
    "inr": ("INR (no warfarin) 0.8–1.2 1.3–1.5 >1.5 >2.0", 147),
    "lac": ("Lactate mmol/L <2.0 — 2.0–3.9 ≥4.0", 147),
    "hcg": ("β-hCG IU/L <5 5–25 (repeat 48 h) >25 positive + pain/shock", 147),
    "qtc": ("QTc ms (ECG) M<450; F<460 Prolonged ≥450 M / ≥460 F — ≥500 markedly prolonged", 147),
}


def lab(an, name, unit, zone, low="", high="", sex="", bound="", basis="", state="", cond="", note="", tag=""):
    quote, page = ROWS[an]
    bands.add(f"{an}.{zone}.{tag or (sex or 'all')}.{low}-{high}", quote, page, analyte=name, unit=unit, zone=zone, sex=sex, basis=basis, v_low=low, v_high=high,
              bound=bound, v_state=state, v_condition=cond, note=note)


# haemoglobin
lab("hb", "haemoglobin", "g/L", "normal", 130, 170, sex="M")
lab("hb", "haemoglobin", "g/L", "normal", 120, 150, sex="F")
lab("hb", "haemoglobin", "g/L", "mild", 110, 129, sex="M", note="The book writes '110–129/119': men 110–129, women up to 119.")
lab("hb", "haemoglobin", "g/L", "mild", 110, 119, sex="F", note="The book writes '110–129/119': men 110–129, women up to 119.")
lab("hb", "haemoglobin", "g/L", "significant", 80, 109)
lab("hb", "haemoglobin", "g/L", "severe", "", 80, bound="below")
lab("hb", "haemoglobin", "g/L", "critical", "", 70, bound="below")
# white cells
lab("wbc", "wbc", "x10^9/L", "normal", 4.0, 11.0)
lab("wbc", "wbc", "x10^9/L", "mild", 11.1, 15.0)
lab("wbc", "wbc", "x10^9/L", "significant", 15.1, 20.0)
lab("wbc", "wbc", "x10^9/L", "severe", 20, "", bound="above", tag="high")
lab("wbc", "wbc", "x10^9/L", "severe", "", 4, bound="below", tag="low")
lab("neut", "neutrophils", "x10^9/L", "normal", 2.0, 7.5)
lab("neut", "neutrophils", "x10^9/L", "significant", "", 1.5, bound="below")
lab("neut", "neutrophils", "x10^9/L", "severe", "", 0.5, bound="below")
lab("plt", "platelets", "x10^9/L", "normal", 150, 400)
lab("plt", "platelets", "x10^9/L", "mild", 100, 149)
lab("plt", "platelets", "x10^9/L", "significant", 50, 99)
lab("plt", "platelets", "x10^9/L", "severe", "", 50, bound="below")
lab("plt", "platelets", "x10^9/L", "critical", "", 30, bound="below")
lab("crp", "crp", "mg/L", "normal", "", 5, bound="below", note="Part 17 leaves 5–9 uncovered (the mild band starts at 10); Part 5.4 says mild is 5–49 (WT-I06).")
lab("crp", "crp", "mg/L", "mild", 10, 49)
lab("crp", "crp", "mg/L", "significant", 50, 149)
lab("crp", "crp", "mg/L", "severe", 150, "")
lab("crp", "crp", "mg/L", "critical", 300, "")
# sodium, potassium, urea
lab("na", "sodium", "mmol/L", "normal", 135, 145)
lab("na", "sodium", "mmol/L", "mild", 130, 134, tag="low")
lab("na", "sodium", "mmol/L", "mild", 146, 150, tag="high")
lab("na", "sodium", "mmol/L", "significant", 125, 129, tag="low")
lab("na", "sodium", "mmol/L", "significant", 151, 155, tag="high")
lab("na", "sodium", "mmol/L", "severe", "", 125, bound="below", tag="low")
lab("na", "sodium", "mmol/L", "severe", 155, "", bound="above", tag="high")
lab("k", "potassium", "mmol/L", "normal", 3.5, 5.3, note="Part 17 leaves 5.4 uncovered (the mild band resumes at 5.5).")
lab("k", "potassium", "mmol/L", "mild", 3.0, 3.4, tag="low")
lab("k", "potassium", "mmol/L", "mild", 5.5, 5.9, tag="high")
lab("k", "potassium", "mmol/L", "significant", 2.5, 2.9, tag="low")
lab("k", "potassium", "mmol/L", "significant", 6.0, 6.4, tag="high")
lab("k", "potassium", "mmol/L", "severe", "", 2.5, bound="below", tag="low")
lab("k", "potassium", "mmol/L", "severe", 6.5, "", tag="high")
lab("urea", "urea", "mmol/L", "normal", 2.5, 7.8)
lab("urea", "urea", "mmol/L", "mild", 7.9, 12)
lab("urea", "urea", "mmol/L", "significant", 12, "", bound="above")
# creatinine and kidney function (multiples of the patient's baseline, or an absolute value)
lab("creat", "creatinine", "umol/L", "normal", 60, 110, sex="M")
lab("creat", "creatinine", "umol/L", "normal", 45, 90, sex="F")
lab("creat", "creatinine", "umol/L", "mild", 1.5, 1.9, basis="baseline", note="Multiples of the patient's baseline.")
lab("creat", "creatinine", "umol/L", "significant", 2, 2.9, basis="baseline", note="Multiples of the patient's baseline.")
lab("creat", "creatinine", "umol/L", "severe", 3, "", basis="baseline", tag="x3", note="Three times the patient's baseline.")
lab("creat", "creatinine", "umol/L", "severe", 354, "", tag="abs", note="Or an absolute value of 354 or more.")
lab("egfr", "egfr", "mL/min/1.73m2", "normal", 90, "")
lab("egfr", "egfr", "mL/min/1.73m2", "mild", 60, 89)
lab("egfr", "egfr", "mL/min/1.73m2", "significant", 30, 59)
lab("egfr", "egfr", "mL/min/1.73m2", "severe", "", 30, bound="below")
# glucose, HbA1c, ketones
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "normal", 6, 10, note="The book adds '(to 12 OK)': up to 12 is acceptable.")
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "acceptable", "", 12, tag="to12", note="'(to 12 OK)': acceptable up to 12 peri-operatively.")
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "mild", 12.1, 14)
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "significant", 14, "", bound="above")
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "severe", "", 4.0, bound="below", tag="low")
lab("glu", "glucose_perioperative_diabetic", "mmol/L", "critical", "", 3.0, bound="below", tag="severe-low", note="The book writes '(severe <3.0)'.")
lab("hba1c", "hba1c", "mmol/mol", "normal", "", 42, bound="below")
lab("hba1c", "hba1c", "mmol/mol", "mild", 42, 47, tag="a")
lab("hba1c", "hba1c", "mmol/mol", "mild", 48, 69, tag="b")
lab("hba1c", "hba1c", "mmol/mol", "significant", 69, "", bound="above", note="'Postpone S1'.")
lab("ket", "ketones", "mmol/L", "normal", "", 0.6, bound="below")
lab("ket", "ketones", "mmol/L", "mild", 0.6, 1.4)
lab("ket", "ketones", "mmol/L", "significant", 1.5, 2.9)
lab("ket", "ketones", "mmol/L", "severe", 3.0, "")
# liver and pancreas
lab("bili", "bilirubin", "umol/L", "normal", 3, 21)
lab("bili", "bilirubin", "umol/L", "mild", 22, 30)
lab("bili", "bilirubin", "umol/L", "significant", 31, 68)
lab("bili", "bilirubin", "umol/L", "severe", 68, "", bound="above")
lab("alt", "alt", "U/L", "normal", "", 40)
lab("alt", "alt", "U/L", "mild", 41, 120)
lab("alt", "alt", "U/L", "significant", 121, 400)
lab("alt", "alt", "U/L", "severe", 400, "", bound="above")
lab("alp", "alp", "U/L", "normal", 30, 130)
lab("alp", "alp", "U/L", "mild", "", 2, basis="upper_limit", note="At most 2 times the upper limit.")
lab("alp", "alp", "U/L", "significant", 2, 5, basis="upper_limit", note="2 to 5 times the upper limit.")
lab("alp", "alp", "U/L", "severe", 5, "", basis="upper_limit", bound="above", note="More than 5 times the upper limit.")
lab("ggt", "ggt", "U/L", "normal", "", 60, sex="M", bound="below")
lab("ggt", "ggt", "U/L", "normal", "", 40, sex="F", bound="below")
lab("ggt", "ggt", "U/L", "mild", "", 3, basis="upper_limit", note="At most 3 times the upper limit.")
lab("ggt", "ggt", "U/L", "significant", 3, "", basis="upper_limit", bound="above", note="More than 3 times the upper limit.")
lab("alb", "albumin", "g/L", "normal", 35, 50)
lab("alb", "albumin", "g/L", "mild", 30, 34)
lab("alb", "albumin", "g/L", "significant", 25, 29)
lab("alb", "albumin", "g/L", "severe", "", 25, bound="below")
lab("lip", "lipase", "U/L", "normal", state="upper limit", basis="upper_limit", note="'<upper limit' of the lab.")
lab("lip", "lipase", "U/L", "mild", 1, 3, basis="upper_limit", note="1 to 3 times the upper limit.")
lab("lip", "lipase", "U/L", "significant", 3, "", basis="upper_limit", bound="above", note="More than 3 times the upper limit (pancreatitis).")
# clotting, lactate, pregnancy test, QTc
lab("inr", "inr", "ratio", "normal", 0.8, 1.2)
lab("inr", "inr", "ratio", "mild", 1.3, 1.5)
lab("inr", "inr", "ratio", "significant", 1.5, "", bound="above")
lab("inr", "inr", "ratio", "severe", 2.0, "", bound="above")
lab("lac", "lactate", "mmol/L", "normal", "", 2.0, bound="below")
lab("lac", "lactate", "mmol/L", "significant", 2.0, 3.9)
lab("lac", "lactate", "mmol/L", "severe", 4.0, "")
lab("hcg", "beta_hcg", "IU/L", "normal", "", 5, bound="below", note="Negative.")
lab("hcg", "beta_hcg", "IU/L", "mild", 5, 25, note="Equivocal: repeat after 48 hours.")
lab("hcg", "beta_hcg", "IU/L", "significant", 25, "", bound="above", note="Positive; with pain or shock it is an emergency.")
lab("qtc", "qtc", "ms", "normal", "", 450, sex="M", bound="below")
lab("qtc", "qtc", "ms", "normal", "", 460, sex="F", bound="below")
lab("qtc", "qtc", "ms", "mild", 450, "", sex="M", note="Prolonged.")
lab("qtc", "qtc", "ms", "mild", 460, "", sex="F", note="Prolonged.")
lab("qtc", "qtc", "ms", "severe", 500, "", note="Markedly prolonged.")

# ------------------------------------------------------------------ critical results (Part 5.27)

crit = Table("critical_results", 650, ["analyte", "direction", "inclusive", "v_value", "v_condition", "d_unit"], "Critical result (the lab phones the doctor)",
             "Part 5.27 table 'Dangerous (critical) results'", evidence="C", size=50)
C = {
    "k": "Potassium 2.5 or below, or 6.5 or above",
    "na": "Sodium 120 or below, or 160 or above",
    "glu": "Glucose 2.5 or below",
    "ca": "Calcium 1.8 or below, or 3.5 or above",
    "creat": "Creatinine 354 or above (without known kidney failure)",
    "crp": "CRP 300 or above",
    "hb": "Haemoglobin Below 70 g/L, or a fall of 20+",
    "plt": "Platelets Below 30",
    "neut": "Neutrophils Below 0.5",
    "inr": "INR 5 or above on warfarin; above 1.5 unexpectedly",
    "lac": "Lactate 4.0 or above",
}


def cr(an, name, direction, inclusive, value, unit, cond="", tag=""):
    crit.add(f"{an}.{direction}{tag}", C[an], "auto", analyte=name, direction=direction, inclusive=inclusive, v_value=value, v_condition=cond, d_unit=unit,
             note=f"Derived: unit '{unit}' as used in Part 17; the 5.27 table does not repeat units.")


cr("k", "potassium", "low", "yes", 2.5, "mmol/L")
cr("k", "potassium", "high", "yes", 6.5, "mmol/L")
cr("na", "sodium", "low", "yes", 120, "mmol/L")
cr("na", "sodium", "high", "yes", 160, "mmol/L")
cr("glu", "glucose", "low", "yes", 2.5, "mmol/L")
crit.add("ca.low", C["ca"], "auto", analyte="calcium", direction="low", inclusive="yes", v_value=1.8, v_condition="", d_unit="mmol/L",
         note="Calcium has no band in Part 17; the unit mmol/L is derived (typical clinical unit), not stated in 5.27.")
crit.add("ca.high", C["ca"], "auto", analyte="calcium", direction="high", inclusive="yes", v_value=3.5, v_condition="", d_unit="mmol/L",
         note="Calcium has no band in Part 17; the unit mmol/L is derived (typical clinical unit), not stated in 5.27.")
cr("creat", "creatinine", "high", "yes", 354, "umol/L", cond="without known kidney failure")
cr("crp", "crp", "high", "yes", 300, "mg/L")
cr("hb", "haemoglobin", "low", "no", 70, "g/L")
crit.add("hb.fall", C["hb"], "auto", analyte="haemoglobin", direction="fall", inclusive="yes", v_value=20, v_condition="", d_unit="g/L",
         note="A fall of 20 or more from the previous value. Derived: unit g/L as in the same row.")
cr("plt", "platelets", "low", "no", 30, "x10^9/L")
cr("neut", "neutrophils", "low", "no", 0.5, "x10^9/L")
crit.add("inr.high_on_warfarin", C["inr"], "auto", analyte="inr", direction="high", inclusive="yes", v_value=5, v_condition="warfarin", d_unit="ratio",
         note="5 or above in a patient on warfarin.")
crit.add("inr.high_unexpected", C["inr"], "auto", analyte="inr", direction="high", inclusive="no", v_value=1.5, v_condition="unexpectedly", d_unit="ratio",
         note="Above 1.5 when it is not expected (not on warfarin).")
cr("lac", "lactate", "high", "yes", 4.0, "mmol/L")


# ------------------------------------------------------------------ sample-quality artefacts (Part 5 introduction point 4, and 5.28)

quality = Table("sample_quality", 700, ["artefact", "v_effect", "v_comment", "response"], "Sample-quality artefact", "Part 5 introduction, point 4 'Faulty samples exist'",
                evidence="B", size=50)
quality.add("haemolysed",
            "Haemolysed sample — the red blood cells burst in the tube (from a difficult blood draw or delay). They release potassium, so the potassium looks falsely high. The lab writes 'haemolysed' on the report.",
            "auto", artefact="haemolysed", v_effect="potassium looks falsely high", v_comment="haemolysed",
            response="Read the lab comment; repeat; if dangerous and possibly real, treat and repeat at the same time.")
quality.add("drip_arm", "Drip-arm sample — blood taken from the same arm as a running drip is diluted (or full of sugar from a sugar drip).", "auto",
            artefact="drip_arm", v_effect="diluted", v_comment="", response="Repeat from the other arm.",
            note="The response is Derived from the book's rule 'if a strange result doesn't fit a well-looking patient, repeat it'; the book does not say 'other arm'.")
quality.add("wrong_tube_edta",
            "Wrong tube contamination — e.g. the purple tube's chemical (EDTA) falsely raises potassium and lowers calcium.", "auto",
            artefact="wrong_tube_edta", v_effect="falsely raises potassium and lowers calcium", v_comment="", response="Repeat in the correct tube.",
            note="The response is Derived from the book's repeat rule; the book does not name the tube.")
quality.add("mislabelled", "Mislabelled sample — results belong to someone else.", "auto", artefact="mislabelled", v_effect="results belong to someone else", v_comment="",
            response="Re-take with bedside labelling; report the incident.",
            note="Response from 5.28 'Mislabelled / rejected sample' row.")
quality.add("rejected", "Mislabelled / rejected sample Lab rejects sample Re-take with bedside labelling; report the incident", "auto", artefact="rejected",
            v_effect="Lab rejects sample", v_comment="", response="Re-take with bedside labelling; report the incident")
quality.add("clumped", "Faulty sample (haemolysis, clumping, drip arm, wrong patient).", "auto", artefact="clumped", v_effect="clumping", v_comment="",
            response="Repeat, using a different tube if the platelets are very low in a well person.",
            note="'Clumping' appears in Part 14 only as a cause of a faulty sample; the effect on platelets is not described there. 5.28 says to repeat a very low platelet count in a well person with a different tube.")

# ------------------------------------------------------------------ turnaround (Part H audit table)

turn = Table("turnaround", 750, ["test", "v_minutes_low", "v_minutes_high", "v_note"], "Turnaround time", "Part H audit table row 'Turnaround times'", evidence="D", size=20)
T = "Turnaround times FBC 45 min, imaging 60–120 min, etc. — Simulation D Design value"
turn.add("fbc", T, "auto", test="fbc", v_minutes_low=45, v_minutes_high=45)
turn.add("imaging", T, "auto", test="imaging", v_minutes_low=60, v_minutes_high=120)
turn.add("other_tests", T, "auto", test="other_tests", v_note="etc.",
         note="UNRESOLVED (WT-I10): the book gives only these two and 'etc.'; the other turnaround times need a design value agreed with the surgeon and anaesthetist.")

TABLES = [bands, crit, quality, turn]
