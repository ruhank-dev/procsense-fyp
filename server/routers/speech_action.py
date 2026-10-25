"""Spoken questions as simulator actions (DT-251, ADR-0027): `POST /speech/organisations/{id}/action` with the transcript, the ids of the actions available now and the recent turns. The answer names the
action ids the trainee asked for in words and who they spoke to; the client then starts those actions exactly as the tablet button would, so the engine decides what happens and what it scores. A signed-in
member, audited (hashes only), rate limited with the other AI calls. The transcript is the trainee's words: it goes to a cloud model only under the same gates as the dialogue (a declared synthetic deployment,
or the institutional approval and this person's own cloud consent). No model, no consent, a refusal or an unreadable answer gives no action and the tablet remains.
"""
from __future__ import annotations

import time
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session as DbSession

from server import ai_features
from server.auth.dependencies import db_session
from server.config import Settings, get_settings
from server.llm import client as client_mod, routing
from server.middleware.audit import AiCallRecord, audited
from server.repositories import privacy as privacy_repository
from server.routers import ai_gateway
from server.speech_action.service import SpeechActions, catalogue

router = APIRouter(tags=["speech actions"])


class Turn(BaseModel):
    who: str = Field(max_length=24)
    text: str = Field(max_length=400)


class ActionIn(BaseModel):
    utterance: str = Field(min_length=1, max_length=400)
    available: list[str] = Field(default=[], max_length=40)
    turns: list[Turn] = Field(default=[], max_length=6)


class ChosenAction(BaseModel):
    id: str
    verb: str
    key: str = ""
    value: str = ""


class ActionOut(BaseModel):
    actions: list[ChosenAction]
    addressee: str
    source: str                          # model | fallback
    reasons: list[str] = []
    cloud: bool = False
    model: str = ""
    ms: int = 0


@lru_cache
def get_speech_actions() -> SpeechActions:
    s = get_settings()
    return SpeechActions(client_mod.OllamaClient(s.ollama_url) if s.ollama_url else None)


@router.post("/speech/organisations/{organisation_id}/action", response_model=ActionOut)
def speech_action(body: ActionIn, call: AiCallRecord = Depends(audited("speech_action")), settings: Settings = Depends(get_settings), sa: SpeechActions = Depends(get_speech_actions),
                  db: DbSession = Depends(db_session)) -> ActionOut:
    with routing.consent(privacy_repository.cloud_consent(db, call.user_id)):
        return _interpret(body, call, settings, sa)


def _interpret(body: ActionIn, call: AiCallRecord, settings: Settings, sa: SpeechActions) -> ActionOut:
    started = time.monotonic()
    cat = catalogue()
    call.set_prompt("speech.action:v1", context_id="")
    call.set_input(body.utterance)

    def out(r, cloud=False, model="") -> ActionOut:
        chosen = [ChosenAction(id=i, verb=cat[i]["verb"], key=cat[i].get("key", ""), value=cat[i].get("value", "")) for i in r.actions if i in cat]
        return ActionOut(actions=chosen, addressee=r.addressee, source=r.source, reasons=r.reasons, cloud=cloud, model=model, ms=round((time.monotonic() - started) * 1000))
    from server.speech_action.service import SpokenAction
    if not ai_features.enabled("speech_actions"):
        call.fell_back("FEATURE_OFF")
        return out(SpokenAction([], "nobody", "fallback", ["FEATURE_OFF"]))
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    route = routing.resolve("speech_action", "speech.action", "live")
    held = False
    if not route.cloud:
        if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
            call.fell_back("MODEL_BUSY")
            return out(SpokenAction([], "nobody", "fallback", ["MODEL_BUSY"]))
        held = True
    try:
        r = sa.interpret(body.available, [t.model_dump() for t in body.turns], body.utterance)
    finally:
        if held:
            ai_gateway._slot.release()
    audit = r.audit or {}
    call.set_model(audit.get("model") or "none")
    if r.source == "fallback":
        call.validated(False, r.reasons or ["REJECTED"])
        call.fell_back((r.reasons or ["REJECTED"])[0])
    else:
        call.validated(True)
    call.set_output(ref=",".join(r.actions) or "none")
    return out(r, cloud=bool(audit.get("cloud")), model=audit.get("model", ""))
