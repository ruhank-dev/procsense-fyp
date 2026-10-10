"""D11 (ultrasound feature plan): the twelve ultrasound finding codes with the book's wording, and the numeric limits a matching report may use.

Source: walkthrough Part 6.3 (gallbladder, S1) and Part 6.4 (appendix, S2). The finding code, not the picture, drives the engine; a report may state only what its
code allows. `d_rules` is a derived machine form of the book's numbers (key op value, joined by ';'), used by the image/report validator.
"""
from framework import Table

findings = Table("ultrasound_findings", 2800, ["code", "scenario", "v_finding", "v_plain", "v_next", "d_rules"], "Ultrasound finding code", "Part 6.3 and 6.4 report tables", evidence="A", size=30)
D = "d_rules is a derived machine form of the numbers in the book's wording."


def f(code, scenario, quote, finding, plain, nxt, rules, note=""):
    findings.add(code, quote, "auto", code=code, scenario=scenario, v_finding=finding, v_plain=plain, v_next=nxt, d_rules=rules, note=(note + " " + D).strip())


f("US-GB-01", "S1", "Gallstones; thin gallbladder wall (3 mm or less); no fluid; bile duct 6 mm or less Simple gallstones Proceed (if liver tests normal)",
  "Gallstones; thin gallbladder wall (3 mm or less); no fluid; bile duct 6 mm or less", "Simple gallstones", "Proceed (if liver tests normal)", "stones=yes;wall_mm<=3;duct_mm<=6;fluid=no")
f("US-GB-02", "S1", "Stones + wall thicker than 4 mm, fluid around gallbladder, tender when the probe presses ('sonographic Murphy's sign'), gallbladder enlarged (long axis over 8 cm, short axis over 4 cm), stone stuck in its neck (Tokyo Guidelines imaging criteria) Inflamed gallbladder (acute cholecystitis) Cancel planned list → acute pathway: antibiotics, senior, early operation usually within days",
  "Stones + wall thicker than 4 mm, fluid around gallbladder", "Inflamed gallbladder (acute cholecystitis)", "Cancel planned list → acute pathway: antibiotics, senior, early operation usually within days",
  "wall_mm>4", "Any of the listed signs supports the finding; the wall limit is the only number that every image of this code must meet.")
f("US-GB-03", "S1", "Bile duct wider than 6 mm (older people and people who've had gallbladder surgery may normally be a bit wider) Possible blockage of the duct Check bilirubin; MRCP/ERCP; senior",
  "Bile duct wider than 6 mm", "Possible blockage of the duct", "Check bilirubin; MRCP/ERCP; senior", "duct_mm>6")
f("US-GB-04", "S1", "Stone seen in the bile duct Duct stone (high risk) ERCP pathway; planned operation is usually deferred",
  "Stone seen in the bile duct", "Duct stone (high risk)", "ERCP pathway; planned operation is usually deferred", "duct_stone=yes")
f("US-GB-05", "S1", "Gas in the wall, pus pocket, gallbladder burst Severe complicated inflammation Urgent senior; CT; maybe drainage",
  "Gas in the wall, pus pocket, gallbladder burst", "Severe complicated inflammation", "Urgent senior; CT; maybe drainage", "complication=gas|pus|perforation")
f("US-GB-06", "S1", "Polyp 10 mm or larger, mass, calcified wall Possible gallbladder cancer Senior; further imaging; consent changes",
  "Polyp 10 mm or larger, mass, calcified wall", "Possible gallbladder cancer", "Senior; further imaging; consent changes", "polyp_mm>=10|mass=yes|calcified_wall=yes")
f("US-GB-07", "S1", "No stones, only 'sludge' Diagnosis less certain Senior reviews whether operation is right",
  "No stones, only 'sludge'", "Diagnosis less certain", "Senior reviews whether operation is right", "stones=no;sludge=yes")
