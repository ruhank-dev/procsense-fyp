"""Spoken orders with read-back (features C32, C35; p1-voice-input-s03): the route the headset's voice client calls after the recogniser.

`POST /orders/organisations/{id}/hear` with {"text"}: the transcript becomes a read-back sentence ("Confirm: order full blood count?") and a signed ticket, or a clarifying question, or the menu. NOTHING is released.
`POST /orders/organisations/{id}/confirm` with {"ticket", "spoken"}: a yes releases the action the ticket names (and only that), a no cancels it, anything else leaves the question open. The action is for the
engine to accept or refuse like any menu action; this route only tells the client what the trainee confirmed. Signed-in member, audited (hash of the text, intent and outcome as codes), rate limited with the other AI
calls. Without the model (or with the `order_fallback` gate off) the grammar answers alone; with the `voice_orders` gate off the answer is the menu. The menu path never depends on this route.
"""
from __future__ import annotations

import logging
import re
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session as DbSession

from server import ai_features
from server.config import Settings, get_settings
from server.auth.dependencies import db_session
from server.llm import client as client_mod, routing
from server.middleware.audit import AiCallRecord, audited
from server.orders import ticket
from server.orders.grammar import NO, YES, Entry, Hearing, ReadBack, vocabulary
from server.orders.cloud_interpreter import CloudOnlyInterpreter
from server.orders.interpreter import OrderInterpreter  # noqa: F401  (the tests build the plain interpreter)
from server.repositories import privacy as privacy_repository
from server.routers import ai_gateway

router = APIRouter(tags=["spoken orders"])
log = logging.getLogger("procsense.orders")


class HearIn(BaseModel):
    text: str = Field(min_length=1, max_length=300)
    context: dict | str | None = None                 # what the headset knows (the patient, what is ordered or given, the previous turn, a read-back waiting for an answer); read only by the interpreter, never trusted as an instruction


class HearOut(BaseModel):
    status: str                                   # confirm | removed_dose_confirm | clarify | menu
    intent: str | None = None
    label: str = ""
    read_back: str = ""
    options: list[str] = []
    dose_removed: bool = False
    source: str = "grammar"                       # grammar | model
    interpretation: str = ""                      # what the semantic interpreter took the sentence for; empty when the grammar answered
    ticket: str = ""                              # present only when there is something to confirm
    error: str = ""
    interpreter: str = "offline_grammar"          # which system answered: gemma_cloud (Gemma 4 31B Cloud read the sentence) | offline_grammar (the conservative grammar with its question guard)
    interpreter_model: str = ""                   # the model asked, when the cloud answered
    interpreter_reported_model: str = ""          # the model name the host reported in its reply: evidence that the cloud model, not a stand-in, answered
    fallback_reason: str = ""                     # why the grammar answered instead (see server/orders/cloud_interpreter.py); empty when the cloud answered
    interpreter_ms: int = 0


class ConfirmIn(BaseModel):
    ticket: str = Field(min_length=10, max_length=2000)
    spoken: str = Field(min_length=1, max_length=60)


class ConfirmOut(BaseModel):
    outcome: str                                  # released | cancelled | open | refused
    action: dict | None = None
    reason: str = ""


@lru_cache
def get_vocabulary() -> list[Entry]:
    return vocabulary()


def get_mapper(settings: Settings = Depends(get_settings)):
    """The semantic interpreter (ADR-0028), only when the host is configured and the `order_fallback` gate is on; otherwise the grammar answers alone. (The name is kept: the older mapper is no longer in this path.)"""
    if not settings.ollama_url or not ai_features.enabled("order_fallback"):
        return None
    return CloudOnlyInterpreter(client_mod.OllamaClient(settings.ollama_url))


