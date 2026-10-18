# p1-personalisation-s03 — Targeting policy: from rules to a contextual bandit

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**


## What
Turn the profile into Objectives v1 for the composer: rule-based targeting of demonstrated weaknesses with spacing and novelty, then a contextual bandit (Thompson sampling over scenario feature arms) once enough sessions exist, adopted only after off-policy evaluation beats the rules.

## Why
Changes the substance of the next case (problems, traps, investigations, events), not only its label.

## How to implement
1. `server/personalisation/policy.py` (rules, bandit, logged propensities).
2. Objectives feed LG-02 and the composer (p1-live-scenario-generation-s13).
3. Exploration caps; practice only.

## Status (17 October 2026)

- **Built:** `server/personalisation/policy.py` (rules scored by shortfall with spacing; Thompson sampling with a floor of exploration, a cap on share and logged propensities; `to_objectives` makes the objectives document, basis `learner_model`).
- **Verification result:** the rules target a demonstrated weakness in at least 90 percent of sessions when one exists (test). **The adoption gate was not met:** in the simulation of 200 learners the rules did not beat the Elo selection (difference -0.0085, interval [-0.0172, -0.0003]) and the bandit was worse (-0.0433); neither is adopted (`content/generation/policy.json`). I tried flatter and steeper weights and less exploration; none helped.

## Verification
Rules target demonstrated weaknesses in 90 percent of sessions; bandit adopted only after it beats rules in off-policy evaluation and simulation.

## Files changed
`server/personalisation/policy.py`

## Dependencies
p1-personalisation-s02

## Common issues
Bandit exploits too early: caps and minimum exploration.

## Sources
Features D68, D44, A7, B20.
