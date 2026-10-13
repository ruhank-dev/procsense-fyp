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
