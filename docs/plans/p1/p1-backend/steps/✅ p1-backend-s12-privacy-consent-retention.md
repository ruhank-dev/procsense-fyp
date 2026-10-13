# p1-backend-s12 — Privacy: consent, retention, delete my data

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
What the service does with trainees' personal data, written down and built: a data map; consent recorded at first sign-in (which text, which version, when); who may see an individual's data and a log of supervisors looking at individuals; a retention period per kind of data; "export my data" and "delete my data" for every person; and no personal data in logs, exports or AI prompts.

## Why
Accounts make trainee data personal data. A sold product is bought by organisations that will ask where the data is, who sees it, how long it is kept and how a person leaves. The patients in every case are synthetic, so no patient data is involved; the work is about the trainees. Doing it with the tables, not afterwards, is cheaper and safer.

## How to implement
1. Data map in `server/PRIVACY.md`: each field, why it is held, where (Supabase Auth, our tables, backups, logs), who can read it, how long it is kept. Personal data is kept to the minimum: email (accounts only), display name, pseudonymous id, session results.
2. Consent: `consent_record` (user, text version, time, what was agreed to: processing of results, research use as a separate optional choice). The headset and the web ask at first sign-in and again when the text version changes; research exports include only people who agreed.
3. Access log: every time a supervisor or admin opens an individual's sessions or exports data, a row is written (who, whom, what, when); the person can see it. Aggregates over several people are not logged per person.
4. Retention: settings per organisation within platform limits (proposed defaults: session logs 24 months, AI audit rows 12 months, access log 24 months; design values to be confirmed, DT-127); a scheduled job deletes or anonymises past the limit.
5. Delete my data: removes the account and profile and anonymises the person's sessions (the link to the person is cut and the pseudonymous id is replaced) so organisation totals stay correct; export my data: a file of the person's own records. Both are tested end to end, including the backup rule (backups are deleted on their own schedule, stated in the data map).
6. Hygiene: no emails, names or tokens in application logs or error reports; AI prompts and audit rows carry pseudonymous ids only.
7. A privacy notice and terms for customers are drafted for a person qualified to review them before any real customer (DT-127); this step does not claim legal compliance.

## Verification
T-PR1 (a delete request leaves no email, name or link to the person in the live tables, and organisation totals are unchanged), T-PR2 (export my data returns only that person's data), T-PR3 (a supervisor viewing an individual appears in that person's access log), T-PR4 (a person who did not agree to research use is absent from a research export), T-PR5 (log files contain no email address) pass.

## Files changed
`server/PRIVACY.md`, `server/models/{consent_record,access_log}.py`, `server/routers/privacy.py`, `server/jobs/retention.py`, Alembic migration, tests, `server/static/consent.html`.

## Dependencies
p1-backend-s02, p1-backend-s08, p1-backend-s11.

## Common issues
Deleting sessions breaks the organisation's totals → anonymise instead. Backups keep deleted data → state how long, and keep backups to the application tables only. A log line with an email is easy to write by accident → a test greps the captured logs.

## Sources
ADR-0009; the data-protection law of the customer's country is for the qualified reviewer to name (DT-127); Implementation Guide B-M16 (pseudonymous ids only).

## Implementation notes (14 Oct 2026)
- **What was built:** `server/PRIVACY.md` (the data map, what a person can do, what deletion does not do, a draft notice and a note that the terms are not written); `server/privacy.py` (the two consent texts and their version); tables and columns in migration `0007` (`access_log`; `organisation.retention_months`; `profile.deleted_at`; `session.log_purged_at`; row-level security on the new table); `server/routers/privacy.py` (`GET/POST /privacy/consent`, `GET /privacy/access-log`, `GET /privacy/export`, `POST /privacy/delete-my-data`, `GET/PUT /organisations/{id}/retention`); `server/repositories/privacy.py`; `server/jobs/retention.py` and `POST /internal/retention`; `server/logging_setup.py`; the Privacy and consent pages in the web page code; `SupabaseAdmin.delete_user`.
- **Decisions that are the project team's, not the book's:** (1) two consent questions: storing results is **required** (without it an upload is refused with `403 consent_required`; the web page asks at first sign-in and when the text version changes) and research use is optional; every answer is kept and the latest counts; a headset can read the question but only the web can answer it; (2) the access log writes a row when a supervisor or administrator opens one person's report, progress, session list or a one-person overview, or exports results; totals over many people and a person looking at themselves are not logged; the same look within five minutes is one row; the person reads the rows on their Privacy page; when the *viewer* later deletes their data the rows stay with the viewer's name replaced; (3) deleting a person removes the sign-in account first (if the account service fails nothing is cut and the person can retry, answer 502), then moves their sessions to a new empty stand-in profile (new random id, new pseudonym, no name) and removes everything else about them, so no cell in any table holds the old id, the name or the old pseudonym while every organisation total stays the same; the last administrator of an organisation is refused; (4) retention removes the stored recording and report page of old sessions but **keeps the results** (score items, behaviours, reports as data), because totals must not change; the period is 24 months by default and an organisation chooses 6 to 60; access-log rows 24 months; (5) the platform administrator's research export holds only people whose latest answer to the research question is yes; the organisation's own export is about results and is not restricted by it; (6) log lines are cleaned as they are made (emails, tokens, keys), a safety net checked by a test that runs a whole sequence of requests and greps the captured log; (7) the stored recordings are not in "download my data" (DT-153).
- **Found while building:** a sign-in token stays valid for up to an hour after the account is deleted (it is signed, not looked up), and would create an empty profile if used; stated in the data map. Two consent answers given in the same clock tick had no order, so the answer time is now forced to be later than the previous one. The research export test showed the platform export had to change: it is now restricted to consenting people.
- **Not done:** the legal review and the customer terms (DT-127); the backup rule (stated in the data map, built in p1-backend-s13); AI audit rows and prompts (not built yet, pseudonymous ids only when they are); limiting supervisors to their cohorts (DT-145: the data map and the consent text say plainly that supervisors see the whole organisation); a command for deleting someone who cannot sign in (DT-156).
- **Tests:** 24 in `server/tests/test_privacy.py` on PostgreSQL 17 (T-PR1 to T-PR5 and more) and the browser test now covers the consent page, the access log page, the download and the delete.

