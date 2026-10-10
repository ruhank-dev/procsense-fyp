# p0-clinical-logic-s08 — Result interpretation, acknowledgement and critical-result calls

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
H/L flags and number kinds (reference / abnormal / concern / critical / simulator trigger), interpretation by finding selection, acknowledgement with consequences via the case graph, and the critical-result phone call: the lab caller phones values from `critical_results.csv`; the trainee must confirm identity, read back, act and document; unacknowledged = blocker.

## Why
Even with an auto-updating EMR, critical results must be phoned and read back (walkthrough 5.27).

## How to implement
1. `Clinical/ResultInterpretation.cs`: flags from `lab_bands.csv`; number kind per value.
2. Verbs `answer_phone`, `confirm_patient_on_call`, `read_back(values)`, `acknowledge_result(test, finding_codes)`.
3. Readiness blocker until critical results acknowledged (T07-2, T07-WT2).
4. `ResultReady` → Records (EMR) + printed report document (p0-patient-records-emr-s01).
5. Lab caller is voice/phone only — no avatar.

## Verification
`dotnet test --filter FullyQualifiedName~CriticalCall` green: critical fixture blocks progress until identity confirmed + read-back + acknowledgement.

## Files changed
`src/ProcSense.Core/Clinical/ResultInterpretation.cs`, tests.

## Dependencies
p0-clinical-logic-s07.

## Common issues
Read-back must match values exactly — compare normalised numbers, not strings.

## Sources
B-M13 Steps 5, 7; §13 L7-03; walkthrough 5.27–5.28; inventory D121, D208.
