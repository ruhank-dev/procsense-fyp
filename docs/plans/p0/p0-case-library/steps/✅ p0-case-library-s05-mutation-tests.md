# p0-case-library-s05 — Mutation generator (EXP-CASE-1)

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Inject each of the 12 contradiction types into valid cards (e.g. medication without indication; normal WBC + sepsis progression event) and prove Stage B rejects all of them.

## Why
A validator that has never been fooled on purpose proves nothing. Mutations generate the exact mistakes a careless author or the AI composer (P1) could make, and prove each one is caught before it reaches a trainee.

## How to implement
1. `tools/composer/mutate.py --type ALL --n 5`.
2. CI test asserts 12/12 types rejected (T-VB1).
3. Mutations per type: 5 per type over different base cases (`--n 5`); each mutated card must fail with the expected defect code.
4. Report table: type → rejected count → defect code.

## Verification
CI test asserts 60/60 mutated cards rejected with the right codes (12 types × 5).

## Files changed
`tools/composer/mutate.py`, test, README experiment log.

## Dependencies
p0-case-library-s04.

## Common issues
Mutations that accidentally produce a *valid* case (e.g. mutation lands on an optional field) look like validator misses — the mutator must assert the mutation actually broke the rule it targets.

## Sources
Implementation Guide B-M2 Step 5, T-VB1; Master Plan §41 T-AI-01.
