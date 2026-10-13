"""T-AC5: a public database role reads nothing from the account tables (the second lock; the Data API is also switched off).

A role stands in for Supabase's `anon` and `authenticated`: it is given SELECT on every account table (which Supabase does by default for new tables), and still sees no rows.
"""
import uuid

import pytest
from sqlalchemy import text
from sqlalchemy.orm import Session

from server.models.accounts import Base
from server.repositories import accounts

pytestmark = pytest.mark.db

ACCOUNT_TABLES = ["organisation", "profile", "membership", "cohort", "cohort_member", "invitation", "consent_record"]


@pytest.fixture
def public_role(migrated):
    with migrated.begin() as conn:
        conn.execute(text("drop role if exists procsense_public"))
        conn.execute(text("create role procsense_public nologin"))
        conn.execute(text("grant usage on schema public to procsense_public"))
        for table in ACCOUNT_TABLES:
            conn.execute(text(f"grant select on {table} to procsense_public"))
    yield "procsense_public"
    with migrated.begin() as conn:
        for table in ACCOUNT_TABLES:
            conn.execute(text(f"revoke all on {table} from procsense_public"))
        conn.execute(text("revoke all on schema public from procsense_public"))
        conn.execute(text("drop role procsense_public"))


def _fill(engine):
    with Session(engine) as s:
        org = accounts.create_organisation(s, "Alpha", "alpha-org")
        uid = uuid.uuid4()
        accounts.ensure_profile(s, uid)
        accounts.add_membership(s, uid, org.id, "trainee")
        cohort = accounts.create_cohort(s, org.id, "Group")
        accounts.set_cohort_member(s, cohort.id, uid, "trainee")
        accounts.create_invitation(s, org.id, "a@example.com", "trainee", None, None, 14)
        s.commit()


def test_T_AC5_the_service_connection_sees_the_rows_and_a_public_role_sees_none(migrated, public_role):
    _fill(migrated)
    with migrated.connect() as conn:
        for table in ACCOUNT_TABLES[:-1]:
            assert conn.execute(text(f"select count(*) from {table}")).scalar_one() >= 1, table   # the owner is not blocked
    with migrated.connect() as conn:
        conn.execute(text(f"set role {public_role}"))
        for table in ACCOUNT_TABLES:
            assert conn.execute(text(f"select count(*) from {table}")).scalar_one() == 0, f"{table} leaked rows to a public role"
        conn.execute(text("reset role"))


def test_the_check_means_something_without_the_lock_the_same_role_would_see_the_rows(migrated, public_role):
    _fill(migrated)
    with migrated.begin() as conn:
        conn.execute(text("alter table profile disable row level security"))
    with migrated.connect() as conn:
        conn.execute(text(f"set role {public_role}"))
        assert conn.execute(text("select count(*) from profile")).scalar_one() >= 1
        conn.execute(text("reset role"))


def test_every_account_table_has_the_lock_switched_on(migrated):
    with migrated.connect() as conn:
        rows = conn.execute(text("select relname, relrowsecurity from pg_class where relname = any(:names) and relkind = 'r'"), {"names": ACCOUNT_TABLES}).all()
    assert {r[0] for r in rows} == set(ACCOUNT_TABLES)
    assert all(r[1] for r in rows), [r[0] for r in rows if not r[1]]


def test_the_models_know_the_same_tables_as_the_lock_list():
    assert set(ACCOUNT_TABLES) <= set(Base.metadata.tables)


SESSION_TABLES = ["session", "session_score_item", "session_behaviour", "session_evidence", "learner_profile", "scenario_package"]


def test_every_session_table_has_the_lock_switched_on_and_a_public_role_would_see_nothing(migrated, public_role):
    with migrated.begin() as conn:
        for table in SESSION_TABLES:
            conn.execute(text(f"grant select on {table} to {public_role}"))
    with migrated.connect() as conn:
        rows = conn.execute(text("select relname, relrowsecurity from pg_class where relname = any(:names) and relkind = 'r'"), {"names": SESSION_TABLES}).all()
        assert {r[0] for r in rows} == set(SESSION_TABLES) and all(r[1] for r in rows)
        conn.execute(text(f"set role {public_role}"))
        for table in SESSION_TABLES:
            assert conn.execute(text(f"select count(*) from {table}")).scalar_one() == 0
        conn.execute(text("reset role"))
    with migrated.begin() as conn:
        for table in SESSION_TABLES:
            conn.execute(text(f"revoke all on {table} from {public_role}"))
