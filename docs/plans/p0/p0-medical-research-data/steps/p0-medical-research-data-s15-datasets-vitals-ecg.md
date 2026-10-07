# p0-medical-research-data-s15 — Datasets: vital-sign noise and ECG templates

Milestone: **M4 Engine complete — VAR-S1-A Steps 1–14 headless**

## What
Open, check and register VitalDB, PTB-XL and (for plausibility only) MIMIC-IV demo; extract only aggregates — per-variable noise parameters (σ, AR(1) phi) and lead-II beat templates for monitor traces — with dataset cards.

## Why
Monitor traces and vital noise look real (EXP-DATA-1); raw patient rows never ship.

## How to implement
1. For each dataset: open the page, record access terms, contents (rows, variables, population) and citation requirement in `docs/research/dataset_register.csv`.
2. `tools/data/extract_vitaldb.py` → `content/physio/noise.json`; `tools/data/extract_ptbxl.py` → `content/physio/ecg_templates.json` (normal sinus, sinus tachycardia, AF templates).
3. Dataset cards `docs/research/datasets/<id>.md` (purpose, method, constraints, what ships: aggregates only).
4. Property test EXP-DATA-2 hook: generated values stay inside case bands over 10k seeds.

## Verification
Register rows complete; extraction scripts reproducible (`uv run`); EXP-DATA-1 blind comparison scheduled with the reviewer.

## Files changed
`tools/data/*.py`, `content/physio/noise.json`, `content/physio/ecg_templates.json`, `docs/research/dataset_register.csv`, `docs/research/datasets/*.md`.

## Dependencies
p0-medical-research-data-s02.

## Common issues
PhysioNet datasets need the citation in every use (record in dataset cards) and some need credentialed access — use the open/demo parts.

## Sources
Inventory D31–D34, D38; dataset protocol in docs/research/README.md.
