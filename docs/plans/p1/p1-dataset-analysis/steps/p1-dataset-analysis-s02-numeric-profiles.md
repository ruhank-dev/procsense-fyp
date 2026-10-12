# p1-dataset-analysis-s02 — Numeric profiles: labs and vital signs

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Profile labs (MIMIC-IV demo, lab bands) and vital signs (VitalDB, MIMIC-IV-ED triage): distribution per analyte and abnormal flag, borderline zones at band edges, missingness, units, pairwise correlations of linked analytes (WCC and CRP, urea and creatinine, Hb and haematocrit), triage vital joint distributions for abdominal complaints.

## Why
A generator designed without this produces random numbers, not realistic patients.

## How to implement
1. `tools/data/profile_numeric.py` reads the git-ignored cache, writes aggregates only to `tools/data/derived/profiles/`.
2. Report per analyte: n, p1, p5, p50, p95, p99, abnormal share, missing share, skew; plots to `docs/research/physio-plots/`.
3. Correlation matrix for linked analytes with n and confidence; mark estimates from fewer than 50 pairs as weak (OQ-02).
4. Write `docs/research/datasets/profiles/numeric.md`.

## Verification
Profiles reproduce the existing `mimic_lab_percentiles.json` p5 and p95; every analyte in `lab_bands.csv` has a profile or a stated gap.

## Files changed
`tools/data/profile_numeric.py`, `tools/data/derived/profiles/*.json`, `docs/research/datasets/profiles/numeric.md`

## Dependencies
p1-dataset-analysis-s01

## Common issues
Demo data is small: say so, never extrapolate silently.

## Sources
DS-0001, DS-0003, DS-0004.
