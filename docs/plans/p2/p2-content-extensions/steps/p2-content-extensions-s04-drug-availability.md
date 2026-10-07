# p2-content-extensions-s04 — Local drug availability and backup labels

Milestone: **M13 P2 extensions**

## What
Mark which medicines in content are locally available (DRAP drug register checked, date recorded) and use DailyMed only as a backup source for contraindication/warning text when openFDA lacks it — never doses.

## Why
Local realism for Pakistani hospitals: trainees shouldn't be taught plans built on unavailable drugs.

## How to implement
1. Lookup + availability flag column; reviewer confirms.
2. For each medicine group, look up availability in the DRAP drug register (record the page/date checked); flag `locally_available: yes/no/unknown`.
3. DailyMed used only to fill contraindication/warning text when openFDA lacks it — no doses.
4. Reviewer confirms flags; unavailable drugs shown with a note in the debrief/explain text.

## Verification
Every medicine row has an availability flag with a source; reviewer approval recorded.

## Files changed
`content/medicines.csv` (columns `locally_available`, `availability_source`, `availability_checked`), register rows, review pack entry.

## Dependencies
p0-medical-research-data-s06.

## Common issues
Brand vs generic names differ locally — map generics to local brands only as display synonyms.

## Sources
Inventory D25 (DailyMed), D27 (DRAP), D24 (openFDA).
