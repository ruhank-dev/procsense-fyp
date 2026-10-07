# p0-medical-research-data-s07 — Clinical scores and risk tools

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Items, points and bands for AIR (default), AAS, Alvarado, TG18 cholecystitis (diagnosis + grade) and cholangitis, ASGE 2019 duct-stone risk, ASA 2020, airway/STOP-BANG, SORT — plus every worked example from the walkthrough as test vectors.

## Why
Calculators compute; items and bands are sourced data; worked examples prove the calculators.

## How to implement
1. Process for every table: (1) draft rows from `walkthrough_extract.json` (s02); (2) Claude re-reads the walkthrough section and fixes each row; (3) a verbatim-check script confirms every value appears on its cited page (as in s01); (4) evidence type A/B/C/D + number kind + register row; (5) for Type A rows open the cited guideline and mark VERIFIED / UNVERIFIED / CONFLICT; conflicts → `issues.md`, never silently changed.
2. Source: walkthrough Part 7 (+ 5B/5D where scores use lab values).
3. Files: `content/scores.json`, `content/scores_examples.json` (inputs → expected total/band, with page refs).
4. Institutional decision 13 (AIR vs AAS default) recorded.

## Verification
Validator green; each score has ≥ 1 worked-example vector with a page reference.

## Files changed
`content/scores.json`, `content/scores_examples.json`, register.

## Dependencies
p0-medical-research-data-s02.

## Common issues
Scores are decision aids — keep the walkthrough's caveat text with each.

## Sources
Walkthrough Part 7; inventory D3, D4, D225–D233.
