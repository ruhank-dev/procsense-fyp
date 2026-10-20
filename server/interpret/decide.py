"""The decision for one utterance (p1-ai-language-s11, s12; ADR-0022): OFFER_EVIDENCE, CLARIFY (once), ABSTAIN or MENU.

    out = Interpreter(classifier).interpret(context, "Do you sleep well at night?", "u-17")        # an InterpretOut-shaped dict

1. The retained classifier gate (hybrid head, TF-IDF agreement, critical-mass cap, critical cues, assertion check; ADR-0020) names the act and ranks the others. Context only ever REMOVES: the act is offered only if the
   engine's context makes it eligible (validate.py); a lower-ranked act is never promoted into an offer.
2. A critical topic (a critical act, a critical cue) is never offered: the answer is the menu, pointing at the verified actions that earn that credit (wristband, allergy check, consent, handover, escalation).
3. An act the gate or the rules refuse for a SAFETY reason (denied, planned, imagined, reported, another speaker, broken off, critical mass, critical cue) is abstained from: nothing is asked, nothing is credited.
4. Uncertainty that is not a safety refusal gets ONE clarification (`clarifications_used` < 1): the two readers disagree, the head is below its threshold between two eligible acts, or a patient's question is waiting and the
   statement could be the answer or an explanation. The question is built from approved wording (content/ai/interpret_phrases.json), never from a model. After that, the menu of the eligible behaviours' actions.
5. A service that is down or an embedder that answers nothing gives the menu. Nothing here credits: the engine's EvidenceStore decides, and a critical act can never be credited from speech.
"""
from __future__ import annotations

import json
from pathlib import Path

try:
    from classify.guard import critical_cue
    from interpret.status import status_of
    from interpret.validate import eligible_entry, phrases, validate
except ImportError:
    from server.classify.guard import critical_cue
    from server.interpret.status import status_of
    from server.interpret.validate import eligible_entry, phrases, validate

ROOT = Path(__file__).resolve().parents[2]
MAPPING = ROOT / "content" / "assessment" / "mapping.json"
INTERPRET_VERSION = "interpret-2026-10-20.2"       # the rule set behind a decision (audit rows carry it); bump when a rule changes
CLARIFY_LIMIT = 1
CLARIFY_TOP_MIN, CLARIFY_SECOND_MIN, PENDING_ANSWER_MIN = 0.35, 0.15, 0.25
CRITICAL_ACT_CUE = {"CONFIRM_IDENTITY": "identity", "STATE_ALLERGY": "allergy", "CONSENT_SIGN": "consent", "HANDOVER_CRITICAL_ELEMENT": "handover", "SBAR_ELEMENT": "handover"}
SAFETY_PREFIXES = ("NOT_PERFORMED", "CRITICAL", "NOT_ELIGIBLE", "ALREADY_ACCEPTED", "QUESTION_NOT_PERFORMING_ACT", "NO_PENDING_QUESTION", "NO_READBACK_PENDING")


def reason_codes(reasons: list[str], decision: str = "") -> list[str]:
    """Audit-safe codes for a decision's reasons (server/middleware/audit.py keeps only A-Z, 0-9 and _): "NOT_PERFORMED:denied" -> "NOT_PERFORMED_DENIED". Reasons are rule names, statuses and act names, never words of the utterance; a decision with none is named by the decision itself."""
    out = []
    for r in reasons or [decision or "NO_REASON"]:
        code = "".join(c if c.isalnum() else "_" for c in str(r).upper()).strip("_")[:40] or "NO_REASON"
        out.append(code if code[0].isalpha() else "R_" + code[:38])
    return out


def _mapping() -> dict:
    return {b["id"]: b for b in json.loads(MAPPING.read_text(encoding="utf-8"))["behaviours"]}


def menu_for(context) -> list[dict]:
    """The existing menu actions of every behaviour that has an eligible act or whose window holds a running step (so the menu is never empty by accident of the AI acts)."""
    m, steps = _mapping(), set(context.steps)
    ids = {e.behaviour for e in context.eligible} | {b for b, v in m.items() if steps & set(v["steps"]) and v["actions"]}
    return [{"behaviour": b, "label": m[b]["label"], "actions": list(m[b]["actions"])} for b in sorted(ids) if m[b]["actions"]]


