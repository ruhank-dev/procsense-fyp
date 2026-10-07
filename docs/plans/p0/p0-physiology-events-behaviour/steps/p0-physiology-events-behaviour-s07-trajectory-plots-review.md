# p0-physiology-events-behaviour-s07 — Trajectory and effect plots for medical review (EXP-PHY-1)

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Scripts that render every effect × patient archetype and each P0 case's vitals/NEWS2 over time (from headless runs) to PNGs for the medical review pack; the reviewer accepts or returns fixes.

## Why
Type D values (effect sizes, timings, the deterioration clock) are design choices, not medicine — they are acceptable only once a medical reviewer has seen them as curves and agreed they behave plausibly.

## How to implement
1. `tools/physio/plot_effects.py` and `tools/physio/plot_case.py` (matplotlib via uv; read CLI `result.json`).
2. Output to `docs/research/physio-plots/<date>/` (PNG, LFS).
3. Include plots in the review pack (p0-medical-research-data-s17); record verdicts + changes in the register.
4. Plot naming `<case>_<branch>_<variable>.png`; an index markdown links them for the review pack.

## Verification
Plots exist for every effect and P0 case; reviewer verdict recorded (EXP-PHY-1).

## Files changed
`tools/physio/*.py`, `docs/research/physio-plots/*`, register notes.

## Dependencies
p0-physiology-events-behaviour-s04, s05.

## Common issues
Different axis scales across plots mislead reviewers — fix y-axis ranges per variable. Plot both treated and untreated branches.

## Sources
B-R13 Step 7; EXP-PHY-1.
