# p0-core-engine

**Tier:** P0 · **Builds on:** p0-foundation · **Feeds:** every simulation phase, p0-vr-platform, p1-backend

## Goal
The simulator's heart: a pure C# engine that holds the patient's truth, applies every trainee action in order, advances time,
and replays any session exactly (same seed + same actions = same result).

## Scope
- **In:** SimClock (fixed 0.5 s step + time-skip), seeded RNG streams, Action API + verb registry + validation, EventBus,
  subsystem pipeline, ObservableState (what Unity/AI may see), JSONL session log, content loader interface, headless CLI, replay harness.
- **Out:** medical rules (data + other phases).

## Deliverables
`src/ProcSense.Core` kernel; `procsense-sim run …` CLI; determinism + performance tests.

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| Target | C# 9, netstandard2.1 | .NET 8 copy into Unity; Python core | Runs unchanged in Unity 6 and headless | Unity moves to newer .NET |
| Time | Fixed 0.5 s steps | Variable dt; pure event-driven | Determinism (B-R11) | EXP-CORE-2 fails |
| RNG | Own xoshiro256** named streams | System.Random | Identical across runtimes | — |
| Trainee verbs | Request/initiate only — no administer verb | Administer in practice mode | Walkthrough role convention | — |
| Unity bridge | Unity reads ObservableState, writes SimAction only | Unity mutates state | Single source of truth | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-CORE-1 | Identical results on PC (.NET 9) and Quest (IL2CPP/ARM)? | Same golden action list, compare log hash | Equal hashes |
| EXP-CORE-2 | Is 0.5 s smooth enough for the monitor? | Unity-side interpolation at headset refresh | No visible stepping |
| EXP-CORE-3 | Allocation per Tick | Profiler/BenchmarkDotNet | 0 bytes after warm-up |
| EXP-CORE-3 result (09 Oct 2026, s05, Release, skeleton 1 case, 3 subsystems) | `PerformanceTests` | `GC.GetAllocatedBytesForCurrentThread` over 1000 ticks after 200 warm-up ticks | **0 bytes**; 0.0002 ms per tick; 0.0047 ms per action. Re-check as physiology and events are added |

## Exit
Replay determinism proven; rejects (unknown verb, wrong location, identity not confirmed) tested; perf targets met; no Unity reference.

## Who does what
Developer: all. Team: run EXP-CORE-1 on the Quest.

## Sources
Implementation Guide B-R11; Master Plan §25.
