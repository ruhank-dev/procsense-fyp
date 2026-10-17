"""Synthetic labelled utterances for the communication classifier (p1-ai-language-s06): `uv run --project tools tools/synth/gen_utterances.py [--check]`.

Writes tests/classifier/{train,dev,test}.jsonl and tests/classifier/splits.json (the hashes that freeze the splits). Each row is one utterance with its act (content/assessment/taxonomy.json), the speaker and the
session it belongs to. The words come from templates filled with slot values taken from the content (tests, medicines, topics, equipment, roles), so nothing clinical is invented here; each speaker has a fixed
style (fillers, politeness, contractions, case) so the same act sounds different from different speakers. SPLIT BY SPEAKER: no speaker appears in two splits (T-TX3), because one speaker in train and test
inflates the scores. The labels are the template's act: there is one labeller, the generator, so this set measures whether a classifier recovers the intended act from varied wording, not whether it agrees with
people; two human labellers and the kappa of the plan are the open work (DT-210). The set is synthetic (SD-006) and deterministic: the same command writes the same bytes.
"""
from __future__ import annotations

import hashlib
import itertools
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tests" / "classifier"
PER_ACT = 24
SPEAKERS = 24
SPLITS = {"train": range(0, 16), "dev": range(16, 20), "test": range(20, 24)}

TESTS = ["a full blood count", "the U and E", "a CRP", "an ECG", "a chest X-ray", "the clotting screen", "a urine dipstick", "a group and save"]
TOPICS = ["your last meal", "your medicines", "any previous anaesthetics", "your pain", "your breathing", "any family problems with anaesthetics", "your smoking", "your alcohol"]
FACTS = ["you take warfarin", "you had toast at seven", "you stopped your tablets two days ago", "the pain started on Tuesday", "you live alone"]
ROLES = ["the anaesthetist", "the senior nurse", "the surgeon", "the porter", "the registrar"]
FAULTS = ["the laparoscopic tower", "the suction", "the pulse oximeter", "the diathermy", "the warming blanket", "the anaesthetic machine"]
MEDS = ["paracetamol", "the antibiotic", "an antiemetic", "fluids", "oxygen"]
ITEMS = ["potassium five point two", "blood pressure one ten over seventy", "the antibiotic is in", "the cannula is in", "oxygen at four litres"]
NAMES = ["Mr Khan", "Mrs Bibi", "Ms Chen", "Mr Rehman", "Ms Farooq"]

