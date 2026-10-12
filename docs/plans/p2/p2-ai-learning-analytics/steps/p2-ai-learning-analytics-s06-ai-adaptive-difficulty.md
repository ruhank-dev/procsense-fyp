# p2-ai-learning-analytics-s06 — AI enhancement to adaptive difficulty (D44)

Milestone: **M15 P2 learning analytics and advanced simulation**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Rank the Selector's valid next cases using patterns and explain the choice; the Elo rating stays the decider.

## Why
Better targeting without losing explainability.

## How to implement
1. LLM or simple scoring ranks only within the valid set.

## Verification
Never selects outside the valid set; A/B on simulated trainees.

## Files changed
`server/analytics/next_case.py`

## Dependencies
p2-ai-learning-analytics-s01, p1-scenario-composer-difficulty-s04

## Common issues
Opaque ranking: always show the rule facts.

## Sources
D43, D44, E56.
