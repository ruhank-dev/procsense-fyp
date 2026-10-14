"""Signing a headset in with a code (p1-backend-s10). The headset has no good keyboard, so it shows a short code and a person approves it on a phone or computer.

Nothing secret is stored: the device code, the access token and the refresh token are random values shown to the headset once, and only their SHA-256 is kept.
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import CheckConstraint, DateTime, ForeignKey, Index, SmallInteger, String, text
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base
from server.models.accounts import _created, _in, _uuid_pk

LOGIN_STATUSES = ("pending", "approved", "denied", "used")


class DeviceLogin(Base):
    """One request to sign a headset in: pending until a person approves or denies it, then used once when the headset collects its tokens."""
    __tablename__ = "device_login"
    __table_args__ = (CheckConstraint(_in("status", LOGIN_STATUSES), name="status_values"), Index("ix_device_login_expires_at", "expires_at"))

    id: Mapped[uuid.UUID] = _uuid_pk()
    device_code_hash: Mapped[str] = mapped_column(String(64), nullable=False, unique=True)
    user_code: Mapped[str] = mapped_column(String(8), nullable=False, unique=True)
    device_name: Mapped[str] = mapped_column(String(60), nullable=False)
    status: Mapped[str] = mapped_column(String(16), nullable=False, server_default=text("'pending'"))
    user_id: Mapped[uuid.UUID | None] = mapped_column(ForeignKey("profile.user_id"))   # who approved it; the tokens are issued to this person only
    interval_seconds: Mapped[int] = mapped_column(SmallInteger, nullable=False)
    last_polled_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))
    expires_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    created_at: Mapped[dt.datetime] = _created()


class DeviceSession(Base):
    """A signed-in headset: the hashes of its current access token and refresh token, and of the refresh token before the last rotation (so a stolen one used twice is noticed)."""
    __tablename__ = "device_session"
    __table_args__ = (Index("ix_device_session_user_id", "user_id"),)

    id: Mapped[uuid.UUID] = _uuid_pk()
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)
    device_name: Mapped[str] = mapped_column(String(60), nullable=False)
    access_hash: Mapped[str] = mapped_column(String(64), nullable=False, unique=True)
    access_expires_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    refresh_hash: Mapped[str] = mapped_column(String(64), nullable=False, unique=True)
    refresh_expires_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    previous_refresh_hash: Mapped[str | None] = mapped_column(String(64))
    revoked_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))
    revoked_reason: Mapped[str | None] = mapped_column(String(40))
    last_used_at: Mapped[dt.datetime] = _created()
    created_at: Mapped[dt.datetime] = _created()
