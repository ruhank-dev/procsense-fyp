"""Which picture result each 'what if' can produce (imaging library, ADR-0004).

Every what-if in the walkthrough (Part 14) that says to look at an ECG, a chest film or an imaging result can end in several different findings depending on the cause the
scenario chooses ('the cause' column). The engine chooses the cause, so the finding code, so the picture: it never edits a picture. The what-if is found by a phrase of its title in
content/whatif_library.csv. Each row is a proposal from the what-if's own 'what it means' and 'check next' text (the book names the tests and the causes, not the picture), approved on
the owner's statement.
"""
import csv
from pathlib import Path

from framework import Table

ROOT = Path(__file__).resolve().parents[3]
URL = "docs/research/imaging_scenarios.md"
M = "A scenario that raises this what-if shows this result when the named test is ordered, for the cause named (the cause comes from the what-if's own list of meanings)."

wi = Table("whatif_imaging", 3500, ["whatif_id", "test", "d_cause", "d_finding_code"], "What-if picture result", "ProcSense imaging library", evidence="B", size=100)

with open(ROOT / "content" / "whatif_library.csv", encoding="utf-8", newline="") as f:
    IDS = {r["title"]: r["id"] for r in csv.DictReader(f)}


def wid(phrase: str) -> str:
    hits = [i for t, i in IDS.items() if phrase in t]
    assert len(hits) == 1, (phrase, hits)
    return hits[0]


def row(phrase, test, cause, code):
    w = wid(phrase)
    wi.add_derived(f"{w}.{test}.{code}", "ProcSense imaging library (ADR-0004)", URL, M, whatif_id=w, test=test, d_cause=cause, d_finding_code=code)


H, S = "heart rate is very high", "heart rate is very slow"
row(H, "ecg", "new atrial fibrillation (sepsis, dehydration, salts)", "ECG-10")
row(H, "ecg", "fast regular narrow-complex rhythm (SVT)", "ECG-06")
row(H, "ecg", "sinus tachycardia (pain, fever, bleeding, dehydration)", "ECG-02")
row(S, "ecg", "heart block", "ECG-05")
row(S, "ecg", "beta-blocker or digoxin: sinus bradycardia", "ECG-04")
row(S, "ecg", "very high potassium", "ECG-08")
row("oxygen saturation is low", "chest_xray", "aspiration or chest infection", "CXR-02")
row("oxygen saturation is low", "chest_xray", "fluid overload after many boluses", "CXR-06")
row("oxygen saturation is low", "chest_xray", "collapsed lung areas from pain", "CXR-07")
row("oxygen saturation is low", "chest_xray", "no chest cause (probe error, opioid)", "CXR-01")
row("tummy becomes rigid", "chest_xray", "perforation: free air under the diaphragm", "CXR-08")
row("tummy becomes rigid", "ct_abdomen_midlevel", "perforation", "CT-AB-02")
row("tummy becomes rigid", "ct_abdomen_midlevel", "blocked bowel", "CT-AB-01")
row("tummy becomes rigid", "ct_appendix_level", "burst appendix with abscess", "CT-AP-02")
row("tummy becomes rigid", "ct_gallbladder_level", "burst or gas-forming gallbladder", "CT-GB-01")
row("tummy becomes rigid", "ultrasound_pelvis", "twisted ovary", "US-PV-02")
row("tummy becomes rigid", "ultrasound_pelvis", "ruptured ectopic pregnancy", "US-PV-01")
row("potassium is high", "ecg", "real high potassium", "ECG-08")
row("potassium is high", "ecg", "faulty sample: the ECG is normal", "ECG-01")
row("potassium is low", "ecg", "real low potassium", "ECG-09")
row("What if the ECG is abnormal", "ecg", "new atrial fibrillation", "ECG-10")
row("What if the ECG is abnormal", "ecg", "heart attack: ST elevation", "ECG-07")
row("What if the patient is pregnant", "ultrasound_pelvis", "ectopic pregnancy until proven otherwise", "US-PV-01")
row("What if the patient is pregnant", "ultrasound_pelvis", "normal early pregnancy: normal pelvis", "US-PV-00")
row("What if the patient is pregnant", "mri_appendix_level", "appendicitis in pregnancy", "MR-AP-01")
row("What if imaging is inconclusive", "ultrasound_appendix", "appendix not visualised", "US-AP-04")
row("What if imaging is inconclusive", "ultrasound_gallbladder", "possible early cholecystitis", "US-GB-08")
row("What if imaging is inconclusive", "ct_appendix_level", "next imaging shows appendicitis", "CT-AP-01")
row("What if imaging is inconclusive", "ct_appendix_level", "next imaging shows no appendicitis", "CT-AP-00")
row("What if liver tests are abnormal", "mrcp", "stone in the duct", "MRCP-01")
row("What if liver tests are abnormal", "mrcp", "no duct stone", "MRCP-00")
row("What if liver tests are abnormal", "ultrasound_gallbladder", "duct wider than 6 mm", "US-GB-03")
row("What if liver tests are abnormal", "ultrasound_gallbladder", "stone seen in the duct", "US-GB-04")
row("What if bilirubin is high", "mrcp", "blocked duct", "MRCP-01")
row("What if the patient deteriorates?", "ct_appendix_level", "perforation or abscess", "CT-AP-02")
row("What if the patient deteriorates?", "ct_abdomen_midlevel", "perforation", "CT-AB-02")
row("What if the patient has a fever", "ultrasound_gallbladder", "inflamed gallbladder", "US-GB-02")
row("What if the patient has a fever", "chest_xray", "chest infection", "CXR-02")
row("What if the patient becomes septic", "ct_appendix_level", "source: appendix abscess", "CT-AP-02")
row("What if the patient becomes septic", "chest_xray", "source: chest", "CXR-02")
row("What if the patient becomes septic", "ultrasound_kidney", "source: obstructed kidney", "US-RN-01")
row("What if the patient becomes confused", "chest_xray", "low oxygen from chest infection", "CXR-02")

