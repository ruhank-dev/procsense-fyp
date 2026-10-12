# p1-personalisation

**Tier:** P1 · **Builds on:** p0-scoring-debrief (evidence), p0-case-library-s06 (ratings), p1-backend (store) · **Feeds:** p1-live-scenario-generation (objectives), p2-ai-learning-analytics

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Learn from each trainee's recorded performance and change the substance of future practice to address demonstrated needs, with uncertainty handled honestly and every choice explainable.

## Scope
- **In:** evidence schema, learner model, targeting policy, LLM interpreter, explanation, evaluation.
- **Out:** assessment decisions (personalisation is practice-only).

## Steps

| Step | Title |
|---|---|
| [p1-personalisation-s01](steps/p1-personalisation-s01-evidence-schema.md) | Session evidence record |
| [p1-personalisation-s02](steps/p1-personalisation-s02-learner-model.md) | Bayesian learner model |
| [p1-personalisation-s03](steps/p1-personalisation-s03-targeting-policy.md) | Targeting policy: from rules to a contextual bandit |
| [p1-personalisation-s04](steps/p1-personalisation-s04-performance-interpreter.md) | LLM performance interpreter |
| [p1-personalisation-s05](steps/p1-personalisation-s05-explanation-evaluation.md) | Why-this-scenario explanations and personalisation evaluation |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
