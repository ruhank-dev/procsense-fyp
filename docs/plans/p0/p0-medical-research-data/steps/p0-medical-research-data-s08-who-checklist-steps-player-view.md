# p0-medical-research-data-s08 — WHO checklist, step specs, flow maps, player view, VR cards, glossary

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Content for the structural walkthrough parts: WHO Sign In / Time Out / Sign Out items with exact 2009 wording + flagged ProcSense extensions and expected-answer sources; the 15 steps × 21-question specifications; S1/S2 20-stage flow maps (10 fields per stage); player view per stage; the 24 VR reasoning cards; glossary.

## Why
Checklist engine, case graph, flow maps, tablet text, debrief and UI wording all read from here.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) Claude re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
2. Sources: walkthrough Part 15 (checklist), Part 11 (steps), Parts F/G (flow), Part 16 (player view), Part C (cards), Part 18 (glossary).
3. Files: `who_checklist.csv` (phase, item_id, exact_text, is_extension, expected_from_state, critical, order), `steps_21_questions.csv`, `flow_S1_20_stages.csv`, `flow_S2_20_stages.csv`, `player_view.csv`, `vr_cards.csv`, `glossary.csv`.
4. Institutional decision 8 (WHO version/additions, site marking) recorded.
5. Counts must match the extractor (T-WT2).

## Verification
Validator green; checklist item count and wording match Part 15 exactly; 15 × 21 step answers present.

## Files changed
`content/who_checklist.csv`, `steps_21_questions.csv`, `flow_S1_20_stages.csv`, `flow_S2_20_stages.csv`, `player_view.csv`, `vr_cards.csv`, `glossary.csv`, register.

## Dependencies
p0-medical-research-data-s02.

## Common issues
WHO wording: copy exactly; extensions in their own column so the UI shows them apart.

## Sources
Walkthrough Parts 11, 15, 16, 18, C, F, G; inventory D1, D213, D216–D221, D407.
