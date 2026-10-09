# p0-core-engine-s06 — Practice/assessment modes, pause and time-skip

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Session mode (practice: hints, warnings, NPC safety nets; assessment: strict logging, only APPROVED_FOR_ASSESSMENT content, no adaptation), pause/resume, and `TimeSkip(untilEventOrMinutes)` that runs fixed steps fast and logs TIME_SKIP — used by diagnostics waits (~10 s on screen = turnaround on the sim clock) and S2.

## Why
The door-transition design and the walkthrough's practice vs assessment behaviour both need these.

## How to implement
1. `Session.Mode` set at construction; content loader filters by review status (practice ≥ APPROVED_FOR_TRAINING; assessment = APPROVED_FOR_ASSESSMENT).
2. `Session.Pause()/Resume()`; Tick ignored while paused (logged).
3. `Session.TimeSkip(until)`: runs the same fixed 0.5 s steps through the full pipeline (physiology, events) — no shortcuts — until the event or minutes elapse.
4. Test: time-skip of N minutes produces the same log hash as N minutes of manual ticks.

## Verification
`dotnet test --filter FullyQualifiedName~TimeSkip` + `dotnet test --filter FullyQualifiedName~Modes` green.

## Files changed
`src/ProcSense.Core/Session.cs`, tests.

## Dependencies
p0-core-engine-s04.

## Common issues
Time-skip must stop early if an event demands attention (e.g. critical-result call) — return the reason.

## Sources
B-R11 Step 2 (TimeSkip), B-M11 mode minimums; Master Plan §27.

## Implementation notes (09 Oct 2026)
- Pause, resume and time-skip are **system actions** (`Channel.System`) that go through `Session.Apply`, so they are logged and a replay reproduces them. `Session.Pause()`, `Resume()` and `TimeSkip(minutes)` are shortcuts for those actions; the CLI script format can use the verbs `pause`, `resume` and `time_skip` (`params.minutes`).
- While paused every action except `resume` is rejected as `SESSION_PAUSED` (and logged). `Tick` does nothing while paused; individual frames are **not** logged, the clock simply stands still.
- `time_skip` runs the same 0.5 s steps through the whole pipeline, 1 minute up to 24 hours (`BAD_MINUTES` otherwise). A subsystem can call `Session.RequestAttention(reason)` to stop a skip early; the log records `skipped_steps:N` and `stopped:reason`. "Skip until an event" is added when the event director exists (p0-physiology-events-behaviour-s08).
- Every action is now logged with the step it **started** at (a time-skip moves the clock); normal actions are unchanged, so existing hashes are the same.
- **Not built:** the review-status filter in the content loader. There is no review-status field in the content yet (DT-055).
- Tests: `SessionControlTests` (21 cases), golden run `pause_and_time_skip`.