def safe_interpret(interpreter, context, utterance: str, utterance_id: str, clarifications_used: int = 0) -> tuple[dict, bool]:
    """interpreter.interpret, but a failure of the classifier or the embedder is the menu with no evidence, never an error (returns (decision, failed)). `except Exception` on purpose: a cancellation (asyncio.CancelledError, KeyboardInterrupt) is not caught, so it
    propagates and no decision, and so no offer, comes out of a request that was cancelled."""
    try:
        return interpreter.interpret(context, utterance, utterance_id, clarifications_used), False
    except Exception:
        return {"decision": "MENU", "reasons": ["INTERPRET_ERROR"], "evidence": None, "clarify": "", "options": [], "menu": menu_for(context), "points_to": [], "source": "interpret"}, True


class Interpreter:
    def __init__(self, classifier):
        self.clf = classifier

    def _out(self, decision: str, reasons: list[str], context=None, **extra) -> dict:
        out = {"decision": decision, "reasons": reasons, "evidence": None, "clarify": "", "options": [], "menu": [], "points_to": [], "source": "interpret"}
        out.update(extra)
        if decision == "MENU" and context is not None and not out["menu"]:
            out["menu"] = menu_for(context)
        return out

    def interpret(self, context, utterance: str, utterance_id: str, clarifications_used: int = 0) -> dict:
        if not hasattr(self.clf, "classify_with_probs"):
            return self._out("MENU", ["NO_RANKING"], context)
        result, probs = self.clf.classify_with_probs(utterance)
        if result.source == "unavailable" or not probs:
            return self._out("MENU", [result.error or "CLASSIFIER_UNAVAILABLE"], context)
        if not context.steps or not context.eligible:
            return self._out("ABSTAIN", ["NO_ELIGIBLE_OBJECTIVE"])
        cue, act = critical_cue(utterance), result.act
        if result.critical or cue:
            kind = cue or CRITICAL_ACT_CUE.get(act, "")
            return self._out("MENU", ["CRITICAL_TOPIC"], context, points_to=list(phrases()["critical_cue_actions"].get(kind, [])))
        if act == "NONE":
            return self._out("ABSTAIN", ["NO_ACT"])
        status = status_of(utterance)
        reasons = validate(act, context, utterance, status, bool(result.credited))
        mass, cap = self.clf.embedding.critical_mass(probs), self.clf.embedding.head.get("critical_mass_max")
        if cap is not None and mass >= cap:
            return self._out("ABSTAIN", ["CRITICAL_MASS"])
        pending_split = self._pending_split(context, act, probs, reasons)
        if not reasons and pending_split is None:
            entry = eligible_entry(context, act)
            return self._out("OFFER_EVIDENCE", [], evidence={"act": act, "behaviour": entry.behaviour, "span": utterance.strip()[:300], "status": status, "confidence": round(probs[act], 3), "utterance_id": utterance_id})
        if any(r.startswith(SAFETY_PREFIXES) for r in reasons) or result.refused.startswith("NON_ASSERTIVE"):
            return self._out("ABSTAIN", reasons + ([result.refused] if result.refused else []))
        second = pending_split or self._second(context, utterance, status, result, probs)
        if second is None:
            return self._out("ABSTAIN", reasons or ["LOW_CONFIDENCE"])
        if clarifications_used >= CLARIFY_LIMIT:
            return self._out("MENU", reasons + ["CLARIFICATION_LIMIT"], context)
        ph = phrases()["act_phrases"]
        return self._out("CLARIFY", reasons + ["AMBIGUOUS"], clarify=f"Did you mean {ph.get(act, act)}, or {ph.get(second, second)}?", options=[act, second])

    def _pending_split(self, context, act: str, probs: dict, reasons: list[str]) -> str | None:
        """A waiting patient question and an explanation that could be its answer: ask which (only when nothing else refused the act)."""
        if reasons or not context.patient_question_pending or act not in ("EXPLAIN_PROCEDURE", "EXPLAIN_TEST"):
            return None
        ok = eligible_entry(context, "ANSWER_QUESTION")
        return "ANSWER_QUESTION" if ok is not None and ok.behaviour not in context.accepted and probs.get("ANSWER_QUESTION", 0) >= PENDING_ANSWER_MIN else None

    def _second(self, context, utterance: str, status: str, result, probs: dict) -> str | None:
        """The other act to ask about, only if both readings are eligible, non-critical and plausible."""
        act = result.act
        other = result.refused.split(":", 1)[1] if result.refused.startswith("HEADS_DISAGREE:") else None
        if other is None:
            if probs[act] < CLARIFY_TOP_MIN:
                return None
            ranked = sorted((p, a) for a, p in probs.items() if a not in (act, "NONE"))
            other = next((a for p, a in reversed(ranked) if p >= CLARIFY_SECOND_MIN), None)
        if other is None or other == "NONE" or validate(other, context, utterance, status, False, require_gate=False):
            return None
        return None if validate(act, context, utterance, status, False, require_gate=False) else other
