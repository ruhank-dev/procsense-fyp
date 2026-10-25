"""One row per finished session that the scenario generator should remember (DT-201, p1-live-scenario-generation-s12): what the next objectives and the novelty check read.

The fields are those of `server/generation/history.py` `HistoryEntry`, so the file store and this table behave the same. A row holds only the story, the tier, the package id and hash, the fingerprint and its levels and the
credits and penalties per behaviour; no text a person said and no name. It belongs to the person: it is part of their export and is deleted with them (the table is guidance for what to give them next, not a result).
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import DateTime, ForeignKey, Index, String
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base
from server.models.accounts import _created


class GenerationHistory(Base):
    __tablename__ = "generation_history"
    __table_args__ = (Index("ix_generation_history_user_ended", "user_id", "ended_at"),)

    session_id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)      # one entry per session: a session verified twice replaces its entry
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)
    case_id: Mapped[str] = mapped_column(String(64), nullable=False)
    story: Mapped[str] = mapped_column(String(64), nullable=False)
    tier: Mapped[str] = mapped_column(String(16), nullable=False)
    behaviours: Mapped[dict] = mapped_column(JSONB, nullable=False, default=dict)          # behaviour -> [credits, penalties]
    ended_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), nullable=False)
    package_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    package_sha256: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    fingerprint: Mapped[str] = mapped_column(String(128), nullable=False, default="")
    levels: Mapped[dict] = mapped_column(JSONB, nullable=False, default=dict)
    recorded_at: Mapped[dt.datetime] = _created()
