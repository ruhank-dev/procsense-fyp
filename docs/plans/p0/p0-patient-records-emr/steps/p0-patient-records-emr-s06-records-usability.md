# p0-patient-records-emr-s06 — Records usability (EXP-REC-1…3)

Milestone: **M7 S1 complete in VR**

## What
Tests with users: find the last HbA1c / previous report (EXP-REC-1, target ≤ 30 s), readability at desk distance (EXP-REC-2), page flipping with controllers (EXP-REC-3); fix the top issues.

## Why
The EMR must be quick and comfortable to use.

## How to implement
1. Run, record, fix, re-test.
2. Tasks: (1) 'Find Mrs Khan's previous ultrasound report' (2) 'What was the last HbA1c for Mrs Nasreen?' (3) 'Add the new LFT report to the folder' (4) 'Read the consent form page 2'.
3. Measure time and errors per task; note readability complaints at desk distance (EXP-REC-2) and page-flip failures (EXP-REC-3).
4. Fix the top 3 issues (e.g. larger tab labels, quick-list sorting, page-turn grab zone) and re-test with 2 new users.

## Verification
Results table in `docs/plans/p0/p0-patient-records-emr/results.md`: EXP-REC-1 median ≤ 30 s, EXP-REC-2 text size chosen, EXP-REC-3 no help needed — or the fixes made.

## Files changed
`docs/plans/p0/p0-patient-records-emr/results.md`, task script `docs/plans/p0/p0-patient-records-emr/usability-tasks.md`, fixes in `Scripts/Records/*`.

## Dependencies
p0-patient-records-emr-s04.

## Common issues
Participants unfamiliar with hospital EMRs need a 30-second orientation — keep it identical for everyone.

## Sources
p0-patient-records-emr README experiments (EXP-REC-1…3).
