# p2-ai-learning-analytics-s01 — Performance patterns and coaching (D39, D40)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Detect recurring errors across sessions and word coaching advice from them.

## Why
Trainees improve faster when patterns are named.

## How to implement
1. Pattern detectors over history (deterministic).
2. LLM wording via the debrief path.

## Status (17 October 2026)

- **Built:** `server/analytics/patterns.py`: repeated error, repeated miss, late step, critical trend and behaviour slide detectors with minimum evidence (3 sessions, half of the sessions where it could occur); neutral template coaching. **Verification result:** planted patterns found in at least 95 percent of 120 synthetic histories; noise alone gave no pattern in at least 95 percent; fewer than three sessions give none (`tools/ci/test_analytics.py`).

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
