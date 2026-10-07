# p0-medical-research-data-s03 — Vital-sign bands, NEWS2 and sepsis tables

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Content tables for vital-sign display zones (normal / amber / red / emergency per parameter — HR, SBP low, BP high, RR, SpO2, temperature, ACVPU, pain, CRT, urine, shock index, NEWS2 band), NEWS2 scoring per parameter (Scale 1 + Scale 2, single-parameter-3), NEWS2 response tiers + reassessment intervals, and NICE NG253 sepsis tiers + timers.

## Why
NEWS2 and sepsis drive escalation and S2 deterioration — exact and traceable values are non-negotiable.

## How to implement
1. Sources: walkthrough Part 4 (4.0–4.11) and Part 17; docs/context/ProcSense_Medical_Walkthrough_v5_1.md.
2. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) the developer re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
3. Files: `content/vitals_bands.csv`, `content/news2_scoring.csv`, `content/news2_response.csv`, `content/sepsis_ng253_tiers.csv`.
4. Keep display zones and NEWS2 scoring as separate tables (WT-I04 temperature mismatch is by design).
5. Open the RCP NEWS2 report (inventory D18 link) and NICE NG253 (D100) to verify; carry WT-I04 and WT-I13 into `issues.md` with current handling.

## Verification
Validator green; verbatim check 100%; 10 random rows spot-checked by you against the walkthrough page; every conflict has an issues.md entry.

## Files changed
`content/vitals_bands.csv`, `news2_scoring.csv`, `news2_response.csv`, `sepsis_ng253_tiers.csv`, `docs/research/source_register.csv`, `docs/research/issues.md`.

## Dependencies
p0-medical-research-data-s02.

## Common issues
Never 'fix' a walkthrough number to match a guideline — log the conflict for the medical professional.

## Sources
Walkthrough Parts 4, 17, H; inventory D18, D100–D103, D150–D161, D206–D207; Change Report WT-I04, WT-I13.
