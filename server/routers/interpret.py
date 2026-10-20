"""Interpretation route (p1-ai-language-s11, s12; ADR-0022): `POST /interpret/organisations/{id}` with the engine's context, the transcript and an utterance id: evidence the engine may take, one clarification, abstention or the menu.

Nothing is credited here: an OFFER_EVIDENCE is an offer, the client applies it to the session as a `speech_evidence` action and the engine's evidence store accepts or refuses. A signed-in member, audited (hash of the text,
the decision and its reason codes: every decision, refusals included, so a refusal can be counted by cause without keeping what was said), rate limited with the other AI calls, one model slot. With the `speech_interpretation` gate off, the classifier or the embedder down (the embedder call has its own
10 s timeout, there is no other wall-clock limit here), or the slot busy, the reply is the menu and no evidence. The route never raises for a service failure. A client that goes away mid-request changes nothing: the route credits nothing, so a discarded answer is only an unused offer.
"""
from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException

from server import ai_features
from server.config import Settings, get_settings
from server.interpret.decide import INTERPRET_VERSION, Interpreter, menu_for, reason_codes, safe_interpret
from server.interpret.semantic import SEMANTIC_VERSION
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway
from server.routers.classify import get_classifier
from server.schemas.interpret import InterpretIn, InterpretOut

router = APIRouter(tags=["interpretation"])


def get_interpreter(clf=Depends(get_classifier), settings: Settings = Depends(get_settings)):
    """The shipped gate-based interpreter; with `interpret_llm_enabled` (off by default) the semantic interpreter, which asks an Ollama Cloud model for a candidate reading behind the same deterministic checks and falls back to the shipped decision on any failure (ADR-0024)."""
    if not settings.interpret_llm_enabled:
        return Interpreter(clf)
    from server.interpret.llm.provider import OllamaCloudProvider
    from server.interpret.llm.reader import SemanticReader
    from server.interpret.semantic import SemanticInterpreter
    key = settings.interpret_llm_api_key.get_secret_value()
    base = settings.interpret_llm_base_url or settings.ollama_url
    provider = OllamaCloudProvider(settings.interpret_llm_model, base, key, timeout=settings.interpret_llm_timeout_seconds)
    return SemanticInterpreter(clf, SemanticReader(provider, settings.interpret_llm_max_attempts, settings.interpret_llm_budget_ms, settings.interpret_llm_timeout_seconds))


def _menu(body: InterpretIn, reason: str) -> InterpretOut:
    return InterpretOut(decision="MENU", reasons=[reason], menu=menu_for(body.context))


@router.post("/interpret/organisations/{organisation_id}", response_model=InterpretOut)
def interpret(body: InterpretIn, call: AiCallRecord = Depends(audited("interpret")), settings: Settings = Depends(get_settings), interpreter: Interpreter = Depends(get_interpreter)) -> InterpretOut:
    call.set_input(body.utterance)
    call.set_prompt(INTERPRET_VERSION, context_id="")                  # which rules produced the decision (no text)
    if not ai_features.enabled("speech_interpretation"):
        call.fell_back("FEATURE_OFF")
        return _menu(body, "FEATURE_OFF")
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return _menu(body, "MODEL_BUSY")
    try:
        out, failed = safe_interpret(interpreter, body.context, body.utterance, body.utterance_id, body.clarifications_used)       # a failure of the classifier or the embedder is a menu, never an error to the trainee
    finally:
        ai_gateway._slot.release()
    if failed:
        call.fell_back("INTERPRET_ERROR")
        return _menu(body, "INTERPRET_ERROR")
    call.set_model(getattr(interpreter.clf, "model", None) or "none")
    reading = getattr(interpreter, "last_reading", None)
    if reading is not None:                                                  # which model read the utterance and how the reading went: codes and counts only, never the words
        call.set_prompt(SEMANTIC_VERSION, context_id=(reading.model or "")[:60])
    if any(r in ("NO_RANKING", "CLASSIFIER_UNAVAILABLE", "NO_MODEL", "EMBEDDING_UNAVAILABLE") for r in out["reasons"]):
        call.fell_back(out["reasons"][0])
    else:
        call.validated(out["decision"] == "OFFER_EVIDENCE", reason_codes(out["reasons"], out["decision"]))      # passed = an offer; every refusal keeps its codes (counts by cause, never the words)
        call.set_output(ref=out["decision"])
    return InterpretOut(**out)
