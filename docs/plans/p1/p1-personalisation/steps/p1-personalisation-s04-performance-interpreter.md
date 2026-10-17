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
- **Measured, first version** (17 October, 9B, 20 simulated learners): 40 hypotheses accepted; confirmed by later sessions 67.5 percent against 70.0 percent for random weak competencies, i.e. no better than chance (the model sometimes cited competencies that were strengths).
- **Changed and measured again** (18 October): the profile now lists weaknesses and hypotheses first, the lowest share first; a new rule refuses a hypothesis that cites a competency not marked weakness or hypothesis (`NOT_A_WEAKNESS`); `prompts/personalisation/interpret_v2.txt` asks for weaknesses only, preferring the lowest share on the most sessions (v1 kept; `Interpreter(version=)`, `eval_interpreter.py --version`). The rule alone (prompt v1) refused most answers: 12 hypotheses from 4 of 20 learners, 75 percent against 50 percent for random. **Prompt v2: 38 hypotheses from 14 of 20 learners, confirmed 84.2 percent against 73.7 percent for random picks (margin 10.5 points, the gate asks 5), 47 s a learner.** The gate passes and the feature is on.
- **Limit:** the learners are simulated (a hidden weak dimension), not people; the margin comes from 38 correlated hypotheses and has a wide interval; 6 of 20 learners got no hypothesis at all. A hypothesis is only ever shown to a teacher as a hypothesis and never changes the learner model.

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
