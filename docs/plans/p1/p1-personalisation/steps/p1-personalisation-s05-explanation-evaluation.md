# p1-personalisation-s05 — Why-this-scenario explanations and personalisation evaluation

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Grounded explanation of each choice from objective facts; evaluation with simulated learners, off-policy estimates and a pilot comparing personalised generated practice with Elo selection on targeted-competency improvement.

## Why
Personalisation must be explainable and proven.

## How to implement
1. Explanation prompt behind the validator.
2. Simulation harness; pilot design in p1-evaluation-s03.

## Status (17 October 2026)

- **Built:** `server/personalisation/explain.py` (template explanation from the facts of the choice; `check` rejects a number that is not a fact and a hypothesis called a weakness) and `server/personalisation/experiment.py` with `tools/personalisation/simulate.py` (elo, rules and bandit on the same simulated learners, paired bootstrap, an inverse-propensity estimate for the bandit).
- **Verification result:** zero ungrounded claims in the template output (tests). **The second criterion was not met:** personalised practice did not show a larger improvement than Elo selection in the simulation (see s03); the pilot is p1-evaluation-s03 (DT-206).

## Verification
Zero ungrounded explanation claims; personalised practice shows larger improvement on targeted competencies than Elo selection in simulation.

## Files changed
`server/personalisation/explain.py`, `tools/personalisation/simulate.py`

## Dependencies
p1-personalisation-s03, p1-evaluation-s03

## Common issues
Confounding in the pilot: randomise.

## Sources
Features D68, E56.
