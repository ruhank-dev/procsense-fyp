"""The web pages (p1-backend-s11). One small page that loads its scripts from /static and calls the JSON API; the same page answers for /, /device (the headset code approval) and /reset (password reset)."""
from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends
from fastapi.responses import FileResponse, JSONResponse

from server.config import Settings, get_settings

STATIC = Path(__file__).resolve().parents[1] / "static"
router = APIRouter(include_in_schema=False)


def _page() -> FileResponse:
    return FileResponse(STATIC / "index.html", media_type="text/html; charset=utf-8", headers={"Cache-Control": "no-cache"})


@router.get("/")
def home() -> FileResponse:
    return _page()


@router.get("/device")
def device() -> FileResponse:
    return _page()


@router.get("/reset")
def reset() -> FileResponse:
    return _page()


@router.get("/config.json")
def public_config(settings: Settings = Depends(get_settings)) -> JSONResponse:
    """What the page needs to sign a person in with the account system: the project address and the publishable key (made to be public; the secret key never leaves the server)."""
    return JSONResponse({"supabase_url": settings.supabase_url.rstrip("/"), "supabase_publishable_key": settings.supabase_publishable_key}, headers={"Cache-Control": "no-cache"})
