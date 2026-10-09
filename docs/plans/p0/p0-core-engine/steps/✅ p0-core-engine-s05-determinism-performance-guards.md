# p0-core-engine-s05 — Determinism guards and performance budget

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Automated guards that keep Core deterministic and fast: a test that scans the Core assembly for banned APIs (`System.Random`, `DateTime.Now/UtcNow`, `Environment.TickCount`, `Guid.NewGuid`, any `UnityEngine` reference); a lint test that fails on `Dictionary` enumeration in output paths (or code review rule); performance test T-K5 (10k actions + 1 h sim time < 2 s headless; zero allocations per Tick after warm-up).

## Why
Determinism and Quest performance are easy to keep and very hard to recover later.

## How to implement
1. `tests/ProcSense.Core.Tests/DeterminismGuardTests.cs`: reflection over the Core assembly's IL (System.Reflection.Metadata) for banned member references.
2. `PerformanceTests.cs`: Release config; `GC.GetAllocatedBytesForCurrentThread()` delta over 1000 ticks after warm-up = 0; Stopwatch budget with generous CI margin.
3. Record numbers in the core-engine README (EXP-CORE-3).

## Verification
`dotnet test --filter FullyQualifiedName~DeterminismGuard` + `dotnet test --filter FullyQualifiedName~Performance` green; adding `DateTime.Now` anywhere in Core makes CI fail.

## Files changed
`tests/ProcSense.Core.Tests/DeterminismGuardTests.cs`, `PerformanceTests.cs`, core-engine README.

## Dependencies
p0-core-engine-s04.

## Common issues
Perf tests can be flaky on shared CI runners — compare against a margin, run in Release.

## Sources
Implementation Guide B-R11 determinism rules, tests T-K1, T-K5, T-K6.
