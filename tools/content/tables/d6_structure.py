"""D6 (p0-medical-research-data s08): WHO checklist, the answers of the 15 steps, the two 20-stage flow maps, the player view, the 24 VR reasoning cards, the glossary.

Sources: Part 15 (WHO checklist, 2009 wording and ProcSense extensions kept apart), Part 11 (steps), Parts F and G (flow), Part 16 (player view), Part C (VR cards),
Part 18 (glossary). Text rows are read by the extractor and re-checked here: every row's text must be on the pages it cites.
Findings: the book answers 270 of 315 question slots (ISS-0003); the glossary does not mark where a term ends and its meaning starts, so each entry is kept as the
book's line.
"""
import re
import sys
from pathlib import Path

from framework import FRAGMENT_SEPARATOR, Table

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import book  # noqa: E402
import extract_walkthrough as ex  # noqa: E402

SEP = FRAGMENT_SEPARATOR
DATA = ex.extract()

# ------------------------------------------------------------------ WHO Surgical Safety Checklist (Part 15)

who = Table("who_checklist", 2150, ["phase", "order", "v_item", "extension", "v_meaning", "v_s1", "v_s2", "v_if_no", "d_expected_from", "d_critical"],
            "WHO checklist item", "Part 15 'The WHO Surgical Safety Checklist, explained'", evidence="A", size=40)
DERIVED = "Derived: expected_from names the state the checklist engine will check, and critical is yes where the book says STOP; both are design readings, not book text."


def wi(phase, order, quote, item, meaning, s1="", s2="", if_no="", frm="", crit="", ext="no", evidence="", note=""):
    who.add(f"{phase}.{order:02d}", quote, "auto", phase=phase, order=order, v_item=item, extension=ext, v_meaning=meaning, v_s1=s1, v_s2=s2, v_if_no=if_no,
            d_expected_from=frm, d_critical=crit, evidence=evidence, note=(note + " " + DERIVED).strip())


wi("sign_in", 1, "Has the patient confirmed his/her identity, site, procedure and consent? Patient says who they are and what is being done; consent present Yes Yes STOP — resolve",
   "Has the patient confirmed his/her identity, site, procedure and consent?", "Patient says who they are and what is being done; consent present", "Yes", "Yes", "STOP — resolve",
   "identity_confirmed; consent_valid", "yes")
wi("sign_in", 2, "Is the site marked? (Yes / Not applicable) Skin mark where laterality/level applies Usually not applicable CONTE XT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION Usually not applicable CONTEXT-DEPENDENT — INSTITUTIONAL PROTOCOL / CLINICIAN CONFIRMATION Ask surgeon",
   "Is the site marked? (Yes / Not applicable)", "Skin mark where laterality/level applies", "Usually not applicable", "Usually not applicable", "Ask surgeon", "site_marking", "no",
   evidence="C", note="Site marking is institution-dependent (institutional decision 8).")
wi("sign_in", 3, "Is the anaesthesia machine and medication check complete? Machine and drugs checked by the anaesthetic team Yes Yes STOP — complete check",
   "Is the anaesthesia machine and medication check complete?", "Machine and drugs checked by the anaesthetic team", "Yes", "Yes", "STOP — complete check", "anaesthesia_machine_checked", "yes")
wi("sign_in", 4, "Is the pulse oximeter on the patient and functioning? Oxygen monitor reading with waveform Yes Yes STOP — replace",
   "Is the pulse oximeter on the patient and functioning?", "Oxygen monitor reading with waveform", "Yes", "Yes", "STOP — replace", "spo2_probe_attached", "yes")
wi("sign_in", 5, "Does the patient have a known allergy? (No / Yes) Any allergy No/Yes + what No/Yes + what Prepare alternatives",
   "Does the patient have a known allergy? (No / Yes)", "Any allergy", "No/Yes + what", "No/Yes + what", "Prepare alternatives", "allergies_known", "no")
wi("sign_in", 6, "Difficult airway or aspiration risk? (No / Yes, and equipment/assistance available) Hard intubation? Full stomach? Usually no Yes — aspiration risk; RSI equipment, suction and assistance available Plan, don't stop",
   "Difficult airway or aspiration risk? (No / Yes, and equipment/assistance available)", "Hard intubation? Full stomach?", "Usually no", "Yes — aspiration risk; RSI equipment, suction and assistance available",
   "Plan, don't stop", "airway_and_aspiration_assessment", "no")
