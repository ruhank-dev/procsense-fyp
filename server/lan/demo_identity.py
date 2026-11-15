"""The synthetic demonstration identity of the LAN profile (P3-NET-11).

A headset paired with this laptop (P3-NET-07) plays as a synthetic demonstration user, one stable user per paired headset, in one synthetic demonstration organisation. No real account is
used, made or touched: real sign-ins (a Supabase token, or a psd_ device-code token) never become a demonstration user, and the identity exists only when the owner declared the deployment
synthetic (cloud_synthetic_deployment), the LAN profile is on and the environment is not prod. With a database the demonstration rows are made on first use (so an upload verifies and is stored
on the laptop); without one the identity is transient and nothing is stored.

No exception to the normal rules (owner decision D2, 15 Nov 2026): the demonstration user is authorised as a trainee of its organisation only, answers the consent questions itself on the
headset (POST /privacy/consent, recorded via="headset"), and its session uploads are refused with 403 consent_required until it has agreed to the current text, exactly like any person."""
from __future__ import annotations

import uuid

from fastapi import Request
from sqlalchemy.orm import Session

from server.auth import device_tokens
from server.config import Settings
from server.lan.pairing import get_store
from server.models.accounts import Membership, Organisation, Profile
from server.repositories import accounts

_NS = uuid.UUID("5d6f4a52-9c1e-5b1e-8d0e-6c1a7d3e4f10")
ORG_ID = uuid.uuid5(_NS, "organisation:lan-demonstration")
ORG_NAME = "LAN demonstration (synthetic)"
ORG_SLUG = "lan-demonstration"
ROLE = "trainee"


def user_id_for(device_id: str) -> uuid.UUID:
    return uuid.uuid5(_NS, "device:" + device_id)


def _token(request: Request) -> tuple[str, str]:
    """(the paired-device token, a different Authorization bearer if the caller sent one). The headset sends the token as X-ProcSense-Device and, after pairing, as the bearer too."""
    auth = request.headers.get("authorization", "")
    bearer = auth[7:].strip() if auth.lower().startswith("bearer ") else ""
    device = request.headers.get("x-procsense-device", "") or bearer
    return device, ("" if bearer == device else bearer)


def device_for(request: Request, settings: Settings) -> str | None:
    """The paired headset's id when this request is the demonstration user, else None (the normal sign-in rules then apply)."""
    if not (settings.lan_profile and settings.cloud_synthetic_deployment) or settings.app_env == "prod":
        return None
    token, other_bearer = _token(request)
    if other_bearer or not token or token.startswith(device_tokens.ACCESS_PREFIX):
        return None
    return get_store().device_id_for(token)


def transient(device_id: str) -> tuple[Profile, Organisation]:
    """Not stored: the identity when there is no database."""
    return (Profile(user_id=user_id_for(device_id), pseudonym=("demo-" + device_id)[:16], display_name="Demonstration user", is_platform_admin=False),
            Organisation(id=ORG_ID, name=ORG_NAME, slug=ORG_SLUG, active=True))


def ensure_rows(session: Session, device_id: str) -> tuple[Profile, Organisation, list[Membership]]:
    """The demonstration organisation, profile and trainee membership, made on first use (idempotent)."""
    user_id = user_id_for(device_id)
    organisation = accounts.get_organisation(session, ORG_ID)
    if organisation is None:
        organisation = Organisation(id=ORG_ID, name=ORG_NAME, slug=ORG_SLUG)
        session.add(organisation)
        session.flush()
    profile = accounts.ensure_profile(session, user_id)
    if not profile.display_name:
        profile.display_name = "Demonstration user"
    membership = accounts.get_membership(session, user_id, ORG_ID) or accounts.add_membership(session, user_id, ORG_ID, ROLE)
    session.commit()
    return profile, organisation, [membership]
