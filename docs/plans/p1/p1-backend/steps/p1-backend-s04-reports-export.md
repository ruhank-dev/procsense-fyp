# p1-backend-s04 — Reports and CSV export

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Server-rendered reports: per session (same content as the P0 CLI report — end state, scores with evidence, timeline, sources) and per trainee over time (sessions, competencies, recurring critical items); `GET /export.csv?cohort=` for the evaluation study with documented columns.

## Why
The supervisor needs results across trainees and sessions; the user studies need clean exported data.

## How to implement
1. Reuse the P0 report JSON format; templates in `server/templates/`.
2. Routers thin; repositories do the queries (ORM).
3. Trainee progress page: sessions over time, competencies, recurring critical items.
4. CSV export pseudonymous only; columns documented in `docs/report/data-dictionary.md`.

## Verification
Report matches the CLI report for the same session; CSV opens cleanly; no real names anywhere (T-BE3).

## Files changed
`server/routers/reports.py`, `server/repositories/reports.py`, `server/schemas/reports.py`, `server/templates/{session,trainee}.html`, `docs/report/data-dictionary.md`, tests.

## Dependencies
p1-backend-s02.

## Common issues
Never put real names in reports or exports — pseudonymous IDs only (T-BE3).

## Sources
Implementation Guide B-M16 Steps 3–4, T-BE3; INSTRUCTIONS §4b (routers/repositories/schemas).
