"""Database access for sessions and generated scenario packages (ORM only)."""
from __future__ import annotations

import gzip
import uuid

from sqlalchemy import select
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.orm import Session as DbSession

from server.models.sessions import ScenarioPackage, Session
from server.sessionfile import ParsedSession


def get_session_row(db: DbSession, session_id: uuid.UUID) -> Session | None:
    return db.get(Session, session_id)


def insert_session(db: DbSession, parsed: ParsedSession, organisation_id: uuid.UUID, user_id: uuid.UUID) -> bool:
    """Stores a session. Returns False when a session with this id already exists (the primary key decides, so two uploads racing cannot both win)."""
    h = parsed.header
    values = dict(
        id=h.session_id, organisation_id=organisation_id, user_id=user_id, case_id=h.case, mode=h.mode, seed=h.seed, content_version=h.content_version, engine_version=h.engine_version,
        scenario_package_hash=h.scenario_package_hash or "", log_hash=h.log_hash, log_entries=len(parsed.entries), ended_step=parsed.entries[-1].step,
        log_gz=gzip.compress(parsed.log_text.encode("utf-8"), compresslevel=6, mtime=0),
    )
    # RETURNING tells us whether this statement inserted the row (an ORM-level insert does not report a row count)
    inserted = db.execute(insert(Session).values(**values).on_conflict_do_nothing(index_elements=["id"]).returning(Session.id)).scalar_one_or_none()
    return inserted is not None


def package_exists(db: DbSession, package_hash: str) -> bool:
    return db.scalar(select(ScenarioPackage.hash).where(ScenarioPackage.hash == package_hash)) is not None


def store_package(db: DbSession, package_hash: str, raw: bytes) -> bool:
    """Stores a package once. Returns False when it was already there."""
    inserted = db.execute(insert(ScenarioPackage).values(hash=package_hash, package_gz=gzip.compress(raw, compresslevel=6, mtime=0), size_bytes=len(raw))
                          .on_conflict_do_nothing(index_elements=["hash"]).returning(ScenarioPackage.hash)).scalar_one_or_none()
    return inserted is not None
