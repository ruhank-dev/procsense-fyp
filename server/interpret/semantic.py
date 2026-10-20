"""The semantic interpreter (p1-ai-language-s18, ADR-0024): the shipped gate-based Interpreter plus a large-language-model READER behind deterministic checks.

    out = SemanticInterpreter(classifier, SemanticReader(OllamaCloudProvider(...))).interpret(context, "Do you sleep well at night?", "u-1")      # an InterpretOut-shaped dict, like Interpreter.interpret

Order, every step only able to REMOVE an offer:
 1. The shipped Interpreter runs first, unchanged, on the same gate result. A critical topic (cue or critical act), an unavailable classifier, no eligible objective, a repeated-clarification limit or a non-performed sentence (denied, planned, hypothetical, reported, another speaker, broken off, quoted, retracted, hedged...) is final: the model is NOT called (no cost, and nothing leaves the machine for those).
 2. Otherwise the reader is asked, once, for a validated reading (strict schema, bounded retries; server/interpret/llm). Any failure (unavailable, rejected reply, timeout, a 401/402/403/429) returns the shipped Interpreter's decision with a reason code: the model's absence never changes what the shipped path would do.
 3. The model's SUPPORTED finding is a CANDIDATE. Exactly one candidate that is a non-critical objective is considered; none, an UNCERTAIN finding or several leaves the shipped decision or abstains (a model that does not support what the gate offered VETOES it); a critical objective is the menu, never a credit.
 4. The candidate then passes the SAME deterministic validation as every other act (eligibility by behaviour window, a waiting patient question for ANSWER_QUESTION, a waiting read-back for CLOSED_LOOP, the behaviour not already credited, the assertion-status check of the sentence) and the critical-mass cap of the gate's head. Valid JSON, a stated reason or a stated certainty prove nothing.
 5. The confidence handed to the engine is (default mode `head`) the HEAD's probability for that objective (not an invented number), so the engine's per-act evidence thresholds, window, duplicate and critical checks still apply to the candidate exactly as to any other evidence, and a candidate below its engine threshold is abstained here with a code instead of being offered to be refused.
Physical or procedural credit still comes only from verified simulation events; the model never awards credit, and the engine's EvidenceStore is the authority. `speech_interpretation` stays OFF and this class is only built when `interpret_llm_enabled` is set."""
from __future__ import annotations

import json
from pathlib import Path

try:
    from classify.guard import critical_cue
    from interpret.decide import Interpreter, menu_for
    from interpret.status import status_of
    from interpret.validate import eligible_entry, validate
except ImportError:
    from server.classify.guard import critical_cue
    from server.interpret.decide import Interpreter, menu_for
    from server.interpret.status import status_of
    from server.interpret.validate import eligible_entry, validate

from server.interpret.llm.prompt import critical_objective_ids
from server.interpret.llm.reader import SemanticReader

ROOT = Path(__file__).resolve().parents[2]
MAPPING = ROOT / "content" / "assessment" / "mapping.json"
SEMANTIC_VERSION = "semantic-2026-10-20.1"


class _Memo:
    """Hands the shipped Interpreter the gate result already computed, so the embedding is asked for once. Everything else is the classifier's own."""

    def __init__(self, clf):
        self._clf, self.last = clf, None
        self.embedding = getattr(clf, "embedding", None)
        self.model = getattr(clf, "model", None)
        self.critical = getattr(clf, "critical", set())

    def classify_with_probs(self, sentence):
        self.last = self._clf.classify_with_probs(sentence)
        return self.last

    def __getattr__(self, name):
        return getattr(self._clf, name)


def thresholds() -> tuple[dict, float]:
    t = json.loads(MAPPING.read_text(encoding="utf-8"))["thresholds"]
    return t["per_act"], t["default"]