TEMPLATES: dict[str, list[str]] = {
    "ASK_TOPIC": ["Can you tell me about {topic}?", "What about {topic}?", "Have you had any problems with {topic}?", "I would like to ask about {topic}.", "Tell me about {topic}, please.", "Let us talk about {topic}.", "Is there anything about {topic} I should know?", "Could you describe {topic} for me?"],
    "CLARIFY": ["Sorry, can you say that again?", "What do you mean by that?", "I did not catch that, could you repeat it?", "Can you explain what you meant about {topic}?", "I am not sure I understood, say it once more.", "Could you be more specific?", "Pardon, what was that?", "Do you mean {fact}?"],
    "ANSWER_QUESTION": ["Yes, you will be asleep for the whole operation.", "It usually takes about an hour.", "No, you will not feel anything during the operation.", "You can have a drink of water until two hours before.", "The nurse will stay with you the whole time.", "You will wake up in the recovery area.", "Yes, a family member can wait here.", "Most people go home the same day."],
    "EXPLAIN_PROCEDURE": ["The surgeon will remove your gallbladder through small cuts in your tummy.", "You will have an anaesthetic so that you sleep through the operation.", "The operation takes the appendix out and usually lasts about an hour.", "We will put a small tube in a vein so the anaesthetist can give you medicines.", "After the operation you will go to the recovery room to wake up.", "The anaesthetist will give you oxygen and then the medicine to send you to sleep.", "They will use a camera and small instruments, so the cuts are small.", "The operation is done under general anaesthetic."],
    "EXPLAIN_TEST": ["We are taking some blood to check how your kidneys are working.", "{test} tells us if there is an infection.", "The ECG records the electrical activity of your heart.", "This test looks for signs of inflammation in your blood.", "We do {test} so that the team knows it is safe to go ahead.", "The X-ray shows us your lungs.", "The urine test checks for infection or sugar.", "The clotting test tells us whether you might bleed more than usual."],
    "EXPLAIN_ACTION": ["I am going to put a cuff on your arm now.", "I will press on your tummy, tell me if it hurts.", "I am going to listen to your chest.", "I will clip a small probe on your finger to measure your oxygen.", "I am about to take some blood from your arm.", "Now I am going to check your wristband.", "I will lift your gown so I can feel your tummy.", "I am going to take your temperature."],
    "CHECK_UNDERSTANDING": ["Can you tell me in your own words what will happen?", "Does that make sense to you?", "Is there anything you would like me to go over again?", "Could you say back to me what we have agreed?", "Have I explained that clearly?", "Do you have any questions about that?", "Tell me what you understand about the operation.", "Are you clear about when to stop eating?"],
    "ACKNOWLEDGE_EMOTION": ["I can see you are worried.", "It is understandable to feel nervous before an operation.", "You seem anxious, that is completely normal.", "I can hear that this is frightening for you.", "Many people feel like that, you are not alone.", "I am sorry you have been in pain for so long.", "It sounds like you have had a hard few days.", "I can tell this is a lot to take in."],
    "REASSURE": ["We will look after you and stay with you the whole time.", "The team does this operation every day.", "You are in safe hands.", "We will make sure you are comfortable.", "There is a whole team watching over you.", "We will give you medicine for the pain.", "You will be monitored carefully throughout.", "Take your time, there is no rush."],
    "ACKNOWLEDGE_FACT": ["Thank you, so {fact}.", "I have noted that {fact}.", "Right, so {fact}, that is helpful.", "Okay, I understand that {fact}.", "Good to know that {fact}.", "I will write down that {fact}.", "So what you are telling me is that {fact}.", "Thanks for telling me that {fact}."],
    "CLOSED_LOOP": ["Understood, giving {med} now, I will tell you when it is in.", "Let me read that back: {item}.", "Okay, {item}, I will do that and report back.", "Received, I am ordering {test} now.", "To confirm, you want {med} given, correct?", "Repeating that: {item}.", "Got it, doing that now and I will confirm when done.", "Message received, {item}."],
    "ESCALATE_SPEECH": ["I need {role} to see this patient now.", "Can you ask {role} to come?", "I am worried, please call {role}.", "This is beyond me, I want {role} here.", "Could {role} review this before we go ahead?", "Please bleep {role}, it is urgent.", "I would like to escalate this to {role}.", "I need senior help with this patient."],
    "REPORT_FAULT": ["{fault} is not working.", "I think {fault} has a problem.", "There is a fault with {fault}.", "{fault} will not switch on.", "Something is wrong with {fault}, it needs checking.", "{fault} is not reading properly.", "Please note {fault} is broken.", "{fault} keeps cutting out."],
    "SBAR_ELEMENT": ["Situation: {name}, planned surgery this morning.", "Background: {fact}.", "Assessment: observations are stable and {fact}.", "Recommendation: I would like {role} to review before theatre.", "Situation: {name} is in bay two and ready to go.", "Background: {name} has had {test} done.", "Assessment: I am concerned about the blood pressure.", "Recommendation: please give {med} before we move."],
    "VOICE_ORDER": ["Could we get {test}?", "Please order {test}.", "I would like {test} sent now.", "Can you call {role}?", "Please arrange transport to theatre.", "Can we have {test} and {test2}?", "Order {test} for this patient.", "Request {med} for the patient."],
    "CONFIRM_IDENTITY": ["Can you tell me your full name and date of birth?", "Could you confirm your name for me?", "The wristband says {name}, is that you?", "Let me check your wristband against the notes.", "What is your date of birth?", "Please tell me your name and hospital number.", "I need to check who you are, what is your name?", "Is this your name on the band?"],
    "STATE_ALLERGY": ["Are you allergic to anything?", "Do you have any allergies to medicines?", "So penicillin gave you a rash.", "What happened when you had that medicine?", "Any reaction to latex or antibiotics before?", "Have you ever had an allergic reaction?", "You are allergic to {med}, correct?", "Tell me about your allergies."],
    "CONSENT_SIGN": ["Are you happy for us to go ahead with the operation?", "Please sign the form if you agree.", "Do you consent to the anaesthetic?", "Are you willing to proceed with the surgery?", "I need your signature on the consent form.", "Is it all right with you if we go ahead?", "Do you agree to have the operation today?", "Can you confirm you are happy to proceed?"],
    "HANDOVER_CRITICAL_ELEMENT": ["Please note she is allergic to penicillin.", "He has not fasted: he had toast at seven.", "The identity band does not match the notes.", "Her blood pressure has been dropping in the last hour.", "Allergy alert: {med}.", "Important: the patient is not fasted.", "Please be aware the oxygen level was low earlier.", "Critical: the name on the band is different."],
    "NONE": ["Hmm.", "Lovely weather today.", "Let me think for a moment.", "Where did I put my pen?", "Right then.", "It has been a long shift.", "Okay.", "Is it lunchtime yet?", "Sorry, my phone.", "Oh dear.", "Mm hm.", "Just a second."],
}
STYLES = [("", "", False, False), ("Um, ", "", False, False), ("So, ", " please", False, False), ("", ", okay?", True, False), ("Right, ", "", False, True), ("Excuse me, ", "", True, False), ("Well, ", " if that is all right", False, False), ("", " thanks", True, True)]


