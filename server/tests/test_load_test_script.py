"""The EXP-BE-3 load-test script (server/scripts/load_synthetic_sessions.py): it refuses a database that is not on this computer, measures what is loaded, and its clean-up removes the load test's
organisations, people and sessions and nothing else. The real-engine run itself is in the README (EXP-BE-3)."""
from __future__ import annotations

import datetime as dt
import gzip
import uuid

import pytest
from sqlalchemy import func, select
from sqlalchemy.orm import Session as DbSession

from server.models.accounts import Organisation, Profile
from server.models.sessions import Session, SessionBehaviour, SessionScoreItem
from server.repositories import accounts
from server.scripts import load_synthetic_sessions as load

pytestmark = pytest.mark.db
NOW = dt.datetime(2026, 10, 16, 12, 0, tzinfo=dt.timezone.utc)


def _template(s: DbSession, org_id: uuid.UUID, user_id: uuid.UUID) -> Session:
    row = Session(id=uuid.uuid4(), organisation_id=org_id, user_id=user_id, case_id="VAR-S1-A", mode="practice", seed="42", content_version="abc123abc123-practice", engine_version="0.0.1",
                  log_hash="0" * 64, header_line="{}", log_entries=1, ended_step=1, log_gz=gzip.compress(b"{}"), status="verified", received_at=NOW, verified_at=NOW, end_state="DISCHARGE_READY",
                  result="SUCCESS", process_score=70, report_json={"schema": "debrief_facts.v1"})
    s.add(row)
    s.flush()
    s.add(SessionScoreItem(session_id=row.id, ordinal=0, item_id="i1", behaviour="b01", severity="critical", status="critical", points=-5, step=1, evidence=[1]))
    s.add(SessionBehaviour(session_id=row.id, behaviour="b01", credits=3, penalties=1, net=2))
    s.flush()
    return row


def test_a_database_that_is_not_on_this_computer_is_refused():
    assert load._local("postgresql://postgres:postgres@localhost:54329/procsense_test")
    assert not load._local("postgresql://postgres.abc:pw@aws-0-eu-west-2.pooler.supabase.com:6543/postgres")
    with pytest.raises(SystemExit, match="not on this computer"):
        load.main(["--database-url", "postgresql://u:p@db.example.com:5432/postgres", "--measure-only"])


def test_it_measures_the_load_and_its_clean_up_leaves_everything_else(api):
    def build(s: DbSession):
        real = accounts.create_organisation(s, "Alpha", "alpha-org")
        person = api.person(s)
        accounts.add_membership(s, person, real.id, "trainee")
        template = _template(s, real.id, person)
        items = list(s.scalars(select(SessionScoreItem).where(SessionScoreItem.session_id == template.id)))
        behaviours = list(s.scalars(select(SessionBehaviour).where(SessionBehaviour.session_id == template.id)))
        orgs = load._organisations(s, 2, 3)
        for n in range(10):
            org_id, _, people = orgs[n % 2]
            load._copy(s, template, items, behaviours, org_id, people[n % 3], NOW - dt.timedelta(days=n))
        return real.id, person
    real, person = api.seed(build)

    with DbSession(api.engine) as s:
        m = load.measure(s)
        assert m["load_test_sessions"] == 10 and m["sessions_per_organisation"] == [5, 5]
        assert set(m["queries_slowest_ms_over_organisations"]) >= {"summary (organisation)", "list_sessions (first page of 50)", "export_rows (whole organisation)"}
        assert m["database_bytes"] > 0 and m["bytes_per_session"] > 0

        assert load.cleanup(s) == {"organisations": 2, "people": 8, "sessions": 10}
        assert s.scalar(select(func.count()).select_from(Organisation)) == 1
        assert s.get(Profile, person) is not None
        assert s.scalar(select(func.count()).select_from(Session).where(Session.organisation_id == real)) == 1
        assert s.scalar(select(func.count()).select_from(SessionScoreItem)) == 1
