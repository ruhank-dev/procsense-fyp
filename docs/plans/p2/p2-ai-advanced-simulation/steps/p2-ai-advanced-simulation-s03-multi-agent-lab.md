# p2-ai-advanced-simulation-s03 — Multi-Agent Clinical Simulation Lab (E49)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Several AI characters acting together with turn-taking, one shared model on 6 GB VRAM.

## Why
Team communication is a core competency.

## How to implement
1. ReactionQueue orchestration; shared model; latency budget.

## Status (17 October 2026)

- **Not built (DT-215):** three characters answering one trainee turn on the one model slot. Each reply takes about 1.9 s on the 4B (dialogue evaluation), so three in sequence exceed the 4 s target; the Core's ReactionQueue (priority, debounce, one spontaneous remark per 8 s) is the place to hide that, and it needs measuring with real turns.

## Verification
p95 under 4 s with three roles.

## Files changed
`server/agents/`

## Dependencies
p1-ai-language-s05

## Common issues
VRAM: one model, several prompts.

## Sources
E49.
