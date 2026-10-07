# p1-backend-s03 — Quest upload client with offline queue (EXP-BE-1)

Milestone: **M10 P1 platform — backend, AI, voice input**

## What
Unity uploads finished sessions when the server is reachable (health check every 30 s), keeps a persistent queue, retries with backoff, and never affects play when offline.

## Why
The headset must always work without the laptop (offline-first). Sessions finished offline still have to reach the supervisor's reports and the evaluation dataset later — without duplicates, without blocking the frame, and without losing a session if the network drops mid-upload.

## How to implement
1. `Scripts/Sync/SessionUploader.cs` (UnityWebRequest, cancellation timeouts).
2. EXP-BE-1: 20 uploads on university Wi-Fi with network drops.
3. Queue stored in persistent data; each upload idempotent by session_id; exponential backoff up to 10 min.
4. Upload only completed sessions (log footer present); in-progress sessions never leave the device.
5. Status in the settings panel: 'N sessions waiting to upload', last upload time, server reachable yes/no.
6. Play Mode test with a mock server: success, 500 error (retry), timeout (retry), duplicate (server says already stored → remove from queue).

## Verification
EXP-BE-1: 20 uploads with network drops → 0 lost; app unaffected offline.

## Files changed
`unity/Assets/_Project/Scripts/Sync/SessionUploader.cs`, `UploadQueue.cs`, settings panel UI, Play Mode test, `docs/plans/p1/p1-backend/README.md` (EXP-BE-1 result).

## Dependencies
p1-backend-s02.

## Common issues
Campus Wi-Fi often blocks device-to-device traffic → travel router or Tailscale (B-M16 troubleshooting).

## Sources
Implementation Guide B-M16 Step 2, T-BE2; B-S4 Step 2 (async transport).
