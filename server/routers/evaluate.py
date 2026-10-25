"""Speech evaluation route (p1-ai-language-s22, ADR-0025): `POST /evaluate/organisations/{id}` with the engine's context, the transcript and an utterance id; the answer is the semantic evaluator's structured JUDGEMENT of what the speech does
(objectives, assertion, actor, grades, a physical-action claim note). Nothing is scored or credited here: the client applies each judged objective to the session as a `speech_evidence` action and the engine decides what it is worth from its own state.
A signed-in member, audited (hash of the text, the decision and its reason codes, never the words), rate limited with the other AI calls, one model slot. Without the `speech_evaluation` gate (OFF until an independent evaluation passes), without a model, on a time-out
or a reply that breaks the contract twice, the answer is UNAVAILABLE with the menu of existing verified actions and no speech credit follows. The route never raises for a service failure.
"""
from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException

from server import ai_features
from server.config import Settings, get_settings
from server.evaluate import prompt as P
from server.evaluate import service
from server.evaluate.provider import OllamaCloudProvider
from server.evaluate.reader import Reader
from server.llm import routing
from server.auth.dependencies import db_session
from server.repositories import privacy as privacy_repository
from sqlalchemy.orm import Session as DbSession
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway
from server.schemas.evaluate import EvaluateIn, EvaluateOut

router = APIRouter(tags=["evaluation"])


def get_reader(settings: Settings = Depends(get_settings)) -> Reader | None:
    """The Gemma evaluator on Ollama Cloud: through a signed-in local Ollama (empty base url, the `-cloud` model name) or the direct API (base https://ollama.com and a key). None when no model host is configured."""
    base = settings.speech_eval_base_url or settings.ollama_url
    if not base or not settings.speech_eval_model:
        return None
    provider = OllamaCloudProvider(settings.speech_eval_model, base, settings.speech_eval_api_key.get_secret_value(), timeout=settings.speech_eval_timeout_seconds)
    return Reader(provider, settings.speech_eval_max_attempts, settings.speech_eval_budget_ms, settings.speech_eval_timeout_seconds)


def _sends_to_cloud(settings: Settings) -> bool:
    return routing.is_cloud(settings.speech_eval_model) or "ollama.com" in (settings.speech_eval_base_url or "")


@router.post("/evaluate/organisations/{organisation_id}", response_model=EvaluateOut)
def evaluate_speech(body: EvaluateIn, call: AiCallRecord = Depends(audited("evaluate")), settings: Settings = Depends(get_settings), reader: Reader | None = Depends(get_reader), db: DbSession = Depends(db_session)) -> EvaluateOut:
    with routing.consent(privacy_repository.cloud_consent(db, call.user_id)):
        return _evaluate(body, call, settings, reader)


def _evaluate(body: EvaluateIn, call: AiCallRecord, settings: Settings, reader: Reader | None) -> EvaluateOut:
    context = body.context.model_dump()
    call.set_input(body.utterance)
    call.set_prompt(P.VERSION, context_id="")                          # which prompt produced the judgement (no text)
    if not ai_features.enabled("speech_evaluation"):
        call.fell_back("FEATURE_OFF")
        return EvaluateOut(**service.unavailable(context, body.utterance_id, ["FEATURE_OFF"]))
    if reader is not None and _sends_to_cloud(settings) and not routing.trainee_cloud_allowed():
        call.fell_back("CLOUD_DATA_NOT_APPROVED")                       # no consent or no approval: the evaluator's model is a cloud model: the trainee's words go there only once the privacy decision is recorded (DT-248) or the deployment is declared synthetic
        return EvaluateOut(**service.unavailable(context, body.utterance_id, ["CLOUD_DATA_NOT_APPROVED"]))
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return EvaluateOut(**service.unavailable(context, body.utterance_id, ["MODEL_BUSY"]))
    try:
        answer, outcome = service.evaluate(reader, context, body.utterance, body.utterance_id)
    except Exception:                                                  # a failure of the evaluator is the menu, never an error to the trainee
        answer, outcome = service.unavailable(context, body.utterance_id, ["EVAL_ERROR"]), None
    finally:
        ai_gateway._slot.release()
    call.set_model((outcome.model if outcome and outcome.model else settings.speech_eval_model) or "none")
    if answer["decision"] == "EVALUATED":
        call.validated(True, [])
        call.set_output(ref="EVALUATED")
    elif answer["reasons"] and answer["reasons"][0] in ("NOT_CONFIGURED", "EVAL_ERROR"):
        call.fell_back(answer["reasons"][0])
    else:
        call.validated(False, answer["reasons"])
        call.set_output(ref="UNAVAILABLE")
    return EvaluateOut(**answer)
