"""A second, held-out set of text inconsistencies for the critic (DT-217): `uv run --project tools tools/generation/inconsistency_set_v2.py [--write]`.

The first set (inconsistency_set.py) is nearly saturated by the rule-based checks written after it was first run (the side of the body, denied pain), so it can no longer show what the model critic adds. This
set is worded differently on purpose and was written AFTER those rules, with no rule in mind: the allergen is one the lexicon does not list, the wrong operation is described without the words the lexicon
looks for, and four kinds are new (a condition denied, the tests said to be normal when they were not, a pregnancy in a man, an age said in other words). Each member changes or adds ONE sentence in one line of
an authored case; the structured case is untouched. It is synthetic and written by the language model that built this tooling (not by a clinician), so it measures whether the critic finds blunt contradictions
with unfamiliar wording and says nothing about subtle clinical errors.

Use: freeze it, run `tools/ai/eval_critic.py --set v2` once per critic version, and never edit the wording after seeing a result (a new set is a new file). The clean cases are the same as the first set's.
"""
from __future__ import annotations

import copy
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
OUT = ROOT / "tests" / "generation" / "text_inconsistencies_v2.json"
PER_KIND = 24
UNLISTED_ALLERGENS = ["kiwi fruit", "bee stings", "hair dye", "dust mites", "cat fur", "strawberries", "sesame seeds", "soy sauce", "plaster glue", "grass pollen", "cow's milk", "hazelnut spread"]
DENIALS = ["Nothing at all, I can take any tablet they give me.", "No, not that I have ever noticed, I have never reacted to anything.", "I don't react to anything, medicines, food or otherwise.",
           "No allergies. I have been given all sorts of drugs and had no trouble.", "None. My body takes everything well."]
WRONG_OP_FOR_GALLBLADDER = ["They are going to take out the inflamed bit of my bowel low on the right hand side.", "It is my bowel that is being operated on today, the worm-like bit.",
                            "I am here to have the small bowel tag removed, the one that gets infected in young people.", "They said they will take away the inflamed bowel tag with a camera."]
WRONG_OP_FOR_APPENDIX = ["I am here to have my gallstones dealt with.", "They are going to take the stones out of the little sac under my liver.", "It is for the stones that cause my colic after fatty food.",
                         "The surgeon will go in for the stones in the bile bag today."]
CONDITIONS_DENIED = ["I have never had any medical problems, I am completely healthy.", "Nothing wrong with me before today, no conditions, no tablets, no check-ups.", "I have been fit all my life and never been under a doctor."]
TESTS_NORMAL = ["The doctors told me all my blood tests were completely normal.", "I was told this morning that every test they did came back normal."]
PREGNANT = ["I am about twelve weeks pregnant, so I was worried about the scan.", "I found out I am pregnant last month and have not told anyone at work yet."]
YOUNG_FOR_OLD = ["I am only in my twenties, nothing like this runs in my family.", "I am twenty-three, so I have never had to think about my health before."]
OLD_FOR_YOUNG = ["I am in my seventies and have been retired for a good while.", "I am sixty-eight, so I have had my share of hospital visits."]
CONDITION_WORDS = re.compile(r"hypertension|high blood pressure|diabet|asthma|copd|sleep apno|thalassaemia|anaemia|heart failure|atrial", re.I)


def has_line(case: dict, topic: str) -> bool:
    return any(h["topic"] == topic and h.get("variant") is None and h.get("answer") for h in case["history"])


def mutations(case: dict, n: int) -> list[tuple[str, str, str]]:
    """[(kind, line id, new text)] that apply to this case; `n` rotates the wording so the members of a kind do not all read the same."""
    out = []
    sex, age = case["identity"]["sex"], case["identity"]["age"]
    if case["allergies"]:
        out.append(("allergy_denied", "allergy", DENIALS[n % len(DENIALS)]))
    else:
        out.append(("allergy_invented", "allergy", f"I come out in hives with {UNLISTED_ALLERGENS[n % len(UNLISTED_ALLERGENS)]} and my lips swell up."))
    pool = WRONG_OP_FOR_GALLBLADDER if case["scenario"] == "S1" else WRONG_OP_FOR_APPENDIX
    out.append(("operation_described_wrongly", "reason", pool[n % len(pool)]))
    if has_line(case, "past_history") and any(CONDITION_WORDS.search(p["label"]) for p in case["problems"]):
        out.append(("condition_denied", "h.past_history", CONDITIONS_DENIED[n % len(CONDITIONS_DENIED)]))
    if any(r["flag"] not in ("normal", "negative") for r in case["results"]) and case["presentation"].get("summary"):
        out.append(("tests_said_normal", "summary", case["presentation"]["summary"].rstrip() + " " + TESTS_NORMAL[n % len(TESTS_NORMAL)]))
    if sex == "M" and has_line(case, "red_flags"):
        out.append(("pregnancy_in_a_man", "h.red_flags", PREGNANT[n % len(PREGNANT)]))
    if has_line(case, "past_history") and age >= 45:
        out.append(("age_said_in_other_words", "h.past_history", YOUNG_FOR_OLD[n % len(YOUNG_FOR_OLD)]))
    elif has_line(case, "past_history") and age <= 30:
        out.append(("age_said_in_other_words", "h.past_history", OLD_FOR_YOUNG[n % len(OLD_FOR_YOUNG)]))
    return out


def apply(case: dict, line: str, text: str) -> dict:
    c = copy.deepcopy(case)
    if line == "allergy":
        c["allergy_answer"] = text
    elif line == "reason":
        c["reason_answer"] = text
    elif line == "summary":
        c["presentation"]["summary"] = text
    else:
        topic = line.split(".", 1)[1]
        for h in c["history"]:
            if h["topic"] == topic and h.get("variant") is None:
                h["answer"] = text
    return c


def build(cases: list[dict]) -> dict:
    counts: dict[str, int] = {}
    members = []
    approved = sorted((c for c in cases if c["status"].startswith("APPROVED")), key=lambda c: c["id"])
    for n, case in enumerate(approved):
        for kind, line, text in mutations(case, n):
            if counts.get(kind, 0) >= PER_KIND:
                continue
            counts[kind] = counts.get(kind, 0) + 1
            members.append({"id": f"{case['id']}:{kind}", "case": case["id"], "kind": kind, "line": line, "text": text})
    clean = [c["id"] for c in approved][::2]
    return {"note": "Synthetic, written by the builder after the side and pain rules existed and with none of them in mind. Each member changes or adds one sentence of one line so that it contradicts the structured case. Held out: score once per critic version, never reword.",
            "members": members, "clean": clean, "counts": counts}


def main(argv: list[str]) -> int:
    from composer import blocks as B
    data = build(B.load_cases())
    print(f"{len(data['members'])} inconsistencies {data['counts']}, {len(data['clean'])} clean cases")
    if "--write" in argv:
        OUT.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
