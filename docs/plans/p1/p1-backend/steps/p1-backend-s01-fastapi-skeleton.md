# p1-backend-s01 — FastAPI service skeleton

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
`server/` FastAPI app following INSTRUCTIONS §4b (routers thin, repositories ORM-only, schemas in `schemas/`), §5 (Pydantic settings, no credentials in code), §5b (ORM only), uv + Taskfile: `main.py` (create app, middleware, include routers only), `config.py` (Pydantic Settings from `.env`), `database.py` (SQLAlchemy 2 engine/session, SQLite in WAL mode), `routers/`, `repositories/`, `schemas/`, Alembic migrations, `/health` (model tag, prompt-pack version, content version), two-stage Dockerfile, `.env.example`.

## Why
A clean, testable base for session sync, reports and the AI gateway.

## How to implement
1. `uv init server` → `pyproject.toml` + `uv.lock`; deps fastapi, uvicorn, sqlalchemy, alembic, pydantic-settings, jinja2, httpx (tests).
2. Health router + schema; tests with FastAPI TestClient.
3. Taskfile: `server-dev`, `server-test`, `server-migrate`.
4. Dockerfile: builder stage (uv sync) → slim runtime stage.
5. Design pattern: repository pattern (tell the user why: routers stay thin, DB access testable/swappable).

## Verification
`task server-dev` → open http://localhost:8000/docs → `/health` responds; `task server-test` green.

## Files changed
`server/*`, `Taskfile.yml`, `.env.example`, `server/Dockerfile`.

## Dependencies
p0-foundation-s04.

## Common issues
SQLite 'database is locked' → WAL mode.

## Sources
Implementation Guide B-M16; INSTRUCTIONS §4b, §5, §5b, §6, §7.
