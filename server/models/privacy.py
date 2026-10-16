"""Who looked at whose data (p1-backend-s12). Every time a supervisor or administrator opens an individual's report, progress or session list, or exports data, a row is written; the person can read the rows about them.
Aggregates over several people (the organisation overview) are not logged per person."""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import CheckConstraint, DateTime, ForeignKey, Index, String
from sqlalchemy.dialects.postgresql import UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base
from server.models.accounts import _created, _in, _uuid_pk

ACCESS_ACTIONS = ("view_report", "view_progress", "view_sessions", "export", "view_ai_calls")


class AccessLog(Base):
    __tablename__ = "access_log"
    __table_args__ = (
        CheckConstraint(_in("action", ACCESS_ACTIONS), name="action_values"),
        Index("ix_access_log_subject_at", "subject_id", "at"),
        Index("ix_access_log_at", "at"),
    )

    id: Mapped[uuid.UUID] = _uuid_pk()
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    viewer_id: Mapped[uuid.UUID | None] = mapped_column(ForeignKey("profile.user_id"))   # empty when the viewer has since deleted their data
    viewer_pseudonym: Mapped[str] = mapped_column(String(16), nullable=False)
    viewer_role: Mapped[str] = mapped_column(String(16), nullable=False)
    subject_id: Mapped[uuid.UUID | None] = mapped_column(ForeignKey("profile.user_id"))  # whose data was opened; empty for an export of several people
    action: Mapped[str] = mapped_column(String(16), nullable=False)
    session_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True))
    detail: Mapped[str] = mapped_column(String(200), nullable=False, default="")
    at: Mapped[dt.datetime] = _created()
