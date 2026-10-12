# p1-dataset-analysis-s05 — Generator parameter packs and evaluation sets

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Turn the profiles into small versioned packs that live generators read (per analyte and condition distributions, correlation matrices, trajectories, artefact rates, ECG parameters) and build the evaluation sets (gold scenarios, cross-modal mutations, retrieval queries).

## Why
Separates offline preparation from live generation: live sessions only read packs and never refit.

## How to implement
1. `tools/generation/build_packs.py` writes `content/generation/packs/*.json` with a version and SHA-256 in the content manifest; `--check` in CI.
2. Evaluation sets under `tests/generation/` with data cards.
3. Every pack row cites its profile and dataset id.

## Verification
Packs rebuild byte-identically; sampling 10,000 values from a pack reproduces its profile within tolerance.

## Files changed
`tools/generation/build_packs.py`, `content/generation/packs/`, `tests/generation/`

## Dependencies
p1-dataset-analysis-s02, p1-dataset-analysis-s03, p1-dataset-analysis-s04

## Common issues
Packs must never contain raw rows.

## Sources
Live generation spec section 3.
