# p1-live-scenario-generation-s13 — AI scenario plan composer: LLM with retrieval, structured generation and solver completion (LG-04)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
The live and batch composer: an LLM receives objectives, difficulty, history summary, a retrieved knowledge-base subgraph and exemplar cases, and returns a ScenarioPlan (problems, comorbidities, investigations with expected findings and band targets, decision points with traps, events, branches, outcomes, rationale per choice) as schema-constrained JSON over approved ids; the deterministic solver completes and checks it; Stage B and cross-modal rules validate it.

## Why
Random recombination is not AI generation; this is the component that composes coherent cases from evidence and explains them.

## How to implement
1. `server/generation/compose_plan.py`; schema `server/schemas/scenario_plan.json` with enums of KB concept and block ids.
2. Prompt `prompts/compose_plan/v1.txt` with rationale fields; every relation the plan relies on must exist in the KB.
3. Solver completion and constraint check; defects returned as avoid-hints (max 3 rounds).
4. Blind comparison with the solver-only baseline (40 cases, two raters).
5. Latency measured on both GPUs; batch mode fills the pool.

## Verification
Schema validity at least 95 percent; coherence and objective coverage higher than solver-only (paired, p under 0.05) at equal Stage B pass rate.

## Files changed
`server/generation/compose_plan.py`, `server/schemas/scenario_plan.json`, `prompts/compose_plan/`

## Dependencies
p1-dataset-analysis-s06, p1-live-scenario-generation-s03, p1-ai-infrastructure-s02, p1-personalisation-s03

## Common issues
Model invents relations: enum schema plus KB relation check reject them.

## Sources
Features A1, A2, A4, A5, A7, A8.
