"""Optional WebSocket for server push to a headset (P3-NET-12). Used only with Settings.lan_profile on; the paired-device token is sent as X-ProcSense-Device (never in the address)."""
from __future__ import annotations

import asyncio

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from server.config import get_settings
from server.lan.notices import get_hub
from server.lan.pairing import get_store
from server.schemas.headset_notice import Notice

router = APIRouter(tags=["headset"])


@router.websocket("/ws/headset/{session_id}")
async def headset_socket(websocket: WebSocket, session_id: str) -> None:
    token = websocket.headers.get("x-procsense-device", "")
    if not get_settings().lan_profile or not get_store().verify(token) or not (0 < len(session_id) <= 64):
        await websocket.close(code=1008)   # refused before the handshake completes
        return
    await websocket.accept()
    hub = get_hub()
    queue = hub.attach(session_id)
    try:
        await websocket.send_text(Notice(kind="hello", session_id=session_id).model_dump_json())
        listen = asyncio.ensure_future(websocket.receive_text())      # the headset sends nothing we need; this notices a close
        while True:
            fetch = asyncio.ensure_future(queue.get())
            await asyncio.wait({fetch, listen}, return_when=asyncio.FIRST_COMPLETED)
            if listen.done():
                fetch.cancel()
                listen.result()   # raises WebSocketDisconnect on a close
                listen = asyncio.ensure_future(websocket.receive_text())
            if fetch.done() and not fetch.cancelled():
                await websocket.send_text(fetch.result().model_dump_json())
    except WebSocketDisconnect:
        pass
    finally:
        hub.detach(queue)
