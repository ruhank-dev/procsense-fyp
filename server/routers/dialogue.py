"""Dialogue routes (p1-ai-language-s04 and s05, p1-backend-s06): `POST /dialogue/organisations/{id}/patient` and `.../staff`.

The body carries the context the Core's builder made for the character (the Core decides what may be said), the question, and the lines to fall back on. A signed-in member, audited (hashes and reason codes),
rate limited, one model slot. The reply says where it came from (`model`, `cache`, `fallback`); a fallback is the approved authored line, never an error. See server/dialogue/dialogue.py.
"""
from __future__ import annotations

from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from server import ai_features
from server.config import Settings, get_settings
from server.dialogue.dialogue import Dialogue
from server.llm import client as client_mod
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway

router = APIRouter(tags=["dialogue"])


class DialogueIn(BaseModel):
    context: dict
    question: str = Field(min_length=1, max_length=400)
    style: str = Field(default="plain", max_length=120)
    age: int = Field(default=50, ge=0, le=120)
    sex: str = Field(default="M", pattern="^[MF]$")
    role: str = Field(default="nurse", max_length=24)
    authored: dict[str, str] = {}
    fallback_lines: dict[str, str] = {}
    asked_fact: str | None = None


class DialogueOut(BaseModel):
    text: str
    intent: str
    fact_ids: list[str]
    source: str
    reasons: list[str] = []


@lru_cache
def get_dialogue() -> Dialogue:
    s = get_settings()
    return Dialogue(client_mod.OllamaClient(s.ollama_url) if s.ollama_url else None)


def _run(body: DialogueIn, role: str, call: AiCallRecord, settings: Settings, d: Dialogue) -> DialogueOut:
    ctx = body.context
    if not isinstance(ctx.get("allowed_intents"), list) or not isinstance(ctx.get("facts"), list) or not ctx["allowed_intents"]:
        raise HTTPException(status_code=422, detail="context_needs_allowed_intents_and_facts")
    call.set_prompt(f"dialogue.{role}:v1", context_id=str(ctx.get("case", ""))[:40])
    if not ai_features.enabled("patient_dialogue"):
        call.fell_back("FEATURE_OFF")
        r = d._fallback(role, ctx, [f for f in ctx["facts"] if f.get("disclosable_now")], body.asked_fact, body.authored, body.fallback_lines, ["FEATURE_OFF"])
        return DialogueOut(text=r.text, intent=r.intent, fact_ids=r.fact_ids, source=r.source, reasons=r.reasons)
    call.set_input(body.question)
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        r = d._fallback(role, ctx, [f for f in ctx["facts"] if f.get("disclosable_now")], body.asked_fact, body.authored, body.fallback_lines, ["MODEL_BUSY"])
        return DialogueOut(text=r.text, intent=r.intent, fact_ids=r.fact_ids, source=r.source, reasons=r.reasons)
    try:
        r = d.reply(ctx, body.question, role if role == "patient" else body.role, body.style, body.age, body.sex, body.authored, body.fallback_lines, body.asked_fact)
    finally:
        ai_gateway._slot.release()
    call.set_model(d.model or "live")
    if r.source == "fallback":
        call.validated(False, r.reasons or ["REJECTED"])
        call.fell_back((r.reasons or ["REJECTED"])[0])
    else:
        call.validated(True)
    call.set_output(text=r.text)
    return DialogueOut(text=r.text, intent=r.intent, fact_ids=r.fact_ids, source=r.source, reasons=r.reasons)


@router.post("/dialogue/organisations/{organisation_id}/patient", response_model=DialogueOut)
def patient(body: DialogueIn, call: AiCallRecord = Depends(audited("dialogue_patient")), settings: Settings = Depends(get_settings), d: Dialogue = Depends(get_dialogue)) -> DialogueOut:
    return _run(body, "patient", call, settings, d)


@router.post("/dialogue/organisations/{organisation_id}/staff", response_model=DialogueOut)
def staff(body: DialogueIn, call: AiCallRecord = Depends(audited("dialogue_staff")), settings: Settings = Depends(get_settings), d: Dialogue = Depends(get_dialogue)) -> DialogueOut:
    return _run(body, "staff", call, settings, d)
