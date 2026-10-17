# p1-live-scenario-generation-s14 — Learned conditional generation of results (LG-07)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Fit per-condition joint models (Gaussian copula where data allows, Bayesian network over discretised findings with KB structure otherwise) offline and sample them live with named seed streams.

## Why
Keeps relationships between results (sepsis markers rise together) that independent band draws break.

## How to implement
1. `tools/generation/fit_joint.py` writes versioned model packs.
2. `server/generation/numeric.py` samples them; band and plausibility checks after sampling.
3. Compare with independent band sampling on correlation error and VR-CM violations.

## Status (17 October 2026)

- **Built:** `tools/generation/fit_joint.py` fits a Gaussian copula over the 19 analytes of the MIMIC-IV demo (pairs with at least 30 observations, kept if the 95 percent interval excludes zero, shrunk by n/(n+20), made positive definite): 92 correlated pairs of 131, written to `tools/data/derived/profiles/joint.json` and, through `build_packs.py`, to the pack `joint.json`. `LabPack.draw_panel` draws a whole panel together (the order asked does not change a value) and `numeric.vary_numbers` uses it for all the free results of a case; the earlier pair method remains when the pack has no joint model.
- **Verification result** (`tools/ci/test_generation_joint.py`): the matrix is a valid correlation matrix; drawn panels keep the correlations of the data with less than half the error of independent draws; every value is inside its zone; the property tests over 200 seeds (skeleton, cross-modal) pass with zero rule violations.
- **Limit:** whole-dataset marginals and correlations, not a model per condition: the demo has 100 patients and is not split by condition (the card says so).

## Verification
Correlation error at least 50 percent lower than baseline; zero VR-CM violations over 10,000 seeds.

## Files changed
`tools/generation/fit_joint.py`, `server/generation/numeric.py`

## Dependencies
p1-dataset-analysis-s05, p1-dataset-analysis-s06

## Common issues
Small data: fall back to KB-structured models and say so in the pack card.

## Sources
Features B14, A2.
