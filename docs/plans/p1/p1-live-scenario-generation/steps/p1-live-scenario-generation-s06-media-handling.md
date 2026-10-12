# p1-live-scenario-generation-s06 — ECG, ultrasound and imaging selection (LG-08)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Tag retrieval from the media index; seeded ECG variants (NeuroKit2 or template); report text filled from slots.

## Why
Images must show exactly the case's findings.

## How to implement
1. `server/generation/media.py`.
2. ECG variant renderer reusing `tools/data/ecg_images.py`.
3. Licence filter.

## Verification
Zero tag mismatches; ECG measured rate within 5 bpm.

## Files changed
`server/generation/media.py`

## Dependencies
p1-dataset-analysis-s04, p1-dataset-analysis-s03

## Common issues
Library gaps: written-only result or block swap.

## Sources
Spec LG-08; B15, B16, B17.
