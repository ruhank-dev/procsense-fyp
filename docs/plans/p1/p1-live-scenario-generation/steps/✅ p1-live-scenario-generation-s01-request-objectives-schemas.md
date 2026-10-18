# p1-live-scenario-generation-s01 — Session request, objectives and schemas (LG-01, LG-02)

Milestone: **M14 P1 live scenario generation — dataset analysis and fresh cases**


## What
Define SessionRequest v1, Objectives v1 and ScenarioPackage v1 JSON schemas; derive objectives from the Elo ratings and dimensions; refuse generation in assessment mode.

## Why
Every later stage depends on these contracts.

## How to implement
1. `server/schemas/session_request.json`, `objectives.json`, `scenario_package.json`.
2. `server/generation/objectives.py` reusing the rating rules (mirror of Difficulty.cs, shared vectors).
3. Prompt and schema registry layout (OFF-09).

## Status (17 October 2026)

- **Built:** `server/schemas/session_request.json`, `objectives.json`, `scenario_package.json` (JSON Schema, draft 2020-12) and `server/generation/contracts.py` (`accept_request`: a valid request is accepted with a logged seed, drawn when absent, and a request id; assessment mode is refused with a 409 telling the caller to serve a fixed case; anything else is a 400 naming the field) and `server/generation/objectives.py` (`Tables`, `Ratings`, `derive`): a Python mirror of the engine's Elo rules (K 16, 3 sessions before adapting), the weakest behaviours and the dimensions that train them, an 8-dimension difficulty vector, the spines and story fingerprints to leave out, and an explanation sentence from the content. `jsonschema` was added to the server's dependencies.
- **Shared vectors:** `server/generation/make_vectors.py` writes `tests/vectors/difficulty_vectors.json` (13 histories); `tests/ProcSense.Core.Tests/DifficultyVectorTests.cs` runs the engine on them and agrees with the Python mirror on every rating (tolerance 1e-6), so the two cannot drift apart.
- **Verification result:** schema vectors accept and reject; the same history gives the same objectives (`tools/ci/test_generation_contracts.py`, 13 tests).
- **Not built:** the prompt and schema registry layout (OFF-09) is first needed by the model steps (s04, s13); the HTTP endpoint that calls `accept_request` is p1-backend-s06.

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
