"""Test setup. The tests that touch the database run against a real PostgreSQL, never SQLite (a different database hides differences until the first customer).

Where the database comes from: TEST_DATABASE_URL. In CI it is the PostgreSQL service container; on a laptop `task server-db-up` starts one in Docker and prints the address.
Without it the database tests are skipped with a reason, unless REQUIRE_DB=1 (CI sets it), when a missing database is an error.
"""
from __future__ import annotations

import os

import pytest
from fastapi.testclient import TestClient
from sqlalchemy import text

from server import config, database
from server.database import make_engine
from server.main import create_app


def _test_url() -> str:
    url = os.environ.get("TEST_DATABASE_URL", "")
    if not url:
        if os.environ.get("REQUIRE_DB") == "1":
            pytest.fail("TEST_DATABASE_URL is not set but REQUIRE_DB=1: the database tests must run (start PostgreSQL, see `task server-db-up`)")
        pytest.skip("TEST_DATABASE_URL is not set: start PostgreSQL with `task server-db-up` to run the database tests")
    return config._psycopg_url(url, "TEST_DATABASE_URL")


@pytest.fixture
def db_url() -> str:
    return _test_url()


@pytest.fixture
def engine(db_url):
    eng = make_engine(db_url, pool_size=2)
    yield eng
    eng.dispose()


@pytest.fixture
def clean_database(engine):
    """An empty database before and after the test (the schema is dropped, so a migration test starts from nothing)."""
    def wipe():
        with engine.begin() as conn:
            conn.execute(text("drop schema public cascade"))
            conn.execute(text("create schema public"))
    wipe()
    yield engine
    wipe()


@pytest.fixture
def app_with_database(monkeypatch, db_url):
    """The application configured to use the test database."""
    monkeypatch.setenv("DATABASE_URL", db_url)
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()
    app = create_app()
    with TestClient(app) as client:
        yield client
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()


@pytest.fixture
def app_without_database(monkeypatch):
    monkeypatch.setenv("DATABASE_URL", "")
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()
    app = create_app()
    with TestClient(app) as client:
        yield client
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()
