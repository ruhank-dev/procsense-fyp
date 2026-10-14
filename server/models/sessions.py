"""Sessions and what hangs off them (p1-backend-s02). The log is stored once, compressed; the score rows are filled by verification (p1-backend-s09).
Also here, for the scenario generator and personalisation plans: the session evidence record, the learner profile and the generated scenario packages."""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import BigInteger, CheckConstraint, DateTime, ForeignKey, Index, Integer, LargeBinary, Numeric, SmallInteger, String, func, text
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base
from server.models.accounts import _in

SESSION_MODES = ("practice", "assessment")
SESSION_STATUSES = ("pending", "verified", "rejected", "unverifiable")
PACKAGE_STATUSES = ("GENERATED_PRACTICE_ONLY",)


class Session(Base):
    """One finished session uploaded by a signed-in person into an organisation. The id is the session id the headset gave it, so a retried upload is the same row."""
    __tablename__ = "session"
    __table_args__ = (
        CheckConstraint(_in("mode", SESSION_MODES), name="mode_values"),
        CheckConstraint(_in("status", SESSION_STATUSES), name="status_values"),
        Index("ix_session_organisation_received", "organisation_id", "received_at"),
        Index("ix_session_user_received", "user_id", "received_at"),
        Index("ix_session_case_id", "case_id"),
        Index("ix_session_status", "status"),
        Index("ix_session_organisation_status_received", "organisation_id", "status", "received_at"),   # the dashboard asks for one organisation's verified sessions by date (p1-backend-s04)
    )

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)
    case_id: Mapped[str] = mapped_column(String(40), nullable=False)
    mode: Mapped[str] = mapped_column(String(16), nullable=False)
    seed: Mapped[str] = mapped_column(String(20), nullable=False)
    content_version: Mapped[str] = mapped_column(String(64), nullable=False)
    engine_version: Mapped[str] = mapped_column(String(40), nullable=False)
    scenario_package_hash: Mapped[str] = mapped_column(String(64), nullable=False, server_default=text("''"))
    log_hash: Mapped[str] = mapped_column(String(64), nullable=False)
    log_entries: Mapped[int] = mapped_column(Integer, nullable=False)
    ended_step: Mapped[int] = mapped_column(BigInteger, nullable=False)
    header_line: Mapped[str] = mapped_column(String, nullable=False, server_default=text("''"))   # the first line of the file as uploaded (the log alone cannot be replayed without it)
    log_gz: Mapped[bytes] = mapped_column(LargeBinary, nullable=False)
    verify_attempts: Mapped[int] = mapped_column(SmallInteger, nullable=False, server_default=text("0"))
    status: Mapped[str] = mapped_column(String(16), nullable=False, server_default=text("'pending'"))
    reject_reason: Mapped[str] = mapped_column(String(40), nullable=False, server_default=text("''"))
    received_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
    verified_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))
    log_purged_at: Mapped[dt.datetime | None] = mapped_column(DateTime(timezone=True))   # when the stored log and report page were removed after the retention period (p1-backend-s12)
    # filled by verification (p1-backend-s09) so dashboards never read the log
    end_state: Mapped[str | None] = mapped_column(String(40))
    result: Mapped[str | None] = mapped_column(String(24))
    process_score: Mapped[float | None] = mapped_column(Numeric(5, 2))
    report_json: Mapped[dict | None] = mapped_column(JSONB)
    report_html_gz: Mapped[bytes | None] = mapped_column(LargeBinary)


class SessionScoreItem(Base):
    __tablename__ = "session_score_item"

    session_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("session.id", ondelete="CASCADE"), primary_key=True)
    ordinal: Mapped[int] = mapped_column(Integer, primary_key=True)
    item_id: Mapped[str] = mapped_column(String(80), nullable=False)
    behaviour: Mapped[str] = mapped_column(String(40), nullable=False, server_default=text("''"))
    severity: Mapped[str] = mapped_column(String(16), nullable=False)
    status: Mapped[str] = mapped_column(String(16), nullable=False)
    points: Mapped[float] = mapped_column(Numeric(6, 2), nullable=False)
    step: Mapped[int] = mapped_column(BigInteger, nullable=False)
    evidence: Mapped[list] = mapped_column(JSONB, nullable=False, server_default=text("'[]'::jsonb"))


class SessionBehaviour(Base):
    __tablename__ = "session_behaviour"

    session_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("session.id", ondelete="CASCADE"), primary_key=True)
    behaviour: Mapped[str] = mapped_column(String(40), primary_key=True)
    credits: Mapped[int] = mapped_column(Integer, nullable=False)
    penalties: Mapped[int] = mapped_column(Integer, nullable=False)
    net: Mapped[int] = mapped_column(Integer, nullable=False)


class SessionEvidence(Base):
    """SessionEvidence v1 of the session (schema owned by p1-personalisation-s01): pseudonymous, no free text from the trainee."""
    __tablename__ = "session_evidence"

    session_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("session.id", ondelete="CASCADE"), primary_key=True)
    evidence: Mapped[dict] = mapped_column(JSONB, nullable=False)
    created_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)


class LearnerProfile(Base):
    """One row per person and organisation: the learner model's parameters and a summary (written by p1-personalisation-s02, read by the person and by the supervisors who may see them)."""
    __tablename__ = "learner_profile"

    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), primary_key=True)
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), primary_key=True)
    model: Mapped[dict] = mapped_column(JSONB, nullable=False, server_default=text("'{}'::jsonb"))
    summary: Mapped[dict] = mapped_column(JSONB, nullable=False, server_default=text("'{}'::jsonb"))
    version: Mapped[int] = mapped_column(Integer, nullable=False, server_default=text("1"))
    updated_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)


class ScenarioPackage(Base):
    """A generated scenario package, stored once, keyed by the SHA-256 of its canonical bytes."""
    __tablename__ = "scenario_package"
    __table_args__ = (CheckConstraint(_in("status", PACKAGE_STATUSES), name="status_values"), CheckConstraint("hash ~ '^[0-9a-f]{64}$'", name="hash_format"))

    hash: Mapped[str] = mapped_column(String(64), primary_key=True)
    package_gz: Mapped[bytes] = mapped_column(LargeBinary, nullable=False)
    size_bytes: Mapped[int] = mapped_column(Integer, nullable=False)
    status: Mapped[str] = mapped_column(String(32), nullable=False, server_default=text("'GENERATED_PRACTICE_ONLY'"))
    created_at: Mapped[dt.datetime] = mapped_column(DateTime(timezone=True), server_default=func.now(), nullable=False)