wi("sign_in", 7, "Risk of >500 mL blood loss (7 mL/kg in children)? (No / Yes, and two IVs/central access and fluids planned) Major bleeding expected? Usually no Usually no; yes if bleeding/coagulopathy Two IVs + fluids/blood planned",
   "Risk of >500 mL blood loss (7 mL/kg in children)? (No / Yes, and two IVs/central access and fluids planned)", "Major bleeding expected?", "Usually no", "Usually no; yes if bleeding/coagulopathy",
   "Two IVs + fluids/blood planned", "blood_loss_risk", "no")
wi("time_out", 1, "Confirm all team members have introduced themselves by name and role Everyone knows who's who and can speak up Yes Yes",
   "Confirm all team members have introduced themselves by name and role", "Everyone knows who's who and can speak up", "Yes", "Yes", "", "team_introduced", "no")
wi("time_out", 2, "Confirm the patient's name, procedure, and where the incision will be made Final identity/procedure/site check Yes Yes",
   "Confirm the patient's name, procedure, and where the incision will be made", "Final identity/procedure/site check", "Yes", "Yes", "", "identity_confirmed; procedure_site_confirmed", "no")
wi("time_out", 3, "Has antibiotic prophylaxis been given within the last 60 minutes? (Yes / Not applicable) Infection-prevention timing Only if indicated; otherwise 'Not applicable' Yes — time confirmed; redose decision if needed",
   "Has antibiotic prophylaxis been given within the last 60 minutes? (Yes / Not applicable)", "Infection-prevention timing", "Only if indicated; otherwise 'Not applicable'",
   "Yes — time confirmed; redose decision if needed", "", "antibiotic_given_within_60_min", "no")
wi("time_out", 4, "Anticipated critical events — To surgeon: What are the critical or non-routine steps? How long will the case take? What is the anticipated blood loss? Team prepared ~60 min; minimal ~45–60 min; minimal",
   "Anticipated critical events — To surgeon: What are the critical or non-routine steps? How long will the case take? What is the anticipated blood loss?", "Team prepared",
   "~60 min; minimal", "~45–60 min; minimal", "", "surgeon_critical_events_briefed", "no")
wi("time_out", 5, "To anaesthetist: Are there any patient-specific concerns? e.g. OSA, diabetes, aspiration As relevant Aspiration, sepsis, kidneys",
   "To anaesthetist: Are there any patient-specific concerns?", "e.g. OSA, diabetes, aspiration", "As relevant", "Aspiration, sepsis, kidneys", "", "anaesthetist_concerns_briefed", "no")
wi("time_out", 6, "To nursing team: Has sterility (including indicator results) been confirmed? Are there equipment issues or any concerns? Instruments truly sterile; equipment works Tower working Lap + open sets",
   "To nursing team: Has sterility (including indicator results) been confirmed? Are there equipment issues or any concerns?", "Instruments truly sterile; equipment works", "Tower working", "Lap + open sets",
   "", "sterility_confirmed", "no")
wi("time_out", 7, "Is essential imaging displayed? (Yes / Not applicable) Scans on screen Ultrasound ± MRCP Ultrasound/CT",
   "Is essential imaging displayed? (Yes / Not applicable)", "Scans on screen", "Ultrasound ± MRCP", "Ultrasound/CT", "", "imaging_displayed", "no")
wi("sign_out", 1, "Nurse verbally confirms: the name of the procedure What was actually done is recorded", "Nurse verbally confirms: the name of the procedure", "What was actually done is recorded", "", "", "", "procedure_name_recorded", "no")
wi("sign_out", 2, "Nurse verbally confirms: completion of instrument, sponge and needle counts Nothing left inside",
   "Nurse verbally confirms: completion of instrument, sponge and needle counts", "Nothing left inside", "", "", "", "counts_correct", "no")
wi("sign_out", 3, "Nurse verbally confirms: specimen labelling (read specimen labels aloud, including patient name) Gallbladder/appendix goes to the lab correctly",
   "Nurse verbally confirms: specimen labelling (read specimen labels aloud, including patient name)", "Gallbladder/appendix goes to the lab correctly", "", "", "", "specimen_labelled", "no")
wi("sign_out", 4, "Nurse verbally confirms: whether there are any equipment problems to be addressed Faults reported",
   "Nurse verbally confirms: whether there are any equipment problems to be addressed", "Faults reported", "", "", "", "equipment_problems_recorded", "no")
