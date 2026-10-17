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

## Status (17 October 2026)

- **Built:** `tools/composer/solve.py` (Solver: slots ordered by tightness, seeded candidate order, `requires` and `excludes` checked on every partial choice against the tags of the spine and the chosen blocks, backtracking with a dead-end memo, stop after 10,000 steps with a clear error; `compose()` sets aside a spine that no block fits) and `tools/composer/cli.py` (`task compose -- compose --scenario S1 --difficulty 2 --n 20 --seed 7 [--targets ...]`, `check FILE`). The card is a full case file with `created_by: composer`, status DRAFT and a `composition` record (template, seed, spine, blocks) so any card can be made again; the old name, title, occupation and age in the story's text become the new ones; the date of birth, wristband and spoken identity are rebuilt; names and record numbers never repeat. `--targets` weights the choice of spine by words in its story.
- **Schema:** `content/schema/case_file.schema.json` accepts `created_by: composer`, a `composition` object and ids `GEN-S1-0007-001`; Stage B finds a composed case's discovery rules and walkthrough rows through its spine.
- **Verification result:** T-CO1 to T-CO5 pass (`tools/composer/test_solver.py`, 13 tests, in CI): 1,020 compositions hold no excluded combination; the same seed gives the same card; 20 S1 and 20 S2 composed cards all pass Stage B (the definition of done asks for 8 and 6).
- **Not built / limits:** Stage B checks 9 and 10 (idle and golden runs through the engine) need the engine to load a generated package (p1-live-scenario-generation-s10); the engine's discovery rules are keyed by case id and must resolve a composed case through its spine there too; the C# loader does not know `composer` yet.

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
