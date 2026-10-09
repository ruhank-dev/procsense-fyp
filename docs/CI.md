# CI/CD — what is checked, and how it grows

Living document (INSTRUCTIONS.md §6b). Whenever a step adds a capability, rule, content type, tool or folder, **add or update its check here and in the workflows in the same step**.

**Run everything locally:** `task ci` (or `python tools/ci/run_all.py`; add `--quick` when only docs/content changed). Green locally = green in GitHub, except the Linux-only format check.

## Workflows

| File | Runs when | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | every push to `main`, every pull request, manually, and called by the release | The three jobs below |
| `.github/workflows/release.yml` | a tag such as `v0.1.0` is pushed | Runs the full CI, then publishes the headless simulator for Linux and Windows (self-contained, with `content/` and the golden runs) as a GitHub Release with SHA-256 checksums |
| `.github/dependabot.yml` | weekly | Pull requests for NuGet, GitHub Actions and Python (uv) updates |

## Checks in place

| # | Job | Check | Guards against | Local command |
|---|---|---|---|---|
| 1 | Engine (Linux + Windows) | Restore with 3 retries, build in Release with warnings as errors | Compile errors, new warnings, flaky network | `dotnet build ProcSense.sln -c Release -warnaserror` |
| 2 | Engine | Unit tests (xUnit, incl. determinism, content, identity, history, observations, golden) | Broken engine logic | `dotnet test ProcSense.sln` |
| 3 | Engine | Golden runs through the CLI | A change that alters the pinned log or final-state hash | `task golden` |
| 4 | Engine | Record a session and replay it | Replay no longer reproduces the same hash | `task sim -- run …` then `replay …` |
| 5 | Engine | Same tests and hashes on **Linux and Windows** | Platform differences in the engine (proxy for the PC-vs-Quest check EXP-CORE-1) | both OS jobs |
| 6 | Engine (Linux) | `dotnet format --verify-no-changes` | Style drift against `.editorconfig` | CI only on Windows checkouts (line endings) |
| 7 | Repo checks | `content/` validated against its schema; every clinical value has a source-register row | Untraced or malformed medical content | `task content-validate` |
| 8 | Repo checks | Plan index (`milestones.md`) up to date; step references resolve | Out-of-date plans, links to missing steps | `task plans-check` |
| 9 | Repo checks | `tools/ci/repo_checks.py`: **core-rules** (no wall clock, `System.Random`, Unity, env vars, console, string hash codes in `src/ProcSense.Core`), **hygiene** (no build output, session files, env files, secrets, big files), **docs-links** (relative Markdown links resolve), **deferred-register** (ids unique, statuses valid), **prompt-history** (numbered 1..N), **steps** (file names and titles agree) | Project rules no compiler checks | `task check` |
| 10 | Repo checks | GitHub workflow and Dependabot files validate against their schemas | A broken workflow file | `uvx check-jsonschema --builtin-schema vendor.github-workflows .github/workflows/*.yml` |
| 11 | Dependencies | No vulnerable NuGet packages (direct or transitive); deprecated packages warn | Known-vulnerable dependencies | `dotnet list ProcSense.sln package --vulnerable --include-transitive` |
| 12 | Release | Tag must equal `CoreInfo.Version` | Releasing a mislabelled build | `release.yml` |

## Roadmap — checks to add as the project grows

Each row becomes a real check in the step that creates the thing it guards. Tracked in `docs/deferred-tasks.md` (DT-044).

| Add when… | Check to add | Planned in |
|---|---|---|
| The Unity project exists | Edit-mode and Play-mode tests in batch mode (GameCI or a local runner); Core as a Unity package compiles | p0-vr-platform-s02, s07 |
| Quest builds exist | Build Dev/Demo/Study variants; performance gate (≥ 72 fps average, no hitch > 50 ms, memory ≤ 2.5 GB); APK + checksum on release | p0-vr-platform-s07 |
| Quest sessions are recorded | Replay a Quest log headless and compare the final-state hash (skeleton 1 done condition) | p0-evaluation-performance-s01 |
| Blender equipment scripts exist | Headless Blender validation + preview render per asset (size, scale, naming, polygon budget) | p0-blender-equipment-s03 |
| Case packaging exists | Content manifest hash (T-CL-5), loader mode checks, contradiction validator, mutation tests | p0-case-library-s01, s04, s05 |
| Python tools grow | `pytest` for `tools/` | with the first tool that has logic worth testing |
| Rules / clinical logic exist | Worked-example tests from the walkthrough (EXP-CL-1), property tests | p0-clinical-logic |
| The backend exists (P1) | FastAPI tests, OpenAPI schema check, Docker build (two-stage), `uv` lock check | p1-backend |
| AI paths exist (P1) | Grounding validator and AI evaluation suite | p1-ai-language, p1-evaluation |
| Repo plan allows it | Branch protection with required checks, code scanning / secret scanning (private repos need a paid GitHub plan) | DT-042 |
| xunit v3 migration done | Remove the deprecated-package warning | DT-041 |

## Cutting a release

1. Update `CoreInfo.Version` (`src/ProcSense.Core/CoreInfo.cs`) and commit.
2. `git tag v<version>` (for example `v0.1.0`) and `git push origin v<version>`.
3. The Release workflow runs CI, builds `procsense-sim-v<version>-linux-x64.zip` and `…-win-x64.zip` with `.sha256` files and creates the GitHub Release with generated notes.

## Status

First run on GitHub: **not yet confirmed** (needs the workflows pushed). Update this line with the date and result after the first green run (DT-045).
