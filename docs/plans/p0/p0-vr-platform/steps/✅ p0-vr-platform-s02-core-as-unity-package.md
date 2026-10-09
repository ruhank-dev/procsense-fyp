# p0-vr-platform-s02 — Core engine as a Unity package

Walking skeleton 1 · step 11 of 15 · blocked until s01

## What
Make `src/ProcSense.Core` loadable by Unity as a local package (ADR-0001), so the exact same engine runs headless and on the Quest.

## Why
One copy of the engine; Unity never contains simulation logic.

## How to implement
1. `src/ProcSense.Core/package.json` (`com.procsense.core`) + `ProcSense.Core.asmdef` with `noEngineReferences: true`.
2. `unity/Packages/manifest.json`: `"com.procsense.core": "file:../../src/ProcSense.Core"`.
3. Exclude `bin/` and `obj/` from Unity import (asmdef folder layout or `.gitignore`d build output outside the package folder — move .NET build output via `Directory.Build.props`).
4. Unity-side JSON reader implementing Core's `IContentReader` (Newtonsoft) + `link.xml` preserving Core and Newtonsoft types.
5. Copy/stream `content/` into `StreamingAssets` at build time (build script), so the Quest reads the same files.
6. Commit the `.meta` files Unity generates inside `src/ProcSense.Core`.

## Verification
Unity compiles with Core referenced; an Editor test runs the skeleton-1 golden script inside Unity and gets the same log hash as `task golden`.

## Files changed
`src/ProcSense.Core/package.json`, `ProcSense.Core.asmdef`, `Directory.Build.props`, `unity/Packages/manifest.json`, `unity/Assets/_Project/Scripts/Content/NewtonsoftContentReader.cs`, `unity/Assets/link.xml`, build script, ADR-0001 status → Accepted.

## Dependencies
p0-core-engine-s04, p0-vr-platform-s01.

## Common issues
`dotnet build` output inside the package folder confuses Unity → redirect output; C# language version mismatch → stay on C# 9 features.

## Sources
ADR-0001 (Core in src/, local Unity package); Implementation Guide B-R11 Step 1 (noEngineReferences), troubleshooting (link.xml); B-M11 Step 4.
