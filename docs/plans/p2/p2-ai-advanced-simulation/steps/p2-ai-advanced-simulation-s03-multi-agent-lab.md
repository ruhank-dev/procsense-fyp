# p2-ai-advanced-simulation-s03 — Multi-Agent Clinical Simulation Lab (E49)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Several AI characters acting together with turn-taking, one shared model on 6 GB VRAM.

## Why
Team communication is a core competency.

## How to implement
1. ReactionQueue orchestration; shared model; latency budget.

## Verification
p95 under 4 s with three roles.

## Files changed
`server/agents/`

## Dependencies
p1-ai-language-s05

## Common issues
VRAM: one model, several prompts.

## Sources
E49.
