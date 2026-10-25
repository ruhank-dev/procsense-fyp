"""Dialogue routes (p1-ai-language-s04 and s05, p1-backend-s06): `POST /dialogue/organisations/{id}/patient` and `.../staff`.

The body carries the context the Core's builder made for the character (the Core decides what may be said), the question, and the lines to fall back on. A signed-in member, audited (hashes and reason codes),
rate limited, one model slot. The reply says where it came from (`model`, `cache`, `fallback`); a fallback is the approved authored line, never an error. See server/dialogue/dialogue.py.
"""
from __future__ import annotations

import json
import time
from functools import lru_cache
from pathlib import Path

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field

from server import ai_features
from server.config import Settings, get_settings
from server.dialogue.dialogue import Dialogue
from server.llm import client as client_mod, routing
from server.auth.dependencies import db_session
from server.repositories import privacy as privacy_repository
from sqlalchemy.orm import Session as DbSession
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


def _run(body: DialogueIn, role: str, call: AiCallRecord, settings: Settings, d: Dialogue, db: DbSession) -> DialogueOut:
    with routing.consent(privacy_repository.cloud_consent(db, call.user_id)):          # the person's own optional consent decides whether their words may go to the cloud
        return _run_inner(body, role, call, settings, d)


def _run_inner(body: DialogueIn, role: str, call: AiCallRecord, settings: Settings, d: Dialogue) -> DialogueOut:
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
def patient(body: DialogueIn, call: AiCallRecord = Depends(audited("dialogue_patient")), settings: Settings = Depends(get_settings), d: Dialogue = Depends(get_dialogue), db: DbSession = Depends(db_session)) -> DialogueOut:
    return _run(body, "patient", call, settings, d, db)


@router.post("/dialogue/organisations/{organisation_id}/staff", response_model=DialogueOut)
def staff(body: DialogueIn, call: AiCallRecord = Depends(audited("dialogue_staff")), settings: Settings = Depends(get_settings), d: Dialogue = Depends(get_dialogue), db: DbSession = Depends(db_session)) -> DialogueOut:
    return _run(body, "staff", call, settings, d, db)


# ---- one trainee turn answered by the patient and the staff in one call (ADR-0026) ---------------------------------------------------------------------------------------------------------------------

INTENTS_FILE = Path(__file__).resolve().parents[2] / "content" / "ai" / "intents.json"
STAFF_LINES_FILE = Path(__file__).resolve().parents[2] / "content" / "ai" / "staff_intent_lines.json"


@lru_cache
def _intents() -> dict:
    return json.loads(INTENTS_FILE.read_text(encoding="utf-8"))["roles"]


@lru_cache
def _staff_lines() -> dict:
    return json.loads(STAFF_LINES_FILE.read_text(encoding="utf-8"))["roles"]


class TeamIn(BaseModel):
    context: dict                                         # the patient's context from the Core's builder: facts with the Core's decision on each, the recent turns
    staff_roles: list[str] = Field(default=["nurse", "anaesthesia"], min_length=0, max_length=4)
    question: str = Field(min_length=1, max_length=400)
    style: str = Field(default="plain", max_length=120)
    age: int = Field(default=50, ge=0, le=120)
    sex: str = Field(default="M", pattern="^[MF]$")
    authored: dict[str, str] = {}
    asked_fact: str | None = None


class RoleReply(BaseModel):
    role: str
    text: str
    intent: str
    fact_ids: list[str] = []
    source: str
    reasons: list[str] = []


class TeamOut(BaseModel):
    replies: list[RoleReply]                              # in the order they are spoken: the patient, then the staff
    cloud: bool = False                                   # the model that answered is a cloud model
    model: str = ""
    ms: int = 0


@router.post("/dialogue/organisations/{organisation_id}/team", response_model=TeamOut)
def team(body: TeamIn, call: AiCallRecord = Depends(audited("dialogue_team")), settings: Settings = Depends(get_settings), d: Dialogue = Depends(get_dialogue), db: DbSession = Depends(db_session)) -> TeamOut:
    with routing.consent(privacy_repository.cloud_consent(db, call.user_id)):
        return _team(body, call, settings, d)


def _team(body: TeamIn, call: AiCallRecord, settings: Settings, d: Dialogue) -> TeamOut:
    """The staff's allowed intents and their approved lines come from the server's own content, not from the request: a client cannot widen the closed lists or choose the words a staff member says."""
    intents, lines = _intents(), _staff_lines()
    roles = [r for r in dict.fromkeys(body.staff_roles) if r in intents and r != "patient" and r in lines]
    ctx = dict(body.context)
    if not isinstance(ctx.get("facts"), list):
        raise HTTPException(status_code=422, detail="context_needs_facts")
    ctx["allowed_intents"] = intents["patient"]
    ctx.setdefault("last_turns", [])
    staff_ctxs = {r: {"role": r, "case": ctx.get("case", ""), "allowed_intents": intents[r], "facts": [], "last_turns": ctx["last_turns"], "results": []} for r in roles}
    shown = [f for f in ctx["facts"] if f.get("disclosable_now")]
    call.set_prompt("dialogue.patient+staff_pick_many:v2", context_id=str(ctx.get("case", ""))[:40])
    call.set_input(body.question)
    started = time.monotonic()

    def out(replies: dict, cloud: bool = False, model: str = "") -> TeamOut:
        order = [r for r in ["patient", *roles] if r in replies]
        return TeamOut(replies=[RoleReply(role=r, text=replies[r].text, intent=replies[r].intent, fact_ids=replies[r].fact_ids, source=replies[r].source, reasons=replies[r].reasons) for r in order],
                       cloud=cloud, model=model, ms=round((time.monotonic() - started) * 1000))
    if not ai_features.enabled("patient_dialogue"):
        call.fell_back("FEATURE_OFF")
        return out(_authored(d, ctx, staff_ctxs, body, lines, shown, ["FEATURE_OFF"]))
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    route = routing.resolve("dialogue_patient", "dialogue.patient", "live")
    held = False
    if not route.cloud:                                    # the local model has one slot; a cloud route does not use it (a local fallback inside the gateway is serialised by the model host)
        if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
            call.fell_back("MODEL_BUSY")
            return out(_authored(d, ctx, staff_ctxs, body, lines, shown, ["MODEL_BUSY"]))
        held = True
    try:
        replies = d.team(ctx, staff_ctxs, body.question, body.style, body.age, body.sex, body.authored, {r: lines[r] for r in roles}, body.asked_fact)
    finally:
        if held:
            ai_gateway._slot.release()
    audit = replies["patient"].audit or {}
    model = audit.get("model", "")
    call.set_model(model or "none")
    if replies["patient"].source == "fallback":
        call.validated(False, replies["patient"].reasons or ["REJECTED"])
        call.fell_back((replies["patient"].reasons or ["REJECTED"])[0])
    else:
        call.validated(True)
    call.set_output(text=replies["patient"].text)
    return out(replies, cloud=bool(audit.get("cloud")), model=model)


def _authored(d: Dialogue, ctx: dict, staff_ctxs: dict, body: TeamIn, lines: dict, shown: list, reasons: list[str]) -> dict:
    """The authored lines without any model call: the patient's authored answer for the fact asked (when the Core released it) or cannot_recall, and for each role the first allowed intent's approved line."""
    out = {"patient": d._fallback("patient", ctx, shown, body.asked_fact, body.authored, {}, reasons)}
    return out                                            # no model chose anything for the staff: they stay silent
