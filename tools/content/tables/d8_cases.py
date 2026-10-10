"""D9 (p0-medical-research-data s10 to s12): case seeds, the measurements the book prints for its example patients, and the Usman deterioration timeline.

What the book gives for a case is a story: who the patient is, what is seen, the key numbers, how the trainee thinks and what the decision is. Full playable
case files (hidden facts with disclose_on, examination findings, every result with flag and comment, imaging, problems, debrief) need content the book does
not contain for most variants; it is NOT invented here. The gaps are listed per case in docs/research/case_content_gaps.md for the clinician.
Ids follow the Change Report (WT-I01): VAR- for Parts 12 and 13, STK- for Part D, SC- for Part 14B; the walkthrough label is kept in a column.
"""
import re
import sys
from pathlib import Path

from framework import Table

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import book  # noqa: E402
import extract_walkthrough as ex  # noqa: E402

LINES = book.WALKTHROUGH.read_text(encoding="utf-8").splitlines()
DATA = ex.extract()


def block(start: str, end: str) -> str:
    """The book's text from the first line matching `start` up to (not including) the next line matching `end`, page footers removed."""
    a = next(i for i, ln in enumerate(LINES) if i >= ex.BODY_STARTS_AT and re.match(start, ln))
    b = next(i for i in range(a + 1, len(LINES)) if re.match(end, LINES[i]))
    return book.normalise(" ".join(ln for ln in LINES[a:b] if not ex.FOOTER.match(ln.strip())))


seeds = Table("case_seeds", 2300, ["case_id", "wt_label", "scenario", "tier", "title", "v_text"], "Case seed", "Parts 12, 13, 14B and D (the case stories)", evidence="D", size=100)
GAPS = "A full playable case file needs more than the book gives: see docs/research/case_content_gaps.md."


def seed(case_id, label, scenario, tier, title, text, note=""):
    seeds.add(case_id, text, "span", case_id=case_id, wt_label=label, scenario=scenario, tier=tier, title=title, v_text=text, note=(note + " " + GAPS).strip())


NEXT_S1 = r"^(Patient S1-|# Part 13 — )"
NEXT_S2 = r"^(Patient S2-|# Part 14 — WHAT IF)"
seed("VAR-S1-A", "Example Patient A", "S1", "P0", "Uncomplicated S1 (everything goes right)", block(r"^Example Patient A — ", r"^Example Patient B — "),
     "The skeleton case file content/cases/VAR-S1-A.json already holds the Step 1 to 3 data of this patient.")
seed("VAR-S1-PB", "Example Patient B", "S1", "P0", "Abnormal S1: the 'elective' patient who isn't well today (high-risk duct stone)", block(r"^Example Patient B — ", r"^More S1 example patients"))
for tier, variants in (("P0", ["E", "B", "C", "D", "G", "H", "J"]), ("P1", ["K", "L", "M", "I"])):
    for x in variants:
        text = block(rf"^Patient S1-{x} — ", NEXT_S1)
        title = re.match(rf"Patient S1-{x} — (.*)", text).group(1)[:90].strip()
        seed(f"VAR-S1-{x}", f"Patient S1-{x}", "S1", tier, re.split(r" What we see", title)[0],
             text, "Variant pairs are inside the one story (S1-B and B2, S1-C and C2, S1-D a and b)." if x in "BCD" else "")
seed("VAR-S2-PC", "Example Patient C", "S2", "P0", "Emergency S2 (classic appendicitis)", block(r"^Example Patient C — ", r"^Example Patient D — "))
seed("VAR-S2-PD", "Example Patient D", "S2", "P0", "The deteriorating patient (watch the numbers change)", block(r"^Example Patient D — ", r"^More S2 example patients"))
for tier, variants in (("P0", ["B", "D", "F", "G"]), ("P1", ["E", "H", "I", "J", "K", "L"])):
    for x in variants:
        text = block(rf"^Patient S2-{x} — ", NEXT_S2)
        title = re.match(rf"Patient S2-{x} — (.*)", text).group(1)[:90].strip()
        seed(f"VAR-S2-{x}", f"Patient S2-{x}", "S2", tier, re.split(r" What we see", title)[0], text)
