"""The generation history (DT-201): written when a session has been verified, read when the next objectives are chosen, exported with the person and deleted with them. ORM only.

The entry type is `server/generation/history.py` `HistoryEntry`, so `objectives.derive`, `novelty.check` and the fingerprint exclusion read this table through the same functions as the file store.
"""
from __future__ import annotations

import datetime as dt
import uuid

from sqlalchemy import delete, select
from sqlalchemy.dialects.postgresql import insert
from sqlalchemy.orm import Session as DbSession

from server.generation import history as H
from server.models.generation_history import GenerationHistory


def _utc(text: str) -> dt.datetime:
    return dt.datetime.strptime(text, "%Y-%m-%dT%H:%M:%SZ").replace(tzinfo=dt.timezone.utc)


def record(db: DbSession, organisation_id: uuid.UUID, user_id: uuid.UUID, entry: H.HistoryEntry) -> None:
    """One row per session: a session recorded again (verified again) replaces its row."""
    values = dict(session_id=uuid.UUID(entry.session_id), organisation_id=organisation_id, user_id=user_id, case_id=entry.case, story=entry.story, tier=entry.tier, behaviours=entry.behaviours,
                  ended_at=_utc(entry.ended_at), package_id=entry.package_id, package_sha256=entry.package_sha256, fingerprint=entry.fingerprint, levels=entry.levels)
    stmt = insert(GenerationHistory).values(**values)
    db.execute(stmt.on_conflict_do_update(index_elements=["session_id"], set_={k: v for k, v in values.items() if k not in ("session_id", "organisation_id", "user_id")}))


def entries(db: DbSession, user_id: uuid.UUID, pseudonym: str) -> list[H.HistoryEntry]:
    """The person's entries, oldest first, as the generator's own type."""
    rows = db.scalars(select(GenerationHistory).where(GenerationHistory.user_id == user_id).order_by(GenerationHistory.ended_at, GenerationHistory.session_id))
    return [H.HistoryEntry(pseudonym, str(r.session_id), r.case_id, r.story, r.tier, {k: list(v) for k, v in r.behaviours.items()}, r.ended_at.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"),
                           r.package_id, r.package_sha256, r.fingerprint, {k: list(v) for k, v in r.levels.items()}) for r in rows]


def exported(db: DbSession, user_id: uuid.UUID) -> list[dict]:
    """For the person's data export: the entries with no pseudonym added."""
    return [{k: v for k, v in e.as_dict().items() if k != "trainee"} for e in entries(db, user_id, "export")]


def delete_of(db: DbSession, user_id: uuid.UUID) -> int:
    return len(db.execute(delete(GenerationHistory).where(GenerationHistory.user_id == user_id).returning(GenerationHistory.session_id)).all())
