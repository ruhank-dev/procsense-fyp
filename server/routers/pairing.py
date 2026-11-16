"""Pairing for the networked Quest demonstration (P3-NET-07). Used only with Settings.lan_profile on."""
from __future__ import annotations

from fastapi import APIRouter, Depends, HTTPException, Request
from pydantic import BaseModel, Field

from server.auth.dependencies import CurrentUser, current_user
from server.config import Settings, get_settings
from server.lan import demo_identity as demo
from server.lan.pairing import get_store
from server.schemas.pairing import DemoIdentityOut

router = APIRouter(prefix="/pairing", tags=["pairing"])


class ExchangeIn(BaseModel):
    code: str = Field(min_length=6, max_length=6, pattern=r"^\d{6}$")
    device_name: str = Field(default="headset", max_length=40)


class ExchangeOut(BaseModel):
    device_id: str
    device_token: str
    tls_fingerprint: str = ""      # P3-NET-15: SHA-256 of the laptop's self-signed certificate when it runs with --tls; the headset compares it with the one it pinned


def _local_only(request: Request) -> None:
    """Listing and revoking devices is done from the laptop itself, never from the network."""
    host = request.client.host if request.client else ""
    if host not in ("127.0.0.1", "::1", "testclient"):
        raise HTTPException(status_code=403, detail="laptop_only")


@router.post("/exchange", response_model=ExchangeOut)
def exchange(body: ExchangeIn, settings: Settings = Depends(get_settings)) -> ExchangeOut:
    if not settings.lan_profile:
        raise HTTPException(status_code=404, detail="pairing_not_enabled")
    store = get_store()
    if store.locked():
        raise HTTPException(status_code=429, detail="too_many_wrong_codes")
    result = store.exchange(body.code, body.device_name)
    if result is None:
        raise HTTPException(status_code=403, detail="wrong_code")
    return ExchangeOut(device_id=result[0], device_token=result[1], tls_fingerprint=settings.lan_tls_fingerprint)


@router.get("/devices", dependencies=[Depends(_local_only)])
def devices() -> list[dict]:
    return get_store().devices()


@router.delete("/devices/{device_id}", dependencies=[Depends(_local_only)])
def revoke(device_id: str) -> dict:
    if not get_store().revoke(device_id):
        raise HTTPException(status_code=404, detail="unknown_device")
    return {"revoked": device_id}


@router.get("/demo-identity", response_model=DemoIdentityOut)
def demo_identity(user: CurrentUser = Depends(current_user)) -> DemoIdentityOut:
    """The synthetic demonstration user of the paired headset (X-ProcSense-Device). 401 for an unknown or revoked device; 403 when this deployment does not play paired headsets as demonstration users."""
    if not user.principal.demo_device_id:
        raise HTTPException(status_code=403, detail="demo_identity_only")
    return DemoIdentityOut(user_id=user.profile.user_id, pseudonym=user.profile.pseudonym, display_name=user.profile.display_name,
                           organisation_id=demo.ORG_ID, organisation_name=demo.ORG_NAME, role=demo.ROLE)
