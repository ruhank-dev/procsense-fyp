# p1-theatre-extension-s01 — Step 15: WHO Sign Out engine

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Sign Out phase in the SafetyChecklist: procedure recorded, instrument/sponge/needle counts complete (discrepancy → search, X-ray per local policy, escalate), specimen labelled and **read aloud including the patient's name**, equipment problems pulled from the Step 11 log, key recovery concerns; omitted antibiotic plan at recovery handover → debrief.

## Why
Completes the WHO checklist beyond Time Out (walkthrough Step 15).

## How to implement
1. Extend `SafetyChecklist` with SIGN_OUT items from `who_checklist.csv`.
2. Equipment problems auto-listed from the OR readiness log.
3. Verb `read_specimen_label` with patient-name check; count verbs + discrepancy branch (EXP-TX-1).
4. Board shows Sign Out items in the same style as Sign In/Time Out (p0-vr-interaction-ui-s08).

## Verification
`dotnet test --filter FullyQualifiedName~SignOut` → T15-1, T-EXT1 green; count-discrepancy fixture escalates correctly.

## Files changed
`src/ProcSense.Core/Checklist/SafetyChecklist.cs` (SIGN_OUT phase), `content/who_checklist.csv` (Sign Out rows), tests.

## Dependencies
p0-simulation-workflow-s16.

## Common issues
Fault not reported → minor item (E15-A).

## Sources
Implementation Guide B-R15 Steps 5, 7; §21 (L15-01, E15-A, T15-1); walkthrough Step 15, Part 15.
