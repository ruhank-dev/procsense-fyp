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

## Status (18 October 2026)

- **Not built.** Under ADR-0008 a clinician's approval is no longer a gate, so that is not what holds it back. What does: the composer (`tools/composer/blocks.py`) varies who the patient is and never the clinical story, so a new combination of problems cannot come from it; each combination has to be authored value by value from the sourced tables (a source row for each new value) and then pass Stage B and the golden runs. The first candidate and the work are recorded as DT-220.

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
