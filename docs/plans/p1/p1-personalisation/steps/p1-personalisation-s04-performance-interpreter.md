# p1-personalisation-s04 — LLM performance interpreter

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
An LLM reads a structured profile summary and proposes hypotheses about learning needs (linked weaknesses, likely misconceptions) with cited evidence ids and confidence; stored as hypotheses and checked against later evidence.

## Why
Relates errors across competencies in ways counts cannot.

## How to implement
1. Prompt and schema; evidence-id check.
2. Confirmation tracking.

## Status (17 October 2026)

- **Built:** `server/personalisation/interpreter.py` and the prompt `personalisation.interpret`: the model reads the profile (competency ids, sessions, share right, status) and proposes up to three hypotheses with cited competency and evidence ids (enums reject invented ones), confidence low with thin evidence; a `Tracker` marks a hypothesis confirmed or refuted by later sessions and never changes mastery.
- **Measured** (`tools/ai/eval_interpreter.py`, 9B, 20 simulated learners): 40 hypotheses accepted; confirmed by later sessions 67.5 percent against 70.0 percent for random weak competencies.

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
