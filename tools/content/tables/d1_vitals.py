"""D1 (p0-medical-research-data s03): vital-sign display bands, NEWS2 scoring and response, sepsis tiers.

Values come from the walkthrough (Part 4.9, 4.10 and the Part 17 tables). The NEWS2 points and Scale 2 come from the RCP report itself
(opened 10 Oct 2026); the sepsis tiers and the re-check intervals from NICE NG253 (opened 10 Oct 2026).
Display bands and NEWS2 scoring stay separate tables on purpose (WT-I04: the temperature zones differ from the NEWS2 cells).
"""
from framework import Table

RCP = "RCP National Early Warning Score (NEWS) 2 (2017)"
RCP_URL = "https://www.rcp.ac.uk/media/a4ibkkbf/news2-final-report_0_0.pdf"
NICE = "NICE NG253 Suspected sepsis in people aged 16 or over"
NICE_URL = "https://www.nice.org.uk/guidance/ng253/chapter/Evaluating-risk"

# ------------------------------------------------------------------ display bands (Part 17, page 146)

vitals = Table("vitals_bands", 100, ["parameter", "unit", "zone", "v_low", "v_high", "v_low2", "v_high2", "bound", "v_state", "v_condition"],
               "Vital-sign display band", "Part 17 table 'Vital signs (adults, not pregnant)'", evidence="A")
vitals.official_name = ""

ROW = {
    "hr": "Heart rate /min 51–90 91–110 or 41–50 111–130 ≥131 or ≤40",
    "sbp": "Top BP (low side) mmHg 111–219 101–110 91–100 ≤90 / MAP below 65",
    "bp_high": "BP (high side) below 140/90 140–179 / 90–109 ≥180/110 (S1 usually deferred after review) ≥180/120 + symptoms",
    "rr": "Breathing /min 12–20 9–11 or 21–24 ≥22 with infection ≥25 or ≤8",
    "spo2": "Oxygen % ≥96 94–95 92–93 ≤91 (critical below 88)",
    "temp": "Temperature °C 36.1–37.9 35.1–36.0 / 38.0–39.0 ≥39.1 ≤35.0",
    "acvpu": "Alertness Alert — New confusion Voice/Pain/Unresponsive",
    "pain": "Pain /10 0–3 4–6 7–10 Uncontrolled",
    "crt": "Capillary refill ≤2 s 2–3 s >3 s Mottled skin",
    "urine": "Urine ≥0.5 mL/kg/h below for under 6 h below for 6 h+ Almost none",
    "shock": "Shock index (HR ÷ SBP) — supporting indicator only 0.5–0.7 (up to ~0.9 in some studies) 0.8–0.9 approaching/above 1.0 (study-derived marker) No validated higher cut-off used",
    "news2": "NEWS2 0 1–4 5–6 or any single 3 ≥7",
}


def band(key, param, unit, zone, low="", high="", low2="", high2="", bound="", state="", row=None, note="", condition=""):
    vitals.add(f"{key}.{zone}", ROW[row or key], 146, parameter=param, unit=unit, zone=zone, v_low=low, v_high=high, v_low2=low2, v_high2=high2,
               bound=bound, v_state=state, v_condition=condition, note=note)


