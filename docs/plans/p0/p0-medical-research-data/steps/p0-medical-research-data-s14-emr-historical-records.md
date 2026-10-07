# p0-medical-research-data-s14 — EMR historical records per P0 case (synthetic)

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Previous admissions, previous reports/tests/scans and past-history entries for each P0 case, consistent with the walkthrough facts (e.g. VAR-S1-A: caesarean section 2012, GP referral, clinic ultrasound with stones) and marked synthetic, for the EMR's history tabs.

## Why
The EMR must show a complete record; the walkthrough mostly gives current-episode data only.

## How to implement
1. Per case, list the walkthrough facts that imply history; write only records that support the case (no filler).
2. File `content/records/<case>.json` {documents[{type, date, title, body, derived_from_ref | authored: true}]}; `synthetic: true`.
3. Never contradict current-episode data; previous results may not change today's decision unless the walkthrough says so.
4. Register rows (evidence type D); medical professional approves; outcome recorded.
5. Include distractor patients' minimal records so 'search the right patient' is a real task (synthetic, flagged).

## Verification
Validator green; every record cites the fact it derives from or is flagged 'authored'; approval recorded.

## Files changed
`content/records/*.json`, register.

## Dependencies
p0-medical-research-data-s10–s12.

## Common issues
Dates relative to the case's 'today' (store as offsets) so cases stay consistent.

## Sources
p0-patient-records-emr README.
