"""In the LAN profile every AI route needs a paired device token and a compatible headset version (P3-NET-07, P3-NET-10).

Pure ASGI so streaming answers (the TTS stream) are not buffered. /health, /pairing/exchange and the static pages stay open."""
from __future__ import annotations

import json

from starlette.datastructures import Headers
from starlette.types import ASGIApp, Receive, Scope, Send

from server.config import get_settings
from server.lan import compat
from server.lan.pairing import get_store

GUARDED = ("/asr", "/speech", "/orders", "/dialogue", "/tts", "/evaluate", "/debrief", "/scenario-packages", "/ai", "/ws")


class LanPairing:
    def __init__(self, app: ASGIApp):
        self.app = app

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http" or not get_settings().lan_profile or not scope["path"].startswith(GUARDED) or scope["path"] == "/ai/health":
            await self.app(scope, receive, send)
            return
        headers = Headers(scope=scope)
        auth = headers.get("authorization", "")
        token = headers.get("x-procsense-device", "") or (auth[7:].strip() if auth.lower().startswith("bearer ") else "")   # the headset sends X-ProcSense-Device; Authorization belongs to the signed-in user
        status, reason = (None, None)
        device_id = get_store().device_id_for(token)
        if device_id is None:
            status, reason = 401, "pairing_required"
        else:
            problem = compat.check(headers, settings=get_settings())
            if problem:
                status, reason = 426, problem
        if status is None:
            await self.app(scope, receive, send)
            return
        body = json.dumps({"detail": reason}).encode("utf-8")
        await send({"type": "http.response.start", "status": status, "headers": [(b"content-type", b"application/json"), (b"content-length", str(len(body)).encode())]})
        await send({"type": "http.response.body", "body": body})
