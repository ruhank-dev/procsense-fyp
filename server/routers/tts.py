"""Live speech route (p1-voice-input-s04): `POST /tts/organisations/{id}/speak` with {"text", "role"}; answers newline-delimited JSON, one object per sentence {"text", "audio_b64"} as each is made, so the client plays the
first sentence while the rest is still being made. A signed-in member, audited (hashes only), rate limited with the other AI calls. If the voice is not available the answer is the fallback signal and the client shows subtitles.
The text is spoken as given: it must already have passed the checks of the gateway (this route is for lines the gateway released and for fixed lines, never for free text from a person).
"""
from __future__ import annotations

import base64
import json
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import JSONResponse, StreamingResponse
from pydantic import BaseModel, Field

from server import ai_features
from server.config import Settings, get_settings
from server.middleware.audit import AiCallRecord, audited
from server.routers import ai_gateway
from server.tts.engine import MAX_CHARS, TtsEngine

router = APIRouter(tags=["text to speech"])


class SpeakIn(BaseModel):
    text: str = Field(min_length=1, max_length=MAX_CHARS)
    role: str = Field(min_length=1, max_length=32)


@lru_cache
def get_tts() -> TtsEngine:
    engine = TtsEngine()
    if engine.available():
        engine.warm()                                                  # load the voice in the background so the first sentence of the day is not slow
    return engine


@router.post("/tts/organisations/{organisation_id}/speak")
def speak(body: SpeakIn, call: AiCallRecord = Depends(audited("tts")), settings: Settings = Depends(get_settings), engine: TtsEngine = Depends(get_tts)):
    if body.role not in engine.roles():
        raise HTTPException(status_code=422, detail="unknown_role")
    call.set_model("piper")
    call.set_prompt("tts:v1", context_id=body.role)
    call.set_input(body.text)
    if not ai_gateway._limiter(settings).allow(call.user_id):
        raise HTTPException(status_code=429, detail="too_many_ai_calls")
    if not ai_features.enabled("tts"):
        call.fell_back("FEATURE_OFF")
        return JSONResponse({"ok": False, "fallback": True, "error": "FEATURE_OFF"})
    if not engine.available():
        call.fell_back("TTS_UNAVAILABLE")
        return JSONResponse({"ok": False, "fallback": True, "error": "TTS_UNAVAILABLE"})
    call.validation_skipped()

    def stream():
        for sentence, audio in engine.speak(body.text, body.role):
            yield json.dumps({"text": sentence, "audio_b64": base64.b64encode(audio).decode("ascii")}) + "\n"
    return StreamingResponse(stream(), media_type="application/x-ndjson")
