# p2-ai-learning-analytics

**Tier:** P2 · **Builds on:** p1-live-scenario-generation (history), p1-ai-language (debrief, validator) · **Feeds:** p2 evaluation

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Turn session histories into learning insight: patterns, coaching, confidence calibration, a reasoning fingerprint, progression, investigation analytics and a grounded investigation interpreter; arithmetic decides, AI only words.

## Scope
- **In:** analytics over pseudonymised logs, AI wording behind the validator.
- **Out:** any change to scoring rules by AI.

## Steps

| Step | Title |
|---|---|
| [p2-ai-learning-analytics-s01](steps/p2-ai-learning-analytics-s01-patterns-coaching.md) | Performance patterns and coaching (D39, D40) |
| [p2-ai-learning-analytics-s02](steps/p2-ai-learning-analytics-s02-confidence-calibration.md) | Confidence calibration coach (D41) |
| [p2-ai-learning-analytics-s03](steps/p2-ai-learning-analytics-s03-reasoning-fingerprint.md) | Clinical Reasoning Fingerprint (D42) |
| [p2-ai-learning-analytics-s04](steps/p2-ai-learning-analytics-s04-progression-investigations.md) | Progression and investigation analytics (D45, E52) |
| [p2-ai-learning-analytics-s05](steps/p2-ai-learning-analytics-s05-investigation-interpreter.md) | Investigation Interpreter (B22) |
| [p2-ai-learning-analytics-s06](steps/p2-ai-learning-analytics-s06-ai-adaptive-difficulty.md) | AI enhancement to adaptive difficulty (D44) |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
