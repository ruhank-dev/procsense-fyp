# p1-backend-s02 — Session models and upload API

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Tables for the session, its score items and its per-behaviour result, and `POST /sessions`: a signed-in user uploads a finished session (the header line and the session log, the same text the simulator writes) into their organisation. The upload is schema-validated line by line, size-limited, idempotent by session id, and stored as received with a status of `pending` for verification (`p1-backend-s09`). Pseudonymous ids identify people inside session data; real names stay in the accounts tables. The tables the scenario generator and the personalisation work need are created here too: the session evidence record, the learner profile and the generated scenario packages (see "Fit with the scenario composer" in the README).

## Why
Uploads are the only way results leave the headset. They must be idempotent (no duplicates when the network drops and the headset retries), validated at the boundary, owned by the right person and organisation, and cheap to store on a small database.

## How to implement
1. Models and an Alembic migration: `session` (id = the session id from the header; organisation, user, case, scenario, mode, seed, content version, engine version, log hash, started and ended times as the server records them, status `pending`, `verified`, `rejected` or `unverifiable`, reject reason, the log stored once as compressed text, the report JSON and page filled by verification, created time), `session_score_item` (item id, behaviour, severity, status, points, step, evidence list) and `session_behaviour` (behaviour, credits, penalties, net). Types: uuid, timestamptz, jsonb, bytea; indexes for organisation + time, user + time and case.
2. A JSON Schema for the session file (header v2 and log lines), `content/schema/session_log.schema.json`, matching the strict C# reader; unknown fields refused.
3. `POST /sessions` (`application/x-ndjson`, optional gzip): the user and organisation come from the login token and membership (`p1-backend-s08`), never from the body; a user who belongs to several organisations names the active one in a header that is checked against their memberships.
4. Idempotency: the same id with the same log hash returns 200 `already stored` and changes nothing; the same id with a different hash returns 409 and is recorded as a security event. A new upload returns 202 with the status URL.
5. Limits in settings: body size (default 2 MB, hard maximum 20 MB) → 413; a malformed line → 422 with the line number; a rate limit per user.
6. `GET /sessions/{id}` returns the status so the headset can drop it from its queue only when the server has it.
7. Repositories use the ORM only; the router only calls them.
8. For personalisation (`p1-personalisation-s01` and `s02`): `session_evidence` (the SessionEvidence v1 record of the session as jsonb, its schema owned by `p1-personalisation-s01`, pseudonymous, no free text from the trainee) and `learner_profile` (one row per person and organisation holding the learner model's parameters and summary, written by `p1-personalisation-s02`; visible to the person and to the supervisors who may see that person, and deleted or anonymised with the person, `p1-backend-s12`). This step creates the tables and the access rules; the content of the evidence record and of the learner model belongs to the personalisation steps.
9. For generated scenarios (`p1-live-scenario-generation`): `scenario_package` (the SHA-256 hash of the canonical package as the key, the package stored compressed, its review status `GENERATED_PRACTICE_ONLY`, created time). The version-2 session header carries `scenario_package_hash` (empty for the fixed cases). If an upload names a package the server does not have, the answer is `package_needed` and the headset sends it to `PUT /scenario-packages/{hash}`; the server recomputes the hash of the bytes and refuses a mismatch; a package is stored once however many sessions use it. A session of a generated scenario is practice only: an upload that claims assessment mode with a package hash is refused.

## Verification
T-BE1 (upload twice → stored once), T-BE2 (partial upload → retry resumes, nothing half-stored), T-BE3 (reports and exports show pseudonyms only) and T-BE4 (a user cannot upload into an organisation they do not belong to; a deactivated member cannot upload) and T-BE6 (a session of a generated scenario uploads in two steps, the package is stored once and a package whose bytes do not match its hash is refused; an assessment upload with a package hash is refused) pass; an oversized upload returns 413; a malformed line returns 422 with its line number.

## Files changed
`server/models/{session,session_score_item,session_behaviour,session_evidence,learner_profile,scenario_package}.py`, `server/routers/scenario_packages.py`, `server/repositories/sessions.py`, `server/routers/sessions.py`, `server/schemas/sessions.py`, `content/schema/session_log.schema.json`, Alembic migration, `server/tests/test_sessions.py`.

## Dependencies
p1-backend-s01, p1-backend-s08.

## Common issues
Validate at the boundary; never log payload contents with identifiers. Two copies of the same upload racing → rely on the primary key and catch the conflict, do not check-then-insert. The database is small on the free plan → store the log and each package once, compressed, and keep the score rows narrow (DT-135). The evidence record and the profile hold personal data → they follow the retention and delete rules of `p1-backend-s12`.

## Sources
Implementation Guide B-M16 Steps 1–2, T-BE1–T-BE3; ADR-0009.

## Implementation notes (13 Oct 2026)
- **Tables** (migration `0003`, `server/models/sessions.py`): `session` (the id from the header as the key, organisation, person, case, mode, seed, content and engine versions, package hash, log hash, number of actions, last step, the log stored once as compressed bytes, status, reject reason, received time, and the result fields and report that verification fills), `session_score_item`, `session_behaviour` (filled by s09), `session_evidence`, `learner_profile`, `scenario_package`. Row-level security on all six, no policies, public rights revoked (the same second lock as the account tables).
- **Address:** `POST /organisations/{id}/sessions` (the organisation is in the address, like every other organisation route of s08, instead of a header; a person in several organisations names the one they mean) and `GET /organisations/{id}/sessions/{session_id}` for the status. `PUT /scenario-packages/{hash}` for packages.
- **The file** (`server/sessionfile.py`): the engine's real format, strictly: a header line and one line per action; unknown fields refused; every line checked on its own so an error names the line and never repeats the content; `log_hash` must be the SHA-256 of the log text (that proves the file arrived whole); `seq` counts from 0 and `step` never goes back; at most 20,000 actions. Version 2 headers (session id, content version, engine version, package hash) are required; a version 1 file (what the engine writes today) is read only by the tools that say `--v1`. The engine and server contract is tested both ways: the published `content/schema/session_log.schema.json` is generated from the server's models and checked, a file the engine wrote is validated against it with an independent validator (tools tests), and `task ci` has the engine write a fresh session and the server read it.
- **Upload rules:** the same id with the same log from the same person is answered 200 `already_stored`; the same id with another log or from another person is 409 and written to the security log; a body over the limit (default 2 MB, up to 20 MB) is 413, also after unpacking gzip (a small file cannot expand into a big one); bad gzip 422, other encodings 415, not UTF-8 422; 120 uploads an hour per person (in memory, so per instance); an assessment session may not name a generated package. A session naming a package the server does not have gets 409 `package_needed`, stores nothing, and succeeds when sent again after the package is stored.
- **Found while building:** an ORM-level insert does not report a row count, which made every first upload look like a duplicate; the code now uses `RETURNING`. Pydantic's validation error is a kind of `ValueError`, so a bad field was reported as "not JSON" until the checks were ordered correctly.
- **Differences from the plan text:** no per-device key (accounts, s08); no "log footer" (the engine writes none: a session is complete when it is uploaded and verified; whether the case ended is decided by verification, s09); a generated package is stored through its own route.
- **Tests:** 44 new (113 in all) on a real PostgreSQL: T-BE1 to T-BE4 and T-BE6, a race between two identical uploads (one row), limits, gzip, who may see a status, the schema export and the engine contract, row-level security on the six tables.
- **Not in this step:** verification and the score rows (s09), the headset side (s03, s10), reports (s04).

