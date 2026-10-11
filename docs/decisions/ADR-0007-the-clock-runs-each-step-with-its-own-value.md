# ADR-0007: A long tick runs each 0.5-second step against the clock at that step

**Status:** Accepted 12 October 2026 (for the owner's approval) · **Related:** ADR-0001

## Context
`Session.Tick(dt)` advanced the clock by all the steps in `dt` and then ran every subsystem once per step, so every subsystem saw the **final** clock value in every step. Anything that queued work relative to "now" and released it when the clock reached a due step behaved differently after one long tick than after many short ones. Found while building the P0 correct paths: the theatre colleagues' answers at Time Out came one at a time per long tick, and the NEWS2 chart time of a blood-pressure cycle was off by the length of the tick. Earlier workarounds counted steps inside subsystems.

## Decision
`Session.Tick` rewinds the clock by the steps it advanced and then moves it one step before each run of the subsystems. A long wait and many short ones now give the same state, the same log and the same hashes. Only one golden file changed (the NEWS2 time moved from 08:02:50 to the exact 08:02:44.5).

## Consequences
Replay and time-skip are consistent, the per-subsystem step counters are no longer needed (they are harmless), and a regression test is the 17 correct-path runs, which use long waits.
