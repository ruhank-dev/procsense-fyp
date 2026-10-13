"""Uploading a finished session and asking what became of it (p1-backend-s02).

The person and the organisation come from the login token and the membership, never from the file. A retried upload is harmless: the same session id with the same log is answered "already stored"; the same id with a different log
is refused and logged as a security event. Nothing counts until the session is verified (p1-backend-s09).
"""
from __future__ import annotations

import gzip
import logging
import uuid
import zlib
from functools import lru_cache

from fastapi import APIRouter, BackgroundTasks, Depends, HTTPException, Request, Response
from sqlalchemy.orm import Session as DbSession

from server.auth.dependencies import OrgContext, db_session, require_member
from server.config import Settings, get_settings
from server.ratelimit import RateLimiter
from server.repositories import sessions as repository
from server.schemas.sessions import SessionStatus, UploadReply
from server.sessionfile import SessionFileError, parse_session_file
from server.verification.worker import verify_in_background

router = APIRouter(tags=["sessions"])
security_log = logging.getLogger("procsense.security")


@lru_cache
def _limiter() -> RateLimiter:
    return RateLimiter(get_settings().upload_rate_per_hour)


def _status(row) -> SessionStatus:
    return SessionStatus(session_id=row.id, status=row.status, reject_reason=row.reject_reason, case_id=row.case_id, mode=row.mode, log_entries=row.log_entries,
                         received_at=row.received_at, end_state=row.end_state, result=row.result, process_score=float(row.process_score) if row.process_score is not None else None)


async def _read_body(request: Request, limit: int) -> bytes:
    """The request body, never more than `limit` bytes after unpacking (so a small gzip file cannot expand into a huge one)."""
    declared = request.headers.get("content-length")
    if declared and declared.isdigit() and int(declared) > limit * 2:
        raise HTTPException(status_code=413, detail="too_large")
    raw = b""
    async for chunk in request.stream():
        raw += chunk
        if len(raw) > limit * 2:
            raise HTTPException(status_code=413, detail="too_large")
    encoding = request.headers.get("content-encoding", "").lower()
    if encoding in ("", "identity"):
        data = raw
    elif encoding == "gzip":
        try:
            decompressor = zlib.decompressobj(16 + zlib.MAX_WBITS)
            data = decompressor.decompress(raw, limit + 1)
        except (zlib.error, EOFError, gzip.BadGzipFile):
            raise HTTPException(status_code=422, detail={"code": "BAD_GZIP"}) from None
    else:
        raise HTTPException(status_code=415, detail="unsupported_content_encoding")
    if len(data) > limit:
        raise HTTPException(status_code=413, detail="too_large")
    return data


@router.post("/organisations/{organisation_id}/sessions", response_model=UploadReply, status_code=202)
async def upload_session(request: Request, response: Response, background: BackgroundTasks, ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session),
                         settings: Settings = Depends(get_settings)) -> UploadReply:
    user_id = ctx.user.profile.user_id
    if not _limiter().allow(user_id):
        raise HTTPException(status_code=429, detail="too_many_uploads", headers={"Retry-After": "600"})
    data = await _read_body(request, settings.max_upload_bytes)
    try:
        parsed = parse_session_file(data.decode("utf-8"))
    except UnicodeDecodeError:
        raise HTTPException(status_code=422, detail={"code": "NOT_UTF8"}) from None
    except SessionFileError as e:
        raise HTTPException(status_code=422, detail={"code": e.code, "line": e.line, "detail": e.detail}) from None
    header = parsed.header
    if header.mode == "assessment" and header.scenario_package_hash:
        raise HTTPException(status_code=422, detail={"code": "ASSESSMENT_CANNOT_USE_GENERATED_SCENARIO", "line": 1, "detail": ""})

    existing = repository.get_session_row(db, header.session_id)
    if existing is not None:
        if existing.log_hash == header.log_hash and existing.user_id == user_id and existing.organisation_id == ctx.organisation.id:
            response.status_code = 200
            return UploadReply(outcome="already_stored", session=_status(existing), status_url=f"/organisations/{ctx.organisation.id}/sessions/{existing.id}")
        security_log.warning("session id reused with different content or by another person: session=%s user=%s organisation=%s", header.session_id, user_id, ctx.organisation.id)
        raise HTTPException(status_code=409, detail="session_id_conflict")

    if header.scenario_package_hash and not repository.package_exists(db, header.scenario_package_hash):
        raise HTTPException(status_code=409, detail={"code": "package_needed", "package_hash": header.scenario_package_hash})

    if not repository.insert_session(db, parsed, ctx.organisation.id, user_id):
        db.rollback()   # lost a race with an identical upload that arrived a moment earlier: answer as for a repeat
        existing = repository.get_session_row(db, header.session_id)
        if existing is None or existing.log_hash != header.log_hash or existing.user_id != user_id:
            raise HTTPException(status_code=409, detail="session_id_conflict")
        response.status_code = 200
        return UploadReply(outcome="already_stored", session=_status(existing), status_url=f"/organisations/{ctx.organisation.id}/sessions/{existing.id}")
    db.commit()
    if settings.verify_on_upload:
        background.add_task(verify_in_background, header.session_id)   # best effort; the sweeper finds anything left pending
    row = repository.get_session_row(db, header.session_id)
    return UploadReply(outcome="accepted", session=_status(row), status_url=f"/organisations/{ctx.organisation.id}/sessions/{row.id}")


@router.get("/organisations/{organisation_id}/sessions/{session_id}", response_model=SessionStatus)
def session_status(session_id: uuid.UUID, ctx: OrgContext = Depends(require_member), db: DbSession = Depends(db_session)) -> SessionStatus:
    """What became of a session. The person who uploaded it sees it; so do the organisation's supervisors and administrators; nobody else (a session of another organisation is simply not found)."""
    row = repository.get_session_row(db, session_id)
    if row is None or row.organisation_id != ctx.organisation.id:
        raise HTTPException(status_code=404, detail="not_found")
    from server.repositories.accounts import ROLE_RANK
    if row.user_id != ctx.user.profile.user_id and ROLE_RANK[ctx.role] < ROLE_RANK["supervisor"]:
        raise HTTPException(status_code=404, detail="not_found")
    return _status(row)
