# p1-personalisation-s05 — Why-this-scenario explanations and personalisation evaluation

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Grounded explanation of each choice from objective facts; evaluation with simulated learners, off-policy estimates and a pilot comparing personalised generated practice with Elo selection on targeted-competency improvement.

## Why
Personalisation must be explainable and proven.

## How to implement
1. Explanation prompt behind the validator.
2. Simulation harness; pilot design in p1-evaluation-s03.

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
