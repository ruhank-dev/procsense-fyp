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

## Status (17 October 2026)

- **Built:** `tools/synth/card_schema.json`, `tools/synth/publish.py` (refuses an incomplete card, checks generators, files and row counts, writes the index) and five cards SD-001 to SD-005 in `docs/research/datasets/synthetic/` for the synthetic datasets that exist (text inconsistencies, generated goldens, difficulty vectors, simulated learners, gold scenarios and queries). `tools/ci/test_synth_cards.py` covers T-SD1 and T-SD4.
- **Not built:** the LLM generators of utterances and handovers (they waited for the classifier dataset step, removed on 27 Oct 2026, ADR-0029); a spot-check of 50 rows by a person (T-SD3).

## Verification
T-SD1–T-SD4 green; card index lists every dataset.

## Files changed
`tools/synth/card_schema.json`, `tools/synth/publish.py`, `tools/synth/gen_*.py`, `data/<dataset>/card.yaml`, card index `docs/research/datasets/README.md`.

## Dependencies
the classifier dataset step (removed 27 Oct 2026, ADR-0029).

## Common issues
LLM repeats itself → vary seeds/temperature; dedupe.

## Sources
Implementation Guide B-M4, T-SD1–T-SD4.
