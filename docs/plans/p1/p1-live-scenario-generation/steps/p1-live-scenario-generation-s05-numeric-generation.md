# p1-live-scenario-generation-s05 — Numeric generation: labs, vitals, trends, anomalies (LG-07)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Seeded sampling from parameter packs: truncated distributions per band and condition, copula for linked analytes, trends, repeats, artefacts.

## Why
Realistic, coherent numbers from data, not random draws.

## How to implement
1. `server/generation/numeric.py`; one seed stream per slot.
2. Extend EXP-DATA-2 to packs.
3. Plausibility and critical-limit checks inline.

## Verification
Band compliance 100 percent over 10,000 seeds; KS and correlation within tolerance.

## Files changed
`server/generation/numeric.py`

## Dependencies
p1-dataset-analysis-s05, p1-live-scenario-generation-s03

## Common issues
Copula with weak correlation estimates: fall back to independent draws and log.

## Sources
Spec LG-07; B14, B20, B21.
