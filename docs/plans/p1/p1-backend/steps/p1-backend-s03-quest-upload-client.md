# p1-backend-s03 — Quest upload client with offline queue (EXP-BE-1)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Unity uploads finished sessions to the hosted service when it is reachable (a health check about every 30 s), sends the signed-in user's token, keeps a persistent queue, retries with backoff, and never affects play when offline or when the service is asleep.

## Why
The headset must always work without a connection (offline-first). Sessions finished offline still have to reach the supervisor's dashboard and the evaluation data later, without duplicates, without blocking the frame, and without losing a session if the network drops mid-upload. The hosted service on the free plan can take a minute to wake up, so waiting and retrying is normal, not an error.

## How to implement
1. `Scripts/Sync/SessionUploader.cs` (UnityWebRequest, cancellation timeouts, HTTPS only; the service address comes from a build setting, never hard-coded in code). It attaches the access token from `p1-backend-s10` and refreshes it on a 401.
2. The host assigns a session id (a new GUID) when a session starts and writes it into the session file header with the content version and engine version (`p1-backend-s09`); the engine itself never makes random numbers.
3. Queue stored in persistent data; each upload idempotent by session id; exponential backoff up to 10 min. A response of 200 or 202 removes the entry only after `GET /sessions/{id}` confirms the server has it.
4. Upload only completed sessions (log footer present); in-progress sessions never leave the device. A session finished while signed out is queued with the identity it was played under and uploaded when that person is signed in again.
5. Handling of responses: 401 → refresh once, then ask the user to sign in; 409, 413, 422 → move to a "needs attention" list with the reason and stop retrying; 429, 5xx and timeouts → retry with backoff; no network → wait.
6. Status in the settings panel: 'N sessions waiting to upload', last upload time, 'service reachable' yes or no, signed-in name.
7. Play Mode test with a mock server: success, 500 (retry), timeout (retry), 401 (refresh), duplicate (already stored → remove), 413 (needs attention).
8. EXP-BE-1 on university Wi-Fi and on a phone hotspot, with the free container asleep at the start of some runs.

## Verification
EXP-BE-1: 20 uploads with network drops, some starting with the service asleep → 0 lost, 0 duplicates; the app is unaffected offline.

## Files changed
`unity/Assets/Scripts/Sync/SessionUploader.cs`, `UploadQueue.cs`, settings panel UI, Play Mode test, `docs/plans/p1/p1-backend/README.md` (EXP-BE-1 result).

## Dependencies
p1-backend-s02, p1-backend-s09 (header v2), p1-backend-s10 (login).

## Common issues
Campus Wi-Fi may block or intercept traffic (sign-in pages, filtered ports) → test with a phone hotspot first. A wrong device clock breaks token checks on the device → trust the server's `expires_in` and timestamps, not the device date. Sleeping free host → the first request can take a minute; use a long first timeout.

## Sources
Implementation Guide B-M16 Step 2, T-BE2; B-S4 Step 2 (async transport); ADR-0009.
