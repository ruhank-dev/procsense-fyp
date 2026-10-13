"""Database access for the health check (ORM and core expressions only, no raw SQL strings)."""
from __future__ import annotations

import time
from dataclasses import dataclass

from sqlalchemy import func, select
from sqlalchemy.orm import Session


@dataclass(frozen=True)
class DbPing:
    latency_ms: float
    server_version: str


def ping(session: Session) -> DbPing:
    """One round trip: asks the database for its version and times it."""
    started = time.perf_counter()
    version = session.execute(select(func.current_setting("server_version"))).scalar_one()
    return DbPing(round((time.perf_counter() - started) * 1000, 2), str(version))
