"""Reports and dashboard data (p1-backend-s04), always for one organisation and always from verified sessions.

Who sees what: a trainee sees their own sessions and nobody else's; a supervisor or an organisation administrator sees the whole organisation (cohorts are a way to group and filter, not a wall: DT-145);
nobody sees another organisation (it is simply not found). A supervisor opening one person's report or progress is written to the access log (`procsense.access`; the table comes with p1-backend-s12).
"""
from __future__ import annotations

import datetime as dt
import gzip
import logging
import uuid
from typing import Literal

from fastapi import APIRouter, Depends, HTTPException, Query, Response
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import OrgContext, db_session, require_member, require_supervisor
from server.repositories import accounts
from server.repositories import dashboard as repository
from server.repositories.dashboard import Filters
from server.schemas.dashboard import Progress, SessionPage, SessionReport, Summary

router = APIRouter(tags=["reports and dashboard"])
access_log = logging.getLogger("procsense.access")
HTML_HEADERS = {
    "Content-Security-Policy": "default-src 'none'; style-src 'unsafe-inline'; img-src data:; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
    "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer", "Cache-Control": "no-store",
}


def _is_supervisor(ctx: OrgContext) -> bool:
    return accounts.ROLE_RANK[ctx.role] >= accounts.ROLE_RANK["supervisor"]


def _filters(trainee_id: uuid.UUID | None = Query(None), cohort_id: uuid.UUID | None = Query(None), case_id: str | None = Query(None, max_length=40),
             mode: Literal["practice", "assessment"] | None = Query(None), result: str | None = Query(None, max_length=24),
             since: dt.datetime | None = Query(None), until: dt.datetime | None = Query(None)) -> Filters:
    return Filters(trainee_id, cohort_id, case_id, mode, result, since, until)


def _filters_in_cohort(trainee_id: uuid.UUID | None = Query(None), case_id: str | None = Query(None, max_length=40), mode: Literal["practice", "assessment"] | None = Query(None),
                       result: str | None = Query(None, max_length=24), since: dt.datetime | None = Query(None), until: dt.datetime | None = Query(None)) -> Filters:
    """The same filters for a route that already names the cohort in its address."""
    return Filters(trainee_id, None, case_id, mode, result, since, until)


def _check_cohort(db: DbSession, ctx: OrgContext, f: Filters) -> None:
    if f.cohort_id is not None and accounts.get_cohort(db, ctx.organisation.id, f.cohort_id) is None:
        raise HTTPException(status_code=404, detail="not_found")   # a cohort of another organisation is not found either


def _scope(ctx: OrgContext, f: Filters) -> Filters:
    """A trainee only ever asks about themselves, whatever they put in the filters."""
    if _is_supervisor(ctx):
        return f
    return Filters(ctx.user.profile.user_id, f.cohort_id, f.case_id, f.mode, f.result, f.since, f.until)


@router.get("/organisations/{organisation_id}/sessions", response_model=SessionPage)
def list_sessions(ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session), f: Filters = Depends(_filters),
                  status: Literal["verified", "pending", "rejected", "unverifiable", "any"] = "verified", limit: int = Query(50, ge=1, le=200), offset: int = Query(0, ge=0)) -> SessionPage:
    _check_cohort(db, ctx, f)
    total, items = repository.list_sessions(db, ctx.organisation.id, _scope(ctx, f), None if status == "any" else status, limit, offset, show_names=_is_supervisor(ctx))
    return SessionPage(total=total, limit=limit, offset=offset, items=items)


def _owned_or_supervised(ctx: OrgContext, db: DbSession, session_id: uuid.UUID):
    found = repository.report_row(db, ctx.organisation.id, session_id)
    if found is None:
        raise HTTPException(status_code=404, detail="not_found")
    row, pseudonym = found
    mine = row.user_id == ctx.user.profile.user_id
    if not mine and not _is_supervisor(ctx):
        raise HTTPException(status_code=404, detail="not_found")
    if not mine:
        access_log.info("report opened: viewer=%s role=%s organisation=%s session=%s", ctx.user.profile.user_id, ctx.role, ctx.organisation.id, session_id)
    if row.status != "verified":
        raise HTTPException(status_code=409, detail={"code": "not_verified", "status": row.status})
    return row, pseudonym


