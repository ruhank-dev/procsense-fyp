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
