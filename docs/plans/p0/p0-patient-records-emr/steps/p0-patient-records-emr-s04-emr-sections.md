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
