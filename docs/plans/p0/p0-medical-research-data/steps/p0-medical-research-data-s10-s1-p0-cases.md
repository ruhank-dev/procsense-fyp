# p0-medical-research-data-s10 — S1 P0 cases as data

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Complete case files for VAR-S1-A (all steps, extending the skeleton file), VAR-S1-PB, -B (+B2), -C (+C2), -D (a/b), -E, -G, -H, -J: identity, history with hidden facts + `disclose_on`, examination findings, vitals, results (value, unit, flag, comment, quality flag, previous values), imaging reports, problems, expected outcomes per decision point, expected end state, debrief block.

## Why
Cases are content, not code — the engine plays any case the schema expresses.

## How to implement
1. Source: walkthrough Part 12 (Example Patient A + abnormal variants), Part 11 step examples; inventory rows D450–D458 as a checklist of key values.
2. Variant pairs as separate files sharing a base via `extends` (no duplicated data).
3. Verbatim-check script per case page (as s01) — 100% before review.
4. Register rows per value (evidence type D, walkthrough page).
5. ID collisions (WT-I01): namespaced IDs, walkthrough label kept in `walkthrough_label`.

## Verification
Validator green; verbatim check 100% per case; each case passes Stage B (p0-case-library-s04).

## Files changed
`content/cases/VAR-S1-*.json`, register.

## Dependencies
p0-case-library-s01.

## Common issues
Two different 'Mr Imran' patients exist in the walkthrough (WT-I01) — check which is which.

## Sources
Walkthrough Part 12; inventory D450–D458; Change Report WT-I01.
