# p0-patient-records-emr-s04 — EMR sections

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Sections: profile (identity, allergies, alerts), current episode (reason, plan, NEWS2 trend), labs (current + previous, flags, comments, trend charts), imaging (reports + image viewer), reports (all documents), medical history, medications (current list + confirmed prescriptions), previous admissions/tests.

## Why
The computer is the trainee's window into the patient's complete history (previous reports, scans, results, admissions) — the information a real clinician checks before surgery.

## How to implement
1. One view per section reading Core documents; trend chart for repeated labs/NEWS2.
2. Image viewer for approved images only.
3. Labs section: table with H/L flags + trend chart for analytes with ≥ 2 values (e.g. NEWS2, K, creatinine).
4. Imaging viewer: approved images only, zoom/pan, report beside the image; report-only notice where no image exists.
5. History section from `content/records/<case>.json` with dates relative to 'today'.

## Verification
Every P0 case shows all sections populated from content; a case with no images shows the report-only notice.

## Files changed
`Scripts/Records/Emr/Sections/*`.

## Dependencies
p0-patient-records-emr-s03; p0-medical-research-data-s14, s16.

## Common issues
Historical data that contradicts the current case misleads trainees — every historical record must be derived from or consistent with walkthrough facts (p0-medical-research-data-s14).

## Sources
User direction (computer = complete record); walkthrough Part 5 (previous values), Part 6 (imaging reports); p0-patient-records-emr README.

## Implementation notes (16 Oct 2026): the Core half
- **Built in Core:** `src/ProcSense.Core/Records/EmrSections.cs`, read through `session.Records.Sections()`, worked out on each call from the record and the session (never stale, never a second copy): **profile** (name, date of birth, number, sex, age, allergies, an alert per allergy); **current episode** (the reason for admission from the case, this admission's documents, the NEWS2 totals the trainee recorded); **labs** (each result with its flag, the laboratory's comment and the previous value the case states, the earlier results from the record, and a trend for any analyte with two values or more, the earlier value first); **imaging** (each report with its approved image, or the notice "Report only: no image is available for this study."); **reports** (every document); **medical history**; **medications** (the treatments a clinician has confirmed or the nurse has given); **previous admissions and tests** (the hospital record).
- **What is deliberately not copied in:** the history and medicines the patient tells when asked are hidden facts the case wants found by asking, so the sections show only what the record itself holds (the summary letter, which the record table wrote from the case file, already lists the regular medicines; the medications section points to it).
- **Tests:** `EmrSectionsTests` (5): every one of the 17 P0 cases shows every section from content (the step's verification, headless), an allergy is an alert, a previous scan without a picture shows the report-only notice, labs with the previous value and a trend (VAR-S1-PB's bilirubin), the NEWS2 trend and confirmed prescriptions. Engine tests pass; the golden runs are unchanged.
- **Not built (the Unity half, Sannan):** the section views on the EMR workstation (`Scripts/Records/Emr/Sections/*`), the trend chart, the image viewer with zoom and pan. It needs the EMR shell (p0-patient-records-emr-s03, Unity). The step stays open until then.
- **Core half approved by the owner on 16 Oct 2026** (decision row 89); the step file stays unticked until the Unity half is done.