for st in DATA["stacked_full"]:
    code = st["id"]
    tier = "P0" if code in ("S1-D", "S2-B") else "P1"
    seed(f"STK-{code}", f"STACKED CASE {code}", code[:2], tier, re.sub(r"^STACKED CASE S[12]-[A-D] — ", "", st["title"]), book.normalise(st["body"]),
         "Part D full playable case (20 sections).")
for sc in DATA["stacked_compact"]:
    seed(sc["id"].replace("SC-", "SC-"), f"{sc['id']} ({sc['scenario']})", sc["scenario"], "P2", sc["title"], book.normalise(sc["body"]), "Part 14B.4 worked stacked case.")

# ------------------------------------------------------------------ measurements the book prints for its example patients

meas = Table("case_measurements", 2400, ["case_id", "kind", "name", "v_value", "v_normal", "v_zone"], "Case measurement", "Part 12 and 13 example-patient tables", evidence="D", size=100)


def m(case, kind, name, quote, value, normal="", zone="", note=""):
    meas.add(f"{case}.{name}", quote, "auto", case_id=case, kind=kind, name=name, v_value=value, v_normal=normal, v_zone=zone, note=note)


PA = "VAR-S1-A"
m(PA, "lab", "bilirubin", "Bilirubin 12 µmol/L 3–21 Bile flowing normally", "12", "3–21", "Bile flowing normally")
m(PA, "lab", "alt", "ALT 24 U/L ~7–40 Liver cells fine", "24", "7–40", "Liver cells fine")
m(PA, "lab", "alp", "ALP 78 U/L ~30–130 Bile ducts not blocked", "78", "30–130", "Bile ducts not blocked")
m(PA, "lab", "ggt", "GGT 22 U/L below 40 (women) Confirms ducts fine", "22", "below 40 (women)", "Confirms ducts fine")
m(PA, "lab", "albumin", "Albumin 42 g/L 35–50 Good nutrition", "42", "35–50", "Good nutrition")
m(PA, "lab", "urine_pregnancy_test", "Urine pregnancy test Negative — Not pregnant", "Negative", "", "Not pregnant")
PB = "VAR-S1-PB"
m(PB, "vital", "heart_rate", "Heart rate 84 Normal Calm", "84", "", "Normal")
m(PB, "vital", "blood_pressure", "Blood pressure 138/84 Normal Fine", "138/84", "", "Normal")
m(PB, "vital", "respiratory_rate", "Breathing 16 Normal Fine", "16", "", "Normal")
m(PB, "vital", "spo2", "Oxygen 97% Normal Fine", "97", "", "Normal")
m(PB, "vital", "temperature_c", "Temperature 37.2 °C Normal No fever — so probably no infection in the duct (yet)", "37.2", "", "Normal")
m(PB, "exam", "eyes", "Eyes Yellow tinge to the whites Abnormal Jaundice confirmed on examination", "Yellow tinge to the whites", "", "Abnormal")
m(PB, "lab", "bilirubin", "Bilirubin 72 µmol/L (4.2 mg/dL) 3–21 HIGH (above 68) — bile is backing up", "72", "3–21", "HIGH (above 68)")
m(PB, "lab", "alp", "ALP 380 U/L ~30–130 About 3× normal — bile duct cells irritated by blockage", "380", "30–130", "About 3× normal")
m(PB, "lab", "ggt", "GGT 410 U/L below 40 Confirms the ALP is coming from the bile system", "410", "below 40", "")
m(PB, "lab", "alt", "ALT 160 U/L ~7–40 Moderately raised — liver cells irritated by backed-up bile", "160", "7–40", "Moderately raised")
m(PB, "lab", "wbc", "WBC 9.8 4.0–11.0 Normal — no strong sign of infection", "9.8", "4.0–11.0", "Normal")
m(PB, "lab", "crp", "CRP 12 below 5 Mildly raised", "12", "below 5", "Mildly raised")
m(PB, "lab", "inr", "INR 1.1 0.8–1.2 Clotting normal (long blockage could raise it)", "1.1", "0.8–1.2", "Normal")
m(PB, "lab", "lipase", "Lipase 45 below ~60 Pancreas not inflamed", "45", "below 60", "Pancreas not inflamed")
m(PB, "imaging", "ultrasound", "Ultrasound today Gallstones; common bile duct 9 mm; no stone seen in duct duct ≤6 mm Duct WIDE — something is blocking it lower down",
  "Gallstones; common bile duct 9 mm; no stone seen in duct", "duct ≤6 mm", "Duct WIDE")
