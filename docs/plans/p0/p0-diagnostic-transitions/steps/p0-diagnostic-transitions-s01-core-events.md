# p0-diagnostic-transitions-s01 — Core events for the diagnostics loop (EXP-DT-2)

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
One deterministic loop in Core: `InvestigationRequested` → transport (patient) or sample dispatch (lab) → `PatientDeparted` / `SampleSent` → time-skip by the test's turnaround while physiology and events keep running → `ResultReady` → `PatientReturned` / `ReportDelivered`.

## Why
Unity only visualises this loop; tests can prove it headless, including deterioration during a CT trip.

## How to implement
1. Wire investigations (p0-clinical-logic-s06) + transport (p0-simulation-workflow-s13) + time-skip (p0-core-engine-s06).
2. Lab tests: no patient movement; samples dispatched via porter/tube station.
3. EXP-DT-2: untreated S2 fixture sent to CT deteriorates on the clock during the trip.
4. Tests: `dotnet test --filter FullyQualifiedName~DiagnosticsLoop`.

## Verification
Loop test and EXP-DT-2 pass headless.

## Files changed
`src/ProcSense.Core/Diagnostics/DiagnosticsLoop.cs`, event types in `src/ProcSense.Core/Events/`, `tests/ProcSense.Core.Tests/DiagnosticsLoopTests.cs`.

## Dependencies
p0-clinical-logic-s06; p0-simulation-workflow-s13; p0-core-engine-s06.

## Common issues
Time-skip must stop early if an event demands attention during the wait (e.g. critical-result call, deterioration alarm) — otherwise the trainee 'misses' something they never had a chance to see.

## Sources
p0-diagnostic-transitions README; Implementation Guide B-R16 worked example and Steps 2–5; B-M13 Step 1; walkthrough Step 6, Part 17 (turnaround).
