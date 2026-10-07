# p0-medical-research-data-s04 — Laboratory bands, critical results, sample quality, turnaround

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Content for lab results: bands per analyte with number kind (reference / abnormal / concern / critical / simulator trigger), critical-result phone list, sample-quality artefact rules (haemolysed, drip arm, EDTA, clumped, mislabelled/rejected) and test turnaround times.

## Why
The result generator, flags and critical-call blockers read only these tables.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) Claude re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
2. Sources: walkthrough Part 5 (incl. 5.27 critical results, 5.28 playbook) and Part 17 bands.
3. Files: `content/lab_bands.csv`, `content/critical_results.csv`, `content/sample_quality.csv`, `content/turnaround.csv`.
4. Turnaround: walkthrough gives FBC 45 min and imaging 60–120 min; everything else Type D, status UNRESOLVED (WT-I10) with the design default noted.
5. Band disagreements (WT-I05 albumin, WT-I06 CRP, WT-I07 sodium, WT-I08 INR/GGT) → Part 17 used for bands, Part 5 text kept for teaching (Change Report handling); UKKA hyperkalaemia version note (WT-I14).
6. Institutional decision 1 (lab ranges/units/critical list) recorded with the approved choice.

## Verification
Validator green; every lab row has a number kind; WT-I05…I08, I10, I14 present in issues.md with handling.

## Files changed
`content/lab_bands.csv`, `critical_results.csv`, `sample_quality.csv`, `turnaround.csv`, register, issues.

## Dependencies
p0-medical-research-data-s02.

## Common issues
Units must be explicit per row (mmol/L vs mg/dL) — the flagger compares numbers only after unit match.

## Sources
Walkthrough Parts 5, 17, H; inventory D116–D121, D162–D186, D204, D208, D400.
