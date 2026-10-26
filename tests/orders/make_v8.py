"""Writes tests/orders/paraphrases_v8.json: the eighth set of spoken-order paraphrases (B23, C32), written by an AI assistant on 25 Oct 2026 (UTC) AFTER the prompt and note work that followed the scoring of sets 6 and 7,
and BEFORE any row was run through any mapper. Synthetic and not independent (the author is a language model; the second human author of EX-18 does not exist yet). Frozen: scored once per model.
Same mix as sets 4, 6 and 7. Rows whose target could reasonably be two items were left out on purpose.
"""
import json
from pathlib import Path

T = [
    ("fbc", "get me her full blood count"), ("crp", "how raised is the C reactive protein"), ("u_and_e", "send urea and electrolytes"), ("lft", "has the liver been damaged, check the enzymes and bilirubin"),
    ("hba1c", "glycated haemoglobin please"), ("clotting", "send a coagulation screen, he is on a blood thinner"), ("group_and_save", "make sure a group and save is in the lab before surgery"),
    ("lactate_cultures", "I would like cultures and lactate before antibiotics"), ("glucose_fingerprick", "what is his capillary glucose right now"), ("urine_dipstick", "could we have a urine dip"),
    ("pregnancy", "rule out pregnancy with a urine test"), ("ecg", "twelve lead ECG now"), ("chest_xray", "chest radiograph please"), ("ultrasound", "an ultrasound of the right upper quadrant"),
    ("ct", "get a CT scan of his abdomen and pelvis"), ("mri", "she needs an MRI of the spine"), ("mrcp", "arrange a cholangiogram by MR"), ("lipase", "a serum lipase for the abdominal pain"),
    ("tsh", "thyroid stimulating hormone level"), ("ferritin", "send a ferritin for the anaemia"), ("tryptase", "send a mast cell tryptase"), ("stopbang", "complete the STOP BANG questionnaire"),
    ("bmi", "calculate her BMI"),
]
M = [
    ("TR-PARACETAMOL", "one gram of intravenous paracetamol"), ("TR-NSAID", "give him some ibuprofen"), ("TR-OPIOID", "an opioid for the severe pain, morphine or similar"),
    ("TR-ANTIEMETIC", "cyclizine, she keeps retching"), ("TR-IV-FLUIDS", "start intravenous fluids, Hartmann's"), ("TR-ABX-PROPHYLAXIS", "prophylactic antibiotic at induction"),
    ("TR-ABX-S2", "treat the chest infection with antibiotics"), ("TR-ADRENALINE", "adrenaline 0.5 milligrams intramuscular for the anaphylaxis"), ("TR-FAST-SUGAR", "get him a glucose drink for the hypo"),
    ("TR-HYPERKALAEMIA", "treat the potassium of seven with calcium gluconate and insulin"), ("TR-IV-GLUCOSE", "intravenous glucose, he is drowsy and hypoglycaemic"), ("TR-NALOXONE", "give naloxone, his breathing is slow from the opioid"),
    ("TR-OXYGEN", "put oxygen on, fifteen litres"), ("TR-SEPSIS-BUNDLE", "begin the sepsis six pathway"), ("TR-STEROID-COVER", "she takes prednisolone, give hydrocortisone cover"),
    ("TR-VTE-PROPHYLAXIS", "enoxaparin to prevent thrombosis"),
]
H = [
    ("metformin", "metformin must be held on the morning of surgery"), ("anticoagulant", "stop the anticoagulation before the procedure"), ("insulin", "do not give the insulin this morning"),
    ("ramipril", "the ramipril should not be taken today"),
]
P = [
    ("xray", "send the patient to X ray"), ("ct", "take her to the CT department"), ("ultrasound", "she is going to the ultrasound department now"), ("theatre", "transfer him to the operating theatre"),
    ("ward", "take him back to the ward"), ("mri", "the patient has to go to MRI"),
]
S = [
    ("surgeon", "get the consultant surgeon"), ("anaesthetist", "bleep the anaesthetist urgently"), ("senior", "I need to escalate to my senior"), ("nurse", "a nurse is needed in bay two"), ("porter", "please call a porter"),
]
N = [
    "the CT scanner was out of order last week", "somebody said the senior was on holiday", "I think the porter has gone for his break", "he takes metformin every day as far as I know",
    "the ward is quite cold today", "what is the date today", "I did not enjoy that lecture on the heart", "the anaesthetist has a lovely voice",
    "the potassium was fine last time", "this simulation is quite realistic",
]
rows, n = [], 0
for kind, intent, items in (("test", "ORDER_TEST", T), ("medicine", "REQUEST_MEDICATION", M), ("home_medicine", "HOLD_MED", H), ("place", "REQUEST_TRANSPORT", P), ("staff", "CALL_STAFF", S)):
    for target, text in items:
        n += 1
        rows.append({"id": f"OZ{n:03d}", "text": text, "kind": kind, "target": target, "intent": intent})
for text in N:
    n += 1
    rows.append({"id": f"OZ{n:03d}", "text": text, "kind": None, "target": None, "intent": None})
assert len(rows) == 64, len(rows)
out = {"synthetic": True, "independent": False,
       "note": "Eighth set of spoken-order paraphrases, written by an AI assistant on 25 Oct 2026 (UTC) after the prompt and note work that followed sets 6 and 7 and before any row was run through any mapper. Synthetic, not independent. Frozen: scored once per model. 'none' lines are not orders; several mention a test or a person.",
       "rows": rows}
Path(__file__).with_name("paraphrases_v8.json").write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
print("ok", len(rows))
