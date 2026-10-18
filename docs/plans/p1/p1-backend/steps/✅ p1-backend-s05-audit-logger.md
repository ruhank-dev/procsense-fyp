# p1-backend-s05 — AI audit logger

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Middleware writing an `ai_call` row for every AI request: session_id, organisation, user (pseudonymous id), task, model, model_version, prompt_id, context_id, input_hash, output_ref, validation result + reasons, latency_ms, fallback, timestamp — asynchronously (< 5 ms overhead); mirrored into the on-device session log; weekly report per organisation and for the platform (fallback rate, latency, failure reasons). Stored in the same PostgreSQL database as the sessions and visible only to the roles that may see them.

## Why
Every AI output that reaches a trainee must be traceable afterwards (which model, which prompt version, what the validator said, whether a fallback was used) — that's both a safety requirement and the evidence for P1 evaluation.

## How to implement
1. FastAPI middleware + ORM model (written in a background task through the pooled connection so a slow write never delays the reply); every row carries the organisation and the pseudonymous user so a customer can ask what the AI said to their trainees; no raw audio stored beyond consent; hashes/refs where text isn't needed.
2. `server/reports/audit_weekly.html`.
3. Weekly audit report: fallback rate, validation failure reasons, latency percentiles per task.

## Verification
T-AU1–T-AU3 pass; weekly report renders from real rows.

## Files changed
`server/middleware/audit.py`, `server/models/ai_call.py`, Alembic migration, `server/templates/audit_weekly.html`, tests.

## Dependencies
p1-backend-s01, p1-backend-s08.

## Common issues
The free database is small and rows add up fast → store hashes and references, not text, and set a retention period (`p1-backend-s12`); never store raw trainee audio beyond consent.

## Sources
Implementation Guide B-M10 Steps 1–3, T-AU1–T-AU3; Master Plan §26 (component 12: AI Audit Logger).

## Notes (16 Oct 2026)
- **Built (branch myt1x54/p1-backend-s05):** table `ai_call` (`server/models/ai_call.py`, migration `0008`, row-level security on like every other table, and the access-log action `view_ai_calls`); `server/middleware/audit.py` with `audited(task)`, the dependency every AI route takes (sign-in as a member of the organisation, then the record), `AuditMiddleware` (times the request, finishes the record on any outcome, puts `X-AI-Call-Id` on the reply) and `AuditWriter` (a background thread writes the rows in batches through the pooled connection; a full queue drops a row and logs it; a failed write is retried 3 times); `unaudited_ai_routes(app)`, which lists any AI route (`/llm`, `/dialogue`, `/behaviour`, `/classify`, `/debrief`, `/asr`, `/tts`) that does not take `audited`; `server/repositories/ai_audit.py` (weekly figures in SQL, the list of calls, export, deletion, retention); `server/routers/ai_audit.py`: `GET /organisations/{id}/ai-audit/weekly` and `.html` (supervisor and up), `GET /ai-audit/weekly` and `.html` (platform administrator), `GET /organisations/{id}/ai-calls` (a trainee sees only their own; a supervisor opening one person's calls is in the access log); `server/templates/audit_weekly.html`; setting `RETENTION_AI_CALL_MONTHS` (12) and `AUDIT_QUEUE_SIZE`; `server/PRIVACY.md` updated.
- **Tests:** `server/tests/test_ai_audit.py` (13) through stand-in AI routes, since the real ones come with s06. T-AU1: six requests with every outcome (answer, validator failure with fallback, 503, bad request, validation skipped, crash) give exactly six rows with the right status, and a request not signed in or not a member starts none; the route check walks the included routers and finds an unaudited `/llm/generate`. T-AU2: the sentence the trainee said is nowhere in the table (only its SHA-256), a reason that is not a code becomes `UNCODED_REASON`, a prompt name that looks like text is hashed, no text column is wider than 80 characters. T-AU3: the audit adds **1.4 ms** to a request (median of 100 against the same route without it; target under 5 ms), and a database that takes a second to answer does not slow the reply. The weekly report (JSON and page) is checked against seeded rows of a known week: counts, fallback rate, p50 latency, failure and fallback reasons, organisation scoping, who may open it. `test_rls.py` now checks that every table of the models has the lock. Whole backend suite: 251 passed, 5 skipped.
- **Found and fixed:** `db_session` turned any `RuntimeError` raised inside a route into "503 database_not_configured", so a crashing route was reported as a missing database; now only opening the session can give that answer.
- **Decisions that are the project team's, not the book's:** the rows hold no text at all (inputs hashed, outputs referenced, reasons as codes), so the words stay only on the headset; a request that is refused before sign-in or membership is not an AI call and writes no row (it is in the service log); a crash reply carries no id (the framework answers it outside the audit) but its row is written with status 500 and validation `error`; rows are kept 12 months (a design value to confirm with a qualified person, DT-127) and move to the anonymous stand-in when a person deletes their data; the weekly report is made on request for any week rather than sent on a schedule.
- **Not done:** the headset's mirror of each call into its own session log (DT-182, with the headset's AI client); the real AI routes (s06), which take `audited(...)`.
- **Approved by Abdul Moiz, 16 Oct 2026** (the server half, with the decisions above). The step stays open for the headset's mirror (DT-182).

<!-- external-testing:start (generated by tools/plans/external_testing.py) -->
## External acceptance

This step has acceptance that only a person, device or account outside the engineering desk can give. The exact procedure, pass rule and evidence are in the single source of truth:

* [EX-11](../../../../testing/SANNAN_EXTERNAL_TESTING.md#ex-11): Real sessions for the weekly report

<!-- external-testing:end -->
