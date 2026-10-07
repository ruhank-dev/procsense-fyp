# p1-backend-s02 — Session models and upload API

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
ORM models trainee (pseudonymous), session, event, score_item, competency; `POST /sessions` uploads a session JSONL (schema-validated, size-limited, idempotent by session_id); per-device API key from settings; pseudonymous IDs only.

## Why
Uploads are the only way results leave the headset; they must be idempotent (no duplicates on retry), validated at the boundary, and pseudonymous.

## How to implement
1. Models + Alembic migration; repositories with ORM only.
2. Request/response schemas in `schemas/` (Pydantic) with limits.
3. API-key dependency; reject oversized payloads with 413/422.
4. Size limit (e.g. 20 MB per upload, configurable in settings); JSONL lines validated against the session-log schema.

## Verification
T-BE1–T-BE3 pass; oversized upload rejected with 413; malformed line rejected with 422 and line number.

## Files changed
`server/models/{trainee,session,event,score_item,competency}.py`, `server/repositories/sessions.py`, `server/routers/sessions.py`, `server/schemas/sessions.py`, Alembic migration, `server/tests/test_sessions.py`.

## Dependencies
p1-backend-s01.

## Common issues
Validate at the boundary; never log payload contents with identifiers.

## Sources
B-M16 Steps 1–2, T-BE1–T-BE3.