class SemanticInterpreter:
    def __init__(self, classifier, reader: SemanticReader | None, confidence_mode: str = "head"):
        if confidence_mode not in ("head", "engine_floor"):
            raise ValueError("confidence_mode must be head or engine_floor")
        self.clf, self.reader, self.confidence_mode = classifier, reader, confidence_mode
        self.model = getattr(classifier, "model", None)
        self.last_reading = None                 # the ReadOutcome of the last call (diagnostics and evaluation; never credit)

    def _tag(self, out: dict, *reasons: str) -> dict:
        out = dict(out)
        out["reasons"] = list(out.get("reasons", [])) + list(reasons)
        out["source"] = "interpret+llm"
        return out

    def interpret(self, context, utterance: str, utterance_id: str, clarifications_used: int = 0) -> dict:
        self.last_reading = None
        memo = _Memo(self.clf)
        base = Interpreter(memo).interpret(context, utterance, utterance_id, clarifications_used)
        if self.reader is None or memo.last is None:
            return base
        result, probs = memo.last
        if base["decision"] == "MENU" or result.source == "unavailable" or not probs or any(r in ("NO_ELIGIBLE_OBJECTIVE", "CRITICAL_TOPIC") for r in base["reasons"]):
            return base
        status = status_of(utterance)
        if status not in ("actual", "question"):                 # a denial, plan, report, quotation, retraction, hedge, fragment...: the shipped refusal is final and nothing is sent to the model
            return base
        read = self.reader.read(context, utterance, key=utterance_id)
        self.last_reading = read
        if read.status != "VALID":
            return self._tag(base, "LLM_" + read.status, *["LLM_" + c.split(":")[0] for c in read.codes[-1:]])
        findings = read.reading.findings
        critical = critical_objective_ids()
        supported = [f for f in findings if f.verdict == "SUPPORTED"]       # strict.parse already requires trainee + actual + evidence in the utterance for these
        uncertain = [f for f in findings if f.verdict == "UNCERTAIN"]
        offered = base["decision"] == "OFFER_EVIDENCE"
        if any(f.objective in critical for f in supported):                # the model read critical content: the menu of the verified actions, never a credit
            return self._tag({"decision": "MENU", "reasons": ["CRITICAL_TOPIC"], "evidence": None, "clarify": "", "options": [], "menu": menu_for(context), "points_to": [], "source": "interpret"}, "LLM_NAMED_CRITICAL")
        if len(supported) > 1:
            return self._tag({**base, "decision": "ABSTAIN", "evidence": None, "clarify": "", "options": []}, "LLM_MULTIPLE_SUPPORTED")
        if not supported:
            if offered:                                                    # the model does not support what the gate offered: veto
                return self._tag({**base, "decision": "ABSTAIN", "evidence": None}, "LLM_NOT_SUPPORTED" if not uncertain else "LLM_UNCERTAIN")
            return self._tag(base, "LLM_NONE_SUPPORTED")
        f = supported[0]
        act = f.objective
        if offered and base["evidence"]["act"] == act:
            return self._tag(base, "LLM_AGREES")
        reasons = validate(act, context, utterance, status, True, require_gate=False)
        if reasons:
            return self._tag({**base, "decision": "ABSTAIN", "evidence": None, "clarify": "", "options": []}, *reasons, "LLM_CANDIDATE_REFUSED")
        mass = memo.embedding.critical_mass(probs) if memo.embedding is not None else 0.0
        cap = memo.embedding.head.get("critical_mass_max") if memo.embedding is not None else None
        if cap is not None and mass >= cap:
            return self._tag({**base, "decision": "ABSTAIN", "evidence": None, "clarify": "", "options": []}, "CRITICAL_MASS", "LLM_CANDIDATE_REFUSED")
        confidence = round(float(probs.get(act, 0.0)), 3)
        per_act, default = thresholds()
        floor = per_act.get(act, default)
        if self.confidence_mode == "engine_floor":                          # PENDING the owner (DT-236): a model-qualified candidate is offered at the engine's own threshold for that act; the engine's other checks still apply. NOT the default.
            confidence = max(confidence, floor)
        elif confidence < floor:
            return self._tag({**base, "decision": "ABSTAIN", "evidence": None, "clarify": "", "options": []}, "LLM_BELOW_EVIDENCE_THRESHOLD")
        entry = eligible_entry(context, act)
        return self._tag({"decision": "OFFER_EVIDENCE", "reasons": [], "evidence": {"act": act, "behaviour": entry.behaviour, "span": f.evidence.strip()[:300], "status": status, "confidence": confidence, "utterance_id": utterance_id},
                          "clarify": "", "options": [], "menu": [], "points_to": [], "source": "interpret"}, "LLM_CANDIDATE")
