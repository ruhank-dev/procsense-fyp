# p2-ai-learning-analytics-s05 — Investigation Interpreter (B22)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Practice-mode explanation of an opened result, grounded in its approved report and bands.

## Why
Turns results into teaching moments.

## How to implement
1. Validator-gated LLM rewrite; off in assessment.

## Verification
Zero validator escapes.

## Files changed
`server/analytics/interpreter.py`

## Dependencies
p1-ai-language-s08

## Common issues
Explanations that go beyond the report: validator rejects.

## Sources
B22.
