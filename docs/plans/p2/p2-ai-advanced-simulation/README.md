# p2-ai-advanced-simulation

**Tier:** P2 · **Builds on:** p1-ai-language, p1-live-scenario-generation, p1-pulse-physiology · **Feeds:** research write-up

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## Goal
Research features that stretch the simulator: a red-team generator for every AI gate, a persistent patient digital twin, a multi-agent clinical lab, offline generative image and waveform synthesis (E64) and conditional LLM adaptation (E65).

## Scope
- **In:** prototypes with evaluation.
- **Out:** anything that changes rules or scoring.

## Steps

| Step | Title |
|---|---|
| [p2-ai-advanced-simulation-s01](steps/✅%20p2-ai-advanced-simulation-s01-red-team-generator.md) | Adversarial generator and red-team suite (E50) |
| [p2-ai-advanced-simulation-s02](steps/✅%20p2-ai-advanced-simulation-s02-patient-digital-twin.md) | Patient Digital Twin (E48) |
| [p2-ai-advanced-simulation-s03](steps/p2-ai-advanced-simulation-s03-multi-agent-lab.md) | Multi-Agent Clinical Simulation Lab (E49) |
| [p2-ai-advanced-simulation-s04](steps/p2-ai-advanced-simulation-s04-generative-image-research.md) | Generative image and waveform synthesis research track (E64, B15.c2) |
| [p2-ai-advanced-simulation-s05](steps/p2-ai-advanced-simulation-s05-llm-adaptation-research.md) | LLM adaptation research (E65) |

## Exit
Every step's verification passes and `uv run --project tools tools/plans/validate_pipeline.py` still passes with the stage statuses updated.

## Sources
Planning update of 12 October 2026: `docs/architecture/` (feature registry, live generation spec, master roadmap), ADR-0008.
