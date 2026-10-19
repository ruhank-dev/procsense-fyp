"""Interpretation route (p1-ai-language-s11, s12; ADR-0022): `POST /interpret/organisations/{id}` with the engine's context, the transcript and an utterance id: evidence the engine may take, one clarification, abstention or the menu.

Nothing is credited here: an OFFER_EVIDENCE is an offer, the client applies it to the session as a `speech_evidence` action and the engine's evidence store accepts or refuses. A signed-in member, audited (hash of the text,
the decision and reason codes), rate limited with the other AI calls, one model slot. With the `speech_interpretation` gate off, the classifier or the embedder down, the slot busy or the answer slow, the reply is the
menu and no evidence. The route never raises for a service failure.
"""
from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException

from server import ai_features
from server.config import Settings, get_settings
from server.interpret.decide import Interpreter, menu_for
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway
from server.routers.classify import get_classifier
from server.schemas.interpret import InterpretIn, InterpretOut

router = APIRouter(tags=["interpretation"])


def get_interpreter(clf=Depends(get_classifier)) -> Interpreter:
    return Interpreter(clf)


def _menu(body: InterpretIn, reason: str) -> InterpretOut:
    return InterpretOut(decision="MENU", reasons=[reason], menu=menu_for(body.context))


@router.post("/interpret/organisations/{organisation_id}", response_model=InterpretOut)
def interpret(body: InterpretIn, call: AiCallRecord = Depends(audited("interpret")), settings: Settings = Depends(get_settings), interpreter: Interpreter = Depends(get_interpreter)) -> InterpretOut:
    call.set_input(body.utterance)
    if not ai_features.enabled("speech_interpretation"):
        call.fell_back("FEATURE_OFF")
        return _menu(body, "FEATURE_OFF")
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return _menu(body, "MODEL_BUSY")
    try:
        out = interpreter.interpret(body.context, body.utterance, body.utterance_id, body.clarifications_used)
    except Exception:                                              # a failure of the classifier or the embedder is a menu, never an error to the trainee
        call.fell_back("INTERPRET_ERROR")
        return _menu(body, "INTERPRET_ERROR")
    finally:
        ai_gateway._slot.release()
    call.set_model(getattr(interpreter.clf, "model", None) or "none")
    if any(r in ("NO_RANKING", "CLASSIFIER_UNAVAILABLE", "NO_MODEL", "EMBEDDING_UNAVAILABLE") for r in out["reasons"]):
        call.fell_back(out["reasons"][0])
    else:
        call.validated(True)
        call.set_output(ref=out["decision"])
    return InterpretOut(**out)
