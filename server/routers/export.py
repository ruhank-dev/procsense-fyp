"""CSV export of verified sessions (p1-backend-s04). Pseudonymous: no names and no emails in any column. The columns are documented in server/DATA_DICTIONARY.md.

A supervisor (or above) exports their own organisation. The platform administrator can export every organisation for the evaluation study; there the organisation is replaced by a stable code,
so rows of one organisation can be grouped but the organisation cannot be named.
"""
from __future__ import annotations

import csv
import hashlib
import io
import logging
import uuid
from collections.abc import Iterator

from fastapi import APIRouter, Depends, HTTPException
from fastapi.responses import StreamingResponse
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import CurrentUser, OrgContext, db_session, require_platform_admin, require_supervisor
from server.repositories import accounts
from server.repositories import dashboard as repository
from server.repositories.dashboard import Filters
from server.routers.dashboard import _check_cohort, _filters
from server.schemas.dashboard import WEIGHTS_PROVISIONAL

router = APIRouter(tags=["export"])
access_log = logging.getLogger("procsense.access")

COLUMNS = ["session_id", "organisation", "trainee", "case_id", "mode", "result", "end_state", "process_score", "process_score_provisional", "credits", "penalties", "critical_items",
           "content_version", "engine_version", "received_at", "verified_at"]


def organisation_code(organisation_id: uuid.UUID) -> str:
    """A stable code that groups the rows of one organisation without naming it."""
    return "O-" + hashlib.sha256(("procsense-export:" + str(organisation_id)).encode("utf-8")).hexdigest()[:8]


def _safe(value) -> str:
    """A cell that a spreadsheet would run as a formula (it starts with = + - @) is made harmless; empty for none."""
    if value is None:
        return ""
    text = value.isoformat() if hasattr(value, "isoformat") else str(value)
    return "'" + text if text[:1] in ("=", "+", "-", "@", "\t", "\r") else text


def _stream(db: DbSession, organisation_id: uuid.UUID | None, f: Filters, label) -> Iterator[str]:
    buffer = io.StringIO()
    writer = csv.writer(buffer, lineterminator="\n")
    writer.writerow(COLUMNS)
    yield buffer.getvalue()
    for (sid, org, pseudonym, case_id, mode, result, end_state, score, credits, penalties, critical, content, engine, received, verified) in repository.export_rows(db, organisation_id, f):
        buffer.seek(0)
        buffer.truncate()
        writer.writerow([_safe(v) for v in (sid, label(org), pseudonym, case_id, mode, result, end_state, f"{float(score):.2f}" if score is not None else None,
                                            "true" if WEIGHTS_PROVISIONAL else "false", int(credits), int(penalties), int(critical), content, engine, received, verified)])
        yield buffer.getvalue()


def _response(chunks: Iterator[str], name: str) -> StreamingResponse:
    return StreamingResponse(chunks, media_type="text/csv; charset=utf-8", headers={"Content-Disposition": f'attachment; filename="{name}"', "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff"})


@router.get("/organisations/{organisation_id}/export.csv")
def export_organisation(ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), f: Filters = Depends(_filters)) -> StreamingResponse:
    _check_cohort(db, ctx, f)
    access_log.info("export: viewer=%s role=%s organisation=%s", ctx.user.profile.user_id, ctx.role, ctx.organisation.id)
    return _response(_stream(db, ctx.organisation.id, f, lambda org: ctx.organisation.slug), f"procsense-{ctx.organisation.slug}.csv")


@router.get("/export.csv")
def export_platform(user: CurrentUser = Depends(require_platform_admin), db: DbSession = Depends(db_session), f: Filters = Depends(_filters)) -> StreamingResponse:
    """Every organisation, with the organisation as a stable code (the evaluation study's data)."""
    if f.cohort_id is not None:
        raise HTTPException(status_code=422, detail="cohort_filter_needs_an_organisation")
    access_log.info("export: platform administrator=%s", user.profile.user_id)
    return _response(_stream(db, None, f, organisation_code), "procsense-platform.csv")
