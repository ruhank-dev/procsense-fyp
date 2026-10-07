# p0-medical-research-data-s06 — Medicines: plans, dose policy, three checks

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Medicine content: groups (paracetamol, NSAIDs, opioids, antiemetics, IV fluids, antibiotics S1/S2, anticoagulants, antiplatelets, diabetes medicines incl. SGLT2/GLP-1/insulin, ACE-i/ARB, beta-blockers, diuretics, statins/CCB, steroids, COCP/HRT, herbal, epilepsy/Parkinson's, VTE prophylaxis, emergency medicines) each with perioperative plan/timing and dose policy (EDUCATIONAL EXAMPLE with source / PATIENT-SPECIFIC → 'clinician decides' / INSTITUTIONAL), the three drug checks, role convention.

## Why
The treatment system and nurse-challenge logic read these; the safety statement forbids trainee dosing.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) Claude re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
2. Source: walkthrough Part 8 (+ 8.1 role convention).
3. Files: `content/medicines.csv` (group, plan, timing, dose_policy, example_text, example_source), `content/drug_checks.json`.
4. openFDA only for contraindication/warning text — no dose import (inventory D24).
5. Institutional decision 2 (antibiotic protocol incl. penicillin-allergy alternative) recorded with the approved choice.
6. Validator test: rows with `dose_policy = PATIENT_SPECIFIC` contain no numbers.

## Verification
Validator green; the no-number test passes; every example dose cites its source.

## Files changed
`content/medicines.csv`, `content/drug_checks.json`, register, validator rule.

## Dependencies
p0-medical-research-data-s05.

## Common issues
Educational example doses are display strings — the engine never parses them.

## Sources
Walkthrough Part 8; inventory D24, D28, D234–D267, D401.
