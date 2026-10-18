# p1-dataset-analysis-s02 — Numeric profiles: labs and vital signs

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Profile labs (MIMIC-IV demo, lab bands) and vital signs (VitalDB, MIMIC-IV-ED triage): distribution per analyte and abnormal flag, borderline zones at band edges, missingness, units, pairwise correlations of linked analytes (WCC and CRP, urea and creatinine, Hb and haematocrit), triage vital joint distributions for abdominal complaints.

## Why
A generator designed without this produces random numbers, not realistic patients.

## How to implement
1. `tools/data/profile_numeric.py` reads the git-ignored cache, writes aggregates only to `tools/data/derived/profiles/`.
2. Report per analyte: n, p1, p5, p50, p95, p99, abnormal share, missing share, skew; plots to `docs/research/physio-plots/`.
3. Correlation matrix for linked analytes with n and confidence; mark estimates from fewer than 50 pairs as weak (OQ-02).
4. Write `docs/research/datasets/profiles/numeric.md`.

## Status (17 October 2026)

- **Built:** `tools/data/profile_numeric.py` (`--check` in CI, tests `tools/ci/test_profile_numeric.py`) writes `tools/data/derived/profiles/labs.json` and `vitals.json` and the report `docs/research/datasets/profiles/numeric.md`: for 19 lab analytes n, p1 to p99, missing share, skew, the dataset's abnormal share, the share outside the walkthrough's normal band and the borderline share; 7 linked-analyte correlations (Pearson, Spearman, 95 % interval, weak under 50 pairs); triage vitals for all ED rows and for abdominal complaints (222 and 27 rows) with their joint correlations; the VitalDB levels and noise quoted.
- **Verification result:** the profiles reproduce the earlier `mimic_lab_percentiles.json` p5, p95 and n for all 14 earlier analytes; every analyte of `lab_bands.csv` has a profile or a stated gap.
- **Not built:** the plots (`docs/research/physio-plots`): there is no plotting library in the tools environment, so the report has tables only. Urea, haematocrit, ggt, lipase, HbA1c and neutrophils are profiled in the dataset's own unit and not compared with a band (the walkthrough prints no conversion factor). The abdominal triage group has 27 rows (weak). The respiratory rate of VitalDB is the least reliable of its six variables.

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
