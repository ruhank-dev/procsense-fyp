# p2-ai-learning-analytics-s05 — Investigation Interpreter (B22)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Practice-mode explanation of an opened result, grounded in its approved report and bands.

## Why
Turns results into teaching moments.

## How to implement
1. Validator-gated LLM rewrite; off in assessment.

## Status (17 October 2026)

- **Built:** `server/analytics/interpreter.py` (prompt `analytics.explain_result`): at most two sentences grounded in the result row and its approved report; numbers must be the row's, no entity outside the row and report, no prescribing; practice mode only; nothing is shown if it fails. Tests: invented number, medicine and instruction refused; never in assessment.

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
