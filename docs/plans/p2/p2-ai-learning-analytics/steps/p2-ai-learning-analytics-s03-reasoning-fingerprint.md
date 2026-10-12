# p2-ai-learning-analytics-s03 — Clinical Reasoning Fingerprint (D42)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Features of reasoning from the log: order of gathering, time to key checks, premature closure, test yield.

## Why
Shows how, not only what.

## How to implement
1. Feature extractor; optional clustering; LLM narration.

## Verification
Features reproduce from golden logs.

## Files changed
`server/analytics/reasoning.py`

## Dependencies
p2-ai-learning-analytics-s01

## Common issues
Labels that sound diagnostic about the trainee: neutral wording.

## Sources
D42.
