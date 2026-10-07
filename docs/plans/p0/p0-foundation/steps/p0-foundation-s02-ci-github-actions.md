# p0-foundation-s02 — CI: build, tests, content validation, plans index

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
GitHub Actions workflow running on every push and pull request: restore + build the .NET solution, run all tests (incl. golden runs), `task content-validate`, and `task plans-index` (fails on broken step references).

## Why
Catch broken engine logic, untraced clinical values or broken plans before they reach `main`.

## How to implement
1. `.github/workflows/ci.yml`: ubuntu-latest; `actions/setup-dotnet` 9.x; `astral-sh/setup-uv`; install Task.
2. Cache NuGet (`~/.nuget/packages`) and uv.
3. `dotnet restore` with up to 3 retries (network-flakiness rule), `dotnet build --no-restore -warnaserror`, `dotnet test --no-build`.
4. `uv run --project tools tools/content/validate.py` and `tools/plans/milestones.py` (check-only mode: fail if `milestones.md` would change).
5. Mark the workflow as a required status check in branch protection.
6. Workflow also builds the Unity project later (B-S11) — keep the .NET job separate so it stays fast.

## Verification
Open a test PR that removes one `refs` entry from `VAR-S1-A.json` → CI red with the validator message; revert → green.

## Files changed
`.github/workflows/ci.yml`, `tools/plans/milestones.py` (add `--check`).

## Dependencies
p0-foundation-s01.

## Common issues
Line endings change hashes — `.gitattributes` already forces LF.

## Sources
p0-foundation README; B-M11 Step 5 (content CI).