# heart rate
band("hr", "heart_rate", "/min", "normal", 51, 90)
band("hr.a", "heart_rate", "/min", "watch", 91, 110, row="hr")
band("hr.b", "heart_rate", "/min", "watch", 41, 50, row="hr")
band("hr", "heart_rate", "/min", "worry", 111, 130)
band("hr.a", "heart_rate", "/min", "emergency", 131, "", row="hr")
band("hr.b", "heart_rate", "/min", "emergency", "", 40, row="hr")
# systolic blood pressure, low side
band("sbp", "systolic_bp_low", "mmHg", "normal", 111, 219)
band("sbp", "systolic_bp_low", "mmHg", "watch", 101, 110)
band("sbp", "systolic_bp_low", "mmHg", "worry", 91, 100)
band("sbp.a", "systolic_bp_low", "mmHg", "emergency", "", 90, row="sbp")
band("sbp.map", "mean_arterial_pressure", "mmHg", "emergency", "", 65, bound="below", row="sbp", note="Part 17 says 'MAP below 65' for the emergency zone.")
# blood pressure, high side (systolic / diastolic)
band("bp_high", "bp_high", "mmHg", "normal", "", 140, "", 90, bound="below", note="Systolic below 140 and diastolic below 90.")
band("bp_high", "bp_high", "mmHg", "watch", 140, 179, 90, 109, note="Systolic 140–179 / diastolic 90–109.")
band("bp_high", "bp_high", "mmHg", "worry", 180, "", 110, "", note="At or above 180/110; the book adds 'S1 usually deferred after review'.")
band("bp_high", "bp_high", "mmHg", "emergency", 180, "", 120, "", condition="symptoms", note="At or above 180/120 with symptoms.")
# breathing
band("rr", "respiratory_rate", "/min", "normal", 12, 20)
band("rr.a", "respiratory_rate", "/min", "watch", 9, 11, row="rr")
band("rr.b", "respiratory_rate", "/min", "watch", 21, 24, row="rr")
band("rr", "respiratory_rate", "/min", "worry", 22, "", condition="infection", note="Worry zone is 22 or more only with infection.")
band("rr.a", "respiratory_rate", "/min", "emergency", 25, "", row="rr")
band("rr.b", "respiratory_rate", "/min", "emergency", "", 8, row="rr")
# oxygen saturation
band("spo2", "spo2", "%", "normal", 96, "")
band("spo2", "spo2", "%", "watch", 94, 95)
band("spo2", "spo2", "%", "worry", 92, 93)
band("spo2.a", "spo2", "%", "emergency", "", 91, row="spo2")
band("spo2.critical", "spo2", "%", "critical", "", 88, bound="below", row="spo2", note="'critical below 88' sits inside the emergency zone.")
# temperature
band("temp", "temperature_c", "°C", "normal", 36.1, 37.9)
band("temp.a", "temperature_c", "°C", "watch", 35.1, 36.0, row="temp")
band("temp.b", "temperature_c", "°C", "watch", 38.0, 39.0, row="temp")
band("temp", "temperature_c", "°C", "worry", 39.1, "")
band("temp", "temperature_c", "°C", "emergency", "", 35.0)
# alertness, pain, capillary refill, urine
band("acvpu", "acvpu", "", "normal", state="Alert")
band("acvpu", "acvpu", "", "worry", state="New confusion")
band("acvpu", "acvpu", "", "emergency", state="Voice/Pain/Unresponsive")
band("pain", "pain_score", "/10", "normal", 0, 3)
band("pain", "pain_score", "/10", "watch", 4, 6)
band("pain", "pain_score", "/10", "worry", 7, 10)
band("pain", "pain_score", "/10", "emergency", state="Uncontrolled")
band("crt", "capillary_refill", "s", "normal", "", 2)
band("crt", "capillary_refill", "s", "watch", 2, 3)
band("crt", "capillary_refill", "s", "worry", 3, "", bound="above", note="'>3 s'.")
band("crt", "capillary_refill", "s", "emergency", state="Mottled skin")
band("urine", "urine_output", "mL/kg/h", "normal", 0.5, "")
band("urine", "urine_output", "mL/kg/h", "watch", state="below for under 6 h", note="Below 0.5 mL/kg/h for under 6 hours.")
band("urine", "urine_output", "mL/kg/h", "worry", state="below for 6 h+", note="Below 0.5 mL/kg/h for 6 hours or more.")
band("urine", "urine_output", "mL/kg/h", "emergency", state="Almost none")
# shock index (a supporting indicator only)
band("shock", "shock_index", "HR/SBP", "normal", 0.5, 0.7, note="Supporting indicator only. The book adds 'up to ~0.9 in some studies'.")
band("shock", "shock_index", "HR/SBP", "watch", 0.8, 0.9, note="'approaching/above 1.0 (study-derived marker)'. The book states no higher validated cut-off.")
# NEWS2 total
band("news2", "news2_total", "points", "normal", 0, 0)
band("news2", "news2_total", "points", "watch", 1, 4)
band("news2", "news2_total", "points", "worry", 5, 6, note="Or any single parameter scoring 3.")
band("news2", "news2_total", "points", "emergency", 7, "")