@router.post("/orders/organisations/{organisation_id}/hear", response_model=HearOut)
def hear(body: HearIn, call: AiCallRecord = Depends(audited("orders")), settings: Settings = Depends(get_settings), mapper=Depends(get_mapper), db: DbSession = Depends(db_session)) -> HearOut:
    call.set_input(body.text)
    if not ai_features.enabled("voice_orders"):
        call.fell_back("FEATURE_OFF")
        return HearOut(status="menu", read_back="Spoken orders are off. Please use the menu.", error="FEATURE_OFF")
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    rb = ReadBack(get_vocabulary(), None, mapper)
    reason = "NO_MODEL_HOST" if not settings.ollama_url else "ORDER_INTERPRETER_OFF"          # why the grammar answers alone when no interpreter is built (no model host, or the order_fallback gate is off)
    if mapper is not None:
        if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
            call.fell_back("MODEL_BUSY")
            reason = "MODEL_BUSY"
            rb = ReadBack(get_vocabulary(), None)
            h: Hearing = rb.hear(body.text)
        else:
            try:
                with routing.consent(privacy_repository.cloud_consent(db, call.user_id)):          # the person's own optional consent decides whether their words may go to the cloud (ADR-0027); without it the local model or the grammar answers
                    h = rb.hear(body.text, body.context)
            finally:
                ai_gateway._slot.release()
    else:
        h = rb.hear(body.text)
    last = getattr(mapper, "last", None) or {}
    if h.source == "model":
        call.set_model("orders_interpret")
        used, reason = "gemma_cloud", ""
    else:
        used = "offline_grammar"
        reason = last.get("reason", reason) if mapper is not None and reason != "MODEL_BUSY" else reason
        if mapper is not None and reason != "MODEL_BUSY":
            call.fell_back(reason)
    log.info("spoken order: interpreter=%s model=%s reported_model=%s fallback_reason=%s ms=%s status=%s interpretation=%s", used, last.get("model", "") if used == "gemma_cloud" else "", last.get("reported_model", "") if used == "gemma_cloud" else "", reason or "-", last.get("ms", 0), h.status, h.interpretation or "-")
    call.validated(True)
    call.set_output(ref=f"{h.status}:{h.intent}:{h.source}")
    t = ticket.issue(call.user_id, h.intent, h.slots, h.action) if h.status in ("confirm", "removed_dose_confirm") else ""
    return HearOut(status=h.status, intent=h.intent, label=h.slots.get("label", ""), read_back=h.read_back, options=h.options, dose_removed=h.dose_removed, source=h.source, interpretation=h.interpretation, ticket=t, interpreter=used, interpreter_model=last.get("model", "") if used == "gemma_cloud" else "", interpreter_reported_model=last.get("reported_model", "") if used == "gemma_cloud" else "", fallback_reason=reason, interpreter_ms=last.get("ms", 0))


@router.post("/orders/organisations/{organisation_id}/confirm", response_model=ConfirmOut)
def confirm(body: ConfirmIn, call: AiCallRecord = Depends(audited("orders"))) -> ConfirmOut:
    call.set_input(body.spoken)
    try:
        claims = ticket.open_ticket(body.ticket, call.user_id)
    except ticket.TicketError as e:
        call.fell_back(str(e))
        return ConfirmOut(outcome="refused", reason=str(e))
    answer = re.sub(r"[^a-z' ]+", " ", body.spoken.lower()).strip()
    if answer in YES or any(answer.startswith(y + " ") for y in YES):
        ticket.decide(claims)
        call.validated(True)
        call.set_output(ref="released" if claims["a"] else "logged_only")
        return ConfirmOut(outcome="released", action=claims["a"], reason="" if claims["a"] else "NO_ENGINE_VERB")      # a hold of a home medicine releases to hold_medicine (DT-208); a hold of an ordered treatment has no engine verb and is read back and logged only
    if answer in NO or answer.split(" ")[0] in NO:
        ticket.decide(claims)
        call.validated(True)
        call.set_output(ref="cancelled")
        return ConfirmOut(outcome="cancelled")
    call.set_output(ref="open")
    return ConfirmOut(outcome="open", reason="ANSWER_NOT_UNDERSTOOD")
