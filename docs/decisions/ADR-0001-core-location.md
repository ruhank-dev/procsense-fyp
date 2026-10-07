# ADR-0001 — Core engine lives in `src/`, Unity consumes it as a local package

**Date:** 07 October 2026 · **Status:** Proposed

## Context
The original Implementation Guide (B-R11) put the Core `.cs` files inside `unity/Assets/_Project/Core/` and had
a separate `sim/` project link to them. That ties the engine to the Unity folder and means the engine can't be
built or tested until a Unity project exists.

## Decision
`src/ProcSense.Core/` is the single home of the engine (netstandard2.1, C# 9, no Unity references).
It builds and tests with plain `dotnet`. When the Unity project is created, we add a `package.json` +
`.asmdef` (`noEngineReferences: true`) inside `src/ProcSense.Core/` and reference it from
`unity/Packages/manifest.json` as `"com.procsense.core": "file:../../src/ProcSense.Core"`.

## Consequences
- Core work can start now, without Unity installed.
- One copy of the code; no file linking.
- Unity will generate `.meta` files inside `src/ProcSense.Core/` — they must be committed.
