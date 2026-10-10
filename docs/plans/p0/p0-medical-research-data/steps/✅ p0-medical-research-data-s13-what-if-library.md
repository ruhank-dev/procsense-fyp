# p0-medical-research-data-s13 — What-if library (47 branches) as data

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
All 47 walkthrough what-ifs with their 11 fields (trigger / what the trainee sees, what it might mean, what to check next, correct action, incorrect action, missed → consequence, escalation, expected patient/monitor response, what happens next S1/S2, source, clinician validation) + trigger type (value threshold / trainee action / omission / system) + case bindings.

## Why
The Event Director and teaching events run what-ifs from data.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); — then verify each branch field against Part 14.
2. File: `content/whatif_library.json`; bindings table `content/whatif_bindings.csv` (case → what-ifs).
3. Mark branches needing P1 features with `requires: P1`.
4. Trigger types normalised: value threshold / trainee action / omission (deadline) / system (equipment, document, corridor).

## Verification
Validator green; exactly 47 entries; every P0 case has its bindings.

## Files changed
`content/whatif_library.json`, `content/whatif_bindings.csv`, register.

## Dependencies
p0-medical-research-data-s02.

## Common issues
Vasovagal faint / 'OT calls early' are not walkthrough events — don't add them.

## Sources
Walkthrough Part 14, Part C event table; inventory D51, D219.
