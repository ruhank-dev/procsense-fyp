# p1-live-scenario-generation-s01 — Session request, objectives and schemas (LG-01, LG-02)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**

> **Planning only (12 October 2026).** Written in the planning update; do not implement until the owner says so. Spec: `docs/architecture/live_generation_spec.md`; canonical graph: `docs/architecture/procsense_full_pipeline.json`.

## What
Define SessionRequest v1, Objectives v1 and ScenarioPackage v1 JSON schemas; derive objectives from the Elo ratings and dimensions; refuse generation in assessment mode.

## Why
Every later stage depends on these contracts.

## How to implement
1. `server/schemas/session_request.json`, `objectives.json`, `scenario_package.json`.
2. `server/generation/objectives.py` reusing the rating rules (mirror of Difficulty.cs, shared vectors).
3. Prompt and schema registry layout (OFF-09).

## Verification
Schema vectors accept and reject; same history gives same objectives.

## Files changed
`server/schemas/*.json`, `server/generation/objectives.py`

## Dependencies
p1-backend-s01, p0-case-library-s06

## Common issues
Duplicated rating logic: share test vectors with the C# version.

## Sources
Spec LG-01, LG-02.
