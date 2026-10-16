"""The AI audit (p1-backend-s05): the weekly report for an organisation and for the platform, and the AI calls an organisation may look at.

Who sees what, as for the sessions (routers/dashboard.py): a trainee sees the calls made for them; a supervisor or an organisation administrator sees the organisation's calls and its weekly report;
the platform administrator sees the platform's report. Opening the calls of one person is written to the access log (`view_ai_calls`).
"""
from __future__ import annotations

import datetime as dt
import html
import uuid
from pathlib import Path
from string import Template

from fastapi import APIRouter, Depends, Query, Response
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import CurrentUser, OrgContext, db_session, require_member, require_platform_admin, require_supervisor
from server.repositories import accounts
from server.repositories import ai_audit as repository
from server.repositories import privacy as privacy_repository
from server.routers.dashboard import HTML_HEADERS
from server.schemas.ai_audit import AiCallPage, WeeklyAudit

router = APIRouter(tags=["AI audit"])
TEMPLATE = Template((Path(__file__).resolve().parents[1] / "templates" / "audit_weekly.html").read_text(encoding="utf-8"))


def _day(week: dt.date | None = Query(None, description="any day of the week (UTC); default: this week")) -> dt.date:
    return week or dt.datetime.now(dt.timezone.utc).date()


def _rate(value: float | None) -> str:
    return "" if value is None else f"{value * 100:.1f}%"


def _cells(values: list, numeric_from: int) -> str:
    return "<tr>" + "".join(("<td class=\"n\">" if i >= numeric_from else "<td>") + html.escape(str(v)) + "</td>" for i, v in enumerate(values)) + "</tr>"


def _rows(rows: list[str], columns: int) -> str:
    return "\n".join(rows) if rows else f'<tr><td class="empty" colspan="{columns}">none this week</td></tr>'


def render_weekly(report: dict, scope: str, generated: dt.datetime | None = None) -> str:
    """The weekly report as a page (server/templates/audit_weekly.html). Every value is escaped."""
    tasks = [_cells([t["task"], t["calls"], _rate(t["fallback_rate"]), t["server_errors"], t["latency_ms"]["p50"], t["latency_ms"]["p95"], t["latency_ms"]["p99"], t["latency_ms"]["max"],
                     *(t["validation"][v] for v in ("passed", "failed", "skipped", "error", "not_reported"))], 1) for t in report["tasks"]]
    reasons = [_cells([r["task"], r["reason"], r["count"]], 2) for r in report["failure_reasons"]]
    fallbacks = [_cells([r["task"], r["reason"] or "(none given)", r["count"]], 2) for r in report["fallback_reasons"]]
    return TEMPLATE.substitute(scope=html.escape(scope), week_start=report["week_start"], week_end=report["week_end"],
                               generated=(generated or dt.datetime.now(dt.timezone.utc)).strftime("%Y-%m-%d %H:%M UTC"), calls=report["calls"], people=report["people"],
                               organisations=report["organisations"], fallbacks=report["fallbacks"], fallback_rate=_rate(report["fallback_rate"]),
                               task_rows=_rows(tasks, 13), reason_rows=_rows(reasons, 3), fallback_rows=_rows(fallbacks, 3))


def _page(text: str) -> Response:
    return Response(content=text, media_type="text/html; charset=utf-8", headers=HTML_HEADERS)


@router.get("/organisations/{organisation_id}/ai-audit/weekly", response_model=WeeklyAudit)
def organisation_weekly(ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), day: dt.date = Depends(_day)) -> WeeklyAudit:
    """The organisation's AI calls in one week: fallback rate, validation results and failure reasons, latency percentiles per task."""
    return WeeklyAudit(**repository.weekly(db, ctx.organisation.id, day))


@router.get("/organisations/{organisation_id}/ai-audit/weekly.html")
def organisation_weekly_html(ctx: OrgContext = Depends(require_supervisor), db: DbSession = Depends(db_session), day: dt.date = Depends(_day)) -> Response:
    return _page(render_weekly(repository.weekly(db, ctx.organisation.id, day), ctx.organisation.name))


@router.get("/ai-audit/weekly", response_model=WeeklyAudit)
def platform_weekly(user: CurrentUser = Depends(require_platform_admin), db: DbSession = Depends(db_session), day: dt.date = Depends(_day)) -> WeeklyAudit:
    """Every organisation's AI calls in one week (the platform administrator)."""
    return WeeklyAudit(**repository.weekly(db, None, day))


@router.get("/ai-audit/weekly.html")
def platform_weekly_html(user: CurrentUser = Depends(require_platform_admin), db: DbSession = Depends(db_session), day: dt.date = Depends(_day)) -> Response:
    return _page(render_weekly(repository.weekly(db, None, day), "the whole platform"))


@router.get("/organisations/{organisation_id}/ai-calls", response_model=AiCallPage)
def list_calls(ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session), trainee_id: uuid.UUID | None = Query(None), session_id: uuid.UUID | None = Query(None),
               since: dt.datetime | None = Query(None), until: dt.datetime | None = Query(None), limit: int = Query(50, ge=1, le=200), offset: int = Query(0, ge=0)) -> AiCallPage:
    """What the AI did for the organisation's trainees (newest first). A trainee sees only the calls made for them."""
    supervisor = accounts.ROLE_RANK[ctx.role] >= accounts.ROLE_RANK["supervisor"]
    if not supervisor:
        trainee_id = ctx.user.profile.user_id
    elif trainee_id is not None and trainee_id != ctx.user.profile.user_id:
        privacy_repository.log_access(db, organisation_id=ctx.organisation.id, viewer=ctx.user.profile, viewer_role=ctx.role, subject_id=trainee_id, action="view_ai_calls")
        db.commit()
    total, items = repository.list_calls(db, ctx.organisation.id, trainee_id, session_id, since, until, limit, offset)
    return AiCallPage(total=total, limit=limit, offset=offset, items=items)
