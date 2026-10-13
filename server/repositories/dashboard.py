"""Reports, dashboard figures and the export (p1-backend-s04). ORM only, aggregates in SQL, and never the stored log text.

Two rules hold for every function here, and are tested with two organisations:
  1. the organisation is an argument and is in every query (a session of another organisation cannot be reached even if its id is known);
  2. figures count VERIFIED sessions only (the one exception is `list_sessions`, which can be asked for other statuses so a person can see their own pending upload).
"""
from __future__ import annotations

import datetime as dt
import uuid
from collections.abc import Iterator
from dataclasses import dataclass

from sqlalchemy import Select, and_, func, select
from sqlalchemy.orm import Session as DbSession

from server.models.accounts import CohortMember, Profile
from server.models.sessions import Session, SessionBehaviour, SessionScoreItem

MAX_EXPORT_ROWS = 200_000
STATUSES = ("pending", "verified", "rejected", "unverifiable")


@dataclass(frozen=True)
class Filters:
    user_id: uuid.UUID | None = None
    cohort_id: uuid.UUID | None = None
    case_id: str | None = None
    mode: str | None = None
    result: str | None = None
    since: dt.datetime | None = None
    until: dt.datetime | None = None


def _conditions(organisation_id: uuid.UUID | None, f: Filters, status: str | None = "verified") -> list:
    """`organisation_id` is None only for the platform administrator's export across organisations."""
    conds = [] if organisation_id is None else [Session.organisation_id == organisation_id]
    if status is not None:
        conds.append(Session.status == status)
    if f.user_id is not None:
        conds.append(Session.user_id == f.user_id)
    if f.cohort_id is not None:
        conds.append(Session.user_id.in_(select(CohortMember.user_id).where(CohortMember.cohort_id == f.cohort_id, CohortMember.role == "trainee")))
    if f.case_id:
        conds.append(Session.case_id == f.case_id)
    if f.mode:
        conds.append(Session.mode == f.mode)
    if f.result:
        conds.append(Session.result == f.result)
    if f.since is not None:
        conds.append(Session.received_at >= f.since)
    if f.until is not None:
        conds.append(Session.received_at < f.until)
    return conds


def _ids_of_sessions(organisation_id: uuid.UUID, f: Filters):
    """The verified sessions the figures are about, as a sub-query of ids."""
    return select(Session.id).where(*_conditions(organisation_id, f)).scalar_subquery()


def summary(db: DbSession, organisation_id: uuid.UUID, f: Filters) -> dict:
    conds = _conditions(organisation_id, f)
    total, trainees, average = db.execute(select(func.count(), func.count(func.distinct(Session.user_id)), func.avg(Session.process_score)).where(*conds)).one()
    week = func.date_trunc("week", Session.received_at)
    per_week = [(w.date(), n) for w, n in db.execute(select(week, func.count()).where(*conds).group_by(week).order_by(week)).all()]
    results = list(db.execute(select(func.coalesce(Session.result, ""), func.count()).where(*conds).group_by(Session.result).order_by(func.count().desc(), Session.result)).all())
    ids = _ids_of_sessions(organisation_id, f)
    critical = list(db.execute(select(SessionScoreItem.item_id, func.count()).where(SessionScoreItem.session_id.in_(ids), SessionScoreItem.status == "critical")
                               .group_by(SessionScoreItem.item_id).order_by(func.count().desc(), SessionScoreItem.item_id).limit(10)).all())
    behaviours = list(db.execute(select(SessionBehaviour.behaviour, func.count(), func.sum(SessionBehaviour.credits), func.sum(SessionBehaviour.penalties), func.sum(SessionBehaviour.net),
                                        func.avg(SessionBehaviour.net)).where(SessionBehaviour.session_id.in_(ids)).group_by(SessionBehaviour.behaviour)
                                 .order_by(func.avg(SessionBehaviour.net), SessionBehaviour.behaviour)).all())
    return dict(sessions=total, trainees=trainees, average_process_score=round(float(average), 2) if average is not None else None,
                per_week=[dict(week=w, sessions=n) for w, n in per_week], results=[dict(result=r, count=n) for r, n in results],
                critical_items=[dict(item_id=i, count=n) for i, n in critical],
                behaviours=[dict(behaviour=b, sessions=n, credits=int(c), penalties=int(p), net=int(t), average_net=round(float(a), 2)) for b, n, c, p, t, a in behaviours])


