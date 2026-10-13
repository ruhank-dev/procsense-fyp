"""Reply shapes for reports, the dashboard and the export (p1-backend-s04). Everything here is about verified sessions only, and says so where a number could be mistaken for a final one."""
from __future__ import annotations

import datetime as dt
import uuid
from typing import Literal

from pydantic import BaseModel

# The weights Ruhan's scorer multiplies the behaviour scores by are provisional design values (the verdict does not depend on them), so every place that shows a process score says so.
WEIGHTS_PROVISIONAL = True


class ResultCount(BaseModel):
    result: str
    count: int


class WeekCount(BaseModel):
    week: dt.date            # the Monday of the week (UTC)
    sessions: int


class CriticalCount(BaseModel):
    item_id: str
    count: int


class BehaviourSummary(BaseModel):
    behaviour: str
    sessions: int            # verified sessions that had evidence for this behaviour
    credits: int
    penalties: int
    net: int
    average_net: float       # net per session: the lowest ones are the behaviours that need work


class Summary(BaseModel):
    sessions: int
    trainees: int
    average_process_score: float | None
    process_score_provisional: bool = WEIGHTS_PROVISIONAL
    per_week: list[WeekCount]
    results: list[ResultCount]
    critical_items: list[CriticalCount]
    behaviours: list[BehaviourSummary]   # lowest average net first


class SessionRow(BaseModel):
    session_id: uuid.UUID
    trainee: str                         # the pseudonymous id
    display_name: str | None = None      # shown to supervisors of the same organisation only
    case_id: str
    mode: Literal["practice", "assessment"]
    status: Literal["pending", "verified", "rejected", "unverifiable"]
    result: str | None
    end_state: str | None
    process_score: float | None
    process_score_provisional: bool = WEIGHTS_PROVISIONAL
    content_version: str
    received_at: dt.datetime


class SessionPage(BaseModel):
    total: int
    limit: int
    offset: int
    items: list[SessionRow]


class Progress(BaseModel):
    trainee: str
    summary: Summary
    series: list[SessionRow]             # the latest sessions, oldest first


class SessionReport(BaseModel):
    session_id: uuid.UUID
    trainee: str
    case_id: str
    mode: Literal["practice", "assessment"]
    content_version: str
    engine_version: str
    verified_at: dt.datetime | None
    result: str | None
    end_state: str | None
    process_score: float | None
    process_score_provisional: bool = WEIGHTS_PROVISIONAL
    facts: dict                          # the debrief facts the engine produced (schema debrief_facts.v1): items with evidence, behaviours, timeline, sources
