# p1-scenario-composer-difficulty-s05 — Composed cases to approval (EXP-COMP-2)

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Produce at least 8 S1 and 6 S2 composed cases that pass Stage B, send them to the medical professional in ≤ 45-minute review packs, and add approved ones to the practice pool (never assessment until separately approved).

> **Revised 12 October 2026 (ADR-0008):** medical review packs are optional research input, not a gate. Cases that pass Stage B and the automated checks enter the practice pool labelled GENERATED_PRACTICE_ONLY; assessment use still needs separate approval. The original wording above is kept for history.

## Why
This is the Master Plan's Definition-of-Done target for the composer and proves it can extend practice variety safely.

## How to implement
1. Batch compose; review pack; import decisions.
2. Compose candidates per scenario until ≥ 8 S1 / ≥ 6 S2 pass Stage B; prefer diversity (different problem sets).
3. Review pack per batch (≤ 45 min); approved cards get status APPROVED_FOR_TRAINING; rejected ones archived with reasons.
4. Track per candidate: Stage B result, reviewer decision, reason — in `docs/research/review-packs/<date>-composed.md`.

## Verification
Counts met with approval records; composed cases playable via golden correct path.

## Files changed
`content/cases/COMPOSED-*.json` (approved), `docs/research/review-packs/<date>-composed.md`, Stage B reports.

## Dependencies
p1-scenario-composer-difficulty-s03.

## Common issues
Composed cases that look alike → bias the solver toward rarely used blocks and different targets.

## Sources
Master Plan §28, §41 (DoD: ≥ 8 S1 / ≥ 6 S2 approved cases); Implementation Guide B-M1, B-M2, B-M17.
