"""Signing a headset in with a code (p1-backend-s10), modelled on the OAuth device authorisation grant (RFC 8628).

The headset asks for a code and shows it; a person signed in on the web approves it; the headset, which keeps asking, receives its own short-lived access token and a refresh token.
The headset's tokens are accepted by the same login check as the web's (`auth/dependencies.py`) but only for training: administering, approving other headsets and changing the account need a web sign-in.
"""
from __future__ import annotations

import logging
import uuid
from functools import lru_cache

from fastapi import APIRouter, Depends, HTTPException, Request, Response
from sqlalchemy.orm import Session as DbSession

from server.auth import device_tokens
from server.auth.dependencies import CurrentUser, current_principal, current_user, db_session, web_principal
from server.auth.tokens import Principal
from server.config import Settings, get_settings
from server.ratelimit import RateLimiter
from server.repositories import devices
from server.schemas.devices import (DeviceDecisionIn, DeviceDecisionOut, DeviceOut, DevicePollIn, DeviceRefreshIn, DeviceStartIn, DeviceStartOut, DeviceTokensOut)

router = APIRouter(tags=["device sign-in"])
security_log = logging.getLogger("procsense.security")
NO_STORE = {"Cache-Control": "no-store"}


@lru_cache
def _start_limiter() -> RateLimiter:
    return RateLimiter(get_settings().device_start_per_hour)


@lru_cache
def _decide_limiter() -> RateLimiter:
    return RateLimiter(get_settings().device_decide_per_hour)


def _out(issued: devices.IssuedTokens) -> DeviceTokensOut:
    return DeviceTokensOut(access_token=issued.access_token, expires_in=issued.access_expires_in, refresh_token=issued.refresh_token, refresh_expires_in=issued.refresh_expires_in)


@router.post("/auth/device/start", response_model=DeviceStartOut)
def start(body: DeviceStartIn, request: Request, response: Response, db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> DeviceStartOut:
    """The headset asks for a code. Nothing about the person is known yet."""
    caller = request.client.host if request.client else "unknown"
    if not _start_limiter().allow(caller):
        raise HTTPException(status_code=429, detail="too_many_requests")
    login, device_code = devices.start_login(db, body.device_name, settings.device_login_minutes, settings.device_poll_seconds)
    db.commit()
    uri = settings.device_verification_uri.strip() or str(request.base_url).rstrip("/") + "/device"
    shown = device_tokens.show_user_code(login.user_code)
    response.headers.update(NO_STORE)
    return DeviceStartOut(device_code=device_code, user_code=shown, verification_uri=uri, verification_uri_complete=f"{uri}?code={shown}",
                          expires_in=settings.device_login_minutes * 60, interval=login.interval_seconds)


@router.post("/auth/device/token", response_model=DeviceTokensOut)
def collect(body: DevicePollIn, response: Response, db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> DeviceTokensOut:
    """The headset asks whether its code was approved. Answers with the OAuth error words: authorization_pending, slow_down, access_denied, expired_token (all HTTP 400)."""
    outcome, issued = devices.poll(db, body.device_code, settings.device_access_minutes, settings.device_refresh_days)
    db.commit()   # what was learned stays (the poll time, the code being used) even when the answer is an error
    response.headers.update(NO_STORE)
    if issued is not None:
        return _out(issued)
    error = {"pending": "authorization_pending", "slow_down": "slow_down", "denied": "access_denied"}.get(outcome, "expired_token")   # an unknown code looks like an expired one
    raise HTTPException(status_code=400, detail=error, headers=NO_STORE)


@router.post("/auth/device/refresh", response_model=DeviceTokensOut)
def refresh(body: DeviceRefreshIn, response: Response, db: DbSession = Depends(db_session), settings: Settings = Depends(get_settings)) -> DeviceTokensOut:
    """Swaps the refresh token for a new pair (the old one stops working). A refresh token used a second time means it was copied: that headset's sign-in is revoked."""
    issued = devices.refresh(db, body.refresh_token, settings.device_access_minutes, settings.device_refresh_days)
    db.commit()
    response.headers.update(NO_STORE)
    if issued is None:
        raise HTTPException(status_code=401, detail="not_signed_in", headers={"WWW-Authenticate": "Bearer", **NO_STORE})
    return _out(issued)


@router.post("/auth/device/decision", response_model=DeviceDecisionOut)
def decide(body: DeviceDecisionIn, user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> DeviceDecisionOut:
    """A person signed in on the web approves (or denies) the code the headset shows. The tokens are issued to this person and nobody else."""
    if not _decide_limiter().allow(user.profile.user_id):
        security_log.warning("device code decisions: too many from user %s", user.profile.user_id)
        raise HTTPException(status_code=429, detail="too_many_requests")
    login = devices.decide(db, body.user_code, user.profile.user_id, body.approve)
    if login is None:
        db.rollback()
        raise HTTPException(status_code=404, detail="code_not_found_or_expired")
    db.commit()
    return DeviceDecisionOut(device_name=login.device_name, status=login.status)


@router.post("/auth/device/logout", status_code=204)
def logout(principal: Principal = Depends(current_principal), db: DbSession = Depends(db_session)) -> Response:
    """A headset signs itself out: its tokens stop working at once."""
    if principal.device_session_id is not None:
        devices.revoke(db, principal.device_session_id, None, "signed_out_on_device")
        db.commit()
    return Response(status_code=204)


@router.get("/me/devices", response_model=list[DeviceOut])
def my_devices(user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> list[DeviceOut]:
    return [DeviceOut(id=d.id, device_name=d.device_name, created_at=d.created_at, last_used_at=d.last_used_at) for d in devices.devices_of(db, user.profile.user_id)]


@router.delete("/me/devices/{device_id}", status_code=204)
def revoke_my_device(device_id: uuid.UUID, user: CurrentUser = Depends(current_user), _web: Principal = Depends(web_principal), db: DbSession = Depends(db_session)) -> Response:
    if not devices.revoke(db, device_id, user.profile.user_id, "revoked_by_person"):
        raise HTTPException(status_code=404, detail="not_found")
    db.commit()
    return Response(status_code=204)
