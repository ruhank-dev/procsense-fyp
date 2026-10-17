# p2-ai-learning-analytics-s06 — AI enhancement to adaptive difficulty (D44)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Rank the Selector's valid next cases using patterns and explain the choice; the Elo rating stays the decider.

## Why
Better targeting without losing explainability.

## How to implement
1. LLM or simple scoring ranks only within the valid set.

## Status (17 October 2026)

- **Built:** `server/analytics/next_case.py`: reorders only the valid set the Selector allows, by the Elo target (the decider) and the trainee's patterns, with an explanation from the matched pattern. **Verification result:** never selects outside the valid set; on 200 simulated trainees the first case exercises the trainee's pattern more often than the Elo order alone by more than 10 points (`tools/ci/test_analytics.py`). The language-model option is left out.

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
