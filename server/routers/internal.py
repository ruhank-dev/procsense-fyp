"""Routes for the service's own jobs, not for people (p1-backend-s09). Closed unless INTERNAL_KEY is set; the caller proves itself with the `X-Internal-Key` header."""
from __future__ import annotations

import hmac

from fastapi import APIRouter, Depends, Header, HTTPException

from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import db_session
from server.config import Settings, get_settings
from server.jobs import retention
from server.verification.worker import get_runner, verify_pending

router = APIRouter(prefix="/internal", tags=["internal"], include_in_schema=False)


def internal_key(x_internal_key: str | None = Header(default=None), settings: Settings = Depends(get_settings)) -> None:
    expected = settings.internal_key.get_secret_value()
    if not expected:
        raise HTTPException(status_code=404, detail="not_found")   # closed: it looks like the route does not exist
    if x_internal_key is None or not hmac.compare_digest(x_internal_key.encode(), expected.encode()):
        raise HTTPException(status_code=403, detail="forbidden")


@router.post("/verify-pending")
def run_verification_sweep(_: None = Depends(internal_key), settings: Settings = Depends(get_settings)) -> dict:
    """Judges up to VERIFY_BATCH pending sessions. A scheduled job calls this (p1-backend-s13), and so does the service itself when it wakes up."""
    if get_runner() is None:
        return {"processed": 0, "configured": False}
    return verify_pending(settings.verify_batch)


@router.post("/retention")
def run_retention(_: None = Depends(internal_key), db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> dict:
    """Removes what has passed its retention period (p1-backend-s12). A scheduled job calls this daily."""
    return retention.run(db, settings)
