# p1-dataset-analysis-s01 — Dataset inventory and licence refresh

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Refresh the dataset register (DS-0001 to DS-0014) and write the licence and provenance register: for every dataset, model, library and voice the licence, what it allows, attribution text, what is committed (raw, derived, none) and the access date.

## Why
Generators must only draw on material whose licence allows the use; the register makes that checkable instead of a blanket block.

## How to implement
1. Re-read each licence page; record URL and access date in `docs/research/research_log.md`.
2. Mark each item ALLOWED, ALLOWED WITH CONDITIONS (attribution, share-alike), NOT USED or REJECTED with the reason.
3. Add the model and library rows (MC-01 to MC-13, EXT-*) from the pipeline JSON.

## Verification
Every DS, MC and EXT id in the pipeline JSON has a register row; `tools/data/dataset_cards.py --check` passes.

## Files changed
`docs/research/licence_provenance_register.md`, `docs/research/dataset_register.csv`

## Dependencies
p0-medical-research-data (existing registers)

## Common issues
Licence pages change; always record the access date.

## Sources
Pipeline JSON `datasets`, `external_dependencies`.
