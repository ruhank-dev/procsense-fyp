# p1-live-scenario-generation-s08 — Quality evaluation, repair and rejection (LG-11, LG-12)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Deterministic quality metrics as the gate, advisory LLM judge, bounded repair strategy table, fallback to pool or library.

## Why
Valid is not the same as good for teaching.

## How to implement
1. `server/generation/quality.py`, `repair.py`.
2. Judge calibration against human ratings before it influences repair.
3. Trap-fairness check for adversarial blocks (A7).

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
