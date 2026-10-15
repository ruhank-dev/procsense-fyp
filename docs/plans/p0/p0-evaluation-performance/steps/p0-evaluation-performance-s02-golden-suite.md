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

## Implementation notes (16 Oct 2026)
- **68 scripts:** the 17 correct paths plus 51 new ones in `tests/golden/<case>_{novice,dangerous,edge}.actions.json`, derived from each correct path by `tools/golden/paths.py` (so they follow it when it changes): **novice** leaves out what a novice forgets that is not critical (introducing oneself, summarising, answering the patient's question); **dangerous** orders and takes blood before the identity is confirmed (the walkthrough's first safety lesson); **edge** adds a pause and resume after the identity check and splits every time-skip in two halves. `task golden` runs 75 golden runs (68 plus the 7 earlier skeleton and wristband runs).
- **Expected files now hold the score** (plan test T-SCO1): the result, the number of credits and every lost item by severity (minor, major, critical), next to the end state, the facts and the log and state hashes (`GoldenRun`; the 24 earlier expected files gained the block, nothing else changed). A deliberate change to an expected file fails with a readable line, for example `score.credits: expected 17, got 16`.
- **Each path is checked for what it promises** (`tools/golden/paths.py --check`, in `task ci` and on GitHub): novice ends as the correct path and costs something (a lost item, fewer credits or a patient who trusts less); dangerous is refused by the engine (IDENTITY_NOT_CONFIRMED) or fails; edge ends in exactly the correct path's state (same state hash), which proves that pausing and splitting a skip change nothing.
- **Two findings for the team (not changed here, they are scoring decisions):** (1) introducing oneself and summarising change only the patient's trust, not the score: in the 9 cases that end before consent the novice path loses no item and no credit (DT-175); (2) on the cases that proceed, deciding without opening any result costs only `major.result_not_reviewed` and still ends READY_SAFE with a SUCCESS result, although the result-review step says an unopened needed result blocks (DT-176). The first dangerous path tried was this one; it was replaced by bloods before identity, which the engine does refuse.
- **Unity:** the Unity golden test runs the same files; Unity batch tests pass.
