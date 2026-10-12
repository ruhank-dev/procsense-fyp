# p2-ai-learning-analytics-s02 — Confidence calibration coach (D41)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Capture confidence at decision points; Brier score and calibration curve; coaching on over- and under-confidence.

## Why
Calibration is a recognised safety skill.

## How to implement
1. Confidence prompt (Unity UI, later).
2. Metrics from logs.

## Verification
Metrics reproduce from logs.

## Files changed
`server/analytics/calibration.py`

## Dependencies
p2-ai-learning-analytics-s01

## Common issues
Prompt fatigue: few prompts per session.

## Sources
D41.
