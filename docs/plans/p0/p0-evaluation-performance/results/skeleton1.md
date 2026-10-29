# Skeleton 1 headset replay check (p0-evaluation-performance-s01, EXP-CORE-1)

**Question.** Does the engine running on the Quest (IL2CPP on ARM) reach exactly the same state as the engine on the PC (.NET 9) for the same actions?

## What was checked

- **The logs.** Ten session logs recorded on the Quest 3. They came from Sannan's earlier Quest build: VAR-S1-A, seed 42, practice mode, all played by hand.
- **Where they came from.** The app's data folder, backed up on 29 October 2026 before the app was reinstalled; copied unchanged into `quest-logs-2026-10-13/` (they hold the simulated patient only, no person).
- **How long they are.** Between 3 and 45 actions.
- **The engine used for the replay.** Each log was replayed headless with the engine and content it was recorded with. The header's content version, `837eb302abbc`, is the content of commit 80fbc987 (13 October 2026). `tools/unity/quest_sessions.py replay` finds that commit by itself.

## Result

**10 of 10 logs replay to the exact hash the headset wrote** (`replay OK, hash match`). The PC engine and the headset engine agree, so EXP-CORE-1 is met for this build.

| Log (session id) | Actions | Replay with the engine of 80fbc987 | Replay with today's engine (5799464d) |
|---|---|---|---|
| 1d5df304 | 3 | match | match |
| 2af07bb7 | 19 | match | match |
| 50d580e1 | 4 | match | match |
| 573fbc07 | 44 | match | differs |
| 6be5d9e7 | 2 | match | match |
| 7fd0ec0a | 6 | match | match |
| 8801bbf6 | 10 | match | differs |
| 8c13705b | 21 | match | match |
| af37c272 | 5 | match | match |
| fa74c7ca | 25 | match | differs |

**Why three logs differ with today's engine.** The engine and its content have changed since 13 October, so this is expected. A log has to be replayed with the engine and content it was recorded with, which is what the service's verification does (per-version content, p1-backend-s09).

## Still open

- **Performance on the headset (EXP-VR-1).** This needs an OVR Metrics capture, which needs a person wearing the headset. It follows the weekly route in `../profiling-route.md`.
- **A fresh headset session.** Play skeleton 1 again on the current Demo build, then pull and replay it:
  ```
  uv run --project tools tools/unity/quest_sessions.py pull
  uv run --project tools tools/unity/quest_sessions.py replay
  ```
- **Usability notes.** Could the tester read the band and the monitor, and find the buttons? These belong to the usability pass (p0-vr-interaction-ui-s11).
