"""Verifying pending sessions (p1-backend-s09). The queue is the `status` column, not memory, because the free host sleeps and restarts: anything pending is picked up by the next sweep.

A session is claimed with FOR UPDATE SKIP LOCKED, so two workers (an upload's background task, the sweeper, another container) never verify the same session, and a worker that dies simply releases its lock.
The outcome is one of: verified (the score rows, the report and the result are stored); rejected (the log is not what its header says, or it is not allowed for its mode: a reason is kept);
unverifiable (this service cannot judge it: another engine or content version, a generated scenario whose loader is missing; stored, kept out of every total, shown to its owner with the reason);
or, after three failed attempts to get any verdict, unverifiable with VERIFIER_ERROR.
"""
from __future__ import annotations

import datetime as dt
import gzip
import json
import logging
import uuid
from functools import lru_cache

from sqlalchemy import delete, select
from sqlalchemy.orm import Session as DbSession

from server.config import Settings, get_settings
from server.database import get_engine
from server.models.sessions import ScenarioPackage, Session, SessionBehaviour, SessionScoreItem
from server.verification.runner import EngineRunner, RunnerError

log = logging.getLogger("procsense.verification")

MAX_ATTEMPTS = 3
# reasons the engine gives that mean "this service cannot judge it", not "this session is wrong"
UNVERIFIABLE = {"ENGINE_VERSION_UNSUPPORTED", "CONTENT_VERSION_MISMATCH", "CONTENT_VERSION_UNKNOWN", "PACKAGE_MISSING", "PACKAGE_LOADER_MISSING", "CONTENT_HASH_MISMATCH", "VERIFIER_ERROR"}


@lru_cache
def get_runner() -> EngineRunner | None:
    s = get_settings()
    command = s.engine_command()
    return EngineRunner(command, s.engine_content_root, s.verify_timeout_seconds) if command else None


def _claim(db: DbSession, session_id: uuid.UUID | None) -> Session | None:
    query = select(Session).where(Session.status == "pending")
    query = query.where(Session.id == session_id) if session_id else query.order_by(Session.received_at)
    return db.scalars(query.limit(1).with_for_update(skip_locked=True)).first()


def _finish(row: Session, status: str, reason: str = "") -> None:
    row.status, row.reject_reason = status, reason[:40]
    row.verified_at = dt.datetime.now(dt.timezone.utc) if status == "verified" else None


def verify_one(db: DbSession, runner: EngineRunner, settings: Settings, session_id: uuid.UUID | None = None) -> bool:
    """Claims one pending session (the oldest, or the one named) and judges it. Returns False when there was nothing to claim. Commits."""
    row = _claim(db, session_id)
    if row is None:
        return False
    try:
        _judge(db, row, runner, settings)
    except RunnerError as e:
        row.verify_attempts += 1
        log.warning("verification of %s gave no verdict (%s); attempt %s of %s", row.id, e, row.verify_attempts, MAX_ATTEMPTS)
        if row.verify_attempts >= MAX_ATTEMPTS:
            _finish(row, "unverifiable", "VERIFIER_ERROR")
    db.commit()
    return True


def _judge(db: DbSession, row: Session, runner: EngineRunner, settings: Settings) -> None:
    if settings.supported_engine_versions and row.engine_version not in settings.supported_engine_versions:
        return _finish(row, "unverifiable", "ENGINE_VERSION_UNSUPPORTED")
    content_dir = runner.content_dir_for(row.content_version)
    if content_dir is None:
        return _finish(row, "unverifiable", "CONTENT_VERSION_UNKNOWN")
    package = None
    if row.scenario_package_hash:
        stored = db.get(ScenarioPackage, row.scenario_package_hash)
        if stored is None:
            return _finish(row, "unverifiable", "PACKAGE_MISSING")
        package = gzip.decompress(stored.package_gz)
    log_text = gzip.decompress(row.log_gz).decode("utf-8")
    verdict = runner.verify(row.header_line, log_text, content_dir, package)
    if not verdict.ok:
        return _finish(row, "unverifiable" if verdict.reason in UNVERIFIABLE else "rejected", verdict.reason or "REJECTED")
    _store_result(db, row, verdict.data, log_text, verdict.report_html)
    _finish(row, "verified")


def _store_result(db: DbSession, row: Session, data: dict, log_text: str, html: bytes | None) -> None:
    facts = data["facts"]
    steps = {}   # the sim step of each logged action, so a score item can say when it happened
    for line in log_text.split("\n"):
        if line:
            entry = json.loads(line)
            steps[entry["seq"]] = entry["step"]
    db.execute(delete(SessionScoreItem).where(SessionScoreItem.session_id == row.id))
    db.execute(delete(SessionBehaviour).where(SessionBehaviour.session_id == row.id))
    for ordinal, item in enumerate(facts["items"]):
        evidence = [int(e) for e in item["evidence"]]
        db.add(SessionScoreItem(session_id=row.id, ordinal=ordinal, item_id=item["id"], behaviour=item.get("behaviour", ""), severity=item["severity"], status=item["status"],
                                points=item["points"], step=steps.get(evidence[0], 0) if evidence else 0, evidence=evidence))
    for c in facts["competencies"]:
        if c["items"] > 0:
            db.add(SessionBehaviour(session_id=row.id, behaviour=c["behaviour"], credits=c["credits"], penalties=c["penalties"], net=c["net"]))
    row.end_state, row.result, row.process_score = facts["end_state"], facts["result"], data["process_total"]
    row.report_json = facts
    row.report_html_gz = gzip.compress(html, compresslevel=6, mtime=0) if html else None


def verify_pending(limit: int, runner: EngineRunner | None = None, settings: Settings | None = None) -> dict:
    """Judges up to `limit` pending sessions, one at a time, each in its own short transaction. Returns what was done and how many are still waiting."""
    settings = settings or get_settings()
    runner = runner or get_runner()
    engine = get_engine()
    if runner is None or engine is None:
        return {"processed": 0, "configured": False}
    processed = 0
    for _ in range(limit):
        with DbSession(engine) as db:
            if not verify_one(db, runner, settings):
                break
        processed += 1
    return {"processed": processed, "configured": True}


def verify_in_background(session_id: uuid.UUID) -> None:
    """Run after an upload has been stored (best effort: if the host stops first, the sweeper finds the session pending)."""
    runner, engine = get_runner(), get_engine()
    if runner is None or engine is None:
        return
    try:
        with DbSession(engine) as db:
            verify_one(db, runner, get_settings(), session_id)
    except Exception:   # a failure here must never reach the person who uploaded; the session stays pending for the sweeper
        log.exception("background verification of %s failed", session_id)