f("US-AP-01", "S2", "Positive — simple Appendix swollen (wider than 6 mm), won't squash under the probe, inflamed fat around it, maybe a hard stone inside (appendicolith) Appendicitis Senior decision; antibiotics; consent; theatre",
  "Appendix swollen (wider than 6 mm), won't squash under the probe, inflamed fat around it, maybe a hard stone inside (appendicolith)", "Appendicitis",
  "Senior decision; antibiotics; consent; theatre", "appendix_mm>6;compressible=no;perforation=no")
f("US-AP-02", "S2", "Positive — complicated Hole/gas outside the appendix, pus pocket (abscess), inflammatory lump, fluid Burst/complicated appendicitis Urgent surgery",
  "Hole/gas outside the appendix, pus pocket (abscess), inflammatory lump, fluid", "Burst/complicated appendicitis", "Urgent surgery", "complication=gas|abscess|mass|fluid")
f("US-AP-03", "S2", "Negative — something else found Normal appendix; e.g. ovarian cyst, kidney stone, inflamed bowel Not appendicitis Switch to that pathway; senior",
  "Normal appendix; e.g. ovarian cyst, kidney stone, inflamed bowel", "Not appendicitis", "Switch to that pathway; senior", "appendix=normal;other_finding=yes")
f("US-AP-04", "S2", "Non-diagnostic / equivocal 'Appendix not seen'; borderline size; no other signs We still don't know — this is NOT the same as negative Re-score; CT (or MRI if pregnant) or admit and re-examine in 6–12 h; senior",
  "'Appendix not seen'; borderline size; no other signs", "We still don't know — this is NOT the same as negative", "Re-score; CT (or MRI if pregnant) or admit and re-examine in 6–12 h; senior",
  "appendix=not_seen|borderline", "The book's classic beginner error: treating 'appendix not visualised' as 'appendix normal'.")
f("US-AP-05", "S2", "Normal appendix, nothing else Appendix seen and normal Appendicitis unlikely Senior decides observe or discharge with safety advice",
  "Appendix seen and normal", "Appendicitis unlikely", "Senior decides observe or discharge with safety advice", "appendix=normal;other_finding=no")

ecg = Table("ecg_findings", 3100, ["code", "d_finding", "d_plain", "d_rules"], "ECG finding code", "PTB-XL rhythm statements (cardiologist-validated records)", evidence="B", size=10)
PTB = "https://physionet.org/content/ptb-xl/1.0.3/"
M = ("Rhythm label from PTB-XL (human-validated); d_rules are the standard rate limits for the label (sinus rhythm 60 to 99, sinus tachycardia 100 or more per minute) used by the "
     "image/report validator. The rate in each report is measured from the recording itself. Pending clinician approval.")
ecg.add_derived("ECG-01", "PTB-XL 1.0.3", PTB, M, code="ECG-01", d_finding="Normal sinus rhythm", d_plain="Regular rhythm, normal rate", d_rules="rhythm=sinus;regular=yes;rate_bpm>=60;rate_bpm<100")
ecg.add_derived("ECG-02", "PTB-XL 1.0.3", PTB, M, code="ECG-02", d_finding="Sinus tachycardia", d_plain="Regular rhythm, fast rate", d_rules="rhythm=sinus;regular=yes;rate_bpm>=100")
ecg.add_derived("ECG-03", "PTB-XL 1.0.3", PTB, M, code="ECG-03", d_finding="Atrial fibrillation", d_plain="Irregularly irregular rhythm, no clear P waves", d_rules="rhythm=af;regular=no;rate_bpm>=40")
ecg.add_derived("ECG-04", "PTB-XL 1.0.3", PTB, M, code="ECG-04", d_finding="Sinus bradycardia", d_plain="Regular rhythm, slow rate", d_rules="rhythm=sinus;regular=yes;rate_bpm<60")
ecg.add_derived("ECG-05", "PTB-XL 1.0.3", PTB, M, code="ECG-05", d_finding="Complete heart block", d_plain="Slow ventricular rate, atria and ventricles out of step", d_rules="rhythm=chb;rate_bpm<65")
ecg.add_derived("ECG-06", "PTB-XL 1.0.3", PTB, M, code="ECG-06", d_finding="Supraventricular tachycardia", d_plain="Fast regular narrow-complex rhythm", d_rules="rhythm=svt;regular=yes;rate_bpm>=140")
ecg.add_derived("ECG-07", "PTB-XL 1.0.3", PTB, M + " ST elevation is measured from the signal (1 mm or more in lead II).", code="ECG-07", d_finding="ST elevation (inferior myocardial infarction pattern)", d_plain="Heart attack pattern",
                d_rules="rhythm=sinus;st=elevation;st_elevation_mm>=1")
