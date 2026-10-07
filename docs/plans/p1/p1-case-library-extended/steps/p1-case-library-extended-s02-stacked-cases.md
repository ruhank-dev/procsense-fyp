# p1-case-library-extended-s02 — Transcribe the 6 remaining stacked cases

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
STK-S1-A, -B, -C and STK-S2-A, -C, -D with all 20 Part D sections (e.g. STK-S1-A: hypoglycaemia P1, SGLT2 taken today P2, HbA1c 76 P3, BP 172/100 P4 per guide example).

## Why
The six remaining stacked cases complete the walkthrough's full playable stacked-case set (Part D) and give advanced trainees prioritisation practice beyond the two P0 stacked cases.

## How to implement
1. Same as P0 stacked cases; ranks verified against Part D.
2. For each case: Part D §4 'immediately available' vs 'only revealed if asked'; §8 priorities; §9 decisions as checkpoints; §17 incorrect paths as branches; §20 debrief.
3. Verbatim check + register rows.

## Verification
Validator green; ladder ranks match Part D (T-DE1-style test per case).

## Files changed
`content/cases/STK-S1-A.json`, `STK-S1-B.json`, `STK-S1-C.json`, `STK-S2-A.json`, `STK-S2-C.json`, `STK-S2-D.json`, register rows, `tests/golden/STK-*`.

## Dependencies
p1-case-library-extended-s01.

## Common issues
Part D reuses variant letters (WT-I01) — namespaced STK- IDs avoid collisions with VAR- cases.

## Sources
Walkthrough Part D; inventory D475–D477, D479, D481–D482; Implementation Guide B-M19 Step 2, B-R17 worked example.
