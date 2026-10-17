"""Speech recognition route (p1-voice-input-s01, p1-backend-s06): `POST /asr/organisations/{id}/transcribe` with the clip as the request body.

A signed-in member, audited (the clip itself is never stored, only the hash of its bytes and the outcome), rate limited with the other AI calls. Clips over 15 s and non-audio are refused; low confidence is
returned as `low_confidence: true` so the client says "Say again" and credits nothing. The recogniser is the optional dependency faster-whisper: without it the route answers the fallback signal.
"""
from __future__ import annotations

from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel

from server import ai_features
from server.asr.service import AsrService, MAX_SECONDS
from server.config import Settings, get_settings
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway

router = APIRouter(tags=["speech recognition"])
MAX_BYTES = 2 * 1024 * 1024


class AsrOut(BaseModel):
    ok: bool
    fallback: bool
    text: str = ""
    low_confidence: bool = False
    avg_logprob: float = 0.0
    words: list[list] = []
    seconds: float = 0.0
    error: str = ""


@lru_cache
def get_asr() -> AsrService:
    s = get_settings()
    return AsrService(s.asr_model, s.asr_device)


@router.post("/asr/organisations/{organisation_id}/transcribe", response_model=AsrOut)
async def transcribe(request: Request, call: AiCallRecord = Depends(audited("asr")), settings: Settings = Depends(get_settings), svc: AsrService = Depends(get_asr)) -> AsrOut:
    body = await request.body()
    if not body or len(body) > MAX_BYTES:
        raise HTTPException(status_code=413 if body else 422, detail="clip_missing_or_too_large")
    call.set_model(svc.model_name)
    call.set_prompt("asr:v1")
    call.set_input(body)
    if not ai_features.enabled("asr"):
        call.fell_back("FEATURE_OFF")
        return AsrOut(ok=False, fallback=True, error="FEATURE_OFF")
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    r = svc.transcribe(body)
    if not r.ok and r.error in ("ASR_UNAVAILABLE", "FFMPEG_MISSING"):
        call.fell_back(r.error)
        return AsrOut(ok=False, fallback=True, error=r.error)
    if not r.ok and r.error in ("BAD_AUDIO", "CLIP_TOO_LONG"):
        call.validated(False, [r.error])
        raise HTTPException(status_code=422, detail=r.error.lower() + (f"_max_{int(MAX_SECONDS)}_seconds" if r.error == "CLIP_TOO_LONG" else ""))
    call.validated(r.ok and not r.low_confidence, [] if r.ok and not r.low_confidence else [r.error or "LOW_CONFIDENCE"])
    call.set_output(text=r.text)
    return AsrOut(ok=r.ok, fallback=False, text=r.text, low_confidence=r.low_confidence, avg_logprob=r.avg_logprob, words=[list(w) for w in r.words], seconds=r.seconds, error=r.error)
