# p0-clinical-logic-s06 — Investigations: state machine, indications, turnaround

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Per-test state machine NOT_ORDERED → REQUESTED → (SAMPLE_TAKEN | TRANSPORTED) → PROCESSING → RESULT_AVAILABLE → VIEWED → ACKNOWLEDGED; indication evaluation APPROPRIATE / NOT_INDICATED / MISSED at order time and at readiness; turnaround on the sim clock; `ResultReady` event.

## Why
Feeds diagnostic door transitions, the EMR and Step 6–7 scoring.

## How to implement
1. `Clinical/Investigations.cs` + `InvestigationState` records; definitions from `content/investigations.json` (test, sample type/tube, location needed, turnaround ref).
2. `IndicationEvaluator` over `test_indications.csv` → result + rule refs; unindicated tests listed with 'cost' in the debrief (B-M13 Step 9).
3. Turnaround from `turnaround.csv` (walkthrough: FBC 45 min, imaging 60–120 min; others Type D/UNRESOLVED); delayed-result events extend timers (T-IV5).
4. Tests that need a location (imaging) create a transport request (p0-simulation-workflow-s13).
5. Raise `ResultReady(test, result_id)` when PROCESSING completes.

## Verification
`dotnet test --filter FullyQualifiedName~Investigations` → T-IV2, T-IV4, T-IV5 green; VAR-S1-A LFTs + UPT reach RESULT_AVAILABLE after their turnaround.

## Files changed
`src/ProcSense.Core/Clinical/Investigations.cs`, `content/investigations.json`, tests.

## Dependencies
p0-medical-research-data-s04, s05; p0-simulation-workflow-s13.

## Common issues
Result never arrives → missing turnaround entry (validator should catch it).

## Sources
Implementation Guide B-M13 Steps 1–2, 9, T-IV2/4/5; walkthrough Step 6; inventory D204, D210.
