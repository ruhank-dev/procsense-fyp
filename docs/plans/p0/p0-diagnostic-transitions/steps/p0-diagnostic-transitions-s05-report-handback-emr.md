# p0-diagnostic-transitions-s05 — Report hand-back and EMR update

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Returning patient/porter carries a printed report (live text from the result); the trainee takes it and places it on the desk or in the folder; the EMR updated at `ResultReady` (badge on the tab).

## Why
You asked for both records to update: the trainee physically receives and files the printed report (current-episode folder), while the computer has already updated automatically — two views of one Core document, never two copies that could disagree.

## How to implement
1. Printed report prop (`SCREEN_Sheet`) with content from Core; desk/folder sockets.
2. EMR listens to ResultReady (p0-patient-records-emr-s05).
3. Printed report prop spawns in the returning patient's/porter's hand; the porter hands it over with a short line; if ignored it's left on the desk (no loss).
4. `file_document` action when placed in the folder; EMR already updated at ResultReady.

## Verification
Play Mode test: CT trip → report prop arrives → filed in folder → visible as a folder page; EMR Imaging tab has the same report.

## Files changed
`unity/Assets/_Project/Scripts/Records/PrintedReport.cs`, desk/folder sockets, `Scripts/Records/Emr/EmrResultListener.cs`, Play Mode tests.

## Dependencies
p0-patient-records-emr-s05.

## Common issues
Report prop text must come from Core (same as EMR) — never a separate copy that can disagree.

## Sources
User direction (physical folder + automatic EMR update); p0-patient-records-emr README; Implementation Guide B-M13 Step 5.