ECGS = ("Synthetic pattern, made by editing a real PTB-XL strip in code (ADR-0003); the book's potassium what-ifs name these ECG changes (Part 14.2) but no labelled open recordings exist. Pending clinician approval.")
ecg.add_derived("ECG-08", "PTB-XL 1.0.3 (edited)", PTB, ECGS, code="ECG-08", d_finding="Hyperkalaemia pattern (peaked T waves)", d_plain="High potassium on the ECG", d_rules="rhythm=sinus;t_wave=peaked")
ecg.add_derived("ECG-09", "PTB-XL 1.0.3 (edited)", PTB, ECGS, code="ECG-09", d_finding="Hypokalaemia pattern (flat T waves with U waves)", d_plain="Low potassium on the ECG", d_rules="rhythm=sinus;t_wave=flat_u")
ecg.add_derived("ECG-10", "PTB-XL 1.0.3 (edited)", PTB, "Synthetic: a real PTB-XL atrial fibrillation strip sped up to the fast rate the book's what-if names (new AF, rate 132, Part 14.3). Pending clinician approval.", code="ECG-10",
                d_finding="Atrial fibrillation with a fast ventricular rate", d_plain="New fast AF", d_rules="rhythm=af;regular=no;rate_bpm>=120")

cxr = Table("cxr_findings", 3200, ["code", "d_finding", "d_plain", "d_rules"], "Chest X-ray finding code", "NIH ChestX-ray14 labels (text-mined, not clinician-checked)", evidence="B", size=10)
NIH = "https://nihcc.app.box.com/v/ChestXray-NIHCC"
MC = ("Finding label from the NIH ChestX-ray14 collection (labels were text-mined from the original reports and are not clinician-checked); d_rules is the label a report must carry. "
      "The book has no chest X-ray report categories, so these codes are proposals for the clinician to confirm or replace. Pending clinician approval.")
for code, label, plain, key in (("CXR-01", "No finding labelled", "Film without a labelled abnormality", "no_finding"), ("CXR-02", "Consolidation", "Airspace opacity (possible pneumonia or aspiration)", "consolidation"),
                                ("CXR-03", "Pleural effusion", "Fluid around the lung", "effusion"), ("CXR-04", "Pneumothorax", "Air around the lung", "pneumothorax"),
                                ("CXR-05", "Cardiomegaly", "Enlarged heart shadow", "cardiomegaly"),
                                ("CXR-06", "Pulmonary oedema", "Fluid in the lungs (fluid overload, heart failure)", "edema"), ("CXR-07", "Atelectasis", "Partly collapsed lung (for example after pain or surgery)", "atelectasis")):
    cxr.add_derived(code, "NIH ChestX-ray14 (Wang et al., CVPR 2017)", NIH, MC, code=code, d_finding=label, d_plain=plain, d_rules=f"label={key}")

findings.add_derived("US-GB-00", "ProcSense scenario library (ADR-0003)", "docs/research/imaging_scenarios.md",
                     "The clean (no problem) result for the gallbladder ultrasound, so a trainee who orders the scan on a patient without a gallbladder problem gets a normal picture and report. The limits are the book's normal values from Part 6.3 (wall 3 mm or less, bile duct 6 mm or less, no fluid, no stones). Pending clinician approval.",
                     code="US-GB-00", scenario="S1", v_finding="", v_plain="", v_next="", d_rules="stones=no;wall_mm<=3;duct_mm<=6;fluid=no")
