# p1-live-scenario-generation-s11 — Live loop on generated cases: events, branches, scoring (LG-16 to LG-18)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Confirm the Core runs generated packages exactly as library cases: event variants within bounds, generated branches from the what-if library, scoring unchanged.

## Why
The engine must not need special cases for generated content.

## How to implement
1. Golden runs for a fixed set of generated packages.
2. Event variant bounds in content.

## Status (17 October 2026)

- **Built:** the `golden` command plays a case from its package when `<name>.package.json` is next to the script; `tools/golden/generated.py make` fixes six generated cases (three per family, each from a different approved case) in `tests/golden_generated` (package, script, expected hashes); `check` keeps the snapshot inside the generation gate. CI plays them on Linux and Windows (row 3e). `tools/ci/engine_version.py` now covers these goldens and `CoreInfo.Version` is 0.0.5.
- **Verification result** (`tools/ci/test_generation_live.py`): the six generated goldens pass; for every spine the correct and dangerous paths give the same end state, result and scoring items on the spine and on its generated case (allergy items are excluded: the composed allergy block changes them on purpose; `PARITY_ALL=1` plays all four paths and passes too); the same case and seed replay identically; other engine seeds run and idle runs keep the pulse and saturation in bounds. No engine special case was needed, so no deferred task was opened for an engine gap.
- **Found on the way:** the idle run of Stage B check 9 inspected only measured vital signs and nothing was measured, so it checked nothing; it now attaches the pulse oximeter and fails if there is no reading.
- **Not built:** event-variant bounds in content (the event director already varies by seed within the events file; no generated-specific bounds were needed); generated what-if branches (the what-if library is p2).

## Verification
Replay hashes stable; scoring identical rules.

## Files changed
`tests/golden/generated_*`

## Dependencies
p1-live-scenario-generation-s10

## Common issues
Generated case exposes an engine gap: log as DT, do not special-case.

## Sources
Spec LG-16 to LG-18; A5, A8.
