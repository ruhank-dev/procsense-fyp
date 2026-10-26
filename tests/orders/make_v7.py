"""Writes tests/orders/paraphrases_v7.json: the seventh set of spoken-order paraphrases (B23, C32), written by an AI assistant on 25 Oct 2026 (UTC) AFTER the prompt and note work that followed the scoring of set 6, and BEFORE
any row was run through any mapper. Synthetic and not independent (the author is a language model; the second human author of EX-18 does not exist yet). Frozen: scored once per model. Same mix as sets 4 and 6.
Rows whose target could reasonably be two items (a surgical registrar is a senior or a surgeon, the sepsis six is a bundle or a blood test) were left out on purpose: set 6 showed they measure the labels, not the mapper.
"""
import json
from pathlib import Path

T = [
    ("fbc", "I would like a haemoglobin and white count on her"), ("crp", "has the inflammatory marker gone up"), ("u_and_e", "check how his kidneys and electrolytes are"),
    ("lft", "I want to know about his liver function"), ("hba1c", "has his diabetes been well controlled lately, the three month marker"), ("clotting", "is there a bleeding tendency, check the clotting"),
    ("group_and_save", "she may need blood so please get a sample grouped and saved"), ("lactate_cultures", "take blood cultures and a lactate"), ("glucose_fingerprick", "prick his finger and read the sugar"),
    ("urine_dipstick", "dip his urine"), ("pregnancy", "could she be pregnant, test it"), ("ecg", "get a heart trace on him"), ("chest_xray", "I want a chest film"),
    ("ultrasound", "scan the abdomen with ultrasound"), ("ct", "request a CT of the abdomen"), ("mri", "book an MRI scan"), ("mrcp", "we need an MRCP to see the ducts"),
    ("lipase", "is the pancreas inflamed, send a lipase"), ("tsh", "is her thyroid overactive or underactive"), ("ferritin", "how much iron does she store"),
    ("tryptase", "check tryptase, I think that was anaphylaxis"), ("stopbang", "do the sleep apnoea screening score"), ("bmi", "what is her weight relative to her height"),
]
M = [
    ("TR-PARACETAMOL", "paracetamol into the vein for his pain"), ("TR-NSAID", "an anti inflammatory such as diclofenac"), ("TR-OPIOID", "give some morphine"),
    ("TR-ANTIEMETIC", "ondansetron for the nausea"), ("TR-IV-FLUIDS", "hang a bag of saline"), ("TR-ABX-PROPHYLAXIS", "give the antibiotic before the incision"),
    ("TR-ABX-S2", "start broad spectrum antibiotics for the sepsis"), ("TR-ADRENALINE", "give adrenaline intramuscularly"), ("TR-FAST-SUGAR", "give him glucose gel, he is hypoglycaemic and awake"),
    ("TR-HYPERKALAEMIA", "give the hyperkalaemia treatment, calcium and insulin dextrose"), ("TR-IV-GLUCOSE", "dextrose through the drip"), ("TR-NALOXONE", "naloxone for the overdose"),
    ("TR-OXYGEN", "high flow oxygen please"), ("TR-SEPSIS-BUNDLE", "start the sepsis bundle now"), ("TR-STEROID-COVER", "give the stress dose steroids"), ("TR-VTE-PROPHYLAXIS", "give the anticlot injection tonight"),
]
H = [
    ("metformin", "do not let her have her metformin today"), ("anticoagulant", "withhold the blood thinner from now"), ("insulin", "leave out his morning insulin"), ("ramipril", "hold the ramipril"),
]
P = [
    ("xray", "bring her down to X ray"), ("ct", "move him across to the CT scanner"), ("ultrasound", "wheel him to the ultrasound department"), ("theatre", "take her to theatre now"),
    ("ward", "return him to the ward"), ("mri", "transfer her into the MRI"),
]
S = [
    ("surgeon", "I need the surgeon at the bedside"), ("anaesthetist", "call the anaesthetist"), ("senior", "bring in the senior doctor"), ("nurse", "ask a nurse to help"), ("porter", "the porter is needed for transport"),
]
N = [
    "the ultrasound room was very busy yesterday", "the senior told me about the weather", "I always forget the porter's name", "she took paracetamol at home before she came",
    "the MRI is a long noisy scan, I read", "what is the name of this ward", "is lunch at one o'clock", "the nurse who looked after her was lovely",
    "I wonder whether the surgeon enjoyed the conference", "her last potassium was normal, I believe",
]
rows, n = [], 0
for kind, intent, items in (("test", "ORDER_TEST", T), ("medicine", "REQUEST_MEDICATION", M), ("home_medicine", "HOLD_MED", H), ("place", "REQUEST_TRANSPORT", P), ("staff", "CALL_STAFF", S)):
    for target, text in items:
        n += 1
        rows.append({"id": f"OY{n:03d}", "text": text, "kind": kind, "target": target, "intent": intent})
for text in N:
    n += 1
    rows.append({"id": f"OY{n:03d}", "text": text, "kind": None, "target": None, "intent": None})
assert len(rows) == 64, len(rows)
out = {"synthetic": True, "independent": False,
       "note": "Seventh set of spoken-order paraphrases, written by an AI assistant on 25 Oct 2026 (UTC) after the prompt and note work that followed the scoring of set 6 and before any row was run through any mapper. Synthetic, not independent. Frozen: scored once per model. 'none' lines are not orders; several mention a test or a person.",
       "rows": rows}
Path(__file__).with_name("paraphrases_v7.json").write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
print("ok", len(rows))
