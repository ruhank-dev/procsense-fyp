"""Test setup. The tests that touch the database run against a real PostgreSQL, never SQLite (a different database hides differences until the first customer).

Where the database comes from: TEST_DATABASE_URL. In CI it is the PostgreSQL service container; on a laptop `task server-db-up` starts one in Docker and prints the address.
Without it the database tests are skipped with a reason, unless REQUIRE_DB=1 (CI sets it), when a missing database is an error.
"""
from __future__ import annotations

import os
import uuid
from dataclasses import dataclass

import pytest
from alembic import command
from alembic.config import Config
from fastapi.testclient import TestClient
from sqlalchemy import text

from pathlib import Path

from sqlalchemy.orm import Session

from server import config, database
from server.auth import dependencies
from server.database import make_engine
from server.main import create_app
from server.middleware import audit
from server.repositories import accounts
from server.repositories import privacy as privacy_repository
from server.routers import sessions as sessions_router
from server.verification import worker
from server.tests.support import SUPABASE_URL, FakeAdmin, SigningKey

# The tests never read a developer's server/.env: it holds real keys and the address of the live database, and any value in it (the internal key, the Supabase address) would change what a test sees.
config.Settings.model_config["env_file"] = None


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


def _alembic(url: str) -> Config:
    cfg = Config(str(Path(__file__).resolve().parents[1] / "alembic.ini"))
    cfg.set_main_option("sqlalchemy.url", url.replace("%", "%%"))
    return cfg


@pytest.fixture
def migrated(clean_database, db_url):
    """An empty database with every migration applied."""
    command.upgrade(_alembic(db_url), "head")
    return clean_database


@dataclass
class Api:
    client: TestClient
    key: SigningKey
    admin: FakeAdmin
    engine: object

    def headers(self, user_id: uuid.UUID, email: str = "", **token_options) -> dict:
        return {"Authorization": "Bearer " + self.key.token(user_id, email, **token_options)}

    def seed(self, fn):
        """Runs fn(session) against the database and commits (for setting up people and organisations without going through the API)."""
        with Session(self.engine) as session:
            result = fn(session)
            session.commit()
            return result

    def person(self, session: Session, *, platform_admin: bool = False, consent: bool = True) -> uuid.UUID:
        user_id = uuid.uuid4()
        profile = accounts.ensure_profile(session, user_id)
        profile.is_platform_admin = platform_admin
        if consent:
            privacy_repository.record_consent(session, user_id, "terms", True)
            privacy_repository.record_consent(session, user_id, "results", True)   # test people have agreed to the storing of their results (p1-backend-s12); tests of the refusal use `person(..., consent=False)`
        return user_id


@pytest.fixture
def api(monkeypatch, migrated, db_url):
    """The application on a migrated test database, with test tokens trusted and invitations captured instead of emailed."""
    monkeypatch.setenv("DATABASE_URL", db_url)
    monkeypatch.setenv("SUPABASE_URL", SUPABASE_URL)
    monkeypatch.setenv("AI_FEATURES_OVERRIDE", "all")           # the evaluation gate is tested on its own; the route tests need the features on
    monkeypatch.setenv("SUPABASE_SECRET_KEY", "test-only-not-a-real-key")
    for cache in (config.get_settings, database.get_engine, dependencies.get_verifier, dependencies.get_admin, sessions_router._limiter, worker.get_runner, audit.get_writer):
        cache.cache_clear()
    key, admin = SigningKey(), FakeAdmin()
    from server.auth.tokens import TokenVerifier
    verifier = TokenVerifier(config.get_settings().jwt_issuer, "authenticated", config.get_settings().jwks_url, fetcher=lambda url: key.jwks())
    app = create_app()
    app.dependency_overrides[dependencies.get_verifier] = lambda: verifier
    app.dependency_overrides[dependencies.get_admin] = lambda: admin
    with TestClient(app) as client:
        yield Api(client, key, admin, database.get_engine())
    for cache in (config.get_settings, database.get_engine, dependencies.get_verifier, dependencies.get_admin, sessions_router._limiter, worker.get_runner, audit.get_writer):
        cache.cache_clear()
