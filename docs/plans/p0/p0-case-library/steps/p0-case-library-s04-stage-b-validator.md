# p0-case-library-s04 — Stage B validator and contradiction detector

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Ordered checks that every case (hand-made or composed later) must pass: 1–7 schema, required fields, constraints, the 12 contradiction types, combos/status, difficulty, event eligibility; 8 every result ↔ orderable test ↔ diagnosis; 9–10 headless runs (5-min idle stays within bounds; golden correct path reaches the expected end state); 11 status (AI-drafted cases max IMPLEMENTED). Plus walkthrough contradictions (clash table, Part 17 bands — e.g. SGLT2 taken today + 'fasting OK' must also check ketones). HTML report per case.

## Why
Every case — hand-transcribed now, composed by the solver or proposed by AI later — must be proven possible and consistent before a trainee can play it; impossible cases teach wrong medicine.

## How to implement
1. `tools/composer/stage_b.py` + `tools/composer/checks/*.py` (one module per check, registry order); contradictions as rules in `contradictions.json`.
2. Checks 9–10 call `procsense-sim run` (parallel processes if slow).
3. Fail fast in order, but collect all defects for the report (Jinja HTML).
4. Report per case `build/stage_b/<case>.html` added to review packs.

## Verification
T-VB2 (valid P0 cases pass), T-VB4 (idle stability), T-VB5 (unsatisfiable event → EVENT_INELIGIBLE) green.

## Files changed
`tools/composer/*`, `content/contradictions.json`, tests.

## Dependencies
p0-case-library-s01, s02.

## Common issues
False positives → tag spelling; check against real cases first.

## Sources
Implementation Guide B-M2 Steps 1–4, 6–7, T-VB2/4/5; walkthrough 14B.3, Part 17.
