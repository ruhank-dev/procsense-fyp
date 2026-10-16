"""Who is calling, and what may they do (p1-backend-s08). The login token says who; the database says which organisations, which role and whether the membership is still active,
and it is asked on every request, so removing someone takes effect on their very next call.

Status codes: no or bad token = 401; a person who is not a member of the organisation = 404 (its existence is not revealed); a member without the role, or a deactivated member = 403.
"""
from __future__ import annotations

import logging
import uuid
from collections.abc import Iterator
from dataclasses import dataclass
from functools import lru_cache

from fastapi import Depends, HTTPException
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from sqlalchemy.orm import Session

from server.auth import device_tokens
from server.auth.supabase_admin import SupabaseAdmin
from server.auth.tokens import Principal, TokenError, TokenVerifier
from server.config import Settings, get_settings
from server.database import get_session
from server.repositories import devices
from server.models.accounts import Membership, Organisation, Profile
from server.repositories import accounts

log = logging.getLogger("procsense.auth")
_bearer = HTTPBearer(auto_error=False)


@lru_cache
def get_verifier() -> TokenVerifier:
    s = get_settings()
    return TokenVerifier(issuer=s.jwt_issuer, audience=s.supabase_jwt_audience, jwks_url=s.jwks_url, cache_seconds=s.jwks_cache_seconds, leeway=s.clock_leeway_seconds)


@lru_cache
def get_admin() -> SupabaseAdmin:
    return SupabaseAdmin.from_settings(get_settings())


def db_session() -> Iterator[Session]:
    """A database session for one request; the service answers 503 when no database is configured."""
    # only opening the session can mean "no database": an error raised later by the route is the route's own and must not be reported as a missing database
    gen = get_session()
    try:
        session = next(gen)
    except RuntimeError:
        raise HTTPException(status_code=503, detail="database_not_configured") from None
    try:
        yield session
    finally:
        gen.close()


def _device_principal(token: str) -> Principal:
    """A headset signed in with a device code (p1-backend-s10): its access token is looked up (by hash) in the database, so a revoked headset is refused on its very next call."""
    try:
        gen = get_session()
        session = next(gen)
    except RuntimeError:
        raise HTTPException(status_code=503, detail="database_not_configured") from None
    try:
        row = devices.device_for_access_token(session, token)
        if row is None:
            log.info("device token refused")
            raise HTTPException(status_code=401, detail="not_signed_in", headers={"WWW-Authenticate": "Bearer"})
        return Principal(user_id=row.user_id, email="", claims={}, device_session_id=row.id)
    finally:
        gen.close()


def current_principal(credentials: HTTPAuthorizationCredentials | None = Depends(_bearer), verifier: TokenVerifier = Depends(get_verifier)) -> Principal:
    """Either kind of login: the account system's token (the web) or a headset's own token (prefix psd_). The rest of the service does not care which."""
    if credentials is None or credentials.scheme.lower() != "bearer":
        raise HTTPException(status_code=401, detail="not_signed_in", headers={"WWW-Authenticate": "Bearer"})
    if credentials.credentials.startswith(device_tokens.ACCESS_PREFIX):
        return _device_principal(credentials.credentials)
    try:
        return verifier.verify(credentials.credentials)
    except TokenError as e:
        log.info("token refused: %s", e)   # the reason goes to the log; the caller only learns that it was refused
        raise HTTPException(status_code=401, detail="not_signed_in", headers={"WWW-Authenticate": "Bearer"}) from None


def web_principal(principal: Principal = Depends(current_principal)) -> Principal:
    """Only a person signed in on the web (the account system's token). A headset's token cannot approve another headset, list devices or change the account."""
    if principal.device_session_id is not None:
        raise HTTPException(status_code=403, detail="web_sign_in_required")
    return principal


@dataclass
class CurrentUser:
    principal: Principal
    profile: Profile
    memberships: list[Membership]

    def membership_for(self, organisation_id: uuid.UUID) -> Membership | None:
        return next((m for m in self.memberships if m.organisation_id == organisation_id), None)


def current_user(principal: Principal = Depends(current_principal), session: Session = Depends(db_session)) -> CurrentUser:
    """The signed-in person. First sign-in makes the profile; any pending invitations for the address in the token are applied (and the address is cleared from them)."""
    profile = accounts.ensure_profile(session, principal.user_id)
    for invitation in accounts.pending_invitations_for(session, principal.email):
        accounts.accept_invitation(session, invitation, principal.user_id)
    session.commit()
    memberships = [m for m, _ in accounts.memberships_of(session, principal.user_id)]
    return CurrentUser(principal, profile, memberships)


def require_platform_admin(user: CurrentUser = Depends(current_user)) -> CurrentUser:
    if not user.profile.is_platform_admin or user.principal.device_session_id is not None:
        raise HTTPException(status_code=403, detail="platform_admin_only")
    return user


@dataclass
class OrgContext:
    user: CurrentUser
    organisation: Organisation
    role: str   # the member's role, or "org_admin" for a platform administrator acting on the organisation


def _org_context(minimum: str):
    def dependency(organisation_id: uuid.UUID, user: CurrentUser = Depends(current_user), session: Session = Depends(db_session)) -> OrgContext:
        organisation = accounts.get_organisation(session, organisation_id)
        if organisation is None or not organisation.active:
            raise HTTPException(status_code=404, detail="not_found")
        if minimum != "trainee" and user.principal.device_session_id is not None:
            raise HTTPException(status_code=403, detail="web_sign_in_required")   # a headset signs a trainee in to train; administering needs the web
        if user.profile.is_platform_admin:
            return OrgContext(user, organisation, "org_admin")
        membership = user.membership_for(organisation_id)
        if membership is None:
            raise HTTPException(status_code=404, detail="not_found")
        if membership.status != "active":
            raise HTTPException(status_code=403, detail="membership_deactivated")
        if accounts.ROLE_RANK[membership.role] < accounts.ROLE_RANK[minimum]:
            raise HTTPException(status_code=403, detail="role_too_low")
        return OrgContext(user, organisation, membership.role)
    return dependency


require_member = _org_context("trainee")
require_supervisor = _org_context("supervisor")
require_org_admin = _org_context("org_admin")
