"""A local copy of the service with a pretend sign-in server and sample data, for trying the web pages in a real browser (p1-backend-s11). Never used in production: it signs people in with a made-up password.

    TEST_DATABASE_URL=postgresql://postgres:postgres@localhost:54329/procsense_test uv run --project server python -m server.tests.browser_stack --port 8765

It empties that database, applies the migrations, adds two organisations with people (all passwords are `test-password-1`), some verified sessions and a few hostile display names, and serves the app on http://127.0.0.1:<port>/.
People: trainee@alpha.test, colleague@alpha.test, supervisor@alpha.test, admin@alpha.test, supervisor@beta.test.
"""
from __future__ import annotations

import argparse
import datetime as dt
import gzip
import os
import uuid
from pathlib import Path

import uvicorn
from alembic import command
from alembic.config import Config
from fastapi import Request
from fastapi.responses import JSONResponse
from sqlalchemy import text
from sqlalchemy.orm import Session as DbSession

PASSWORD = "test-password-1"
HOSTILE = '<img src=x onerror="alert(1)"> & "quotes"'


def build(port: int, database_url: str):
    base = f"http://127.0.0.1:{port}/fake"
    os.environ.update(DATABASE_URL=database_url, SUPABASE_URL=base, SUPABASE_PUBLISHABLE_KEY="sb_publishable_browser_stack", SUPABASE_SECRET_KEY="browser-stack-not-a-key", APP_ENV="test")
    from server import config, database
    from server.auth import dependencies
    from server.auth.tokens import TokenVerifier
    from server.main import create_app
    from server.models.sessions import Session, SessionBehaviour, SessionScoreItem
    from server.repositories import accounts
    from server.tests.support import FakeAdmin, SigningKey

    config.get_settings.cache_clear()
    database.get_engine.cache_clear()
    engine = database.get_engine()
    with engine.begin() as conn:
        conn.execute(text("drop schema public cascade"))
        conn.execute(text("create schema public"))
    cfg = Config(str(Path(__file__).resolve().parents[1] / "alembic.ini"))
    cfg.set_main_option("sqlalchemy.url", database_url.replace("%", "%%"))
    command.upgrade(cfg, "head")

    people: dict[str, uuid.UUID] = {}
    with DbSession(engine) as s:
        a, b = accounts.create_organisation(s, "Alpha Hospital", "alpha"), accounts.create_organisation(s, "Beta Clinic", "beta")
        for email, org, role, name in (("trainee@alpha.test", a, "trainee", "Jane Doe"), ("colleague@alpha.test", a, "trainee", HOSTILE), ("supervisor@alpha.test", a, "supervisor", "Sam Supervisor"),
                                       ("admin@alpha.test", a, "org_admin", "Ada Admin"), ("supervisor@beta.test", b, "supervisor", "Bea Beta")):
            uid = uuid.uuid4()
            people[email] = uid
            accounts.ensure_profile(s, uid).display_name = name
            accounts.add_membership(s, uid, org.id, role)
        cohort = accounts.create_cohort(s, a.id, "Cohort 1")
        accounts.set_cohort_member(s, cohort.id, people["trainee@alpha.test"], "trainee")
        now = dt.datetime.now(dt.timezone.utc)
        for i, (who, org) in enumerate([("trainee@alpha.test", a)] * 6 + [("colleague@alpha.test", a)] * 3 + [("supervisor@beta.test", b)] * 2):
            sid = uuid.uuid4()
            ok = i % 3 != 0
            facts = {"schema": "debrief_facts.v1", "case": "VAR-S1-A", "mode": "practice", "end_state": "DISCHARGE_READY", "result": "SUCCESS" if ok else "FAIL", "items": [], "competencies": []}
            s.add(Session(id=sid, organisation_id=org.id, user_id=people[who], case_id="VAR-S1-A", mode="practice" if i % 2 else "assessment", seed="1", content_version="abc123abc123-practice", engine_version="0.0.1",
                          log_hash="a" * 64, log_entries=3, ended_step=10, log_gz=gzip.compress(b"x"), status="verified", received_at=now - dt.timedelta(days=i * 4), verified_at=now,
                          end_state="DISCHARGE_READY", result="SUCCESS" if ok else "FAIL", process_score=55 + i * 3, report_json=facts,
                          report_html_gz=gzip.compress(f"<html><body><h1>Report {HOSTILE}</h1><p>Written by the engine.</p><script>document.title='ran'</script></body></html>".encode())))
            s.flush()
            s.add(SessionBehaviour(session_id=sid, behaviour="b01", credits=4, penalties=i % 3, net=4 - i % 3))
            s.add(SessionBehaviour(session_id=sid, behaviour="b07", credits=1, penalties=2, net=-1))
            if not ok:
                s.add(SessionScoreItem(session_id=sid, ordinal=0, item_id="critical.wrong_patient", behaviour="b01", severity="critical", status="critical", points=-5, step=1, evidence=[1]))
        s.commit()

    key, admin = SigningKey(), FakeAdmin()
    issuer = base + "/auth/v1"
    verifier = TokenVerifier(issuer, "authenticated", issuer + "/.well-known/jwks.json", fetcher=lambda url: key.jwks())
    app = create_app()
    app.dependency_overrides[dependencies.get_verifier] = lambda: verifier
    app.dependency_overrides[dependencies.get_admin] = lambda: admin
    refresh_tokens: dict[str, str] = {}

    def issue(email: str) -> dict:
        refresh = uuid.uuid4().hex
        refresh_tokens[refresh] = email
        return {"access_token": key.token(people[email], email, issuer=issuer, expires_in=3600), "refresh_token": refresh, "expires_in": 3600, "token_type": "bearer"}

    @app.post("/fake/auth/v1/token")
    async def fake_token(request: Request, grant_type: str = "password"):
        body = await request.json()
        if grant_type == "refresh_token":
            email = refresh_tokens.pop(body.get("refresh_token", ""), None)
            return JSONResponse(issue(email)) if email else JSONResponse({"error": "invalid_grant"}, status_code=400)
        if body.get("email") in people and body.get("password") == PASSWORD:
            return JSONResponse(issue(body["email"]))
        return JSONResponse({"error": "invalid_grant"}, status_code=400)

    @app.post("/fake/auth/v1/recover")
    async def fake_recover():
        return JSONResponse({})

    @app.put("/fake/auth/v1/user")
    async def fake_user():
        return JSONResponse({})

    return app


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    url = os.environ.get("TEST_DATABASE_URL", "")
    if not url:
        raise SystemExit("set TEST_DATABASE_URL (see task server-db-up)")
    from server import config
    uvicorn.run(build(args.port, config._psycopg_url(url, "TEST_DATABASE_URL")), host="127.0.0.1", port=args.port, log_level="warning")