wi("sign_out", 5, "To surgeon, anaesthetist and nurse: What are the key concerns for recovery and management of this patient? Handover to recovery",
   "To surgeon, anaesthetist and nurse: What are the key concerns for recovery and management of this patient?", "Handover to recovery", "", "", "", "recovery_concerns_briefed", "no")
EXT = "ProcSense educational extensions (NOT WHO checklist items) Extension Why ProcSense adds it Basis "
for i, (item, why, basis, frm) in enumerate((
        ("VTE prophylaxis confirmed", "Reinforces NICE NG89 teaching", "NICE NG89; included in some national adaptations (e.g. NPSA UK 2010)", "vte_prophylaxis_planned"),
        ("Capillary glucose recorded for patients with diabetes", "Reinforces CPOC teaching", "CPOC 2021/2023", "glucose_recorded_if_diabetic"),
        ("Patient temperature ≥36.0 °C before induction", "Reinforces hypothermia prevention", "NICE CG65", "temperature_at_least_36"),
        ("Pregnancy status documented where relevant", "Reinforces NICE NG45", "NICE NG45", "pregnancy_status_documented"),
        ("Team brief before the list / debrief after", "Teamwork teaching", "UK 'Five steps to safer surgery' (NPSA 2010)", "team_brief_done")), start=1):
    wi("extension", i, f"{item} {why} {basis}", item, why, "", "", "", frm, "no", ext="yes", note="ProcSense educational extension, NOT a WHO item: the UI shows it apart.")

# ------------------------------------------------------------------ the 15 steps, question by question (Part 11)

steps = Table("steps_21_questions", 1500, ["step", "step_title", "question_no", "question", "v_answer"], "Step answer", "Part 11 'The steps, one by one'", evidence="B", size=300)
for s in DATA["steps"]:
    for q, answer in s["questions"].items():
        n = ex.QUESTIONS.index(q) + 1
        steps.add(f"step{s['number']:02d}.q{n:02d}", book.normalise(answer), "span", step=s["number"], step_title=s["title"], question_no=n, question=q, v_answer=book.normalise(answer),
                  note="The book answers 270 of the 315 question slots; a missing question is not applicable to that step (ISS-0003).")

# ------------------------------------------------------------------ the two 20-stage flow maps (Parts F and G)

flow = Table("flow_stages", 1800, ["scenario", "stage", "title", "v_medical_detail", "v_trainee_action", "v_sees", "v_normal_pathway", "v_abnormal_branches", "v_escalation",
                                   "v_equipment_response", "v_if_missed", "v_source", "v_validation"], "Flow stage", "Parts F and G, scenario master maps", evidence="B", size=50)
LABELS = {k: book.normalise(label) for k, label in ex.FLOW_FIELDS}
for scenario in ("S1", "S2"):
    for st in DATA["flow"][scenario]:
        frags = [f"{st['number']}. {st['title']}"] + [f"{LABELS[k]} {v}" for k, v in st["fields"].items()]
        flow.add(f"{scenario}.{st['number']:02d}", SEP.join(frags), "span", scenario=scenario, stage=st["number"], title=st["title"],
                 v_medical_detail=st["fields"]["medical_detail"], v_trainee_action=st["fields"]["trainee_action"], v_sees=st["fields"]["sees"],
                 v_normal_pathway=st["fields"]["normal_pathway"], v_abnormal_branches=st["fields"]["abnormal_branches"], v_escalation=st["fields"]["escalation"],
                 v_equipment_response=st["fields"]["equipment_response"], v_if_missed=st["fields"]["if_missed"], v_source=st["fields"]["source"],
                 v_validation=st["fields"]["validation"], note="Stage numbers differ between Parts 3, 11, 16, F and G (WT-I09); the 15 steps are the canonical ids, stages only order them.")

# ------------------------------------------------------------------ player view (Part 16)

LINES = book.WALKTHROUGH.read_text(encoding="utf-8").splitlines()


def block(start_pattern: str, end_pattern: str) -> list[str]:
    a = next(i for i, ln in enumerate(LINES) if re.match(start_pattern, ln))
    b = next(i for i in range(a + 1, len(LINES)) if re.match(end_pattern, LINES[i]))
    return [ln for ln in LINES[a:b] if not ex.FOOTER.match(ln.strip())]


PV_LABELS = [("on_screen", "On screen / in front of the player"), ("patient_says", "Patient — what they say"), ("monitor", "Monitor — numbers"), ("interaction", "Interaction"),
             ("information", "Information revealed"), ("decision", "Decision"), ("correct", "Correct action →"), ("incorrect", "Incorrect action →"),
             ("missed", "Missed abnormality →"), ("escalation", "Escalation →")]
