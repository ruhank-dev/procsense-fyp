"""Ground truth for the direct synthetic development set, derived from the approved ProcSense rules and nothing else.

A sentence is never read to decide a label. The label of an act in a specification is:
  performs_now     = the trainee performs the act in this utterance (situation 'performed')
  credit_eligible  = an ORDINARY act (not one of mapping.json critical_acts), performed now, inside the behaviour window (the steps of the behaviours whose ai_acts contain it),
                     with the state it needs (ANSWER_QUESTION: a patient question is waiting; CLOSED_LOOP: a read-back is waiting)
Critical acts are recorded from speech and never credited from it (ADR-0005/0022/0023, mapping.json critical_acts). Sources: content/assessment/mapping.json, taxonomy.json."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
TAXONOMY = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))
ACTS = [a["id"] for a in TAXONOMY["acts"]]
CRITICAL_ACTS = list(MAPPING["critical_acts"])
ORDINARY_ACTS = [a for a in ACTS if a not in CRITICAL_ACTS and a != "NONE"]
ALL_STEPS = list(range(1, 16))
WINDOW: dict[str, set[int]] = {}
for _b in MAPPING["behaviours"]:
    for _a in _b.get("ai_acts") or []:
        WINDOW.setdefault(_a, set()).update(_b["steps"])
NEEDS_STATE = {"ANSWER_QUESTION": "patient_question_pending", "CLOSED_LOOP": "readback_pending"}
NOT_PERFORMED = ["negated", "intended_later", "hypothetical", "reported_about_someone_else", "other_actor_does_it"]
NO_ACT = ["small_talk", "thinking_aloud", "filler_or_noise_words", "off_topic_statement", "near_miss", "critical_topic_discussion"]
SITUATIONS = ["performed"] + NOT_PERFORMED + NO_ACT


def in_window(act: str, step) -> bool:
    return act in WINDOW and step in WINDOW[act]


def act_gold(act: str, situation: str, step, flags: dict) -> dict:
    performs = situation == "performed"
    critical = act in CRITICAL_ACTS
    state_ok = True
    if act in NEEDS_STATE:
        state_ok = bool(flags.get(NEEDS_STATE[act]))
    credit = bool(act in ORDINARY_ACTS and performs and in_window(act, step) and state_ok)
    return {"act": act, "situation": situation, "performs_now": performs, "critical": critical, "credit_eligible": credit}


def derive(spec: dict) -> dict:
    """gold for a specification: one record per act; the item is credit-eligible when any act is."""
    flags = {"patient_question_pending": spec["patient_question_pending"], "readback_pending": spec["readback_pending"]}
    if spec.get("acts"):
        recs = [act_gold(x["act"], x["situation"], spec["step"], flags) for x in spec["acts"]]
    elif spec["objective"] == "NONE":
        recs = []
    else:
        recs = [act_gold(spec["objective"], spec["situation"], spec["step"], flags)]
    return {"acts": recs, "performs_now": any(r["performs_now"] for r in recs), "credit_eligible": any(r["credit_eligible"] for r in recs),
            "credit_acts": [r["act"] for r in recs if r["credit_eligible"]], "critical": any(r["critical"] for r in recs) or spec.get("critical_topic", False)}
