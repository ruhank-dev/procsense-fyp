# p0-core-engine-s02 — Actions, Session and the session log

Walking skeleton 1 · step 3 of 15

## What
The way anything happens in the simulator: a trainee action (`SimAction`) goes into `Session.Apply()`, is validated against a
**verb registry**, passes through an ordered **subsystem pipeline**, and is written to an append-only **session log** (JSONL).
`Session.Tick(dt)` advances the clock through the same pipeline.

## Why
One entry point for every action makes the engine testable headless, replayable, and the only place rules can be enforced
(e.g. "no blood tests before identity is confirmed").

## How to implement
1. `Actions/SimAction.cs` (`Verb`, `Target`, `Params` sorted dictionary, `Channel` = vr | tablet | cli), `ActionOutcome`
   (`Accepted`, `RejectReason`, `Effects`).
2. `Actions/VerbRegistry.cs`: verbs registered by subsystems; unknown verb → reject `UNKNOWN_VERB` (T-K2).
3. `ISubsystem` (`Init`, `OnAction`, `OnTick`) + fixed pipeline order (B-R11 Step 6); subsystems optional per scenario.
4. `EventBus` — synchronous typed events, pooled lists (no allocation per publish).
5. `Session` — constructed with content package, case card, seed, mode (practice | assessment); `Apply`, `Tick`.
6. `Logging/SessionLog.cs` — append-only entries: seq, sim time, action, outcome, effects; JSONL writer; **log hash** (SHA-256 of canonical JSONL).
7. Tests: unknown verb rejected; 100 actions with same seed replay to identical log hash (T-K1, initial version); rejected actions are logged with reason.

## Verification
`task test` green; a test prints the log hash and asserts it's equal across two independent runs.

## Files changed
`src/ProcSense.Core/Actions/*.cs`, `src/ProcSense.Core/Pipeline/*.cs`, `src/ProcSense.Core/Session.cs`, `src/ProcSense.Core/Logging/*.cs`, tests.

## Dependencies
p0-core-engine-s01.

## Common issues
- Canonical JSON: sort keys, invariant culture for numbers, `\n` line endings — or hashes differ across machines.
- Don't iterate `Dictionary` for anything that affects output (B-R11 determinism rules).
- Design pattern: **Pipeline / chain of responsibility** for subsystems — tell user when implemented.

## Sources
Implementation Guide B-R11 Steps 4–6 (ActionAPI, EventBus, pipeline), tests T-K1, T-K2; Master Plan §25 action pipeline.