def _row_query(organisation_id: uuid.UUID, f: Filters, status: str | None) -> Select:
    return (select(Session.id, Profile.pseudonym, Profile.display_name, Session.case_id, Session.mode, Session.status, Session.result, Session.end_state, Session.process_score,
                   Session.content_version, Session.received_at)
            .join(Profile, Profile.user_id == Session.user_id).where(*_conditions(organisation_id, f, status)))


def _row(r, show_names: bool) -> dict:
    return dict(session_id=r.id, trainee=r.pseudonym, display_name=(r.display_name or None) if show_names else None, case_id=r.case_id, mode=r.mode, status=r.status, result=r.result,
                end_state=r.end_state, process_score=float(r.process_score) if r.process_score is not None else None, content_version=r.content_version, received_at=r.received_at)


def list_sessions(db: DbSession, organisation_id: uuid.UUID, f: Filters, status: str | None, limit: int, offset: int, show_names: bool) -> tuple[int, list[dict]]:
    total = db.scalar(select(func.count()).select_from(Session).where(*_conditions(organisation_id, f, status)))
    rows = db.execute(_row_query(organisation_id, f, status).order_by(Session.received_at.desc(), Session.id).limit(limit).offset(offset)).all()
    return total or 0, [_row(r, show_names) for r in rows]


def series(db: DbSession, organisation_id: uuid.UUID, f: Filters, limit: int = 200) -> list[dict]:
    """The latest verified sessions, oldest first (for a progress chart)."""
    rows = db.execute(_row_query(organisation_id, f, "verified").order_by(Session.received_at.desc(), Session.id).limit(limit)).all()
    return [_row(r, False) for r in reversed(rows)]


def report_row(db: DbSession, organisation_id: uuid.UUID, session_id: uuid.UUID) -> tuple[Session, str] | None:
    """The session and its owner's pseudonym, only when it belongs to this organisation."""
    found = db.execute(select(Session, Profile.pseudonym).join(Profile, Profile.user_id == Session.user_id).where(Session.id == session_id, Session.organisation_id == organisation_id)).first()
    return (found[0], found[1]) if found else None


def export_rows(db: DbSession, organisation_id: uuid.UUID | None, f: Filters, research_only: bool = False) -> Iterator[tuple]:
    """One row per verified session, pseudonymous: no names, no emails. `organisation_id` None = every organisation (the platform administrator's export, which the caller relabels)."""
    critical = (select(func.count()).select_from(SessionScoreItem).where(SessionScoreItem.session_id == Session.id, SessionScoreItem.status == "critical").correlate(Session).scalar_subquery())
    credits = (select(func.coalesce(func.sum(SessionBehaviour.credits), 0)).where(SessionBehaviour.session_id == Session.id).correlate(Session).scalar_subquery())
    penalties = (select(func.coalesce(func.sum(SessionBehaviour.penalties), 0)).where(SessionBehaviour.session_id == Session.id).correlate(Session).scalar_subquery())
    conds = _conditions(organisation_id, f)
    if research_only:
        from server.repositories import privacy
        conds.append(Session.user_id.in_(privacy.research_consenting_users()))   # a person who did not agree to research use is not in a research export (T-PR4)
    stmt = (select(Session.id, Session.organisation_id, Profile.pseudonym, Session.case_id, Session.mode, Session.result, Session.end_state, Session.process_score, credits, penalties, critical,
                   Session.content_version, Session.engine_version, Session.received_at, Session.verified_at)
            .join(Profile, Profile.user_id == Session.user_id).where(and_(*conds)).order_by(Session.received_at, Session.id).limit(MAX_EXPORT_ROWS))
    for row in db.execute(stmt.execution_options(yield_per=500)):
        yield tuple(row)
