# p1-live-scenario-generation-s09 — Novelty fingerprints and provenance (LG-13)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Structured three-level fingerprints, weighted Jaccard distances to trainee history and library, thresholds, fallback; provenance record and seed reproducibility.

## Why
Fresh means new coherent combinations, measurably.

## How to implement
1. `server/generation/novelty.py`; datasketch MinHash when history grows.
2. Fingerprint schema in the package schema.
3. Simulation over 1,000 synthetic trainees.

## Verification
Novelty target met (spec section 6); fingerprints deterministic.

## Files changed
`server/generation/novelty.py`

## Dependencies
p1-live-scenario-generation-s08

## Common issues
Over-novelty hurts learning: thresholds tunable (OQ-04).

## Sources
Spec LG-13; A6, A13.
