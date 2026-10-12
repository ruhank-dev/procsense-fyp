# p1-personalisation-s04 — LLM performance interpreter

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
An LLM reads a structured profile summary and proposes hypotheses about learning needs (linked weaknesses, likely misconceptions) with cited evidence ids and confidence; stored as hypotheses and checked against later evidence.

## Why
Relates errors across competencies in ways counts cannot.

## How to implement
1. Prompt and schema; evidence-id check.
2. Confirmation tracking.

## Verification
Hypotheses confirmed by later evidence more often than random competency picks.

## Files changed
`server/personalisation/interpreter.py`

## Dependencies
p1-personalisation-s02, p1-ai-infrastructure-s02

## Common issues
Treating hypotheses as facts: they never change mastery directly.

## Sources
Feature D67.
