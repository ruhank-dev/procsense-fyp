# p1-backend-s09 — Server-side session verification by replay

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Every uploaded session is replayed on the server through the same engine that produced it, and counts only if the replay reproduces the log. Two parts: a `verify` command in the command-line simulator (and a version-2 session file header), and a verification worker in the server that runs it for pending sessions and stores the result, the report and the scores.

## Why
The product sells results. If the headset is trusted, a modified application can send any score it likes. The engine is deterministic and every action is logged, so the server can re-run the session and check the hash, which no spreadsheet-style product can do. It also tells the dashboard exactly which content and engine version a result belongs to.

## How to implement
1. Session file header v2 (engine side, `src/ProcSense.Core/Replay/SessionFile.cs`): add the session id (given by the host), the content version (from the manifest) and the engine version. They are outside the replay hash, so existing golden runs and hashes do not change; replay still reads version 1 files.
2. `procsense-sim verify --log <file> --content <dir> [--json out.json]`: loads the content, checks it against the manifest, replays, and prints a JSON result `{ok, reason, case, mode, content_version, engine_version, log_hash, end_state, result, process_score}` and writes the report page and facts JSON. Exit code 0 only if everything reproduces. The reasons are fixed codes: `HASH_MISMATCH`, `CONTENT_VERSION_UNKNOWN`, `ENGINE_VERSION_UNSUPPORTED`, `CASE_NOT_ALLOWED_FOR_MODE`, `LOG_MALFORMED`.
3. Mode check: a session uploaded as assessment counts as assessment only if its case was approved for assessment in that content version (`ContentMode`, `p0-case-library-s01`); otherwise it is stored as practice and says why.
4. Server container: the published self-contained Linux binary of the simulator and the content packages of the supported content versions are in the image; the supported versions are a setting. A log made with an older engine or content version is `unverifiable`, stored, kept out of every total and shown to its owner with the reason, never silently dropped.
5. Worker: pending sessions are verified one at a time with a timeout and memory limit. The queue is the `status` column, not memory, because the free host sleeps and restarts; a sweeper runs when the service wakes and from a scheduled call (`p1-backend-s13`). A verified session gets its score rows, behaviour rows, report page and facts JSON filled in one transaction.
6. A rejected session keeps the reason and the hash and is visible to the platform administrator; repeated rejections from one user are counted.

## Verification
EXP-BE-5: change one score, one action and the content version in a log → all three refused with the right reason. A correct log from the headset, from the command line and from a golden run verifies and gives the same report as the command-line `report`. The verifier is time-limited and cannot be made to read a file outside its temporary folder. Golden runs and hashes are unchanged by the header change.

## Files changed
`src/ProcSense.Core/Replay/SessionFile.cs`, `src/ProcSense.Cli/VerifyCommand.cs`, tests in the Core test project, `server/verification/runner.py`, `server/verification/worker.py`, `server/Dockerfile`, `server/tests/test_verification.py`, `docs/CI.md`.

## Dependencies
p1-backend-s02; p0-scoring-debrief-s05; p0-case-library-s01.

## Common issues
Replaying costs time and memory on a small free host → one at a time, a queue in the database, a sweeper after a wake-up. An engine change makes old logs unverifiable → keep supported versions in a setting and record which engine version each result came from. Never pass an uploaded file name to a command line → write it to a temporary file with a generated name.

## Sources
ADR-0009; Master Plan §25 (evaluation and logging); Implementation Guide B-M16; ADR-0006 (review-status gate and content integrity).
