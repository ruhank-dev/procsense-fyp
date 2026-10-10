"""Which picture result each case gives when a test is ordered (imaging library, ADR-0004).

Rule: a case lists only the PROBLEM results it is built around. When the trainee orders a test the case does not list, the engine returns that test's CLEAN (no problem) result.
The engine never edits a picture: it picks the finding code (from this table, or from a scenario event that changes it) and then picks an approved picture with that code.
`d_basis` says where each row comes from: 'book' = the walkthrough's story states the result; 'proposed' = the story names the test but not its result, so the code is the scenario
library's proposal for the clinician. Both are approved on the owner's statement (docs/research/imaging_scenarios.md).
"""
from framework import Table

ci = Table("case_imaging", 3400, ["case_id", "test", "d_finding_code", "d_basis", "d_note"], "Case imaging result", "ProcSense imaging library", evidence="B", size=80)
URL = "docs/research/imaging_scenarios.md"
M = "Result a case gives when this test is ordered; tests not listed for a case return the clean result (see tools/content/imaging_library.py)."


def row(case, test, code, basis, note):
    ci.add_derived(f"{case}.{test}", "ProcSense imaging library (ADR-0004)", URL, M, case_id=case, test=test, d_finding_code=code, d_basis=basis, d_note=note)


row("VAR-S1-A", "ultrasound_gallbladder", "US-GB-01", "book", "Clinic ultrasound in the history: several stones, wall 2 mm, duct 4 mm")
row("VAR-S1-PB", "ultrasound_gallbladder", "US-GB-03", "book", "Bile duct 9 mm")
row("VAR-S1-PB", "mrcp", "MRCP-01", "proposed", "The story offers MRCP for an intermediate-risk duct; a stone is the result that keeps the ERCP pathway testable")
row("VAR-S1-E", "ultrasound_gallbladder", "US-GB-02", "book", "Inflamed gallbladder")
row("STK-S1-D", "ultrasound_gallbladder", "US-GB-04", "proposed", "Stacked case that ends on the ERCP pathway: stone in the duct")
row("VAR-S2-PC", "ultrasound_appendix", "US-AP-01", "book", "Positive, simple appendicitis")
row("VAR-S2-PD", "ct_appendix_level", "CT-AP-02", "proposed", "Septic patient, source search: complicated appendicitis with abscess")
row("VAR-S2-D", "ultrasound_appendix", "US-AP-04", "book", "Appendix not seen: equivocal")
row("VAR-S2-D", "ct_appendix_level", "CT-AP-01", "proposed", "CT scan is the senior's option after an equivocal ultrasound")
row("VAR-S2-H", "ultrasound_appendix", "US-AP-04", "proposed", "Ultrasound first")
row("VAR-S2-H", "mri_appendix_level", "MR-AP-01", "proposed", "MRI if unclear (CT avoided as first choice)")
row("STK-S2-B", "ct_appendix_level", "CT-AP-02", "proposed", "Stacked case that uses CT")
row("STK-S2-C", "chest_xray", "CXR-02", "proposed", "Aspiration suspected: chest X-ray shows consolidation")

# scenario cards and cases whose story names the result (the story text is in content/case_seeds.csv)
row("VAR-S1-D", "ecg", "ECG-03", "book", "Atrial fibrillation on apixaban")
row("VAR-S2-B", "ultrasound_pelvis", "US-PV-01", "book", "Ectopic pregnancy mimicking appendicitis")
row("VAR-S2-E", "ct_appendix_level", "CT-AP-04", "proposed", "Older patient: the story says bowel cancer or diverticulitis can mimic appendicitis and CT is preferred; diverticulitis here, a caecal mass is SC-20")
row("SC-1", "ultrasound_appendix", "US-AP-02", "proposed", "Burst appendix with sepsis")
row("SC-1", "ct_appendix_level", "CT-AP-02", "proposed", "Burst appendix with sepsis")
row("SC-2", "ultrasound_pelvis", "US-PV-01", "book", "Ruptured tubal ectopic pregnancy, a classic appendicitis mimic")
row("SC-3", "ultrasound_appendix", "US-AP-04", "book", "Ultrasound non-diagnostic")
row("SC-3", "mri_appendix_level", "MR-AP-01", "proposed", "Pregnant: MRI after a non-diagnostic ultrasound")
row("SC-5", "ecg", "ECG-10", "book", "HR 124 irregular: new fast AF")
row("SC-7", "chest_xray", "CXR-02", "proposed", "Drowsy and vomiting after morphine: aspiration risk")
row("SC-8", "ecg", "ECG-08", "proposed", "K 6.6 with sepsis and kidney injury")
row("SC-9", "ultrasound_gallbladder", "US-GB-04", "proposed", "Duct stone with obstruction and infection (bilirubin 85, fever)")
row("SC-9", "mrcp", "MRCP-01", "proposed", "Duct stone")
row("SC-11", "ct_appendix_level", "CT-AP-02", "proposed", "Untreated appendix perforated within hours")
row("SC-14", "ultrasound_appendix", "US-AP-04", "book", "Appendix not seen")
row("SC-14", "ct_appendix_level", "CT-AP-02", "proposed", "Deterioration overrides wait-and-see: CT shows abscess")
row("SC-20", "ct_appendix_level", "CT-AP-03", "proposed", "70-year-old with anaemia: caecal cancer presenting like appendicitis")

row("VAR-S1-H", "pregnancy_test", "PREG-01", "book", "Urine pregnancy test positive")
row("VAR-S2-B", "pregnancy_test", "PREG-01", "book", "Urine pregnancy test positive")
row("SC-2", "pregnancy_test", "PREG-01", "book", "hCG positive")
row("SC-3", "pregnancy_test", "PREG-01", "proposed", "Pregnant (appendicitis in pregnancy)")
row("VAR-S1-J", "blood_sample_appearance", "TUBE-01", "book", "Potassium 6.4 haemolysed")
row("SC-12", "blood_sample_appearance", "TUBE-01", "book", "K 6.1 haemolysed")
row("SC-19", "urine_dipstick", "DIP-02", "proposed", "Ketones 3.5 on an SGLT2 inhibitor with sepsis")

row("VAR-S1-L", "ecg", "ECG-10", "book", "HR 128 irregularly irregular: new fast AF")
row("STK-S1-B", "ecg", "ECG-03", "book", "Known atrial fibrillation, rate 84")
row("STK-S1-C", "ecg", "ECG-08", "book", "Tall peaked T waves with potassium 6.6")
row("STK-S2-D", "ultrasound_appendix", "US-AP-04", "book", "Appendix not visualised")
row("STK-S2-D", "mri_appendix_level", "MR-AP-01", "book", "MRI after non-diagnostic ultrasound (no ionising radiation)")
row("VAR-S2-J", "urine_dipstick", "DIP-01", "book", "Leucocytes 2+, nitrite negative, trace blood")
row("VAR-S2-L", "ultrasound_appendix", "US-AP-01", "proposed", "Simple appendicitis with no stone inside")

# every planned (S1) patient is booked for gallbladder removal because of gallstones, and every simple S2 appendicitis case shows the same on ultrasound
for c in ("VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J"):
    row(c, "ultrasound_gallbladder", "US-GB-01", "proposed", "Booked for gallbladder removal for gallstones: the clinic ultrasound shows simple gallstones")
for c in ("VAR-S2-F", "VAR-S2-G"):
    row(c, "ultrasound_appendix", "US-AP-01", "proposed", "Appendicitis managed like Example Patient C: ultrasound shows a simple inflamed appendix")

TABLES = [ci]
OFFICIAL_FILES = {}
