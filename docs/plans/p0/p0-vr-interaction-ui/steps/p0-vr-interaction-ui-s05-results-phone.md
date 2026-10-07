# p0-vr-interaction-ui-s05 — Results viewer and critical-result phone

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Results viewer modes: table (value, H/L flag, units, local range, lab comment like 'haemolysed', previous value, acknowledge button), image (licensed images), ECG strip; interpretation picker → decision card; phone that rings for critical results with read-back choices and acknowledgement.

## Why
Step 7 teaching; unacknowledged critical results block progress.

## How to implement
1. `Scripts/UI/Results/ResultsView.cs` (table / image / strip), `PhoneView.cs`.
2. Same Core data as the EMR (one source).
3. T-UI4 correct H/L flags.
4. Previous values shown with dates (from Records history) and a trend arrow.
5. Lab comment shown verbatim from content (e.g. 'sample haemolysed — please repeat').

## Verification
Play Mode test: VAR-S1-J fixture shows K 6.4 with the haemolysis comment, repeat shows 4.6; critical fixture rings the phone and blocks until read-back.

## Files changed
`unity/Assets/_Project/Scripts/UI/Results/{ResultsView,InterpretPicker,PhoneView}.cs`, phone prefab, Play Mode tests.

## Dependencies
p0-clinical-logic-s08.

## Common issues
Units must display exactly as content (mmol/L, g/L) — never converted in Unity.

## Sources
B-S10 Steps 3, 6, T-UI4; walkthrough 5.27.
