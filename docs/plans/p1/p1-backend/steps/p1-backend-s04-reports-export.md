# p1-backend-s04 — Reports, dashboard data and CSV export

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
The data behind the global dashboard, as JSON endpoints scoped by role and organisation: a session report (the same content as the command-line report: end state, scores with evidence, timeline, debrief, sources), a trainee's progress over time, a cohort and an organisation overview, and `GET /export.csv` with documented columns. Only verified sessions are counted.

## Why
The supervisor needs results across trainees, cohorts and sites; the user studies need clean exported data; and a customer needs to see its own organisation and nobody else's.

## How to implement
1. The session report is the one `p1-backend-s09` already produced when it verified the session (the command-line `report` output: the HTML page and the facts JSON, schemas `debrief_facts.v1` and `session_report.v1`). `GET /sessions/{id}/report` returns it; `GET /sessions/{id}/report.html` returns the page.
2. Dashboard endpoints: `GET /me/progress` (a trainee: sessions over time, process score, credits and penalties per behaviour, recurring critical items), `GET /organisations/{id}/overview` (sessions per week, result distribution, most common critical items, behaviours that need work), `GET /cohorts/{id}/overview`, `GET /organisations/{id}/sessions` with filters (person, cohort, case, mode, result, dates, verified).
3. Visibility by role: a trainee sees their own sessions; a supervisor sees the people in the cohorts and organisation they supervise; an organisation admin sees the whole organisation; nobody sees another organisation. A supervisor looking at one person is written to the access log (`p1-backend-s12`).
4. Queries are SQL aggregates through the ORM (repositories), with indexes; no query reads the stored log text.
5. `GET /export.csv?organisation=&cohort=` is limited to supervisors and above; columns are pseudonymous (no display names, no emails) and documented in `server/DATA_DICTIONARY.md`; a platform-wide export for the evaluation study needs the platform administrator and has the organisation replaced by a stable code.
6. Reports and exports state the content version and whether the weights are provisional.

## Verification
The report matches the command-line report for the same session; CSV opens cleanly; no real names or emails anywhere in exports (T-BE3); T-BE5 (a supervisor of organisation A gets 403 or an empty result for every endpoint on organisation B, and an unverified session never appears in a total); every dashboard query under 1 s on the EXP-BE-3 data.

## Files changed
`server/routers/{reports,dashboard,export}.py`, `server/repositories/{reports,dashboard}.py`, `server/schemas/{reports,dashboard}.py`, `server/DATA_DICTIONARY.md`, tests.

## Dependencies
p1-backend-s02, p1-backend-s08, p1-backend-s09.

## Common issues
Never put real names or emails in reports or exports for evaluation: pseudonymous ids only (T-BE3). Counting unverified sessions makes the dashboard untrustworthy → every aggregate filters on verified. Leaking across organisations is the worst bug this product can have → every repository function takes the organisation as an argument and is tested with two organisations.

## Sources
Implementation Guide B-M16 Steps 3–4, T-BE3; INSTRUCTIONS §4b (routers/repositories/schemas); ADR-0009.
