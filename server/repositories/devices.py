"""Database access for the device-code login and signed-in headsets (ORM only). The decisions that must not race (approve once, collect once, rotate a refresh token once) are single UPDATE ... RETURNING statements."""
from __future__ import annotations

import datetime as dt
import uuid
from dataclasses import dataclass

from sqlalchemy import select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session as DbSession

from server.auth import device_tokens as tokens
from server.models.device_session import DeviceLogin, DeviceSession


def _now() -> dt.datetime:
    return dt.datetime.now(dt.timezone.utc)


@dataclass(frozen=True)
class IssuedTokens:
    session_id: uuid.UUID
    access_token: str
    access_expires_in: int
    refresh_token: str
    refresh_expires_in: int


def start_login(db: DbSession, device_name: str, valid_minutes: int, interval: int) -> tuple[DeviceLogin, str]:
    """A new pending login. Returns the row and the device code (kept only by the headset: the row holds its hash)."""
    for _ in range(8):
        device_code = tokens.new_secret()
        login = DeviceLogin(device_code_hash=tokens.hash_secret(device_code), user_code=tokens.new_user_code(), device_name=device_name,
                            interval_seconds=interval, expires_at=_now() + dt.timedelta(minutes=valid_minutes))
        try:
            with db.begin_nested():
                db.add(login)
            return login, device_code
        except IntegrityError:   # the short code is already in use: draw another
            continue
    raise RuntimeError("could not draw an unused user code")


def decide(db: DbSession, user_code: str, user_id: uuid.UUID, approve: bool) -> DeviceLogin | None:
    """Approve or deny a pending, unexpired login by the code the person read from the headset. None = no such pending code."""
    row = db.execute(update(DeviceLogin).where(DeviceLogin.user_code == tokens.clean_user_code(user_code), DeviceLogin.status == "pending", DeviceLogin.expires_at > _now())
                     .values(status="approved" if approve else "denied", user_id=user_id).returning(DeviceLogin)).scalar_one_or_none()
    return row


def poll(db: DbSession, device_code: str, settings_access_minutes: int, settings_refresh_days: int) -> tuple[str, IssuedTokens | None]:
    """The headset's poll. Answers: unknown, expired, slow_down, pending, denied, or tokens (issued once: the login becomes `used`)."""
    now = _now()
    login = db.execute(select(DeviceLogin).where(DeviceLogin.device_code_hash == tokens.hash_secret(device_code)).with_for_update()).scalar_one_or_none()
    if login is None:
        return "unknown", None
    if login.status in ("used", "denied") or login.expires_at <= now:
        return ("denied" if login.status == "denied" else "expired"), None
    too_fast = login.last_polled_at is not None and (now - login.last_polled_at).total_seconds() < login.interval_seconds
    login.last_polled_at = now
    if too_fast:
        return "slow_down", None
    if login.status == "pending":
        return "pending", None
    login.status = "used"   # the row is locked, so two polls at once cannot both collect
    issued = issue(db, login.user_id, login.device_name, settings_access_minutes, settings_refresh_days)
    return "tokens", issued


def issue(db: DbSession, user_id: uuid.UUID, device_name: str, access_minutes: int, refresh_days: int) -> IssuedTokens:
    now = _now()
    access, refresh = tokens.new_secret(tokens.ACCESS_PREFIX), tokens.new_secret(tokens.REFRESH_PREFIX)
    row = DeviceSession(user_id=user_id, device_name=device_name, access_hash=tokens.hash_secret(access), access_expires_at=now + dt.timedelta(minutes=access_minutes),
                        refresh_hash=tokens.hash_secret(refresh), refresh_expires_at=now + dt.timedelta(days=refresh_days))
    db.add(row)
    db.flush()
    return IssuedTokens(row.id, access, access_minutes * 60, refresh, refresh_days * 86400)


def refresh(db: DbSession, refresh_token: str, access_minutes: int, refresh_days: int) -> IssuedTokens | None:
    """Swaps a refresh token for a new pair; the old refresh token stops working. A refresh token that was already swapped is a sign it was copied: the headset session is revoked. None = refused."""
    now = _now()
    digest = tokens.hash_secret(refresh_token)
    row = db.execute(select(DeviceSession).where(DeviceSession.refresh_hash == digest).with_for_update()).scalar_one_or_none()
    if row is None:
        reused = db.execute(select(DeviceSession).where(DeviceSession.previous_refresh_hash == digest).with_for_update()).scalar_one_or_none()
        if reused is not None and reused.revoked_at is None:
            reused.revoked_at, reused.revoked_reason = now, "refresh_token_reused"
        return None
    if row.revoked_at is not None or row.refresh_expires_at <= now:
        return None
    access, new_refresh = tokens.new_secret(tokens.ACCESS_PREFIX), tokens.new_secret(tokens.REFRESH_PREFIX)
    row.previous_refresh_hash = row.refresh_hash
    row.refresh_hash, row.access_hash = tokens.hash_secret(new_refresh), tokens.hash_secret(access)
    row.access_expires_at, row.refresh_expires_at, row.last_used_at = now + dt.timedelta(minutes=access_minutes), now + dt.timedelta(days=refresh_days), now
    return IssuedTokens(row.id, access, access_minutes * 60, new_refresh, refresh_days * 86400)


def device_for_access_token(db: DbSession, access_token: str) -> DeviceSession | None:
    row = db.execute(select(DeviceSession).where(DeviceSession.access_hash == tokens.hash_secret(access_token))).scalar_one_or_none()
    if row is None or row.revoked_at is not None or row.access_expires_at <= _now():
        return None
    return row


def devices_of(db: DbSession, user_id: uuid.UUID) -> list[DeviceSession]:
    return list(db.scalars(select(DeviceSession).where(DeviceSession.user_id == user_id, DeviceSession.revoked_at.is_(None)).order_by(DeviceSession.created_at.desc())))


def revoke(db: DbSession, device_id: uuid.UUID, user_id: uuid.UUID | None, reason: str) -> bool:
    """Signs a headset out. With `user_id` only that person's own headset can be revoked."""
    stmt = update(DeviceSession).where(DeviceSession.id == device_id, DeviceSession.revoked_at.is_(None)).values(revoked_at=_now(), revoked_reason=reason)
    if user_id is not None:
        stmt = stmt.where(DeviceSession.user_id == user_id)
    return db.execute(stmt.returning(DeviceSession.id)).scalar_one_or_none() is not None


def revoke_all_of(db: DbSession, user_id: uuid.UUID, reason: str) -> int:
    return len(db.execute(update(DeviceSession).where(DeviceSession.user_id == user_id, DeviceSession.revoked_at.is_(None)).values(revoked_at=_now(), revoked_reason=reason)
                          .returning(DeviceSession.id)).all())