pv = Table("player_view", 1850, ["stage", "title", "v_on_screen", "v_patient_says", "v_monitor", "v_interaction", "v_information", "v_decision", "v_correct", "v_incorrect",
                                 "v_missed", "v_escalation"], "Player-view stage", "Part 16 'What the player actually sees'", evidence="B", size=20)
region = block(r"^# Part 16 — ", r"^### End states")
heads = [i for i, ln in enumerate(region) if ln.startswith("### Stage")]
for n, i in enumerate(heads):
    chunk = region[i:heads[n + 1] if n + 1 < len(heads) else len(region)]
    title = chunk[0][4:].strip()
    text = book.normalise(" ".join(chunk[1:]))
    pos, cursor = [], 0
    for key, label in PV_LABELS:
        at = text.find(label, cursor)
        if at < 0:
            raise SystemExit(f"player_view {title}: label not found: {label}")
        pos.append((at, key, label))
        cursor = at + len(label)
    vals = {key: text[at + len(label):(pos[k + 1][0] if k + 1 < len(pos) else len(text))].strip() for k, (at, key, label) in enumerate(pos)}
    frags = [title] + [f"{label} {vals[key]}" for _, key, label in pos]
    pv.add(f"stage{n + 1:02d}", SEP.join(frags), "span", stage=re.match(r"Stages? ([\d–]+)", title).group(1), title=title,
           **{f"v_{k}": v for k, v in vals.items()})

# ------------------------------------------------------------------ VR clinical reasoning cards (Part C)

VR_LABELS = ["Why", "Trainee action", "What the trainee sees", "Information", "Interpretation", "Decision", "Correct action", "Incorrect action", "Missed abnormality",
             "Escalation", "Consequence", "Next step"]
vr = Table("vr_cards", 1870, ["card", "title", "v_" + "why", "v_trainee_action", "v_sees", "v_information", "v_interpretation", "v_decision", "v_correct", "v_incorrect",
                              "v_missed", "v_escalation", "v_consequence", "v_next_step"], "VR reasoning card", "Part C 'Expanded VR Clinical Reasoning'", evidence="B", size=30)
KEYS = ["why", "trainee_action", "sees", "information", "interpretation", "decision", "correct", "incorrect", "missed", "escalation", "consequence", "next_step"]
for card in DATA["vr_cards"]:
    body = [ln for ln in card["body"].split("\n") if ln.strip()]
    fields, current = {}, None
    for ln in body:
        hit = next((lab for lab in VR_LABELS if ln == lab or ln.startswith(lab + " ")), None)
        if hit and hit not in fields:
            fields[hit] = ln[len(hit):].strip()
            current = hit
        elif current:
            fields[current] += " " + ln.strip()
    missing = [lab for lab in VR_LABELS if lab not in fields]
    clean = {lab: book.normalise(fields[lab]) for lab in VR_LABELS if lab in fields}
    frags = [f"{card['id']} {card['title']}"] + [f"{lab} {clean[lab]}" for lab in VR_LABELS if lab in clean]
    vr.add(card["id"], SEP.join(frags), "span", card=card["id"], title=card["title"],
           **{f"v_{k}": clean.get(lab, "") for k, lab in zip(KEYS, VR_LABELS)},
           note=("Card has no: " + ", ".join(missing) + ". ") if missing else "")

# ------------------------------------------------------------------ glossary (Part 18)

gl = Table("glossary", 1900, ["entry_no", "v_entry"], "Glossary entry", "Part 18 'Dictionary of every medical word and abbreviation'", evidence="B", size=250)
region = block(r"^# Part 18 — ", r"^# PART B — ")
start = next(i for i, ln in enumerate(region) if ln.startswith("Word / abbreviation"))
entries, current = [], ""
for ln in region[start + 1:]:
    ln = ln.strip().rstrip("\\").strip()
    if not ln:
        continue
    current = (current + " " + ln).strip()
    if current.endswith((".", ")")) or ln.endswith("."):
        entries.append(book.normalise(current))
        current = ""
if current:
    entries.append(book.normalise(current))
for i, e in enumerate(entries, start=1):
    gl.add(f"g{i:03d}", e, "span", entry_no=i, v_entry=e,
           note="The book does not mark where the word ends and its meaning starts, so the entry is kept as printed." if i == 1 else "")

TABLES = [who, steps, flow, pv, vr, gl]
OFFICIAL_FILES = {}
