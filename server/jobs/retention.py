"""The scheduled clean-up (p1-backend-s12): removes what has passed its retention period. Run it daily: `python -m server.jobs.retention`, or call `POST /internal/retention` with the internal key (p1-backend-s13 schedules it).

What goes: the stored log and the report page of sessions older than their organisation's period (the results, score items and behaviours stay, so totals do not change), pending sessions that were purged
before they could be verified (rejected), access-log rows older than the platform's period, and the email address of invitations nobody accepted a month after they expired.
"""
from __future__ import annotations

import json
import sys

from sqlalchemy.orm import Session as DbSession

from server.config import Settings, get_settings
from server.database import get_engine
from server.repositories import privacy


def run(db: DbSession, settings: Settings | None = None, now=None) -> dict:
    settings = settings or get_settings()
    result = {
        "sessions_purged": privacy.purge_sessions(db, settings.retention_session_months, now),
        "pending_rejected": 0,
        "access_log_deleted": privacy.purge_access_log(db, settings.retention_access_log_months, now),
        "invitation_emails_cleared": privacy.clear_stale_invitations(db, now=now),
    }
    result["pending_rejected"] = privacy.reject_purged_pending(db)
    db.commit()
    return result


def main() -> int:
    engine = get_engine()
    if engine is None:
        print("no database configured", file=sys.stderr)
        return 1
    with DbSession(engine) as db:
        print(json.dumps(run(db)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
