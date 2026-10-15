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

## Implementation notes (15 Oct 2026)
- **Built:** `tools/doctor.py` (standard library only) and `task doctor`: one row per tool with what it is needed for, the wanted version, the version found and the install command when it is missing. Required (exit code 1 if missing or too old): Git, .NET SDK of the major version in `global.json`, uv, Task, the pre-push hook. Shown but never an error: Git LFS, Docker (and whether its engine is running), Unity of the version in `ProjectVersion.txt` and its Android Build Support module, Blender of the version in `tools/blender/VERSION`, adb and the number of connected headsets. The wanted versions are read from those files, so the guide and the check cannot drift apart. README section "Requirements" rewritten as a table with versions and install commands, including Quest developer mode.
- **Tests:** `tools/ci/test_doctor.py` (4 tests: versions read from the repository and from tool output, a machine with nothing installed reports every tool missing with a fix and fails, an old .NET is flagged), run by `task ci` and on GitHub. `task doctor` itself is not run in CI: a runner has no Unity, Blender, adb or hook by design.
- **Verified on the owner's laptop (15 Oct 2026):** every required tool OK; Docker installed but its engine stopped; Unity 6000.5.10f1 present but Android Build Support missing (needed for Quest builds on this laptop); Blender missing; adb present, no headset connected.
