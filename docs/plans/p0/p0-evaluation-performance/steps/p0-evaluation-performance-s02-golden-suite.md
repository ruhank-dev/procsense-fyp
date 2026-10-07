# p0-evaluation-performance-s02 — Golden suite: 17 cases × 4 paths in CI

Milestone: **M8 S2 complete in VR**

## What
Correct / novice / dangerous / edge action scripts for all 17 P0 cases with expected end states, key facts and score reports, run in CI on every change.

## Why
The regression guard for everything; protects medical behaviour from accidental change.

## How to implement
1. Write paths from the walkthrough (Part D §16–19 for stacked cases; variant teaching points for others).
2. `tests/golden/<case>_<path>.actions.json` + expected JSON; T-SCO1 expected reports.
3. Diff-friendly expected files; deliberate updates via `task golden -- --update`.
4. Path definitions: correct = walkthrough correct path; novice = misses non-critical items (e.g. no summary, incomplete SBAR); dangerous = violates a blocker (e.g. bloods before identity, proceed with fasting breach); edge = timing/ordering corner cases (deadline on tick boundary, repeated skips).
5. Expected files contain end state, critical items, key score items and log hash; review diffs in PRs.

## Verification
`task golden` runs 68 scripts; CI shows the count; a deliberate change to one expected file makes CI fail with a readable diff.

## Files changed
`tests/golden/*`, tests.

## Dependencies
p0-case-library-s02.

## Common issues
Score changes → the golden diff in the PR explains why.

## Sources
Master Plan §41; inventory D73; B-M15 T-SCO1.
