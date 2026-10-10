# p0-medical-research-data-s17 — Review pack for the medical professional

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
A generated review pack (≤ 45 min) bundling every open `issues.md` item (WT-I01…WT-I14, ISS-*), every Type C value with its chosen default, Type D values that need realism review (with the physiology plots), and the walkthrough Part I checklist sections relevant to finished content; decisions imported back into the register and issues log.

## Why
Conflicts must be resolved by a person, not the engine.

## How to implement
1. `tools/content/review_pack.py` → `docs/research/review-packs/<date>.md` (columns: ID, statement, source, proposed handling, Correct?, Fix, Comment).
2. `tools/content/import_decisions.py` updates `issues.md` statuses and register rows (T-RV1); rejected items excluded from assessment content (T-RV2).
3. Open clinically significant questions block that content in assessment mode (T-RV3).
4. Pack sent ≥ 7 days before the session (B-M17 Step 4).

## Verification
After a session: open-conflict count decreases; every closed item names the decider.

## Files changed
`tools/content/review_pack.py`, `tools/content/import_decisions.py`, `docs/research/review-packs/*.md`, issues, register.

## Dependencies
p0-medical-research-data-s03–s06.

## Common issues
Prioritise items that affect P0 cases; split packs that exceed 45 min.

## Sources
Implementation Guide B-M17 Steps 1–3, 5, T-RV1–T-RV3; walkthrough Part I, Part H.
