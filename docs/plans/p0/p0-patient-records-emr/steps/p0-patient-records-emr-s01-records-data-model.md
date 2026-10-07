# p0-patient-records-emr-s01 — Records data model in Core

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Core `Records`: current-episode documents (clerking note, observation chart, consent, ward checklist, printed results) vs historical record (previous admissions, reports, tests, scans from `content/records/`); documents created by events (`ResultReady`, consent signed, clerking lines); verbs `open_document`, `view_image`, `search_patient` logged for scoring ('did the trainee look at the previous HbA1c?').

## Why
One source of truth for the folder and the EMR; opening records is evidence.

## How to implement
1. `src/ProcSense.Core/Records/*`: Document {id, type, episode: current | historical, date_offset, title, body, image_ref?}.
2. Folder view = current-episode documents; EMR view = all documents.
3. Tests: ResultReady adds a document visible in both views; historical documents only in the EMR.
4. Document types enum in content (clerking, obs_chart, consent, ward_checklist, lab_report, imaging_report, referral, discharge_summary…).
5. Search (`search_patient`) returns only the case patient + distractor patients (to practise finding the right record).

## Verification
`dotnet test --filter FullyQualifiedName~Records` green: ResultReady → document in both views; historical only in EMR; `open_document` logged.

## Files changed
`src/ProcSense.Core/Records/*`, tests.

## Dependencies
p0-core-engine-s04.

## Common issues
Two copies of a result (folder vs EMR) that can disagree → one Core document, two views.

## Sources
User direction (folder = current episode; computer = complete record); p0-patient-records-emr README.
