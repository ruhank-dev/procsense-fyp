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

## Implementation notes (15 Oct 2026)
- **Built:** `src/ProcSense.Core/Diagnostics/DiagnosticsLoop.cs` (a subsystem at the end of the pipeline) and `DiagnosticsEvents.cs`: `InvestigationRequested`, `PatientDeparted` (imaging: the patient leaves for the scan room), `SampleSent` (a sample leaves for the laboratory, via the porter; the tube station is p0-diagnostic-transitions-s04), the existing `ResultReady`, `PatientReturned` and `ReportDelivered` (with the patient, from the laboratory, or at the bedside). Each test has a trip with the sim step of every stage (`session.Diagnostics.Trips`), which the Unity transition controller and the EMR will read. The event types live in `Diagnostics/` (not `Events/` as first planned) so the loop's types sit together.
- **It observes, it does not decide:** the loop reads the investigations and transport steps that already move the patient and process the tests (p0-clinical-logic-s06, p0-simulation-workflow-s13), and changes no state and no score; a test proves the log hash of a session is identical with and without it. A report found while another event is being handed out is announced at the end of the step, so every view sees the result arrive before the report.
- **Time-skip stops for the trainee:** during a skip the loop asks for attention when the laboratory phones with a critical result, when an event fires (a vital-sign event, for example) and when the patient deteriorates in the corridor, so the skip stops after that step (`stopped:<reason>` in the skip's effects).
- **One golden run changed, on purpose:** in STK-S2-B the 60-minute skip now stops after 27 minutes because E-HIGH-HR fires (`stopped:event:E-HIGH-HR`); the next action's time is the same, so the end state and the state hash are unchanged and only the log hash moved (expected file updated with `task golden -- --update`).
- **Engine version not raised (0.0.1):** a session recorded by an older build with a skip across an event would now replay to a different log and be refused by the service. No such session exists (one demo session, no skip), headset builds and the service are built from the same main, and raising the version would make the service refuse every headset build made before it; DT-174 records the rule for the first real release.
- **EXP-DT-2 (done):** the untreated septic patient (the Usman timeline, VAR-S2-PD) sent to CT with a full escort was away 25 minutes; heart rate 103 when leaving and 111 when back: the deterioration clock keeps running while the patient is away.
- **Tests:** `DiagnosticsLoopTests` (7): blood (requested, sent by porter, ready, delivered from the laboratory), imaging (patient away, actions on the patient refused, report back with the patient at the later of result and return), bedside, a skip runs the loop exactly like play, EXP-DT-2, a skip stops on a critical call (once), and the log is unchanged by the loop. Unity batch tests 149 of 149 pass with the new files (their `.meta` files are committed).
