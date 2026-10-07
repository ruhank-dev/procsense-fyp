# p0-simulation-workflow-s04 — Golden run for skeleton 1

Walking skeleton 1 · step 9 of 15

## What
Three action scripts for VAR-S1-A Steps 1–3 with their expected results, run in CI:
- `golden/skeleton1_correct.actions.json` — the walkthrough's Part 12 Scene 1–3 sequence; expected: identity verified, all asked facts revealed, all vitals recorded.
- `golden/skeleton1_bloods_first.actions.json` — orders bloods before identity; expected: rejected `IDENTITY_NOT_CONFIRMED`.
- `golden/skeleton1_closed_question.actions.json` — reads name to patient; expected: `risk.wrong_blood` armed.

## Why
A fixed reference the VR version must match (step 15), and a regression guard for every later change.

## How to implement
1. Scripts in `tests/golden/` + expected JSON (key facts, not the whole state) + expected log hash for seed 42.
2. xUnit test that runs each script through `Session` and compares; `task golden` runs them via the CLI too.
3. Add to CI (GitHub Actions `dotnet test`).

## Verification
`task golden` → 3/3 pass; changing one action breaks the expected hash (prove the test is real).

## Files changed
`tests/golden/*.json`, `tests/ProcSense.Core.Tests/GoldenRunTests.cs`, `Taskfile.yml`, CI workflow.

## Dependencies
p0-simulation-workflow-s01…s03.

## Common issues
- Expected hashes change whenever the log format changes — regenerate deliberately (`task golden -- --update`) and review the diff.

## Sources
Master Plan §41 (golden runs); inventory D73; walkthrough Part 12 Scenes 1–3.
