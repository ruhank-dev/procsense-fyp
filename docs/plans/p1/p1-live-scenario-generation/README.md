# p1-live-scenario-generation

**Tier:** P1 · **Builds on:** p1-dataset-analysis, p1-scenario-composer-difficulty (blocks, solver, Stage A), p0-case-library (Stage B), p1-backend, p1-ai-language · **Feeds:** p1-evaluation, p2-ai-learning-analytics

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Generate a fresh, coherent, validated practice scenario for each session from the trainee's objectives, using the cases and datasets as templates, evidence and evaluation material: the 21-stage pipeline LG-01 to LG-21.

## Scope
- **In:** practice-mode generation, validation, novelty, provenance, packaging, pooled offline fallback.
- **Out:** assessment-mode generation (assessment keeps fixed cases); retraining during sessions; image-generating models.

## Steps

| Step | Title |
|---|---|
| [p1-live-scenario-generation-s01](steps/✅%20p1-live-scenario-generation-s01-request-objectives-schemas.md) | Session request, objectives and schemas (LG-01, LG-02) |
| [p1-live-scenario-generation-s02](steps/✅%20p1-live-scenario-generation-s02-retrieval-index.md) | Retrieval index and context retrieval (OFF-05, LG-03) |
| [p1-live-scenario-generation-s03](steps/✅%20p1-live-scenario-generation-s03-selection-skeleton.md) | Component selection (solver) and scenario skeleton (LG-04 baseline, LG-05) |
| [p1-live-scenario-generation-s04](steps/✅%20p1-live-scenario-generation-s04-ai-composition.md) | AI composition: narrative, profile wording, persona (LG-06) |
| [p1-live-scenario-generation-s05](steps/✅%20p1-live-scenario-generation-s05-numeric-generation.md) | Numeric generation: labs, vitals, trends, anomalies (LG-07) |
| [p1-live-scenario-generation-s06](steps/✅%20p1-live-scenario-generation-s06-media-handling.md) | ECG, ultrasound and imaging selection (LG-08) |
| [p1-live-scenario-generation-s07](steps/✅%20p1-live-scenario-generation-s07-crossmodal-validation.md) | Cross-modal consistency and Stage B on generated packages (LG-09, LG-10) |
| [p1-live-scenario-generation-s08](steps/✅%20p1-live-scenario-generation-s08-quality-repair.md) | Quality evaluation, repair and rejection (LG-11, LG-12) |
| [p1-live-scenario-generation-s09](steps/✅%20p1-live-scenario-generation-s09-novelty-provenance.md) | Novelty fingerprints and provenance (LG-13) |
| [p1-live-scenario-generation-s10](steps/✅%20p1-live-scenario-generation-s10-packaging-unity.md) | Packaging, pre-generated pool and Unity delivery (LG-14, LG-15) |
| [p1-live-scenario-generation-s11](steps/✅%20p1-live-scenario-generation-s11-live-loop.md) | Live loop on generated cases: events, branches, scoring (LG-16 to LG-18) |
| [p1-live-scenario-generation-s12](steps/✅%20p1-live-scenario-generation-s12-debrief-history-feedback.md) | Grounded debrief, history and the offline feedback loop (LG-19 to LG-21) |
| [p1-live-scenario-generation-s13](steps/p1-live-scenario-generation-s13-ai-scenario-composer.md) | AI scenario plan composer: LLM with retrieval, structured generation and solver completion (LG-04) |
| [p1-live-scenario-generation-s14](steps/✅%20p1-live-scenario-generation-s14-learned-conditional-generation.md) | Learned conditional generation of results (LG-07) |
| [p1-live-scenario-generation-s15](steps/✅%20p1-live-scenario-generation-s15-consistency-critic-reports.md) | Generated reports and the cross-modal text consistency critic |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
