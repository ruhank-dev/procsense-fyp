"""Writes tests/orders/paraphrases_v6.json: the sixth set of spoken-order paraphrases for B23 and C32, written by an AI assistant on 25 Oct 2026 (UTC) BEFORE any row was run through any mapper and after the
prompt work on the development sets (1 to 3) was finished. Synthetic and not independent (the author is a language model; the second human author of EX-18 does not exist yet). Frozen: scored once per model.
Same shape and mix as set 4: 23 tests, 16 medicines, 4 home medicines, 6 places, 5 staff and 10 lines that are not orders (several of them mention an item or a person).
"""
import json
from pathlib import Path

T = [  # (target, text)
    ("fbc", "can we see her full blood picture"), ("crp", "I would like the inflammation level checked"), ("u_and_e", "what are his sodium and potassium and kidney function"),
    ("lft", "send off the liver profile please"), ("hba1c", "a long term sugar control marker"), ("clotting", "does his blood clot normally, I want a coag screen"),
    ("group_and_save", "get her blood typed in case she needs a transfusion"), ("lactate_cultures", "lactate and blood cultures, she looks septic"), ("glucose_fingerprick", "quick finger prick sugar now"),
    ("urine_dipstick", "test a sample of his water with a dipstick"), ("pregnancy", "is she pregnant, do a test"), ("ecg", "put him on a heart tracing"), ("chest_xray", "a plain film of the chest"),
    ("ultrasound", "scan her tummy with the probe"), ("ct", "I need a cross sectional scan of the abdomen"), ("mri", "arrange magnetic resonance imaging"), ("mrcp", "image the bile ducts with an MR scan"),
    ("lipase", "check the pancreatic enzyme"), ("tsh", "how is the thyroid working"), ("ferritin", "iron stores please"), ("tryptase", "mast cell marker after the reaction"),
    ("stopbang", "screen him for sleep apnoea with the questionnaire"), ("bmi", "work out her body mass index"),
]
M = [
    ("TR-PARACETAMOL", "give a gram of paracetamol through the drip"), ("TR-NSAID", "ibuprofen type of painkiller"), ("TR-OPIOID", "morphine for severe pain"),
    ("TR-ANTIEMETIC", "she feels sick, give her an anti sickness drug"), ("TR-IV-FLUIDS", "run in a litre of fluid"), ("TR-ABX-PROPHYLAXIS", "the pre operative antibiotic dose"),
    ("TR-ABX-S2", "start the antibiotics for the infection"), ("TR-ADRENALINE", "adrenaline now, she is in anaphylaxis"), ("TR-FAST-SUGAR", "a sugary drink for the low sugar"),
    ("TR-HYPERKALAEMIA", "treat the high potassium"), ("TR-IV-GLUCOSE", "ten percent dextrose into the vein"), ("TR-NALOXONE", "reverse the opioid with naloxone"),
    ("TR-OXYGEN", "put him on oxygen by mask"), ("TR-SEPSIS-BUNDLE", "begin the sepsis six"), ("TR-STEROID-COVER", "cover her with hydrocortisone, she is on long term steroids"),
    ("TR-VTE-PROPHYLAXIS", "an injection to prevent clots in the legs"),
]
H = [
    ("metformin", "hold his diabetes tablet metformin this morning"), ("anticoagulant", "he should not take the anticoagulant today"), ("insulin", "skip her insulin dose"),
    ("ramipril", "no ramipril before the operation"),
]
P = [
    ("xray", "send him round to plain film"), ("ct", "she needs to go to the CT department"), ("ultrasound", "take her to the ultrasound room"), ("theatre", "bring him up to the operating theatre"),
    ("ward", "return her to her bed on the ward"), ("mri", "to the MRI suite please"),
]
S = [
    ("surgeon", "page the surgical registrar"), ("anaesthetist", "I want the anaesthetist here now"), ("senior", "get my senior over"), ("nurse", "can a nurse come to the bedside"), ("porter", "I need a porter for transport"),
]
N = [
    "the x ray machine broke down yesterday", "she told me the nurse brought her tea", "do you know where the canteen is", "the surgeon is running late today",
    "last week the potassium was a bit high", "I am worried about getting this wrong", "what time does the list start", "the porter said the lift is out of order",
    "morphine makes me feel sleepy, my patient said", "tell me about the weather",
]
rows = []
n = 0
for kind, intent, items in (("test", "ORDER_TEST", T), ("medicine", "REQUEST_MEDICATION", M), ("home_medicine", "HOLD_MED", H), ("place", "REQUEST_TRANSPORT", P), ("staff", "CALL_STAFF", S)):
    for target, text in items:
        n += 1
        rows.append({"id": f"OX{n:03d}", "text": text, "kind": kind, "target": target, "intent": intent})
for text in N:
    n += 1
    rows.append({"id": f"OX{n:03d}", "text": text, "kind": None, "target": None, "intent": None})
assert len(rows) == 64, len(rows)
out = {"synthetic": True, "independent": False,
       "note": "Sixth set of spoken-order paraphrases, written by an AI assistant on 25 Oct 2026 (UTC) BEFORE any row was run through any mapper, after the prompt work on the development sets 1 to 3 was finished. Synthetic, not independent. Frozen: scored once per model. 'none' lines are not orders; several mention a test or a person.",
       "rows": rows}
Path(__file__).with_name("paraphrases_v6.json").write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
print("ok", len(rows))