# ------------------------------------------------------------------ NEWS2 scoring (book 4.9 ranges; points and Scale 2 from the RCP chart)

news2 = Table("news2_scoring", 200, ["scale", "parameter", "v_low", "v_high", "v_state", "o_points"], "NEWS2 scoring cell",
              "Part 4.9 table 'What is measured'", evidence="A", official_name=RCP, official_url=RCP_URL)

BOOK = {
    "rr": "Breathing rate (per min) ≤8 9–11 12–20 21–24 ≥25",
    "spo2": "Oxygen level SpO2 (%) — usual scale ≤91 92–93 94–95 ≥96",
    "o2": "On extra oxygen? Yes No (room air)",
    "sbp": "Top BP number (mmHg) ≤90 91–100 101–110 111–219 ≥220",
    "hr": "Heart rate (per min) ≤40 41–50 51–90 91–110 111–130 ≥131",
    "acvpu": "Alertness Alert Confused / Voice / Pain / Unresponsive",
    "temp": "Temperature (°C) ≤35.0 35.1–36.0 36.1–38.0 38.1–39.0 ≥39.1",
}
POINT_LINE = {
    "rr": "Respiration rate: ≤8 = 3; 9–11 = 1; 12–20 = 0; 21–24 = 2; ≥25 = 3",
    "spo2": "SpO2 Scale 1: ≤91 = 3; 92–93 = 2; 94–95 = 1; ≥96 = 0",
    "sbp": "Systolic blood pressure: ≤90 = 3; 91–100 = 2; 101–110 = 1; 111–219 = 0; ≥220 = 3",
    "hr": "Pulse: ≤40 = 3; 41–50 = 1; 51–90 = 0; 91–110 = 1; 111–130 = 2; ≥131 = 3",
    "temp": "Temperature: ≤35.0 = 3; 35.1–36.0 = 1; 36.1–38.0 = 0; 38.1–39.0 = 1; ≥39.1 = 2",
}


def pts(scale, param, rowkey, low, high, points, state="", official=""):
    key = f"{scale}.{param}." + (state or f"{low}-{high}")
    news2.add(key, BOOK.get(rowkey, "") if scale == "1" else "", "auto" if scale == "1" else "",
              official=official or POINT_LINE.get(rowkey, ""), official_page="Chart 1, report p.29 (points read from the chart image)",
              scale=scale, parameter=param, v_low=low, v_high=high, v_state=state, o_points=points,
              note="" if scale == "1" else "Scale 2 is not in the walkthrough text (it only says Scale 2 exists, target 88–92%); taken from the RCP chart.")


# Scale 1
for lo, hi, p in [("", 8, 3), (9, 11, 1), (12, 20, 0), (21, 24, 2), (25, "", 3)]:
    pts("1", "respiratory_rate", "rr", lo, hi, p)
for lo, hi, p in [("", 91, 3), (92, 93, 2), (94, 95, 1), (96, "", 0)]:
    pts("1", "spo2", "spo2", lo, hi, p)
for lo, hi, p in [("", 90, 3), (91, 100, 2), (101, 110, 1), (111, 219, 0), (220, "", 3)]:
    pts("1", "systolic_bp", "sbp", lo, hi, p)
for lo, hi, p in [("", 40, 3), (41, 50, 1), (51, 90, 0), (91, 110, 1), (111, 130, 2), (131, "", 3)]:
    pts("1", "heart_rate", "hr", lo, hi, p)
for lo, hi, p in [("", 35.0, 3), (35.1, 36.0, 1), (36.1, 38.0, 0), (38.1, 39.0, 1), (39.1, "", 2)]:
    pts("1", "temperature_c", "temp", lo, hi, p)
