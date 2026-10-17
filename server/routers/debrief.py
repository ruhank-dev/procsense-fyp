"""Debrief route (p1-ai-language-s08, p1-backend-s06): `POST /debrief/organisations/{id}/rewrite` with {"report": the engine's debrief facts}.

The answer always holds the template debrief (complete, from the facts alone). `ai` holds the model's rewording only if the feature is on and the strict validator passed; `used` says which the client should show.
A signed-in member, audited (hash of the report, reason codes), rate limited, one model slot. A model that is down or refused leaves the template.
"""
from __future__ import annotations

import hashlib
import json
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel

from server import ai_features
from server.config import Settings, get_settings
from server.debrief.rewrite import Rewriter, facts_of, template
from server.llm import client as client_mod
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway

router = APIRouter(tags=["debrief"])


class DebriefIn(BaseModel):
    report: dict


class DebriefOut(BaseModel):
    template: list[dict]
    ai: list[dict] | None = None
    used: str
    reasons: list[str] = []
    latency_ms: int = 0


@lru_cache
def get_rewriter() -> Rewriter:
    s = get_settings()
    return Rewriter(client_mod.OllamaClient(s.ollama_url) if s.ollama_url else None)


@router.post("/debrief/organisations/{organisation_id}/rewrite", response_model=DebriefOut)
def rewrite(body: DebriefIn, call: AiCallRecord = Depends(audited("debrief")), settings: Settings = Depends(get_settings), rw: Rewriter = Depends(get_rewriter)) -> DebriefOut:
    r = body.report
    if not isinstance(r.get("items"), list) or "end_state" not in r or "result" not in r:
        raise HTTPException(status_code=422, detail="not_a_debrief_report")
    call.set_prompt("debrief.rewrite:v1", context_id=str(r.get("case", ""))[:40])
    call.set_input(json.dumps(r, sort_keys=True))
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    facts = facts_of(r)
    if not ai_features.enabled("debrief_ai"):
        call.fell_back("FEATURE_OFF")
        return DebriefOut(template=template(facts), used="template", reasons=["FEATURE_OFF"])
    if not ai_gateway._slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return DebriefOut(template=template(facts), used="template", reasons=["MODEL_BUSY"])
    try:
        out = rw.make(r)
    finally:
        ai_gateway._slot.release()
    call.set_model(rw.model or "live")
    if out["used"] == "ai":
        call.validated(True)
    else:
        call.validated(False, out["reasons"] or ["REJECTED"])
        call.fell_back((out["reasons"] or ["REJECTED"])[0])
    call.set_output(text=json.dumps(out["template"]))
    return DebriefOut(template=out["template"], ai=out["ai"], used=out["used"], reasons=out["reasons"], latency_ms=out["latency_ms"])