PC = "VAR-S2-PC"
m(PC, "vital", "heart_rate", "Heart rate 108, regular Fast (91–110) Body stressed — pain? fever? dehydration? infection?", "108, regular", "", "Fast (91–110)")
m(PC, "vital", "blood_pressure", "Blood pressure 128/76 Normal Circulation coping", "128/76", "", "Normal")
m(PC, "vital", "respiratory_rate", "Breathing 18 Normal OK", "18", "", "Normal")
m(PC, "vital", "spo2", "Oxygen 98% on air Normal Lungs fine", "98", "", "Normal")
m(PC, "vital", "temperature_c", "Temperature 37.9 °C Normal-high (fever starts at 38.0) Mild inflammation", "37.9", "", "Normal-high")
m(PC, "vital", "acvpu", "Alertness Alert Normal —", "Alert", "", "Normal")
m(PC, "vital", "pain_score", "Pain 8/10 Severe Needs pain relief now", "8", "", "Severe")
m(PC, "lab", "wbc", "WBC 13.6 4.0–11.0 Raised — fighting infection", "13.6", "4.0–11.0", "Raised")
m(PC, "lab", "neutrophils", "Neutrophils 82% 40–75% First-responder cells high → bacterial", "82", "40–75", "high")
m(PC, "lab", "crp", "CRP (20 h of symptoms) 38 mg/L below 5 Moderate inflammation", "38", "below 5", "Moderate inflammation", note="20 hours of symptoms: the AAS CRP band for pain under 24 h applies.")
m(PC, "lab", "haemoglobin", "Haemoglobin 148 g/L 130–170 Normal — no bleeding", "148", "130–170", "Normal")
m(PC, "lab", "creatinine", "Creatinine 82 µmol/L 60–110 Kidneys fine", "82", "60–110", "Kidneys fine")
m(PC, "lab", "potassium", "Potassium 3.9 3.5–5.3 Normal", "3.9", "3.5–5.3", "Normal")
m(PC, "lab", "urine_dipstick", "Urine dipstick Ketones +, otherwise negative Negative Not eating (starvation ketones); no infection/blood", "Ketones +, otherwise negative", "Negative", "Not eating")
m(PC, "imaging", "ultrasound",
  "Ultrasound (60–120 min later): 'Non-compressible appendix 9 mm, surrounding fat inflamed, no free fluid, no abscess.' → Positive — simple (uncomplicated) appendicitis.",
  "Non-compressible appendix 9 mm, surrounding fat inflamed, no free fluid, no abscess", "", "Positive — simple (uncomplicated) appendicitis")