cxr.add_derived("CXR-08", "NIH ChestX-ray14 (edited)", NIH, "Synthetic: free gas painted under the right hemidiaphragm of a real 'No Finding' film (the book's rigid-abdomen what-if lists perforation and imaging, Part 14.1). Pending clinician approval.",
                code="CXR-08", d_finding="Free air under the diaphragm (pneumoperitoneum)", d_plain="Gas outside the bowel: perforation", d_rules="free_air=yes")

USX = ("Extra ultrasound results the book's scenarios need (docs/research/imaging_scenarios.md): the clean pelvic and kidney scans, the equivocal gallbladder ('possible early cholecystitis', Part 14.6), ectopic pregnancy, ovarian torsion "
       "(Part 14.1 rigid abdomen, 14.4 pregnancy) and a kidney stone with hydronephrosis (Part 6.4 'kidney stone'). The codes and limits are proposals. Pending clinician approval.")
for code, scen, rules in (("US-GB-08", "S1", "wall_mm>3;wall_mm<=4;stones=no;fluid=no;equivocal=yes"), ("US-PV-00", "S2", "uterus=normal;fluid=no;adnexal_mass=no"),
                          ("US-PV-01", "S2", "uterus=empty;sac=no;adnexal_mass=yes;fluid=yes"), ("US-PV-02", "S2", "ovary_mm>=50;doppler_flow=reduced"),
                          ("US-RN-00", "S2", "hydronephrosis=no;stone=no"), ("US-RN-01", "S2", "hydronephrosis=yes;stone=yes;pelvis_mm>=10")):
    findings.add_derived(code, "ProcSense scenario library (ADR-0003)", "docs/research/imaging_scenarios.md", USX, code=code, scenario=scen, v_finding="", v_plain="", v_next="", d_rules=rules)

xsec = Table("xsec_findings", 3300, ["code", "d_finding", "d_plain", "d_rules"], "CT, MRI and MRCP finding code", "ProcSense scenario library: CT, MRI and MRCP results (proposed)", evidence="B", size=20)
XM = ("Proposed finding code for the scenario library (docs/research/imaging_scenarios.md): the walkthrough names CT, MRI and MRCP as choices (Parts 6, 7, equipment rows 23 and 24) but gives no "
      "report categories, so the codes and the numeric limits in d_rules (the book's 6 mm appendix and 6 mm bile duct limits) are proposals. Pending clinician approval.")
SCEN = "ProcSense scenario library (ADR-0003)"
for code, finding, plain, rules in (
        ("CT-AP-00", "CT: no appendicitis", "Clean result: no problem at the right iliac fossa", "appendix=normal;collection=no"),
        ("CT-AP-01", "CT: acute appendicitis, uncomplicated", "Inflamed appendix, no collection", "appendix_mm>6;collection=no"),
        ("CT-AP-02", "CT: appendicitis with abscess", "Inflamed appendix with a pus collection", "appendix_mm>6;collection=yes;abscess_mm>=10"),
        ("CT-AP-03", "CT: caecal mass", "A tumour of the caecum that can mimic appendicitis in an older patient", "mass_mm>=20;appendix=normal"),
        ("CT-AP-04", "CT: right-sided colonic diverticulitis", "Inflamed outpouching of the colon that can mimic appendicitis", "wall_mm>4;diverticulum=yes;appendix=normal"),
        ("CT-GB-00", "CT: no acute gallbladder abnormality", "Clean result: normal gallbladder", "wall_mm<=3"),
        ("CT-GB-01", "CT: complicated (emphysematous) cholecystitis", "Thick, gas-containing gallbladder wall", "wall_mm>4;complication=gas"),
        ("CT-AB-00", "CT: no acute abdominal abnormality at the mid abdomen", "Clean result: no obstruction, no free gas", "obstruction=no"),
        ("CT-AB-01", "CT: small bowel obstruction", "Dilated fluid-filled small bowel loops", "bowel_mm>25;obstruction=yes"),
        ("CT-AB-02", "CT: free intraperitoneal air", "Gas outside the bowel: perforation", "free_air=yes"),
        ("MR-AP-00", "MRI: no appendicitis", "Clean result: normal appendix", "appendix=normal;collection=no"),
        ("MR-AP-01", "MRI: acute appendicitis", "Inflamed appendix on MRI (for example in pregnancy)", "appendix_mm>6;collection=no"),
        ("MRCP-00", "MRCP: normal biliary tree", "Clean result: normal bile duct", "duct_mm<=6;stone=no"),
        ("MRCP-01", "MRCP: stone in the common bile duct", "Dilated duct with a filling defect", "duct_mm>6;stone=yes")):
    xsec.add_derived(code, SCEN, "docs/research/imaging_scenarios.md", XM, code=code, d_finding=finding, d_plain=plain, d_rules=rules)

