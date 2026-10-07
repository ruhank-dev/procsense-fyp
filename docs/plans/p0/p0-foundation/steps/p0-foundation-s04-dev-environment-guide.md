# p0-foundation-s04 — Developer environment guide and `task doctor`

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
README section listing every tool with pinned versions and install commands — .NET 9 SDK, uv, Task, Git + Git LFS, Unity 6 LTS (exact editor version + Android Build Support), Blender (exact version), Meta Quest developer mode — and a `task doctor` command that prints installed versions and flags anything missing.

## Why
A teammate or a clean machine (EXP-REL-1) must reproduce the setup without asking.

## How to implement
1. Pin versions after installing: `unity/ProjectSettings/ProjectVersion.txt` (Unity writes it), `tools/blender/VERSION`.
2. `tools/doctor.py` (stdlib only): runs `dotnet --version`, `uv --version`, `task --version`, `git lfs version`, checks Unity Hub/editor and Blender paths, prints a table with OK/MISSING.
3. `task doctor` in Taskfile.
4. `task doctor` exit code non-zero if a required tool is missing (useful in CI and onboarding).

## Verification
`task doctor` on your machine lists every tool as OK (Unity/Blender show MISSING until installed — expected).

## Files changed
`README.md`, `tools/doctor.py`, `Taskfile.yml`.

## Dependencies
p0-foundation-s00.

## Common issues
Windows PATH isn't refreshed after winget installs → open a new terminal (seen during setup: uv/Task needed a fresh shell).

## Sources
p0-foundation README.
