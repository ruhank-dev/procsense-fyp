"""Reply shapes for the AI audit (p1-backend-s05). No text anywhere: hashes, references and codes only."""
from __future__ import annotations

import datetime as dt
import uuid

from pydantic import BaseModel


class Latency(BaseModel):
    p50: float
    p95: float
    p99: float
    max: int


class TaskWeek(BaseModel):
    task: str
    calls: int
    fallbacks: int
    fallback_rate: float
    server_errors: int
    latency_ms: Latency
    validation: dict[str, int]   # passed, failed, skipped, error, not_reported


class ReasonCount(BaseModel):
    task: str
    reason: str
    count: int


class WeeklyAudit(BaseModel):
    week_start: dt.date          # a Monday (UTC)
    week_end: dt.date            # the Sunday
    calls: int
    people: int
    organisations: int
    fallbacks: int
    fallback_rate: float | None
    tasks: list[TaskWeek]
    failure_reasons: list[ReasonCount]   # the validator's reasons, for calls it failed
    fallback_reasons: list[ReasonCount]


class AiCallOut(BaseModel):
    id: uuid.UUID
    at: dt.datetime
    trainee: str | None
    session_id: uuid.UUID | None
    task: str
    route: str
    model: str
    model_version: str
    prompt_id: str
    context_id: str
    input_hash: str
    output_ref: str
    validation: str
    reasons: list[str]
    fallback: bool
    fallback_reason: str
    status_code: int
    latency_ms: int


class AiCallPage(BaseModel):
    total: int
    limit: int
    offset: int
    items: list[AiCallOut]
