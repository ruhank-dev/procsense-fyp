# p1-personalisation-s02 — Bayesian learner model

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Beta-binomial mastery per competency with evidence weights and a forgetting factor (optionally Bayesian knowledge tracing), pattern status (hypothesis until supported by at least 3 sessions), event-response and interpretation statistics, trends, and empirical difficulty calibration (A11.c2). Updated cheaply after each session; recalibrated offline.

## Why
Separates a demonstrated weakness from a single slip and states how sure it is.

## How to implement
1. `server/personalisation/learner_model.py`.
2. Simulated learners with known mastery for recovery tests.
3. Comparison with Elo on next-session prediction.

## Verification
Lower next-session log-loss than Elo; one slip never flips a competency to 'demonstrated weakness'.

## Files changed
`server/personalisation/learner_model.py`

## Dependencies
p1-personalisation-s01

## Common issues
Too little data early: wide intervals shown, no strong claims.

## Sources
Features D67, A11, D45.
