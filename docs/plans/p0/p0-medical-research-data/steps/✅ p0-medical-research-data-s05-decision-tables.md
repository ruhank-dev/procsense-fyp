# p0-medical-research-data-s05 — Decision tables

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Every decision rule as content: fasting rules, S1/S2 test indications, decision thresholds (inventory D187–D205: fasting, BP postpone, HbA1c, glucose target, duct-stone risk, gallbladder grade, appendix width, AIR/AAS bands, sepsis antibiotic timing, antibiotic before incision, theatre temperature, Sign In blood loss, appendectomy timing, cholecystitis surgery timing, transfer escort, deterioration clock, turnaround, low-SpO2 trigger), priority ladder, problem types, Part D priority scale, treatment clash table, decision outcomes, end states.

## Why
The decision and priority engines, blockers and transport rules must read rules from data, each with its source.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) the developer re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
2. Sources: walkthrough Part 3, Part 9, Step 6 test-indication table, Part 14B (14B.1 ladder, 14B.3 clashes), Part D scale, Part 16, Part 17.
3. Files: `fasting_rules.csv`, `test_indications.csv`, `decision_rules.csv`, `priority_ladder.csv`, `problem_types.csv`, `priority_scale_part_d.csv`, `treatment_clashes.csv`, `decision_outcomes.csv`, `end_states.csv`.
4. Each threshold's notes say what it triggers ('think / involve whom'), never 'cancel' (walkthrough decision principle).
5. Institutional decisions 4 (fasting), 5 (anaesthetic protocols), 11 (transfer escort), 13 (appendicitis score) recorded with approved choices.

## Verification
Validator green; each inventory rule D187–D205 appears exactly once with a register row.

## Files changed
`content/*.csv` (9 files), register, issues.

## Dependencies
p0-medical-research-data-s02.

## Common issues
Simulator triggers (e.g. low-SpO2 emergency trigger, D205) are Type D and must say 'simulator trigger, not guideline cut-off'.

## Sources
Walkthrough Parts 3, 9, 14B, 16, 17, D; inventory D187–D212, D403–D405, D410, D412.
