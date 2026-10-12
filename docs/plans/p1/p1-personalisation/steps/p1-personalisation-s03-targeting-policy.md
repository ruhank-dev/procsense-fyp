# p1-personalisation-s03 — Targeting policy: from rules to a contextual bandit

Milestone: **M16 P1 AI infrastructure and personalisation — inference hosts, dual-GPU benchmark, labelling, learner model**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Turn the profile into Objectives v1 for the composer: rule-based targeting of demonstrated weaknesses with spacing and novelty, then a contextual bandit (Thompson sampling over scenario feature arms) once enough sessions exist, adopted only after off-policy evaluation beats the rules.

## Why
Changes the substance of the next case (problems, traps, investigations, events), not only its label.

## How to implement
1. `server/personalisation/policy.py` (rules, bandit, logged propensities).
2. Objectives feed LG-02 and the composer (p1-live-scenario-generation-s13).
3. Exploration caps; practice only.

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
