"""One row per AI request (p1-backend-s05): which model and prompt answered, what the validator said, how long it took and whether the deterministic fallback was used.

No text is kept: the input is a SHA-256 hash, the output a reference (a hash, or the id of a pre-approved line), the reasons are short codes. So a row can show what the AI was asked and said only
to someone who holds the text (the headset's own log), and nothing a trainee said is stored here (T-AU2). Every row carries the organisation and the person, so an organisation can ask what the AI
said to its trainees; the rows go with the person's data export, move to the stand-in on deletion, and are removed after the retention period (p1-backend-s12).
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import Boolean, CheckConstraint, ForeignKey, Index, Integer, SmallInteger, String
from sqlalchemy.dialects.postgresql import JSONB, UUID
from sqlalchemy.orm import Mapped, mapped_column

from server.database import Base
from server.models.accounts import _created, _in

VALIDATIONS = ("passed", "failed", "skipped", "error", "not_reported")


class AiCall(Base):
    __tablename__ = "ai_call"
    __table_args__ = (
        CheckConstraint(_in("validation", VALIDATIONS), name="validation_values"),
        CheckConstraint("latency_ms >= 0", name="latency_not_negative"),
        Index("ix_ai_call_organisation_at", "organisation_id", "at"),
        Index("ix_ai_call_user_at", "user_id", "at"),
        Index("ix_ai_call_at", "at"),
    )

    id: Mapped[uuid.UUID] = mapped_column(UUID(as_uuid=True), primary_key=True)   # given when the request starts, so the reply can carry it (X-AI-Call-Id) and the headset can mirror it
    organisation_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("organisation.id"), nullable=False)
    user_id: Mapped[uuid.UUID] = mapped_column(ForeignKey("profile.user_id"), nullable=False)    # the pseudonymous person; their name is never here
    session_id: Mapped[uuid.UUID | None] = mapped_column(UUID(as_uuid=True))                     # the training session it was for (it may not be uploaded yet, so no link)
    task: Mapped[str] = mapped_column(String(32), nullable=False)             # e.g. dialogue_patient, classify, debrief, asr, tts
    route: Mapped[str] = mapped_column(String(64), nullable=False)
    model: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    model_version: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    prompt_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    context_id: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    input_hash: Mapped[str] = mapped_column(String(64), nullable=False, default="")
    output_ref: Mapped[str] = mapped_column(String(80), nullable=False, default="")
    validation: Mapped[str] = mapped_column(String(16), nullable=False)
    reasons: Mapped[list] = mapped_column(JSONB, nullable=False, default=list)   # codes such as UNSUPPORTED_CLAIM, never sentences
    fallback: Mapped[bool] = mapped_column(Boolean, nullable=False, default=False)
    fallback_reason: Mapped[str] = mapped_column(String(40), nullable=False, default="")
    status_code: Mapped[int] = mapped_column(SmallInteger, nullable=False)
    latency_ms: Mapped[int] = mapped_column(Integer, nullable=False)
    at: Mapped[dt.datetime] = _created()