row("tummy becomes rigid", "ultrasound_gallbladder", "burst gallbladder", "US-GB-05")
row("tummy becomes rigid", "ultrasound_appendix", "burst appendix with abscess", "US-AP-02")
row("What if liver tests are abnormal", "ultrasound_gallbladder", "mass or polyp: possible gallbladder cancer", "US-GB-06")
row("What if imaging is inconclusive", "ultrasound_gallbladder", "sludge only: diagnosis less certain", "US-GB-07")
row("What if the patient is pregnant", "ultrasound_appendix", "no ectopic: another pelvic finding (ovarian cyst)", "US-AP-03")
row("oxygen saturation is low", "chest_xray", "pleural effusion", "CXR-03")
row("oxygen saturation is low", "chest_xray", "collapsed lung (pneumothorax)", "CXR-04")
row("oxygen saturation is low", "chest_xray", "long-standing heart failure: enlarged heart", "CXR-05")
row("What if the patient is pregnant", "ultrasound_pelvis", "twisted ovary", "US-PV-02")
row("What if the patient becomes septic", "ct_appendix_level", "older patient: caecal mass", "CT-AP-03")
row("What if the patient deteriorates?", "ct_appendix_level", "older patient: right-sided diverticulitis", "CT-AP-04")
row("What if the patient has a fever", "ultrasound_kidney", "urine infection with a stone", "US-RN-01")
row("What if the patient has a fever", "ultrasound_appendix", "appendicitis", "US-AP-01")

row("What if the patient is pregnant", "pregnancy_test", "positive: normal early pregnancy or ectopic", "PREG-01")
row("What if the patient has a fever", "urine_dipstick", "urine infection", "DIP-01")
row("blood sugar is high", "urine_dipstick", "ketones in the urine: suspected ketoacidosis", "DIP-02")
row("What if the patient becomes septic", "urine_dipstick", "source: urine infection", "DIP-01")
row("tummy becomes rigid", "urine_dipstick", "stone: blood in the urine", "DIP-03")
row("potassium is high", "blood_sample_appearance", "faulty sample: haemolysed", "TUBE-01")
row("What if a test result doesn't make sense", "blood_sample_appearance", "haemolysed sample", "TUBE-01")
row("What if a test result doesn't make sense", "blood_sample_appearance", "lipaemic sample", "TUBE-02")
row("What if a critical result is phoned through", "blood_sample_appearance", "check the sample: haemolysed", "TUBE-01")

row("What if a test result doesn't make sense", "ecg", "implausible potassium in a well patient: the ECG is normal", "ECG-01")
row("What if a critical result is phoned through", "ecg", "real critical potassium", "ECG-08")
row("What if a critical result is phoned through", "ecg", "critical result is spurious: the ECG is normal", "ECG-01")
TABLES = [wi]
OFFICIAL_FILES = {}
