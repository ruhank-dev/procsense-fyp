# p0-case-library-s02 — All 17 P0 cases run headless to their expected end state

Milestone: **M5 Content complete — all 17 P0 cases as data**

## What
Load every P0 case into the engine and write a correct-path golden script per case that reaches the walkthrough's expected end state, plus a 'never ask' test per hidden fact showing it surfaces as the walkthrough describes (e.g. VAR-S1-D(b): apixaban 07:00 today, if never asked → anaesthetist reveals at Sign In → case stops → POSTPONED_APPROPRIATELY).

## Why
Proves engine + content cover the full P0 set before any VR work depends on it.

## How to implement
1. One `tests/golden/<case>_correct.actions.json` per case, written from the walkthrough's correct path.
2. Expected key facts + end state per case in `tests/golden/<case>_expected.json`.
3. T-CASE2 tests for hidden facts.
4. A case that can't be expressed → schema/engine gap ticket, never special-case code.

## Verification
T-CASE1: 17/17 correct-path runs reach the expected end state; T-CASE2 passes for every hidden fact.

## Files changed
`tests/golden/*`, tests.

## Dependencies
p0-medical-research-data-s10–s12; p0-simulation-workflow-s17.

## Common issues
Case values differing between walkthrough parts → follow the issues log, ask the reviewer.

## Sources
B-M19 Step 1, T-CASE1/2; walkthrough Parts 12, 13, D.
