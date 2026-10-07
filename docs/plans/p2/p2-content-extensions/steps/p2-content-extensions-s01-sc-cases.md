# p2-content-extensions-s01 — SC-1..SC-20 compact stacked cases

Milestone: **M13 P2 extensions**

## What
Expand the 20 compact stacked cases (walkthrough 14B) into full case files using the composer for structure; values from the walkthrough; Stage B + medical approval.

## Why
The 20 compact stacked cases complete the walkthrough's stacked-case coverage for advanced practice.

## How to implement
1. Expand; validate; review pack; approve.
2. Read walkthrough 14B compact table per SC case; expand to the full case schema with the composer filling structure only — walkthrough values are never changed.
3. Hidden facts, problems with ladder ranks, expected end state, debrief from the compact rows.
4. Verbatim check of every value against the 14B pages; register rows.

## Verification
20/20 SC cases valid, verbatim-checked, Stage B pass, approved.

## Files changed
`content/cases/SC-01.json` … `SC-20.json`, register rows, Stage B reports, review pack.

## Dependencies
p1-scenario-composer-difficulty-s02.

## Common issues
Compact rows lack detail the full schema needs — the composer may only add structure, never new clinical values; missing values go to the reviewer.

## Sources
Walkthrough Part 14B (compact table, 14B.5 recipe); inventory D483.
