"""D12 (p0-medical-research-data s14): historical records for the EMR, and distractor patients.

Only facts the walkthrough states are used: each record row holds the book's own words (checked against the page) in `v_fact`; `d_body` restates exactly that fact in
document form and adds no clinical fact. Dates are offsets from the case's 'today' so cases stay consistent. The book gives few historical facts, so few records exist:
more would have to be written by the clinician (docs/research/case_content_gaps.md). Distractor patients (so that 'search the right patient' is a real task) are authored
identities with NO clinical history, flagged synthetic; names and numbers are made up and map to no real person.
"""
from framework import Table

hist = Table("emr_history", 3000, ["case_id", "doc_type", "title", "date_offset", "v_fact", "d_body", "synthetic", "status"], "EMR historical record", "Part 12 example patients, history facts", evidence="D", size=30)


def rec(key, quote, case, doc_type, title, offset, fact, body, note=""):
    hist.add(key, quote, "auto", case_id=case, doc_type=doc_type, title=title, date_offset=offset, v_fact=fact, d_body=body, synthetic="yes", status="DRAFT",
             note=(note + " Synthetic record restating the book's fact; DRAFT until the clinician approves it.").strip())


rec("A.previous_anaesthetic", "Previous anaesthetic: caesarean section 2012, no problems.", "VAR-S1-A", "anaesthetic_history", "Previous anaesthetic record", "2012 (about 13 years before today)",
    "caesarean section 2012, no problems", "Previous anaesthetic: caesarean section 2012, no problems.")
rec("A.clinic_ultrasound", "(Ultrasound from clinic) Several stones; wall 2 mm; duct 4 mm", "VAR-S1-A", "imaging_report", "Clinic ultrasound report (before booking)", "before the booking (date not stated in the book)",
    "Several stones; wall 2 mm; duct 4 mm", "Ultrasound (clinic): Several stones; wall 2 mm; duct 4 mm. Appearance: simple gallstones, duct normal.",
    note="Finding code US-GB-01. Matches content/ultrasound_findings.csv (wall 3 mm or less, duct 6 mm or less).")
rec("C.hba1c", "HbA1c 58 mmol/mol (2 months ago)", "VAR-S1-C", "lab_result", "Previous HbA1c", "2 months before today", "HbA1c 58 mmol/mol (2 months ago)",
    "HbA1c 58 mmol/mol, taken 2 months ago.")
rec("D.egfr", "Kidney function eGFR 68.", "VAR-S1-D", "lab_result", "Kidney function", "not stated in the book", "eGFR 68", "Kidney function: eGFR 68.")

dis = Table("emr_distractors", 3050, ["patient_id", "d_name", "d_dob", "d_mrn", "d_location", "d_note"], "EMR distractor patient", "Authored synthetic identities (no clinical content)", size=20)
NOTE = ("Authored synthetic identity so that searching for the right patient is a real task. The name, date of birth and number are invented and belong to no real person; "
        "there is no clinical history. The ward and bed are invented too, so the EMR ward list shows where each patient is (DT-264).")
for i, (name, dob, mrn, where) in enumerate((("Hassan Raza", "1971-08-02", "MR 204117", "Ward 6, bed 4"), ("Mariam Siddiqui", "1992-11-19", "MR 118842", "Ward 9, bed 11"),
                                             ("Ayesha Khan Malik", "1984-03-15", "MR 102983", "Ward 6, bed 2"), ("Ayesha Kaan", "1984-03-14", "MR 109238", "Day surgery unit, bay 3"),
                                             ("Imran Qureshi", "1957-05-30", "MR 187764", "Ward 4, bed 7"), ("Sana Iqbal", "1985-01-26", "MR 150731", "Ward 9, bed 3")), start=1):
    dis.add_derived(f"p{i}", "ProcSense authored synthetic identities", "", NOTE + (" Deliberately similar to Mrs Ayesha Khan (name or date of birth one digit off): a near-miss on search." if i in (3, 4) else ""),
                    patient_id=f"DIST-{i:02d}", d_name=name, d_dob=dob, d_mrn=mrn, d_location=where, d_note="no clinical history; synthetic")

TABLES = [hist, dis]
OFFICIAL_FILES = {}
