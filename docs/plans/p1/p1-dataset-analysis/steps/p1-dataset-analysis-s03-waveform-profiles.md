# p1-dataset-analysis-s03 — Waveform profiles: ECG and monitor signals

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Profile PTB-XL classes needed by cases (sinus, tachycardia, AF, hyperkalaemia-like changes): rate, PR, QRS, QT distributions, noise; VitalDB artefact rates and episode slopes (hypotension, desaturation).

## Why
ECG variants and monitor behaviour must stay inside what real recordings show.

## How to implement
1. `tools/data/profile_waveforms.py`; aggregates only.
2. Class coverage table against case needs; list classes needing parametric variation (OQ-05).
3. Try NeuroKit2 `ecg_simulate` parameters that match each class's rate and noise (design note only; generator built in p1-live-scenario-generation-s06).

## Verification
Coverage table complete; every needed rhythm has a source (real strip or parametric plan).

## Files changed
`tools/data/profile_waveforms.py`, `docs/research/datasets/profiles/waveforms.md`

## Dependencies
p1-dataset-analysis-s01

## Common issues
PTB-XL labels are statement-level; check by eye for the strips used.

## Sources
DS-0001, DS-0002.
