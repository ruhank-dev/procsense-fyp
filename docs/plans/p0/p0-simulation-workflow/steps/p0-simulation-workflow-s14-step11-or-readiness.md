# p0-simulation-workflow-s14 — Step 11: theatre preparation and OR readiness

Milestone: **M3 Walking skeleton 3 — OT Sign In / Time Out**

## What
OR readiness engine: required equipment groups per procedure (anaesthetic machine check, suction, monitoring, lap tower: camera/light/insufflator, CO2 + spare cylinder, warming, airway kit, diathermy pad) each with a state (unchecked / ready / fault); faults from scenario or events; Time Out blocked until all required groups are ready.

## Why
Walkthrough Step 11: an unfixed fault means Time Out cannot complete.

## How to implement
1. `content/or_requirements.json`: `or_requirement {group, required_if, items[], readiness_condition, owner_role: trainee | npc_verified}` (B-R15 interface).
2. `Workflow/OrReadiness.cs`: `Evaluate()` per group; per-group status in ObservableState (station panels).
3. Verbs `check_station(station)`, `report_fault(station)`, `declare_or_ready` (L11-01…L11-03); report → replacement delay; ignored fault → major item (E11-A).
4. Readiness blocker `OR_NOT_READY` on `complete(TIME_OUT)`.
5. S2: substitute theatre allowed (institutional, content).

## Verification
`dotnet test --filter FullyQualifiedName~OrReadiness` → T11-1, T11-2, T-SC4 green; VAR-S1-A Scene 10 items (keyhole tower, CO2 + spare, suction, warming, anaesthetic machine check) all confirmed.

## Files changed
`src/ProcSense.Core/Workflow/OrReadiness.cs`, `content/or_requirements.json`, tests.

## Dependencies
p0-simulation-workflow-s05.

## Common issues
Station states will later come from VR equipment interactions — keep them as plain Core verbs so headless tests stay possible.

## Sources
Implementation Guide §17 (L11-01…L11-03, E11-A/B, T11-1/2), B-R15 Step 4; walkthrough Step 11, Part 12 Scene 10.
