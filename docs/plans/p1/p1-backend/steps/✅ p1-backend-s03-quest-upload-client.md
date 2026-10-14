# p1-backend-s03 — Quest upload client with offline queue (EXP-BE-1)

Milestone: **M17 Cloud platform — Supabase database, accounts, sync and dashboard**

## What
Unity uploads finished sessions to the hosted service when it is reachable (a health check about every 30 s), sends the signed-in user's token, keeps a persistent queue, retries with backoff, and never affects play when offline or when the service is asleep.

## Why
The headset must always work without a connection (offline-first). Sessions finished offline still have to reach the supervisor's dashboard and the evaluation data later, without duplicates, without blocking the frame, and without losing a session if the network drops mid-upload. The hosted service on the free plan can take a minute to wake up, so waiting and retrying is normal, not an error.

## How to implement
1. Upload address: `POST /organisations/{id}/sessions` (the active organisation is part of the address), body = the session file, optional gzip. `Scripts/Sync/SessionUploader.cs` (UnityWebRequest, cancellation timeouts, HTTPS only; the service address comes from a build setting, never hard-coded in code). It attaches the access token from `p1-backend-s10` and refreshes it on a 401.
2. The host assigns a session id (a new GUID) when a session starts and writes it into the session file header with the content version and engine version (`p1-backend-s09`); the engine itself never makes random numbers.
3. Queue stored in persistent data; each upload idempotent by session id; exponential backoff up to 10 min. A response of 200 or 202 removes the entry only after `GET /sessions/{id}` confirms the server has it.
4. Upload only finished sessions (the player ended it or the case ended); a session still being played never leaves the device. (The engine writes no footer; the server decides at verification whether the case really ended.) A session finished while signed out is queued with the identity it was played under and uploaded when that person is signed in again.
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

## Implementation notes (14 Oct 2026)
- **Built (unity/Assets/Scripts/Sync, Bridge/SimulationHost):** the host gives each session an id and saves it with the version 2 header; `EndSession()` saves and queues it for the signed-in person; a persistent queue file (`outbox.json`); the sender with the plan's response handling (confirm with `GET /sessions/{id}` before removing, backoff to 10 min, 401 waits, 409/413/422 set aside, consent_required retried); a background uploader every 30 s and at once on session end or sign-in; a one-line status text. 17 EditMode and 5 PlayMode tests pass in Unity batch mode.
- **Decisions that are the project team's, not the book's:** (1) a session ended while nobody is signed in is saved but **never queued**: sending it later under whoever signs in next would give the result to the wrong person; (2) a queued session is only sent as the person it was played under; (3) the session ends when the app closes or `EndSession()` is called, not when the case's end state resolves, because play may continue after an end state and a log that changed after it was sent would be refused as a different log with the same id; a "finish session" control is the next piece; (4) saving again (pause, then close) replaces the same file, named with the session id; (5) the image now also holds the whole `content/` folder as a verifiable version, because the Editor and the build script read that and not a packaged mode (without it every Editor session would be `unverifiable`); `--no-dev` removes it for a release (DT-162); (6) no gzip yet (the files are small).
- **Not built:** the "finish session" control, the status and last-results panel (DT-159), a settings panel line, the sessions-needing-attention screen, EXP-BE-1 on a Quest with the service asleep.
- **Found while testing:** in Unity batch mode two EditMode tests (`CoreInUnityTests`: a stale pinned hash and a case that is not loaded) and five PlayMode tests (locomotion, simulator input and the wristband ray) fail without any change of mine: they fail the same on the merged branch before this work (DT-163).
- **Panel and finish control (14 Oct 2026):** the tablet's new Session tab (account, upload line, "Finish session and upload", "Refresh my results", the latest five sessions fetched from the service) is built and tested: 4 EditMode tests for the results wording and request, 4 PlayMode tests for the tab (it shows the person and the results; offline wording; finishing saves, sends, empties the queue and then says "Saved on the service"; finishing while signed out keeps the session on the device and says so). The existing tablet tests still pass.
- **Real-network check:** `LiveServiceTests` ran the whole chain with the real `UnityWebRequest` against a local copy of the service with the real engine: sign in, play, end, upload, the service's engine verified the session (the whole-content version) and the result came back. The same test can be pointed at the live service with a real account.
- **Not done / needs a device:** a Quest check of the tab's buttons (reach and legibility), EXP-BE-1 (20 uploads with network drops and the service asleep), and a settings-panel line for sessions that need attention.

