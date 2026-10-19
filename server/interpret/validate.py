"""Deterministic validation of one candidate act (p1-ai-language-s11, ADR-0022). Every rule only removes an offer; nothing here, and no model or classifier confidence, can override a refusal.

    reasons = validate("ASK_TOPIC", context, "Do you sleep well at night?", status="question", gate_credited=True)      # [] means the act may be offered to the engine

Rules, in order: the act is eligible now (a behaviour window of the engine's context contains a running step) and is not critical; the status fits the act (a statement act is not performed by asking; a denied, imagined, reported,
intended, other-speaker or broken-off sentence performs nothing); ANSWER_QUESTION needs a patient question waiting; CLOSED_LOOP needs a critical result waiting for read-back; the behaviour is not already credited from speech;
and, unless the caller is only preparing a clarification, the classifier gate (hybrid head, TF-IDF agreement, critical-mass cap, critical cues, assertion check, per-act threshold) credited it. The engine's EvidenceStore applies its
own checks again (window, duplicate, threshold, critical): this is the first line, that is the authority.
"""
from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PHRASES = ROOT / "content" / "ai" / "interpret_phrases.json"


@lru_cache
def phrases() -> dict:
    return json.loads(PHRASES.read_text(encoding="utf-8"))


def eligible_entry(context, act: str):
    """The eligible entry for the act (the first whose behaviour is not yet credited, else the first), or None."""
    entries = [e for e in context.eligible if e.act == act]
    fresh = [e for e in entries if e.behaviour not in context.accepted]
    return (fresh or entries or [None])[0]


def validate(act: str, context, sentence: str, status: str, gate_credited: bool, require_gate: bool = True) -> list[str]:
    reasons: list[str] = []
    entry = eligible_entry(context, act)
    if entry is None:
        return ["NOT_ELIGIBLE"]
    if entry.critical:
        return ["CRITICAL_NEVER_FROM_SPEECH"]
    if status == "question":
        if act not in phrases()["question_ok_acts"]:
            reasons.append("QUESTION_NOT_PERFORMING_ACT")
    elif status != "actual":
        reasons.append("NOT_PERFORMED:" + status)
    if act == "ANSWER_QUESTION" and not context.patient_question_pending:
        reasons.append("NO_PENDING_QUESTION")
    if act == "CLOSED_LOOP" and not context.readback_pending:
        reasons.append("NO_READBACK_PENDING")
    if entry.behaviour in context.accepted:
        reasons.append("ALREADY_ACCEPTED")
    if require_gate and not gate_credited:
        reasons.append("GATE_NOT_CREDITED")
    return reasons
