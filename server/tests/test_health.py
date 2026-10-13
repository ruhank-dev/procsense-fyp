"""The health endpoints."""
import time

import pytest
from fastapi.testclient import TestClient

from server import config, database
from server.main import create_app


def test_health_answers_without_a_database(app_without_database):
    r = app_without_database.get("/health")
    assert r.status_code == 200
    body = r.json()
    assert body["status"] == "ok" and body["service"] == "procsense-server"


def test_health_db_says_when_no_database_is_configured(app_without_database):
    r = app_without_database.get("/health/db")
    assert r.status_code == 503
    assert r.json() == {"status": "unavailable", "reason": "database_not_configured", "latency_ms": 0.0, "server_version": "", "supported_engine_versions": [], "supported_content_versions": []}


@pytest.mark.db
def test_health_db_with_a_real_database(app_with_database):
    r = app_with_database.get("/health/db")
    assert r.status_code == 200
    body = r.json()
    assert body["status"] == "ok"
    assert body["server_version"].startswith("17")   # the project and CI run PostgreSQL 17
    assert body["latency_ms"] >= 0


def test_an_unreachable_database_is_a_503_that_does_not_leak_the_address(monkeypatch):
    canary = "leak" + "check" + "pw"   # a stand-in password that must never appear in a reply
    monkeypatch.setenv("DATABASE_URL", f"postgresql://postgres:{canary}@127.0.0.1:1/none")
    monkeypatch.setenv("DB_CONNECT_TIMEOUT", "1")
    config.get_settings.cache_clear()
    database.get_engine.cache_clear()
    started = time.perf_counter()
    try:
        with TestClient(create_app()) as client:
            r = client.get("/health/db")
    finally:
        config.get_settings.cache_clear()
        database.get_engine.cache_clear()
    assert r.status_code == 503
    assert r.json()["reason"] == "database_unreachable"
    assert canary not in r.text and "127.0.0.1" not in r.text
    assert time.perf_counter() - started < 15   # fails fast: the connect timeout applies


def test_the_service_lists_its_routes_in_the_api_documentation(app_without_database):
    r = app_without_database.get("/openapi.json")
    assert r.status_code == 200
    assert {"/health", "/health/db"} <= set(r.json()["paths"])
