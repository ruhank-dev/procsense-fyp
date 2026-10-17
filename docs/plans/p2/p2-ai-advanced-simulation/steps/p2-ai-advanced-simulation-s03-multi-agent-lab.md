# p2-ai-advanced-simulation-s03 — Multi-Agent Clinical Simulation Lab (E49)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
Several AI characters acting together with turn-taking, one shared model on 6 GB VRAM.

## Why
Team communication is a core competency.

## How to implement
1. ReactionQueue orchestration; shared model; latency budget.

## Status (17 October 2026)

- **Measured, not built** (`tools/ai/eval_multiagent.py`, `reports/ai/multiagent-eval.json`, qwen3.5:4b-q4_K_M, 12 turns): the patient, the nurse and the anaesthetist answer one trainee turn through the single model slot, one after another. First reply p50 2.32 s, p95 2.6 s; all three p50 6.91 s, p95 7.14 s; none of the turns had all three within 4.0 s; 0 percent fell back. **The target (p95 under 4 s with three roles) is not met** on this GPU.
- **Reading:** speak the first reply at once and let the Core's ReactionQueue (priority, debounce, one spontaneous remark per 8 s) stagger the others; a second slot would need a second model in memory, which the 6 GB card cannot hold. Orchestration code (`server/agents/`) is not built because the measurement shows the Core queue, not a new module, is the lever (DT-215).

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