# Air or oxygen, and alertness (text cells)
news2.add("1.oxygen.air", BOOK["o2"], "auto", official="Air or oxygen: Oxygen = 2; Air = 0", official_page="Chart 1, report p.29", scale="1", parameter="supplemental_oxygen",
          v_state="room air", o_points=0, note="Chart: Air = 0. The book writes 'No (room air)'.")
news2.add("1.oxygen.oxygen", BOOK["o2"], "auto", official="Air or oxygen: Oxygen = 2; Air = 0", official_page="Chart 1, report p.29", scale="1", parameter="supplemental_oxygen",
          v_state="Yes", o_points=2, note="Chart: Oxygen = 2.")
news2.add("1.acvpu.alert", BOOK["acvpu"], "auto", official="Consciousness: Alert = 0; CVPU = 3", official_page="Chart 1, report p.29", scale="1", parameter="acvpu",
          v_state="Alert", o_points=0)
news2.add("1.acvpu.cvpu", BOOK["acvpu"], "auto", official="Consciousness: Alert = 0; CVPU = 3", official_page="Chart 1, report p.29", scale="1", parameter="acvpu",
          v_state="Voice", o_points=3, note="C (new confusion), V, P and U all score 3 (RCP: new confusion scores 3).")
# Scale 2 (only SpO2 differs; the other parameters use the Scale 1 cells)
S2 = "SpO2 Scale 2 (%) ≤83 84–85 86–87 88–92 ≥93 on air 93–94 on oxygen 95–96 on oxygen ≥97 on oxygen"
for lo, hi, st, p in [("", 83, "", 3), (84, 85, "", 2), (86, 87, "", 1), (88, 92, "", 0), (93, "", "on air", 0), (93, 94, "on oxygen", 1), (95, 96, "on oxygen", 2), (97, "", "on oxygen", 3)]:
    key = "2.spo2." + (st.replace(" ", "_") + "." if st else "") + f"{lo}-{hi}"
    pline = {3: "≤83 = 3", 2: "84–85 = 2", 1: "86–87 = 1", 0: "88–92 = 0"}
    line = {("", 83): "≤83 = 3", (84, 85): "84–85 = 2", (86, 87): "86–87 = 1", (88, 92): "88–92 = 0", (93, ""): "≥93 on air = 0",
            (93, 94): "93–94 on oxygen = 1", (95, 96): "95–96 on oxygen = 2", (97, ""): "≥97 on oxygen = 3"}[(lo, hi)]
    news2.add(key, "", "", official="SpO2 Scale 2: ≤83 = 3; 84–85 = 2; 86–87 = 1; 88–92 = 0; ≥93 on air = 0; 93–94 on oxygen = 1; 95–96 on oxygen = 2; ≥97 on oxygen = 3", official_page="Chart 1, report p.29 (points read from the chart image)", scale="2", parameter="spo2",
              v_low=lo, v_high=hi, v_state=st, o_points=p,
              note="Scale 2 is not in the walkthrough text (it only says Scale 2 exists, target 88–92%); taken from the RCP chart. Use only for confirmed hypercapnic respiratory failure (target 88–92%), decided by a competent clinician.")

# ------------------------------------------------------------------ NEWS2 response (book 4.9 table; official tiers from RCP chart 2)

resp = Table("news2_response", 300, ["band", "v_min", "v_max", "risk", "frequency", "responder"], "NEWS2 response tier",
             "Part 4.9 table 'Total score'", evidence="A", official_name=RCP, official_url=RCP_URL)
T = "Total score Risk How often to check Who responds"
resp.add("0", "0 Low At least every 12 hours Routine", "auto", band="0", v_min=0, v_max=0, risk="Low", frequency="At least every 12 hours", responder="Routine",
         note="RCP chart 2 groups aggregate scores 0 to 4 together as Low; the split of 0 and the hours are the book's.")
resp.add("1-4", "1–4 Low At least every 4–6 hours Nurse decides whether to call a doctor", "auto", band="1-4", v_min=1, v_max=4, risk="Low",
         frequency="At least every 4–6 hours", responder="Nurse decides whether to call a doctor",
         note="RCP chart 2: aggregate 0 to 4 is Low with a ward-based response; the finer split and the hours are the book's.")
