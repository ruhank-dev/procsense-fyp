# p1-live-scenario-generation-s03 — Component selection (solver) and scenario skeleton (LG-04 baseline, LG-05)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Use the composer solver live with difficulty bounds and a novelty pre-filter. This is the deterministic baseline and fallback of LG-04; the AI plan composer that proposes the plan arrives in p1-live-scenario-generation-s13; assemble a case-file-shaped skeleton with typed slots and discovery rules.

## Why
This is the deterministic heart: a fresh case exists before any LLM runs.

## How to implement
1. Call `tools/composer/solver.py` as a library.
2. `server/generation/skeleton.py`.
3. Property tests over 10,000 seeds.

## Verification
Zero constraint violations; every problem discoverable.

## Files changed
`server/generation/skeleton.py`

## Dependencies
p1-scenario-composer-difficulty-s02, p1-live-scenario-generation-s02

## Common issues
Unsatisfiable templates: relax optional slots in fixed order.

## Sources
Spec LG-04, LG-05; A4, A11.
