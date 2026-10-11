"""Builds content/behaviour/transitions.json, modes.json and disclosure.json: the patient behaviour model (p0-physiology-events-behaviour-s10).

    uv run --project tools tools/content/build_behaviour.py            # write
    uv run --project tools tools/content/build_behaviour.py --check    # fail if the files are out of date

Source: the guide's patient persona table (13 attributes, their ranges and what drives them) and its table of bounded transitions (appropriate explanation, jargon, repeated unexplained actions, a dismissive reply,
reassurance with diminishing returns, a long unexplained wait, an approved event), the consent rules L9-03 and E9-B, and the worked example (reassure: anxiety 7, 6, 5.3, 4.8 with a factor of 0.7; ANXIOUS to CALM when
anxiety is below 5). The magnitudes are simulation design values reviewed for plausibility (SR-2844); the modes and their limits are SR-2845; the capacity rule (walkthrough Part 10) is SR-2846; the attributes and
their starting values are SR-2843. The sensitive topics come from content/history_topics.json.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "behaviour"

# attribute id -> (min, max, start, driven by)
ATTRIBUTES = {
    "anxiety": (0, 10, 3, "explanation and reassurance lower it; unexplained actions and waits raise it"),
    "trust": (0, 10, 5, "introduction and honesty raise it; a dismissive reply or ignoring questions lowers it"),
    "cooperation": (0, 10, 5, "trust and understanding raise it; a dismissive reply lowers it"),
    "understanding": (0, 10, 3, "plain language and teach-back raise it; jargon lowers it"),
    "pain": (0, 10, 0, "read from the physiology (the pain score) and lowered by analgesia"),
    "fear": (0, 10, 2, "risks discussed without support raise it; reassurance lowers it; an approved event raises it"),
    "frustration": (0, 10, 0, "repeated questions and long waits raise it; analgesia lowers it"),
    "confusion": (0, 3, 0, "driven by the physiology (low oxygen, low sugar, sepsis) and by jargon"),
    "willingness": (0, 10, 5, "follows trust; gates the disclosure of sensitive facts"),
}
PERSONA = {"communication_style": "talkative", "question_frequency": 0.5, "need_for_reassurance": 1.0, "communication_difficulty": "none"}
STYLES = ["talkative", "terse", "anxious_questioning", "stoic"]
DIFFICULTIES = ["none", "language", "hearing"]


def rule(id, trigger, deltas, diminishing=1.0, every=1, when=None, ref="SR-2844", note="", question=""):
    return {"id": id, "trigger": trigger, "deltas": deltas, "diminishing": diminishing, "every": every, "when": when, "ref": ref, "note": note, "question": question}


RULES = [
    rule("introduce_self", "introduce_self", {"trust": 1, "anxiety": -1, "willingness": 1}, ref="SR-0035", note="introducing yourself first builds trust (rule L1-03)"),
    rule("summarise", "summarise", {"trust": 1, "understanding": 1}, ref="SR-0036", note="summarising builds trust and understanding (rule L2-05)"),
    rule("explain_before_action", "explain_before_action", {"understanding": 1, "trust": 1}, note="appropriate explanation before an action"),
    rule("jargon_understanding", "jargon", {"understanding": -1}, note="poor communication or jargon lowers understanding"),
    rule("jargon_confusion", "jargon", {"confusion": 1}, when={"<=": [{"var": "understanding"}, 3]}, note="and adds to confusion if understanding is already low"),
    rule("unexplained_action", "unexplained_action", {"anxiety": 1}, every=2, question="What are you doing?", note="every second unexplained action raises anxiety and the patient asks"),
    rule("dismissive_reply", "dismissive_reply", {"trust": -2, "cooperation": -1, "willingness": -1}, note="a dismissive reply to a question (T-BM5: trust -2, cooperation -1)"),
    rule("reassure", "reassure", {"anxiety": -1, "fear": -1}, diminishing=0.7, note="reassurance with a clear explanation, diminishing (anxiety 7, 6, 5.3, 4.8)"),
    rule("long_wait", "long_wait", {"anxiety": 1, "frustration": 1}, note="a long unexplained wait, per interval"),
    rule("approved_event", "approved_event", {"fear": 2}, note="an approved event fires (for example anaphylaxis)"),
    rule("analgesia_given", "analgesia_given", {"frustration": -1}, note="analgesia lowers irritability (the pain itself follows the physiology)"),
    rule("answer_approved", "answer_approved", {"trust": 1}, ref="SR-2844", note="an approved answer to the patient's question (L9-03)"),
    rule("answer_wrong", "answer_wrong", {"trust": -1}, note="a wrong answer, or none (L9-03)"),
    rule("teach_back_done", "teach_back", {"understanding": 1}, note="teach-back checks and builds understanding"),
    rule("false_reassurance", "false_reassurance", {"anxiety": -1}, note="false reassurance: the patient relaxes (E9-B); honesty is scored by the consent step"),
    rule("pressure", "pressure", {"cooperation": -2, "trust": -2, "willingness": -2}, note="pressure to consent is a professionalism failure"),
    rule("risks_unsupported", "risks_unsupported", {"fear": 1}, note="risks discussed without support raise fear"),
]
# first match wins; a mode is entered at limit + hysteresis and held while the value stays at the limit or beyond (so there is no flicker)
MODES = [
    {"mode": "CONFUSED", "attribute": "confusion", "op": ">=", "limit": 1, "hysteresis": 0},        # whole-number scales step by 1, so no margin
    {"mode": "DROWSY", "attribute": "sedation", "op": ">=", "limit": 2, "hysteresis": 0},
    {"mode": "IN_PAIN", "attribute": "pain", "op": ">=", "limit": 6, "hysteresis": 0.5},
    {"mode": "NAUSEATED", "attribute": "nausea", "op": ">=", "limit": 5, "hysteresis": 0.5},
    {"mode": "DISTRESSED", "attribute": "fear", "op": ">=", "limit": 7, "hysteresis": 0.5},
    {"mode": "DISTRESSED", "attribute": "anxiety", "op": ">=", "limit": 8, "hysteresis": 0.5},
    {"mode": "UNCOOPERATIVE", "attribute": "cooperation", "op": "<=", "limit": 2, "hysteresis": 0.5},
    {"mode": "ANXIOUS", "attribute": "anxiety", "op": ">=", "limit": 5, "hysteresis": 0.5},
]
# capacity (walkthrough Part 10): lost while any cause is active; treat the cause and try again
CAPACITY = {"confusion_at": 1, "severe_pain_at": 8, "strong_painkiller_minutes": 120, "sodium_below": 125, "sepsis_tiers": ["moderate", "high"], "physio_flags": ["confusion"]}
DISCLOSURE = {"pregnancy": {"trust": 3, "willingness": 3}}
WAIT_MINUTES = 10


def build() -> dict[str, str]:
    topics = json.loads((CONTENT / "history_topics.json").read_text(encoding="utf-8"))["topics"]
    bad = [t for t in DISCLOSURE if t not in topics["ids"]]
    if bad:
        raise SystemExit(f"disclosure thresholds for topics the history does not have: {bad}")
    not_sensitive = [t for t in DISCLOSURE if t not in topics["sensitive"]]
    if not_sensitive:
        raise SystemExit(f"disclosure gates on topics that are not sensitive: {not_sensitive}")
    attrs = [{"id": k, "min": v[0], "max": v[1], "start": v[2], "driven_by": v[3]} for k, v in ATTRIBUTES.items()]
    unknown = sorted({a for r in RULES for a in r["deltas"]} - set(ATTRIBUTES))
    if unknown:
        raise SystemExit(f"transitions on attributes the model does not have: {unknown}")
    transitions = {"attributes": attrs, "persona": PERSONA, "persona_styles": STYLES, "persona_difficulties": DIFFICULTIES, "wait_minutes": WAIT_MINUTES, "rules": RULES, "refs": ["SR-2843", "SR-2844"]}
    modes = {"modes": MODES, "default": "CALM", "capacity": CAPACITY, "refs": ["SR-2845", "SR-2846", "SR-0094", "SR-0530"]}
    disclosure = {"gates": [{"topic": t, **v} for t, v in DISCLOSURE.items()], "refs": ["SR-2845"]}
    dump = lambda d: json.dumps(d, indent=1, ensure_ascii=False) + "\n"
    return {"transitions.json": dump(transitions), "modes.json": dump(modes), "disclosure.json": dump(disclosure)}


def main(argv: list[str]) -> int:
    files = build()
    if "--check" in argv:
        stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
        if stale:
            print(f"FAIL  content/behaviour/{', '.join(stale)} out of date; run tools/content/build_behaviour.py")
            return 1
        print("content/behaviour is up to date")
        return 0
    OUT.mkdir(parents=True, exist_ok=True)
    for n, t in files.items():
        (OUT / n).write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files in content/behaviour")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