resp.add("single3", "Any single item scoring 3 Low–medium At least hourly Urgent review by the ward doctor", "auto", band="single3", v_min=3, v_max=3, risk="Low-medium",
         frequency="At least hourly", responder="Urgent review by the ward doctor",
         official="Red score Low–medium Urgent ward-based response Score of 3 in any individual parameter", official_page="Chart 2, report p.30",
         note="A single parameter scoring 3, whatever the total.")
resp.add("5-6", "5–6 Medium At least hourly Urgent review by a doctor skilled in acutely ill patients", "auto", band="5-6", v_min=5, v_max=6, risk="Medium",
         frequency="At least hourly", responder="Urgent review by a doctor skilled in acutely ill patients",
         official="Aggregate score 5–6 Medium Key threshold for urgent response", official_page="Chart 2, report p.30")
resp.add("7+", "7 or more High Continuous monitoring Emergency team response, including critical-care skills", "auto", band="7+", v_min=7, v_max="", risk="High",
         frequency="Continuous monitoring", responder="Emergency team response, including critical-care skills",
         official="Aggregate score 7 or more High Urgent or emergency response", official_page="Chart 2, report p.30")

# ------------------------------------------------------------------ sepsis tiers (book 4.10 table; criteria and re-check intervals from NICE NG253)

sepsis = Table("sepsis_ng253_tiers", 400, ["tier", "kind", "v_news2_min", "v_news2_max", "single3", "v_hours", "d_minutes", "text"],
               "Sepsis tier (NICE NG253)", "Part 4.10 table 'NICE NG253 risk category'", evidence="A", official_name=NICE, official_url=NICE_URL)
HIGH = ("High risk of severe illness or death from sepsis Suspected infection AND NEWS2 ≥7, or NEWS2 5–6 with a single parameter scoring 3 "
        "(or confirmed high risk on medical review) Senior review; IV antibiotics within 1 hour; IV fluid bolus within 1 hour (unless contraindicated); "
        "consider critical care NEWS2 every 30 min; escalate if no response within 1 hour")
MOD = ("Moderate risk NEWS2 5–6, or NEWS2 3–4 with a single parameter scoring 3 Review by a clinician within 1 hour, including venous lactate; "
       "antibiotics within 3 hours if infection likely (time used to clarify diagnosis) NEWS2 hourly")
LOW = "Low risk NEWS2 1–4 Clinical assessment; antibiotics within 6 hours if infection is the likely diagnosis NEWS2 every 4–6 hours"
VLOW = "Very low risk NEWS2 0 Routine care unless clinical concern With standard observations"
sepsis.add("high.criteria", HIGH, "auto", tier="high", kind="criteria", v_news2_min=7, single3="", text="Suspected infection and NEWS2 7 or more",
           official="a score of 7 or more suggests high risk of severe illness or death from sepsis")
sepsis.add("high.single3", HIGH, "auto", tier="high", kind="criteria", v_news2_min=5, v_news2_max=6, single3="yes",
           text="NEWS2 5–6 with a single parameter scoring 3 counts as high risk (book); NICE asks for a high-priority clinician review to decide",
           note="NICE 1.6.2 words this as a high-priority review for a definite decision; the book states the upgrade directly. Same intent, different wording (ISS-0005).")
sepsis.add("moderate.criteria", MOD, "auto", tier="moderate", kind="criteria", v_news2_min=5, v_news2_max=6, single3="", text="NEWS2 5–6 (NICE calls this 'medium' risk)",
           official="a score of 5 or 6 suggests a medium risk of severe illness or death from sepsis")
sepsis.add("moderate.single3", MOD, "auto", tier="moderate", kind="criteria", v_news2_min=3, v_news2_max=4, single3="yes",
           text="NEWS2 3–4 with a single parameter scoring 3 counts as moderate risk (book)",
           note="Not stated in this form on the NICE page I read (ISS-0005).")