@router.get("/organisations/{organisation_id}/sessions/{session_id}/report", response_model=SessionReport)
def session_report(session_id: uuid.UUID, ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session)) -> SessionReport:
    """The report the engine produced when the session was verified: the same content as the command-line report (end state, every score item with its evidence, behaviours, timeline, sources)."""
    row, pseudonym = _owned_or_supervised(ctx, db, session_id)
    return SessionReport(session_id=row.id, trainee=pseudonym, case_id=row.case_id, mode=row.mode, content_version=row.content_version, engine_version=row.engine_version,
                         verified_at=row.verified_at, result=row.result, end_state=row.end_state, process_score=float(row.process_score) if row.process_score is not None else None,
                         facts=row.report_json or {})


@router.get("/organisations/{organisation_id}/sessions/{session_id}/report.html")
def session_report_html(session_id: uuid.UUID, ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session)) -> Response:
    """The same report as a page. It carries no script, and the headers forbid loading anything else into it."""
    row, _ = _owned_or_supervised(ctx, db, session_id)
    if row.report_html_gz is None:
        raise HTTPException(status_code=404, detail="no_html_report")
    return Response(content=gzip.decompress(row.report_html_gz), media_type="text/html; charset=utf-8", headers=HTML_HEADERS)


def _progress(db: DbSession, ctx: OrgContext, user_id: uuid.UUID, pseudonym: str, f: Filters) -> Progress:
    own = Filters(user_id, None, f.case_id, f.mode, f.result, f.since, f.until)
    return Progress(trainee=pseudonym, summary=Summary(**repository.summary(db, ctx.organisation.id, own)), series=repository.series(db, ctx.organisation.id, own))


@router.get("/organisations/{organisation_id}/progress", response_model=Progress)
def my_progress(ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session), f: Filters = Depends(_filters)) -> Progress:
    """The signed-in person's own progress: sessions over time, the process score, credits and penalties per behaviour, the critical items that keep coming back."""
    return _progress(db, ctx, ctx.user.profile.user_id, ctx.user.profile.pseudonym, f)


@router.get("/organisations/{organisation_id}/members/{user_id}/progress", response_model=Progress)
def member_progress(user_id: uuid.UUID, ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), f: Filters = Depends(_filters)) -> Progress:
    membership = accounts.get_membership(db, user_id, ctx.organisation.id)
    profile = accounts.get_profile(db, user_id)
    if membership is None or profile is None:
        raise HTTPException(status_code=404, detail="not_found")
    if user_id != ctx.user.profile.user_id:
        access_log.info("progress opened: viewer=%s role=%s organisation=%s trainee=%s", ctx.user.profile.user_id, ctx.role, ctx.organisation.id, user_id)
    return _progress(db, ctx, user_id, profile.pseudonym, f)


@router.get("/organisations/{organisation_id}/overview", response_model=Summary)
def organisation_overview(ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), f: Filters = Depends(_filters)) -> Summary:
    """The organisation (or one cohort, with ?cohort_id=, or one person, with ?trainee_id=): sessions per week, results, the most common critical items, the behaviours that need work."""
    _check_cohort(db, ctx, f)
    return Summary(**repository.summary(db, ctx.organisation.id, f))


@router.get("/organisations/{organisation_id}/cohorts/{cohort_id}/overview", response_model=Summary)
def cohort_overview(cohort_id: uuid.UUID, ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), f: Filters = Depends(_filters_in_cohort)) -> Summary:
    f = Filters(f.user_id, cohort_id, f.case_id, f.mode, f.result, f.since, f.until)
    _check_cohort(db, ctx, f)
    return Summary(**repository.summary(db, ctx.organisation.id, f))
