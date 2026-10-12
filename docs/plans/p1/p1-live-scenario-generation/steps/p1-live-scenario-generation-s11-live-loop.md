# p1-live-scenario-generation-s11 — Live loop on generated cases: events, branches, scoring (LG-16 to LG-18)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Confirm the Core runs generated packages exactly as library cases: event variants within bounds, generated branches from the what-if library, scoring unchanged.

## Why
The engine must not need special cases for generated content.

## How to implement
1. Golden runs for a fixed set of generated packages.
2. Event variant bounds in content.

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
