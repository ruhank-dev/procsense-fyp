"""The web pages (p1-backend-s11): they are served, they carry the security headers, and the page code never does the things that would defeat them (no markup built from text, no token in storage,
nothing loaded from another site). The behaviour in a browser is covered by test_browser_smoke.py."""
from __future__ import annotations

import re
from pathlib import Path

import pytest

STATIC = Path(__file__).resolve().parents[1] / "static"
SOURCES = sorted(p for p in STATIC.rglob("*") if p.suffix in (".js", ".html", ".css"))


@pytest.fixture
def client(app_without_database):
    return app_without_database


@pytest.mark.parametrize("path", ["/", "/device", "/reset"])
def test_the_page_is_served_with_the_security_headers(client, path):
    r = client.get(path)
    assert r.status_code == 200 and r.headers["content-type"].startswith("text/html")
    csp = r.headers["content-security-policy"]
    assert "script-src 'self'" in csp and "default-src 'none'" in csp and "frame-ancestors 'none'" in csp and "object-src 'none'" in csp and "base-uri 'none'" in csp
    script_policy = re.search(r"script-src ([^;]*)", csp).group(1)
    assert "unsafe-inline" not in script_policy and "unsafe-eval" not in script_policy       # no inline script, no eval
    assert r.headers["x-content-type-options"] == "nosniff" and r.headers["x-frame-options"] == "DENY" and r.headers["referrer-policy"] == "no-referrer"
    assert "<script type=\"module\" src=\"/static/js/app.js\">" in r.text and "onclick" not in r.text   # the one script comes from this service


def test_every_response_gets_the_headers_not_only_the_pages(client):
    for path in ("/health", "/no/such/route", "/static/js/app.js", "/config.json"):
        r = client.get(path)
        assert r.headers["x-content-type-options"] == "nosniff", path
        assert "frame-ancestors 'none'" in r.headers["content-security-policy"], path
    assert "strict-transport-security" not in client.get("/").headers   # development: no HSTS pinned to localhost


def test_the_page_may_talk_to_the_account_system_and_nothing_else(monkeypatch, app_without_database):
    from server import config
    monkeypatch.setenv("SUPABASE_URL", "https://abcdef.supabase.co/")
    config.get_settings.cache_clear()
    csp = app_without_database.get("/").headers["content-security-policy"]
    assert "connect-src 'self' https://abcdef.supabase.co;" in csp
    config.get_settings.cache_clear()


def test_hsts_is_sent_in_production(monkeypatch, app_without_database):
    from server import config
    monkeypatch.setenv("APP_ENV", "prod")
    config.get_settings.cache_clear()
    assert "max-age=31536000" in app_without_database.get("/").headers["strict-transport-security"]
    config.get_settings.cache_clear()


def test_the_public_config_has_the_project_address_and_the_publishable_key_but_never_the_secret(monkeypatch, app_without_database):
    from server import config
    monkeypatch.setenv("SUPABASE_URL", "https://abcdef.supabase.co/")
    monkeypatch.setenv("SUPABASE_PUBLISHABLE_KEY", "sb_publishable_test_value")
    monkeypatch.setenv("SUPABASE_SECRET_KEY", "sb_secret_must_not_leak")
    config.get_settings.cache_clear()
    r = app_without_database.get("/config.json")
    assert r.json() == {"supabase_url": "https://abcdef.supabase.co", "supabase_publishable_key": "sb_publishable_test_value"}
    assert "must_not_leak" not in r.text
    config.get_settings.cache_clear()


def test_the_static_files_are_served_and_unknown_ones_are_not(client):
    for name in ("js/app.js", "js/api.js", "js/views.js", "js/dom.js", "js/charts.js", "css/app.css"):
        assert client.get("/static/" + name).status_code == 200, name
    assert client.get("/static/../config.py").status_code in (400, 404)
    assert client.get("/static/nothing.js").status_code == 404


# --- what the page code must never do ----------------------------------------------------------------------------------------------------------------------------------

FORBIDDEN = {
    "markup built from text": r"innerHTML|outerHTML|insertAdjacentHTML|document\.write|createContextualFragment|DOMParser",
    "code built from text": r"\beval\s*\(|new\s+Function|setTimeout\s*\(\s*['\"`]|setInterval\s*\(\s*['\"`]",
    "a token kept in storage": r"localStorage|sessionStorage|document\.cookie|indexedDB",
    "an inline handler or script": r"\son[a-z]+\s*=\s*[\"']|javascript:",
}


@pytest.mark.parametrize("what", sorted(FORBIDDEN))
def test_the_page_code_never_does(what):
    pattern = re.compile(FORBIDDEN[what])
    offenders = [f"{p.relative_to(STATIC)}:{n}" for p in SOURCES for n, line in enumerate(p.read_text(encoding="utf-8").splitlines(), 1) if pattern.search(line) and not line.lstrip().startswith("//")]
    assert offenders == [], f"{what}: {offenders}"


def test_nothing_is_loaded_from_another_site():
    allowed = {"http://www.w3.org/2000/svg"}
    found = [(p.name, u) for p in SOURCES for u in re.findall(r"https?://[^\s\"'`)]+", p.read_text(encoding="utf-8")) if u not in allowed]
    assert found == []