def fill(template: str, rng: random.Random) -> str:
    t1, t2 = rng.sample(TESTS, 2)
    return template.format(test=t1, test2=t2, topic=rng.choice(TOPICS), fact=rng.choice(FACTS), role=rng.choice(ROLES), fault=rng.choice(FAULTS), med=rng.choice(MEDS), item=rng.choice(ITEMS), name=rng.choice(NAMES))


def style(text: str, speaker: int) -> str:
    pre, post, contract, lower = STYLES[speaker % len(STYLES)]
    t = text
    if contract:
        for a, b in (("I am ", "I'm "), ("do not", "don't"), ("is not", "isn't"), ("cannot", "can't"), ("It is ", "It's "), ("I will ", "I'll "), ("you are", "you're"), ("You are ", "You're ")):
            t = t.replace(a, b)
    t = t.rstrip(".?!") + ("?" if text.endswith("?") else ".") if post == "" else t.rstrip(".?!") + post + ("?" if text.endswith("?") and "okay" not in post else "")
    t = pre + (t[0].lower() + t[1:] if pre and t[0].isalpha() and not t.startswith("I ") else t)
    return t.lower() if lower else t


def build() -> dict[str, list[dict]]:
    taxonomy = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))
    rows = []
    for act in taxonomy["acts"]:
        templates = TEMPLATES[act["id"]]
        rng = random.Random("utterances:" + act["id"])
        seen: set[str] = set()
        speaker_cycle = itertools.cycle(rng.sample(range(SPEAKERS), SPEAKERS))
        attempts = 0
        while len([r for r in rows if r["act"] == act["id"]]) < PER_ACT and attempts < 2000:
            attempts += 1
            speaker = next(speaker_cycle)
            text = style(fill(rng.choice(templates), rng), speaker)
            if text in seen:
                continue
            seen.add(text)
            rows.append({"id": f"U{len(rows):04d}", "speaker": f"spk-{speaker:02d}", "session": f"sess-{speaker:02d}-{len([r for r in rows if r['speaker'] == f'spk-{speaker:02d}']) // 6}", "text": text, "act": act["id"], "critical": act["critical"],
                         "synthetic": True, "generator": "gen_utterances.v1"})
    out = {name: [r for r in rows if int(r["speaker"][4:]) in rng_] for name, rng_ in SPLITS.items()}
    return out


def render(rows: list[dict]) -> str:
    return "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in rows)


def files() -> dict[str, str]:
    splits = build()
    out = {f"{name}.jsonl": render(rows) for name, rows in splits.items()}
    out["splits.json"] = json.dumps({"note": "Frozen splits (p1-ai-language-s06). Split by speaker. Changing a file changes its hash: the classifier results are only valid for these hashes.",
                                     "sha256": {n: hashlib.sha256(t.encode("utf-8")).hexdigest() for n, t in sorted(out.items())},
                                     "rows": {n: len(r) for n, r in splits.items()}, "speakers": {n: sorted({r["speaker"] for r in rows}) for n, rows in splits.items()}}, indent=1) + "\n"
    return out


def main(argv: list[str]) -> int:
    fs = files()
    if "--check" in argv:
        bad = [n for n, t in fs.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
        splits = json.loads(fs["splits.json"])
        overlap = set(splits["speakers"]["train"]) & set(splits["speakers"]["test"]) | set(splits["speakers"]["train"]) & set(splits["speakers"]["dev"]) | set(splits["speakers"]["dev"]) & set(splits["speakers"]["test"])
        for n in bad:
            print("FAIL", n, "is out of date: run tools/synth/gen_utterances.py")
        if overlap:
            print("FAIL a speaker is in two splits:", sorted(overlap))
        print("classifier dataset: " + ("current, split by speaker" if not bad and not overlap else "problems"))
        return 1 if bad or overlap else 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in fs.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print("wrote", {n: len(t.splitlines()) for n, t in fs.items() if n != "splits.json"})
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
