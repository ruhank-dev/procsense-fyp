# p2-content-extensions-s02 — New combinations with the 14B.5 recipe

Milestone: **M13 P2 extensions**

## What
Compose new multi-problem cases following the walkthrough recipe (each problem typed, ranked, checked, with end state, clock and handover items); approval required.

## Why
Once the walkthrough's cases are all built, new combinations give trainees fresh practice — but only if each follows the walkthrough's recipe and is clinically approved.

## How to implement
1. Composer + recipe enforcement; review.
2. Pick target problem combinations from gaps in the P0/P1 pool (e.g. diabetes + anticoagulant + fasting breach).
3. Composer builds candidates; each must list problems, types, ladder order, checks, end state, clock and handover items (14B.5).
4. Stage B + golden correct path; review pack; approved ones enter the practice pool only.

## Verification
Approved new cases pass Stage B and golden runs; approvals recorded.

## Files changed
`content/cases/SC-NEW-*.json`, composer targets file, Stage B reports, review pack.

## Dependencies
p2-content-extensions-s01.

## Common issues
Combinations that are clinically implausible can still pass Stage B — reviewer approval is mandatory.

## Sources
Walkthrough 14B.5 (recipe); Implementation Guide B-R10 Step 5, B-M1.
