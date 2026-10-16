"""The AI audit rows (p1-backend-s05): the weekly report, the list of calls an organisation may look at, and what privacy needs (export, deletion, retention). ORM only, aggregates in SQL.

The organisation is an argument of every query (None = the whole platform, for the platform administrator's report), as in repositories/dashboard.py.
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import case, delete, func, literal_column, select, update
from sqlalchemy.orm import Session as DbSession

from server.models.accounts import Profile
from server.models.ai_call import VALIDATIONS, AiCall

PERCENTILES = (0.5, 0.95, 0.99)


def week_bounds(day: dt.date) -> tuple[dt.datetime, dt.datetime]:
    """Monday 00:00 UTC of the week holding `day`, and the Monday after."""
    monday = day - dt.timedelta(days=day.weekday())
    start = dt.datetime(monday.year, monday.month, monday.day, tzinfo=dt.timezone.utc)
    return start, start + dt.timedelta(days=7)


def _where(organisation_id: uuid.UUID | None, start: dt.datetime, end: dt.datetime) -> list:
    conds = [AiCall.at >= start, AiCall.at < end]
    if organisation_id is not None:
        conds.append(AiCall.organisation_id == organisation_id)
    return conds


def weekly(db: DbSession, organisation_id: uuid.UUID | None, day: dt.date) -> dict:
    """Fallback rate, validation results and failure reasons, and latency percentiles per task, for the week holding `day`."""
    start, end = week_bounds(day)
    conds = _where(organisation_id, start, end)
    pct = [func.percentile_cont(p).within_group(AiCall.latency_ms) for p in PERCENTILES]
    tasks = []
    for row in db.execute(select(AiCall.task, func.count(), func.sum(case((AiCall.fallback, 1), else_=0)), func.sum(case((AiCall.status_code >= 500, 1), else_=0)), *pct,
                                 func.max(AiCall.latency_ms), *[func.sum(case((AiCall.validation == v, 1), else_=0)) for v in VALIDATIONS])
                          .where(*conds).group_by(AiCall.task).order_by(AiCall.task)).all():
        task, calls, fallbacks, errors, p50, p95, p99, slowest, *validation = row
        tasks.append(dict(task=task, calls=calls, fallbacks=int(fallbacks), fallback_rate=round(fallbacks / calls, 4), server_errors=int(errors),
                          latency_ms=dict(p50=round(float(p50), 1), p95=round(float(p95), 1), p99=round(float(p99), 1), max=slowest),
                          validation={v: int(n) for v, n in zip(VALIDATIONS, validation)}))
    reason = func.jsonb_array_elements_text(AiCall.reasons).table_valued("value").alias("reason")
    reasons = [dict(task=t, reason=r, count=n) for t, r, n in db.execute(
        select(AiCall.task, reason.c.value, func.count()).select_from(AiCall).join(reason, literal_column("true")).where(*conds, AiCall.validation == "failed")
        .group_by(AiCall.task, reason.c.value).order_by(func.count().desc(), AiCall.task, reason.c.value).limit(50)).all()]
    fallbacks = [dict(task=t, reason=r, count=n) for t, r, n in db.execute(
        select(AiCall.task, AiCall.fallback_reason, func.count()).where(*conds, AiCall.fallback).group_by(AiCall.task, AiCall.fallback_reason)
        .order_by(func.count().desc(), AiCall.task, AiCall.fallback_reason).limit(50)).all()]
    calls, people, organisations, fell_back = db.execute(select(func.count(), func.count(func.distinct(AiCall.user_id)), func.count(func.distinct(AiCall.organisation_id)),
                                                                func.coalesce(func.sum(case((AiCall.fallback, 1), else_=0)), 0)).where(*conds)).one()
    return dict(week_start=start.date(), week_end=(end - dt.timedelta(days=1)).date(), calls=calls, people=people, organisations=organisations, fallbacks=int(fell_back),
                fallback_rate=round(fell_back / calls, 4) if calls else None, tasks=tasks, failure_reasons=reasons, fallback_reasons=fallbacks)


def list_calls(db: DbSession, organisation_id: uuid.UUID, user_id: uuid.UUID | None, session_id: uuid.UUID | None, since: dt.datetime | None, until: dt.datetime | None,
               limit: int, offset: int) -> tuple[int, list[dict]]:
    conds = [AiCall.organisation_id == organisation_id]
    if user_id is not None:
        conds.append(AiCall.user_id == user_id)
    if session_id is not None:
        conds.append(AiCall.session_id == session_id)
    if since is not None:
        conds.append(AiCall.at >= since)
    if until is not None:
        conds.append(AiCall.at < until)
    total = db.scalar(select(func.count()).select_from(AiCall).where(*conds)) or 0
    rows = db.execute(select(AiCall, Profile.pseudonym).join(Profile, Profile.user_id == AiCall.user_id).where(*conds).order_by(AiCall.at.desc(), AiCall.id).limit(limit).offset(offset)).all()
    return total, [_row(c, pseudonym) for c, pseudonym in rows]


def _row(c: AiCall, pseudonym: str | None) -> dict:
    return dict(id=c.id, at=c.at, trainee=pseudonym, session_id=c.session_id, task=c.task, route=c.route, model=c.model, model_version=c.model_version, prompt_id=c.prompt_id,
                context_id=c.context_id, input_hash=c.input_hash, output_ref=c.output_ref, validation=c.validation, reasons=c.reasons, fallback=c.fallback, fallback_reason=c.fallback_reason,
                status_code=c.status_code, latency_ms=c.latency_ms)


# ---------------------------------------------------------------- privacy (p1-backend-s12)

def calls_of_person(db: DbSession, user_id: uuid.UUID, limit: int = 10_000) -> list[dict]:
    rows = db.scalars(select(AiCall).where(AiCall.user_id == user_id).order_by(AiCall.at).limit(limit))
    return [{k: v for k, v in _row(c, None).items() if k != "trainee"} for c in rows]


def move_to_stand_in(db: DbSession, user_id: uuid.UUID, stand_in: uuid.UUID) -> None:
    """On deletion the rows stay (an organisation can still see what its AI did) but no longer lead to the person."""
    db.execute(update(AiCall).where(AiCall.user_id == user_id).values(user_id=stand_in))


def purge(db: DbSession, months: int, now: dt.datetime) -> int:
    return len(db.execute(delete(AiCall).where(AiCall.at < now - dt.timedelta(days=30 * months)).returning(AiCall.id)).all())
