# p1-dataset-analysis

**Tier:** P1 · **Builds on:** p0-medical-research-data (registers, extracts, derived aggregates) · **Feeds:** p1-live-scenario-generation

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Understand every dataset and content table well enough to design generators that produce realistic, coherent values and media: normal, abnormal, borderline, missing, artefact and correlated patterns per modality, measured before any generator is written.

## Scope
- **In:** dataset inventory and licence refresh, numeric profiles, waveform profiles, image tagging, parameter packs, evaluation sets.
- **Out:** training any model; committing raw restricted data.

## Steps

| Step | Title |
|---|---|
| [p1-dataset-analysis-s01](steps/✅%20p1-dataset-analysis-s01-dataset-inventory-licences.md) | Dataset inventory and licence refresh |
| [p1-dataset-analysis-s02](steps/✅%20p1-dataset-analysis-s02-numeric-profiles.md) | Numeric profiles: labs and vital signs |
| [p1-dataset-analysis-s03](steps/✅%20p1-dataset-analysis-s03-waveform-profiles.md) | Waveform profiles: ECG and monitor signals |
| [p1-dataset-analysis-s04](steps/✅%20p1-dataset-analysis-s04-image-tagging.md) | Image library tagging and quality grading |
| [p1-dataset-analysis-s05](steps/✅%20p1-dataset-analysis-s05-parameter-packs-eval-sets.md) | Generator parameter packs and evaluation sets |
| [p1-dataset-analysis-s06](steps/✅%20p1-dataset-analysis-s06-scenario-knowledge-base.md) | Scenario knowledge base: relations from tables, cases, walkthrough and datasets (A66) |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
