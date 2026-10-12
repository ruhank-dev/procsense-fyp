# p1-live-scenario-generation-s14 — Learned conditional generation of results (LG-07)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Fit per-condition joint models (Gaussian copula where data allows, Bayesian network over discretised findings with KB structure otherwise) offline and sample them live with named seed streams.

## Why
Keeps relationships between results (sepsis markers rise together) that independent band draws break.

## How to implement
1. `tools/generation/fit_joint.py` writes versioned model packs.
2. `server/generation/numeric.py` samples them; band and plausibility checks after sampling.
3. Compare with independent band sampling on correlation error and VR-CM violations.

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
