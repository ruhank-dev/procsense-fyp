# p1-scenario-composer-difficulty-s02 — Constraint solver, case cards, CLI

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Deterministic solver: order slots by constraint tightness, sample compatible blocks (seeded), check requires/excludes incrementally, backtrack (≤ 10k steps, dead-end memo); output an authoritative case card + hidden facts; CLI `compose --scenario S1 --difficulty 2 --n 20 --seed 7`; walkthrough case values never contradicted.

## Why
A deterministic solver composes valid new cases without any AI and without contradicting walkthrough values, so variety grows safely.

## How to implement
1. `tools/composer/solve.py` (pure Python).
2. Cards pass Stage B (p0-case-library-s04) before review.
3. `--targets 'handover,communication'` biases PER/EVT selection.
4. Property test over 1000 seeds for excluded combos (T-CO1).
5. Card output includes `created_by: composer`, seed and template so any card can be regenerated.

## Verification
T-CO1–T-CO5 pass; ≥ 8 S1 / ≥ 6 S2 cards pass Stage B.

## Files changed
`tools/composer/solve.py`, `tools/composer/cli.py`, tests (T-CO1–T-CO5), `Taskfile.yml` (`task compose`).

## Dependencies
p1-scenario-composer-difficulty-s01.

## Common issues
Solver slow on tight templates → order slots by tightness and memoise dead ends.

## Sources
Implementation Guide B-M1 Steps 3–6, T-CO1–T-CO5.
