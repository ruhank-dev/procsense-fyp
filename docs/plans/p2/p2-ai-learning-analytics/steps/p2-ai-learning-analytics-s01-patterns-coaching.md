# p2-ai-learning-analytics-s01 — Performance patterns and coaching (D39, D40)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Detect recurring errors across sessions and word coaching advice from them.

## Why
Trainees improve faster when patterns are named.

## How to implement
1. Pattern detectors over history (deterministic).
2. LLM wording via the debrief path.

## Verification
Planted patterns found in 95 percent of synthetic histories.

## Files changed
`server/analytics/patterns.py`

## Dependencies
p1-live-scenario-generation-s12

## Common issues
Over-claiming from few sessions: minimum evidence counts.

## Sources
D39, D40.
