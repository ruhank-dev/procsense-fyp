# p1-personalisation-s02 — Bayesian learner model

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Beta-binomial mastery per competency with evidence weights and a forgetting factor (optionally Bayesian knowledge tracing), pattern status (hypothesis until supported by at least 3 sessions), event-response and interpretation statistics, trends, and empirical difficulty calibration (A11.c2). Updated cheaply after each session; recalibrated offline.

## Why
Separates a demonstrated weakness from a single slip and states how sure it is.

## How to implement
1. `server/personalisation/learner_model.py`.
2. Simulated learners with known mastery for recovery tests.
3. Comparison with Elo on next-session prediction.

## Status (17 October 2026)

- **Built:** `server/personalisation/learner_model.py` (Beta-binomial per behaviour, fading 0.9 a session, severe errors weigh 2 and 3, 80 percent intervals, statuses unknown, hypothesis, weakness, strength, developing) and `simulate.py` (learners with a hidden mastery).
- **Verification result:** one slip never makes a weakness (it makes a hypothesis); three sessions of failure do; intervals cover the truth 79 percent of the time at a nominal 80; next-session log-loss 0.7187 against Elo 0.7216 over 10,821 simulated items: lower, by a small margin. Not built: the Bayesian knowledge tracing option.

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
