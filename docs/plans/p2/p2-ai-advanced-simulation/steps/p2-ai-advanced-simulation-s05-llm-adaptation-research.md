# p2-ai-advanced-simulation-s05 — LLM adaptation research (E65)

Milestone: **M15 P2 learning analytics and advanced simulation**


## What
If the benchmark and evaluation show a quality gap that prompting cannot close, train QLoRA adapters for a 1.7B to 4B model on approved, authored and validated synthetic data, offline; serve the adapter only if it beats the base model.

## Why
Revives the rejected fine-tuning feature as a conditional option.

## How to implement
1. Trigger: a documented gap in the E57 or E55 reports.
2. Dataset of approved pairs; split; card.
3. QLoRA training (local 1.7B; 4B if VRAM allows; else remote).
4. Evaluate against base on the same benchmark and validator.

## Status (17 October 2026)

- **Trigger analysed, not met (18 October 2026):** `docs/research/llm_adaptation_trigger.md`. Every remaining quality gap (debrief latency and acceptance, the interpreter, the plan composer, three-character latency) has a cheaper remedy that is built or being measured, and the only data to tune on would be model-written; fine-tuning is not started (DT-212).

## Verification
Better than base with no rise in validator rejections, or a documented decision not to adopt.

## Files changed
`tools/research/adapt_llm/`

## Dependencies
p1-ai-infrastructure-s02, p1-ai-language-s09

## Common issues
Training on unvalidated text: only approved data.

## Sources
E65.
