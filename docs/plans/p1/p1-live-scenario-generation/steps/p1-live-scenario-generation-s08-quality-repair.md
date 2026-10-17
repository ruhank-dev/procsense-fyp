# p1-live-scenario-generation-s08 — Quality evaluation, repair and rejection (LG-11, LG-12)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Deterministic quality metrics as the gate, advisory LLM judge, bounded repair strategy table, fallback to pool or library.

## Why
Valid is not the same as good for teaching.

## How to implement
1. `server/generation/quality.py`, `repair.py`.
2. Judge calibration against human ratings before it influences repair.
3. Trap-fairness check for adversarial blocks (A7).

## Status (17 October 2026)

- **Built:** `server/generation/quality.py` (`evaluate`: teachable, discoverable, difficulty, coverage, fairness, reading, persona, coherence decide; trap fairness is advisory; the language-model judge has a slot that is logged and never changes the verdict), `server/generation/repair.py` (a table from defect code to strategies, cheapest first: redraw numbers, choose pictures again, swap blocks, swap spine; `Budget` of 3 rounds and 20 seconds) and `server/generation/pipeline.py` (`generate`: runs the whole deterministic path and repeats every check, from the first, on every repaired candidate). `build_skeleton` gained `force_spine`, `numeric_seed` and `media_seed` so a repair changes only what it means to change.
- **Verification result** (`tools/ci/test_generation_quality.py`, 14 tests): at least 90 percent of the 53 approved cases pass; every damaged copy (no actions, no correct option, silent wrong option, placeholder text, wrong pronoun, rambling sentence, duplicate options, all options correct, far from the target difficulty, an unfindable problem) is rejected on its own code; the budget is never exceeded and the fallback is named.
- **Limits:** the gold set is made by damaging approved cases, not rated by a teacher, so it measures whether the gate catches what it is built to catch (DT-196). The judge is not built. Trap fairness needs adversarial blocks that the library does not hold (DT-197). A spine with no discovery rules is reported as not measured.

## Verification
Gold poor rejected, 90 percent of gold good passed; budgets never exceeded.

## Files changed
`server/generation/quality.py`, `server/generation/repair.py`

## Dependencies
p1-live-scenario-generation-s07

## Common issues
Judge disagreement: judge stays advisory.

## Sources
Spec LG-11, LG-12; A7, A9.
