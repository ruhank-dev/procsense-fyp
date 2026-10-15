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

## Implementation notes (15 Oct 2026)
- **Content:** `tools/content/build_records.py` writes `content/records/records.json` (checked in CI and run by `task content-refresh`): 13 document types, each current-episode (folder and EMR) or record (EMR only); the current documents a session starts with (clerking note, observation chart) and the ones a flag creates (consent form at `consent.signed`, ward checklist at `ward.checklist_signed`); the type a result becomes (blood: laboratory report, bedside: bedside result, imaging: imaging report); each case's hospital record from `content/emr_case_records.csv` (98 records for 53 cases, each with its register row; the table's previous imaging becomes `previous_imaging` so it is never mistaken for this admission's scan); the 6 distractor patients of `content/emr_distractors.csv` (SR-3050 to SR-3055). The step's `content/records/` folder of hand-written records was not needed: the record table written from the case files already existed.
- **Core:** `src/ProcSense.Core/Records/` (`RecordTables`, `RecordsSubsystem`, read through `session.Records`). One `RecordDocument` per document; the folder and the EMR are two views of it (`Folder`, `Emr`), so they cannot disagree. A result becomes a document when it is ready (EMR) and goes into the folder when its report is delivered (p0-diagnostic-transitions-s01): a blood or bedside result at once, a scan's report when it comes back with the patient. Verbs `open_document` (view folder or emr), `view_image` and `search_patient` (name, number or date of birth; the ward list holds near-miss names) are logged and kept in `History` as evidence for the scorer; nothing changes the patient, and the golden runs are unchanged. `OnTick` allocates nothing (the engine's per-tick rule caught a first version that did).
- **Tests:** `RecordsTests` (7): the content, the starting folder and EMR, a result as one document in both views, a scan in the EMR first and in the folder after it comes back, opening logged and refusals, consent signed adds the form, search with near misses. Engine tests 1,209 pass; Unity batch tests 149 of 149 with the new files (their `.meta` files committed).
- **Not here (later steps):** distractor patients' own records and choosing the wrong patient (p0-patient-records-emr-s03), EMR sections (s04), badges (s05), scoring what was opened (the scorer reads `History` later).
