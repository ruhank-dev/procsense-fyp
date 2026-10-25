"""The AI gateway (p1-backend-s06): one controlled door for the model calls a client may ask for.

    POST /llm/organisations/{organisation_id}/generate     a registered prompt with its variables; the answer is checked (schema, then the prompt's validator) before it is returned
    GET  /ai/health                                        whether the model host answers and which models it holds

What the gateway guarantees, for every route under it (see middleware/audit.py): a signed-in member of the organisation, one audit row (hashes, ids and reason codes, never text), a rate limit per person,
one model call at a time on the host (a 6 GB GPU thrashes otherwise: a request waits a few seconds for the slot and then gets the fallback signal), a timeout, and the fallback signal instead of an error when the
model is down, slow or wrong. A prompt can be called only if it is registered with `gateway: true` AND has a validator (server/llm/validators.py): no check, no route. The caller always has a deterministic
line to use when `fallback` is true; the gateway never says anything on its own. The model host is on the team's machines next to the service (OQ-07, DT-122); a cloud copy of the service with no host answers every call
with the fallback signal.
"""
from __future__ import annotations

import threading
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, Field

from server.config import Settings, get_settings
from server import ai_features
from server.llm import client as client_mod, gateway, prompts, routing
from server.llm.validators import VALIDATORS
from server.middleware.audit import AiCallRecord, audited
from server.ratelimit import RateLimiter

FEATURE_OF = {"compose.narrative": "narrative"}            # the feature a gateway prompt belongs to; unlisted prompts are not callable anyway
router = APIRouter(tags=["AI gateway"])
health_router = APIRouter(tags=["AI gateway"])
_slot = threading.Lock()                      # one model call at a time on this host
_limiters: dict[int, RateLimiter] = {}


class GenerateIn(BaseModel):
    prompt_id: str = Field(min_length=1, max_length=64)
    variables: dict[str, str]
    version: int | None = None
    session_id: str | None = None


class GenerateOut(BaseModel):
    ok: bool
    fallback: bool
    value: dict | None = None
    prompt: str
    model: str = ""
    attempts: int = 0
    reasons: list[str] = []


@lru_cache
def get_llm_client() -> client_mod.OllamaClient | None:
    url = get_settings().ollama_url
    return client_mod.OllamaClient(url) if url else None


def _limiter(settings: Settings) -> RateLimiter:
    return _limiters.setdefault(settings.ai_rate_per_hour, RateLimiter(settings.ai_rate_per_hour, 3600.0))


def _code(defect: str) -> str:
    head = defect.split(":", 1)[0].strip()
    return head if head.isupper() and head.replace("_", "").isalnum() else "UNCODED_REASON"


@router.post("/llm/organisations/{organisation_id}/generate", response_model=GenerateOut)
def generate(body: GenerateIn, request: Request, call: AiCallRecord = Depends(audited("llm_generate")), settings: Settings = Depends(get_settings),
             client: client_mod.OllamaClient | None = Depends(get_llm_client)) -> GenerateOut:
    try:
        prompt = prompts.get(body.prompt_id, body.version)
    except prompts.PromptError:
        raise HTTPException(status_code=404, detail="unknown_prompt") from None
    if not prompt.gateway or prompt.id not in VALIDATORS:
        raise HTTPException(status_code=403, detail="prompt_not_available_through_the_gateway")
    if set(body.variables) != set(prompt.variables):
        raise HTTPException(status_code=422, detail="variables_do_not_match_the_prompt")
    call.set_prompt(f"{prompt.id}:v{prompt.version}")
    call.set_input(prompt.render(**body.variables)[1])
    if not _limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    out = GenerateOut(ok=False, fallback=True, prompt=prompt.key)
    if not ai_features.enabled(FEATURE_OF.get(prompt.id, "narrative")):
        call.fell_back("FEATURE_OFF")
        return out
    if client is None:
        call.fell_back("MODEL_HOST_OFF")
        return out
    if not _slot.acquire(timeout=settings.ai_queue_wait_seconds):
        call.fell_back("MODEL_BUSY")
        return out
    try:
        timeout = settings.ai_live_timeout_seconds if prompt.role == "live" else settings.ai_batch_timeout_seconds
        result = gateway.run(client, prompt.id, body.variables, validate=lambda v: VALIDATORS[prompt.id](body.variables, v), timeout=timeout, version=prompt.version, task="llm_generate")
    finally:
        _slot.release()
    a = result.audit
    call.set_model(a["model"], a["model_digest"][:12])
    reasons = [_code(d) for d in a["defects"]]
    out = GenerateOut(ok=result.ok, fallback=result.fallback, value=result.value if result.ok else None, prompt=prompt.key, model=a["model"], attempts=a["attempts"], reasons=reasons[:10])
    if result.ok:
        call.validated(True)
        call.set_output(text=str(result.value))
    else:
        call.validated(False, reasons or [a["error"] or "MODEL_FAILED"])
        call.fell_back(a["error"] or "VALIDATION_FAILED")
    return out


@health_router.get("/ai/health")
def ai_health(client: client_mod.OllamaClient | None = Depends(get_llm_client)) -> dict:
    """Whether the model host answers (no sign-in: it says nothing about any person)."""
    if client is None:
        return {"llm": {"up": False, "configured": False}, "features": ai_features.status(), "routing": routing.status()}
    h = client.health()
    return {"llm": {"up": h["up"], "configured": True, "models": len(h["models"]), "ms": h["ms"]}, "features": ai_features.status(), "routing": routing.status()}