sepsis.add("low.criteria", LOW, "auto", tier="low", kind="criteria", v_news2_min=1, v_news2_max=4, single3="", text="NEWS2 1–4",
           official="a score of 1 to 4 suggests a low risk of severe illness or death from sepsis")
sepsis.add("very_low.criteria", VLOW, "auto", tier="very_low", kind="criteria", v_news2_min=0, v_news2_max=0, single3="", text="NEWS2 0",
           official="a score of 0 suggests a very low risk of severe illness or death from sepsis")
sepsis.add("high.reassess", HIGH, "auto", tier="high", kind="reassess", d_minutes=30, text="NEWS2 every 30 minutes",
           official="every 30 minutes, for those at high risk of severe illness or death from sepsis", note="Derived: 30 minutes as written.")
sepsis.add("moderate.reassess", MOD, "auto", tier="moderate", kind="reassess", d_minutes=60, text="NEWS2 hourly",
           official="every hour, for those at medium risk of severe illness or death from sepsis",
           note="Derived: 'every hour' = 60 minutes.")
sepsis.add("low.reassess", LOW, "auto", tier="low", kind="reassess", d_minutes="240-360", text="NEWS2 every 4–6 hours",
           official="every 4 to 6 hours, for those at low risk of severe illness or death from sepsis",
           note="Derived: 4 to 6 hours = 240 to 360 minutes.")
sepsis.add("very_low.reassess", VLOW, "auto", tier="very_low", kind="reassess", text="With standard observations",
           official="when standard observations are carried out, in line with local protocol, for those at very low risk of severe illness or death from sepsis")
sepsis.add("high.antibiotics", HIGH, "auto", tier="high", kind="antibiotics", v_hours=1, text="IV antibiotics within 1 hour; IV fluid bolus within 1 hour (unless contraindicated)",
           note="NICE confirms 1-, 3- and 6-hour maximums by risk level; which hour belongs to which tier is in the AoMRC 2022 statement, which has not been opened.")
sepsis.add("moderate.antibiotics", MOD, "auto", tier="moderate", kind="antibiotics", v_hours=3, text="Antibiotics within 3 hours if infection is likely",
           note="As above: the tier-to-hour mapping rests on the AoMRC 2022 statement (not opened).")
sepsis.add("low.antibiotics", LOW, "auto", tier="low", kind="antibiotics", v_hours=6, text="Antibiotics within 6 hours if infection is the likely diagnosis",
           note="As above: the tier-to-hour mapping rests on the AoMRC 2022 statement (not opened).")
sepsis.add("moderate.review", MOD, "auto", tier="moderate", kind="review", v_hours=1, text="Review by a clinician within 1 hour, including venous lactate")
sepsis.add("high.review", HIGH, "auto", tier="high", kind="review", v_hours=1, text="Senior review; escalate if no response within 1 hour; consider critical care")

TABLES = [vitals, news2, resp, sepsis]
OFFICIAL_FILES = {"news2_scoring": "rcp_news2_2017.txt", "news2_response": "rcp_news2_2017.txt", "sepsis_ng253_tiers": "nice_ng253.txt"}


def check_points() -> list[str]:
    """Every NEWS2 cell's points must appear in the RCP chart reading as the exact pair 'range = points'."""
    import framework
    text = framework.official_text("rcp_news2_2017.txt")
    problems = []
    for r in news2.rows:
        lo, hi, st, p = str(r.get("v_low", "")), str(r.get("v_high", "")), r.get("v_state", ""), r["o_points"]
        if r["parameter"] == "supplemental_oxygen":
            pair = {"room air": "Air = 0", "Yes": "Oxygen = 2"}[st]
        elif r["parameter"] == "acvpu":
            pair = {"Alert": "Alert = 0", "Voice": "CVPU = 3"}[st]
        else:
            rng = f"≤{hi}" if not lo else f"≥{lo}" if not hi else f"{lo}–{hi}"
            if st:
                rng = rng + " " + st
            pair = f"{rng} = {p}"
        if framework.book.normalise(pair) not in text:
            problems.append(f"news2_scoring/{r['key']}: '{pair}' is not in the RCP chart reading")
    return problems
