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

## Implementation notes (14 Oct 2026)
- **Addresses.** Like every other organisation route, the organisation is part of the address: `GET /organisations/{id}/sessions` (filters: `trainee_id`, `cohort_id`, `case_id`, `mode`, `result`, `since`, `until`, `status` default `verified`, `limit`, `offset`), `/sessions/{sid}/report`, `/sessions/{sid}/report.html`, `/progress` (the signed-in person), `/members/{user}/progress`, `/overview`, `/cohorts/{cohort}/overview`, `/export.csv`; and `GET /export.csv` for the platform administrator. The step text's shorter addresses (`/me/progress`, `/cohorts/{id}/overview`) were not used; a person in two organisations has progress in each.
- **The report** is the debrief facts verification stored (`report_json`, schema `debrief_facts.v1`) and the page it stored (`report_html_gz`); nothing is recomputed. The page is served with a policy that forbids scripts and anything loaded from elsewhere. A session that is not verified answers 409 `not_verified` with its status.
- **Figures** (`summary`): sessions, distinct trainees, average process score, sessions per week (Monday, UTC), result distribution, the ten most common critical items, and every behaviour with credits, penalties, net and the average net per session, lowest first (the ones that need work). The same figures serve a person, a cohort and the organisation. They are SQL aggregates through the ORM and never read the log text.
- **Decisions that are the project team's, not the book's:** (1) a supervisor or organisation administrator sees the **whole organisation**; cohorts group and filter, they do not limit a supervisor (the step text said "the cohorts and organisation they supervise"; limiting a supervisor to their cohorts is DT-145, and it matches how session status was already built in s02); (2) a trainee asking for a list or figures is always narrowed to themselves, whatever filters they send; (3) a session of another person is "not found" for a trainee, never "forbidden"; (4) the list is the one place other statuses can be asked for, so a person can see their own pending upload, and no figure ever includes them; (5) supervisors see display names in the list; reports, progress and the export carry only the pseudonymous id; (6) the access log is a log line for now (`procsense.access`), the table is s12 (DT-146); (7) the process score is marked provisional everywhere because the scorer's weights are (DT-147); (8) the export is capped at 200,000 rows and cells that start with `=`, `+`, `-` or `@` are neutralised; (9) a headset's token can list, read reports and progress of its own person but cannot open overviews or exports (s10 rule).
- **Speed:** one new index (migration `0006`, organisation + status + date). The test puts 4,000 sessions with 4,000 critical items and 28,000 behaviour rows in one organisation and requires every dashboard query under a second and the whole export under five.
- **Tests:** 19 in `server/tests/test_dashboard.py` on PostgreSQL 17, including T-BE3 (no names or emails in exports) and T-BE5 (every endpoint refuses a person of another organisation; an unverified session never appears in a total). EXP-BE-3 on real-sized data is still to do (DT-148).

