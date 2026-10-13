# p1-backend-s01 — FastAPI service skeleton on PostgreSQL

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
`server/` FastAPI app following INSTRUCTIONS §4b (routers thin, repositories ORM-only, schemas in `schemas/`), §5 (Pydantic settings, no credentials in code), §5b (ORM only), uv and Taskfile. `main.py` (create app, middleware, include routers only), `config.py` (Pydantic Settings from the environment: the application database URL, the migration database URL, the Supabase project URL and keys, the allowed content and engine versions), `database.py` (SQLAlchemy 2 engine and session for PostgreSQL), `routers/`, `repositories/`, `schemas/`, Alembic migrations, `/health` (liveness) and `/health/db` (a database round trip, plus the engine version, content version and prompt-pack version), a two-stage Dockerfile, `.env.example`. The existing `server/validation/` (grounding validator) is untouched.

## Why
A clean, testable base for accounts, session sync, verification, reports and the AI gateway, on the database the product will really use. Testing against SQLite and deploying on PostgreSQL would hide differences until the first customer.

## How to implement
1. `uv init` for `server/` → `pyproject.toml` + `uv.lock`; dependencies: fastapi, uvicorn, sqlalchemy (2.x), psycopg (v3, binary), alembic, pydantic-settings, jinja2, httpx and pytest (tests).
2. `database.py`: two URLs. The application uses Supabase's connection pooler (IPv4, transaction mode), so prepared statements are switched off and the SQLAlchemy pool is a small fixed size (or `NullPool`) to respect the free plan's connection limit. Alembic uses the migration URL (session mode), never the transaction pooler. Record the chosen major PostgreSQL version of the Supabase project in the README and use the same image in CI.
3. `GET /health` (no database) and `GET /health/db` (`SELECT 1`, timing) with schemas; tests with FastAPI TestClient.
4. Alembic initialised with a naming convention for constraints and a first empty migration; `alembic upgrade head` and `alembic check` (no drift between the models and the migrations) run in tests.
5. Taskfile: `server-dev` (uvicorn against the dev Supabase project), `server-test`, `server-migrate`, `server-db-up` (optional local Postgres through Docker for people without access to the dev project).
6. Dockerfile: builder stage (uv sync) → slim runtime stage; runs as a non-root user; listens on the port the host provides.
7. CI: a job with a PostgreSQL service container (same major version as Supabase) runs `task server-test`. Tests never use SQLite.
8. Design pattern: repository pattern (tell the user why: routers stay thin, database access is testable and swappable).

## Verification
`task server-dev` → open http://localhost:8000/docs → `/health` and `/health/db` respond; `task server-test` green in CI with the Postgres service; `alembic check` clean.

## Files changed
`server/*`, `Taskfile.yml`, `server/.env.example`, `server/Dockerfile`, `.github/workflows/ci.yml`, `docs/CI.md`.

## Dependencies
p0-foundation-s04; p1-backend-s07 (the Supabase projects and secrets).

## Common issues
Prepared statements fail through the transaction pooler ("prepared statement does not exist") → switch them off for the application engine only. The free plan's direct connection is IPv6-only and many networks cannot reach it → use the pooler addresses. Too many connections on the free plan → keep the pool small and close sessions per request.

## Sources
Implementation Guide B-M16; INSTRUCTIONS §4b, §5, §5b, §6, §7; ADR-0009.

## Implementation notes (13 Oct 2026)
- **Service:** `server/` is a FastAPI package (`server.main:app`) with its own locked uv project (`server/pyproject.toml`, `server/uv.lock`, Python 3.12). `config.py` (settings from the environment and a git-ignored `server/.env`; the address must be PostgreSQL, a Supabase address is rewritten to the psycopg 3 driver, migrations refuse the transaction pooler port 6543, secrets never print), `database.py` (SQLAlchemy 2 engine: prepared statements off, small pool or none, a connect timeout), `routers/health.py`, `repositories/health.py`, `schemas/health.py`. `GET /health` does not touch the database; `GET /health/db` does one timed round trip and on failure answers 503 with a short reason and never the address or the error text.
- **A real bug found by the tests:** with the database unreachable a request hung for about two minutes on Windows (no connect timeout). The engine now has `DB_CONNECT_TIMEOUT` (default 10 s, limits 1 to 60); a paused free database fails fast.
- **Migrations:** Alembic in `server/migrations` with a constraint naming convention and an empty baseline `0001`; `env.py` reads `MIGRATION_DATABASE_URL`. Tests run upgrade, downgrade, a second upgrade and `alembic check` against a real PostgreSQL.
- **Tests:** 22 pytest tests (`server/tests`) on a real PostgreSQL 17, never SQLite. `tools/ci/server_tests.py` finds the database (`TEST_DATABASE_URL`, else a throwaway Docker container `procsense-test-pg`, else it skips the database tests with a notice; `REQUIRE_DB=1` makes that a failure, and CI sets it). `task server-test`, `server-dev`, `server-migrate`, `server-db-up`, `server-db-down`. CI has a `server` job with a PostgreSQL 17 service container, and it builds the image and checks `/health` and `/health/db` in the running container.
- **Image:** `server/Dockerfile` (two stages, locked dependencies, runs as an ordinary user, health check on `/health`) and `server/Dockerfile.dockerignore` (only `server/` enters the build; no `.env`, no tests).
- **Secret scan:** a throwaway database address on this computer (`localhost`, `127.0.0.1`) is no longer reported as a leaked password.
- **Not in this step:** the Render service (it is created from the image CI builds; waits for the container-registry publishing of `p1-backend-s13`, and the Render team), accounts (s08), sessions (s02), Jinja templates (added with the dashboard, s11).

