# p1-case-library-extended-s04 — Golden runs and VR checks for P1 cases

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Make all 16 P1 cases (10 variants + 6 stacked) playable and proven: for each, a correct-path and at least one wrong-path golden script derived from the walkthrough, a VR playthrough on the Quest, headless replay of the VR session with matching end state, and a short video for the medical reviewer.

## Why
P1 cases must meet the same bar as P0 — reproducible, walkthrough-faithful, reviewed — before entering the practice pool.

## How to implement
1. As p0-scenario steps.
2. Per case: correct + one wrong path golden script from the walkthrough; VR playthrough; replay agreement; reviewer video.
3. Maternity (VAR-S2-H) and minor-consent (VAR-S2-I) cases reviewed specifically for institutional rules.

## Verification
16/16 cases reach expected end states in VR and headless; reviewer notes logged.

## Files changed
`tests/golden/*`, results.

## Dependencies
p1-case-library-extended-s03.

## Common issues
Cases that need P1-only engine features (maternity tool, minor consent) fail until p1-case-library-extended-s03 lands — run those last.

## Sources
Walkthrough Parts 12–13, D; inventory D459–D482; Implementation Guide B-M19 T-CASE1/2.
