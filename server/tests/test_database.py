"""The database layer against a real PostgreSQL (and the parts that need none)."""
from unittest.mock import patch

import pytest
from sqlalchemy import select, text
from sqlalchemy.orm import Session
from sqlalchemy.pool import NullPool

from server.database import NAMING_CONVENTION, Base, make_engine
from server.repositories import health as repository

URL = "postgresql+psycopg://u:p@127.0.0.1:1/none"


def test_prepared_statements_are_off_for_the_transaction_pooler():
    with patch("server.database.create_engine") as create:
        make_engine(URL)
    assert create.call_args.kwargs["connect_args"] == {"prepare_threshold": None, "connect_timeout": 10}
    assert create.call_args.kwargs["pool_pre_ping"] is True


def test_a_pool_size_of_zero_means_no_pool():
    assert isinstance(make_engine(URL, pool_size=0).pool, NullPool)


def test_constraint_names_follow_the_convention():
    assert Base.metadata.naming_convention == NAMING_CONVENTION


@pytest.mark.db
def test_a_session_round_trip(engine):
    with Session(engine) as session:
        assert session.execute(select(text("1"))).scalar_one() == 1


@pytest.mark.db
def test_the_repository_reports_the_server_version_and_a_latency(engine):
    with Session(engine) as session:
        ping = repository.ping(session)
    assert ping.server_version.startswith("17")
    assert ping.latency_ms >= 0


@pytest.mark.db
def test_a_connection_made_the_way_the_application_makes_it_can_run_the_same_query_twice(engine):
    # with prepared statements on, the second run of one query would be prepared; through the transaction pooler that fails. Off here and in production.
    with Session(engine) as session:
        for _ in range(6):
            assert repository.ping(session).server_version
