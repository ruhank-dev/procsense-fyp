# ADR-0001 — Core engine lives in `src/`, Unity consumes it as a local package

**Date:** 07 October 2026 · **Status:** Accepted (09 October 2026 — implemented in p0-vr-platform-s02)

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

## Implementation notes (09 October 2026)

- `src/ProcSense.Core/package.json` (`com.procsense.core`) and `ProcSense.Core.asmdef` (`noEngineReferences: true`) make the folder a Unity package; `unity/Packages/manifest.json` references it as `file:../../src/ProcSense.Core`. `ProcSense.Core.rsp` turns nullable annotations on for Unity's compiler.
- .NET build output moved out of the package folder with the repository's `Directory.Build.props` (`artifacts/obj`, `artifacts/bin`), so Unity never imports generated files.
- Unity-generated `.meta` files inside `src/ProcSense.Core/` are committed.
