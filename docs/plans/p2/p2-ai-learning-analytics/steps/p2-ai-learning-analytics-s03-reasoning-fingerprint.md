# p2-ai-learning-analytics-s03 — Clinical Reasoning Fingerprint (D42)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Features of reasoning from the log: order of gathering, time to key checks, premature closure, test yield.

## Why
Shows how, not only what.

## How to implement
1. Feature extractor; optional clustering; LLM narration.

## Status (17 October 2026)

- **Built:** `server/analytics/reasoning.py`: order of gathering, history before tests, time to key checks, premature closure, test yield; neutral descriptions. Reproduces from a golden session. **Limit:** the evidence holds only the actions the scoring maps to a behaviour (DT-209); clustering waits for real sessions.

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
