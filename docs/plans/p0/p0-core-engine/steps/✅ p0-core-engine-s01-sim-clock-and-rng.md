# p0-core-engine-s01 — Simulation clock and deterministic random numbers

Walking skeleton 1 · step 2 of 15

## What
Two foundation pieces of `ProcSense.Core`:
- **SimClock** — simulated time that advances only in fixed 0.5 s steps; leftover time is carried, never lost; supports a time-skip; starts at a scenario clock time (e.g. 08:00).
- **SimRandom** — our own seeded random generator (xoshiro256\*\*) with **named streams** ("events", "physio", "iv"…) so adding randomness in one subsystem never changes another.

## Why
Same seed + same actions must give exactly the same session, on PC and on Quest (B-R11). `System.Random`, `DateTime.Now`
and variable time steps break that.

## How to implement
1. `src/ProcSense.Core/Time/SimClock.cs`: `double SimSeconds`, `const double Step = 0.5`, `Advance(double dt)` → returns number of whole
   steps to run and keeps the remainder; `TimeSkip(double seconds)`; `ClockTime` = scenario start + SimSeconds (no `DateTime.Now`).
2. `src/ProcSense.Core/Random/SimRandom.cs`: xoshiro256\*\* seeded via SplitMix64 from the session seed; `Stream(string name)` derives a
   stream from seed + stable hash of the name (own FNV-1a — `string.GetHashCode()` is randomised per process).
3. Tests in `tests/ProcSense.Core.Tests/`: T-K3 (dt = 0.73 repeatedly → whole steps only, remainder carried), same seed → same sequence,
   different stream names → independent sequences, known first values for seed 42 (golden numbers).

## Verification
`task test` → new tests green; golden numbers identical on repeated runs.

## Files changed
`src/ProcSense.Core/Time/SimClock.cs`, `src/ProcSense.Core/Random/SimRandom.cs`, `tests/ProcSense.Core.Tests/SimClockTests.cs`, `tests/ProcSense.Core.Tests/SimRandomTests.cs`.

## Dependencies
p0-foundation-s00 (repo skeleton).

## Common issues
- Floating-point drift: accumulate with a step counter (`long Steps`), derive seconds = Steps × 0.5.
- C# 9 / netstandard2.1: no `Random.Shared`, no `System.HashCode` guarantees — use own hashing.

## Sources
Implementation Guide B-R11 Steps 2–3 (SimClock, deterministic RNG), test T-K3; determinism rules.
