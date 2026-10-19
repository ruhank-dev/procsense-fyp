"""Reply shapes for session upload and scenario packages."""
from __future__ import annotations

import datetime as dt
import uuid
from typing import Literal

from pydantic import BaseModel


class SessionStatus(BaseModel):
    session_id: uuid.UUID
    status: Literal["pending", "verified", "rejected", "unverifiable"]
    reject_reason: str = ""
    case_id: str
    mode: Literal["practice", "assessment"]
    log_entries: int
    received_at: dt.datetime
    end_state: str | None = None
    result: str | None = None
    process_score: float | None = None


class UploadReply(BaseModel):
    outcome: Literal["accepted", "already_stored"]
    session: SessionStatus
    status_url: str


class PackageRequest(BaseModel):
    """What the headset asks for: a pre-generated package for a scenario family and level. The lists name stories the trainee has already met."""
    scenario: Literal["S1", "S2"]
    level: Literal["beginner", "advanced"]
    exclude_spines: list[str] = []
    exclude_fingerprints: list[str] = []


class PackageOffer(BaseModel):
    package_hash: str
    scenario: str
    level: str
    case_id: str
    spine: str


class PackageReply(BaseModel):
    package_hash: str
    outcome: Literal["stored", "already_stored"]
