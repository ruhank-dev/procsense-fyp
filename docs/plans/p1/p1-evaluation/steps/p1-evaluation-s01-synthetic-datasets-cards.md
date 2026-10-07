# p1-evaluation-s01 — Synthetic datasets and cards

Milestone: **M12 P1 evaluation**

## What
Card template enforced by the pipeline (purpose, generator, source, method, constraints, validation, review, labels, model use, runtime vs dev-only); rule-based generators (results, events, edge cases, contradictions) and LLM generators (utterances, handovers with controlled omissions, patient questions); every row tagged `synthetic: true`, dataset_id, generator_version.

## Why
AI tests and the classifier depend on synthetic data; without cards (purpose, generator, constraints, labels) nobody can tell later what the data is, so synthetic data could be mistaken for truth (T-AI-11).

## How to implement
1. `tools/synth/card_schema.json`; `publish.py` refuses incomplete cards (T-SD1).
2. Generators deterministic with seed (T-SD2).
3. 18 dataset types listed in the Master Plan AI section get a card each as they're created.
4. Spot-check 50 rows per generated dataset (T-SD3 ≥ 90% usable).

## Verification
T-SD1–T-SD4 green; card index lists every dataset.

## Files changed
`tools/synth/card_schema.json`, `tools/synth/publish.py`, `tools/synth/gen_*.py`, `data/<dataset>/card.yaml`, card index `docs/research/datasets/README.md`.

## Dependencies
p1-ai-language-s06.

## Common issues
LLM repeats itself → vary seeds/temperature; dedupe.

## Sources
Implementation Guide B-M4, T-SD1–T-SD4.