PD = "VAR-S2-PD"
m(PD, "lab", "lactate", "Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling). CT: perforated appendix with free fluid.", "3.8", "", "raised — organs short of oxygen")
m(PD, "lab", "wbc", "Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling). CT: perforated appendix with free fluid.", "19.8")
m(PD, "lab", "crp", "Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling). CT: perforated appendix with free fluid.", "210")
m(PD, "lab", "creatinine", "Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling). CT: perforated appendix with free fluid.", "142", "", "kidneys struggling")
m(PD, "imaging", "ct", "Results: lactate 3.8 (raised — organs short of oxygen); WBC 19.8; CRP 210; creatinine 142 (kidneys struggling). CT: perforated appendix with free fluid.",
  "perforated appendix with free fluid")
m(PD, "response", "after_treatment", "BP rises to 108/64 after fluids, HR 112, lactate falls to 2.4 on repeat, urine starts flowing.", "108/64; 112; 2.4", "", "")
m("VAR-S1-E", "vital", "vitals", "T 38.4, HR 108, BP 118/72, RR 20, SpO2 97%. Murphy's sign positive. WBC 15.2, CRP 96. Ultrasound: wall 6 mm, fluid around gallbladder.",
  "T 38.4, HR 108, BP 118/72, RR 20, SpO2 97%", "", "")
m("VAR-S1-E", "lab", "wbc_crp", "T 38.4, HR 108, BP 118/72, RR 20, SpO2 97%. Murphy's sign positive. WBC 15.2, CRP 96. Ultrasound: wall 6 mm, fluid around gallbladder.", "WBC 15.2, CRP 96")
m("VAR-S1-E", "imaging", "ultrasound", "T 38.4, HR 108, BP 118/72, RR 20, SpO2 97%. Murphy's sign positive. WBC 15.2, CRP 96. Ultrasound: wall 6 mm, fluid around gallbladder.", "wall 6 mm, fluid around gallbladder")

# ------------------------------------------------------------------ the Usman deterioration timeline (Part 13, Example Patient D)

tl = Table("case_timelines", 2500, ["case_id", "time", "v_hr", "v_bp", "v_rr", "v_spo2", "v_temp", "v_acvpu", "v_news2_book", "v_what"], "Timeline row (Example Patient D)",
           "Part 13 Example Patient D table", evidence="D", size=20)
tl.add("14:00", "14:00 102 124/78 20 97% 38.3 A 2 Infection, coping. Workup started.", "auto", case_id="VAR-S2-PD", time="14:00", v_hr="102", v_bp="124/78", v_rr="20",
       v_spo2="97", v_temp="38.3", v_acvpu="A", v_news2_book="2", v_what="Infection, coping. Workup started.")
tl.add("14:45", "14:45 (no fluids/antibiotics given yet) 116 112/70 22 96% 38.9 A 5 Something has changed. Heart faster, BP lower, breathing faster, fever higher.", "auto",
       case_id="VAR-S2-PD", time="14:45", v_hr="116", v_bp="112/70", v_rr="22", v_spo2="96", v_temp="38.9", v_acvpu="A", v_news2_book="5",
       v_what="Something has changed. Heart faster, BP lower, breathing faster, fever higher.", note="Untreated: no fluids or antibiotics given yet.")
tl.add("15:15", "15:15 128 92/58 26 94% 39.2 A 10 Sepsis with poor circulation — EMERGENCY", "auto", case_id="VAR-S2-PD", time="15:15", v_hr="128", v_bp="92/58", v_rr="26",
       v_spo2="94", v_temp="39.2", v_acvpu="A", v_news2_book="10", v_what="Sepsis with poor circulation — EMERGENCY")
tl.add("15:45", "15:45 if nobody acts 138 82/48 30 91% 39.4 Confused 16 Septic shock — near collapse", "auto", case_id="VAR-S2-PD", time="15:45", v_hr="138", v_bp="82/48", v_rr="30",
       v_spo2="91", v_temp="39.4", v_acvpu="Confused", v_news2_book="16", v_what="Septic shock — near collapse",
       note="If nobody acts. The NEWS2 table gives 17 for these numbers, the book prints 16: see ISS-0016.")

TABLES = [seeds, meas, tl]
OFFICIAL_FILES = {}
