# p2-ai-learning-analytics-s02 — Confidence calibration coach (D41)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Capture confidence at decision points; Brier score and calibration curve; coaching on over- and under-confidence.

## Why
Calibration is a recognised safety skill.

## How to implement
1. Confidence prompt (Unity UI, later).
2. Metrics from logs.

## Status (17 October 2026)

- **Built:** `server/analytics/calibration.py`: Brier score, calibration curve, signed gap, coaching from numbers, at least 10 reports before it speaks. Reproduces from logs (tests). Confidence reports are not recorded by the engine yet (hook 8 of `docs/architecture/unity_ai_hooks.md`).

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
