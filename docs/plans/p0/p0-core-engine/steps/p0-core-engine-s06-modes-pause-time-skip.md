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
