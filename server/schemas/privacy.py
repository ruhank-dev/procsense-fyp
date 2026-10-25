"""Request and reply shapes for consent, the access log, export and deletion (p1-backend-s12)."""
from __future__ import annotations

import datetime as dt
import uuid
from typing import Literal

from pydantic import BaseModel, Field


class ConsentKind(BaseModel):
    kind: Literal["results", "research", "terms", "cloud"]
    title: str
    text: str
    required: bool
    granted: bool | None            # the person's latest answer, or null when they have never answered
    answered_version: str | None
    documents: list[str] = []       # the legal documents this question points to (read them at GET /legal/{name})
    current: bool                   # answered yes to the current text (or, for an optional kind, has answered it at all)


class ConsentState(BaseModel):
    version: str
    needs_consent: bool             # the required answer is missing or was given to an older text
    kinds: list[ConsentKind]


class ConsentIn(BaseModel):
    kind: Literal["results", "research", "terms", "cloud"]
    granted: bool


class AccessEntry(BaseModel):
    at: dt.datetime
    organisation: str
    viewer: str
    viewer_pseudonym: str
    viewer_role: str
    action: str
    session_id: uuid.UUID | None


class DeleteIn(BaseModel):
    confirm: str = Field(max_length=20)


class RetentionOut(BaseModel):
    retention_months: int | None
    effective_months: int
    default_months: int


class RetentionIn(BaseModel):
    retention_months: int | None = Field(ge=6, le=60)   # null puts the platform default back