bed = Table("bedside_results", 3600, ["code", "test", "d_finding", "d_text", "d_rules"], "Bedside result", "ProcSense scenario library: pregnancy test, urine dipstick, blood sample appearance", evidence="B", size=20)
BM = ("Bedside results named by the book's what-ifs (positive pregnancy test, ketones in the urine, a haemolysed sample, Part 14). These are shown as RESULTS ONLY, with no picture (owner decision, "
      "10 October 2026): the text is the result the trainee reads. The codes and wording are proposals. Pending clinician approval.")
for code, test, finding, text, rules in (
        ("PREG-00", "pregnancy_test", "Pregnancy test negative", "Urine pregnancy test: negative (one line, at the control mark only).", "result=negative"),
        ("PREG-01", "pregnancy_test", "Pregnancy test positive", "Urine pregnancy test: positive (two lines, at the control mark and the test mark).", "result=positive"),
        ("DIP-00", "urine_dipstick", "Urine dipstick normal", "Urine dipstick: leukocytes negative, nitrite negative, protein negative, blood negative, glucose negative, ketones negative.",
         "glucose=negative;ketones=negative;blood=negative;nitrite=negative"),
        ("DIP-01", "urine_dipstick", "Urine dipstick: infection pattern", "Urine dipstick: leukocytes ++, nitrite positive, protein trace, blood trace, glucose negative, ketones negative. Suggests a urinary tract infection.",
         "leukocytes=positive;nitrite=positive"),
        ("DIP-02", "urine_dipstick", "Urine dipstick: glucose and ketones", "Urine dipstick: glucose ++++, ketones +++, protein trace, leukocytes negative, nitrite negative, blood negative. Suspected diabetic ketoacidosis.",
         "glucose=positive;ketones=positive"),
        ("DIP-03", "urine_dipstick", "Urine dipstick: blood", "Urine dipstick: blood +++, protein trace, leukocytes negative, nitrite negative, glucose negative, ketones negative. Blood in the urine (haematuria).", "blood=positive"),
        ("TUBE-00", "blood_sample_appearance", "Blood sample acceptable", "Sample appearance after centrifugation: serum clear and straw-coloured; no haemolysis, no lipaemia.", "sample=acceptable"),
        ("TUBE-01", "blood_sample_appearance", "Blood sample haemolysed", "Sample appearance after centrifugation: serum pink to red, haemolysis visible. Potassium and some other results are unreliable; repeat the sample.", "sample=haemolysed"),
        ("TUBE-02", "blood_sample_appearance", "Blood sample lipaemic", "Sample appearance after centrifugation: serum cloudy and milky, lipaemia visible. Some results may be unreliable; the lab will comment.", "sample=lipaemic")):
    bed.add_derived(code, "ProcSense scenario library (ADR-0004)", "docs/research/imaging_scenarios.md", BM, code=code, test=test, d_finding=finding, d_text=text, d_rules=rules)

TABLES = [findings, ecg, cxr, xsec, bed]
OFFICIAL_FILES = {}
