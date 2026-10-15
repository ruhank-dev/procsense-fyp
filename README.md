# ProcSense

[![CI](https://github.com/ruhank-dev/procsense-fyp/actions/workflows/ci.yml/badge.svg)](https://github.com/ruhank-dev/procsense-fyp/actions/workflows/ci.yml)

VR training simulator (Meta Quest 2/3, Unity 6) for **pre-operative preparation**: the trainee takes an invented
patient from arrival to the WHO **Time Out** for two scenarios —
**S1** planned laparoscopic cholecystectomy and **S2** emergency appendectomy — across 15 walkthrough steps.

> Educational simulation only — not a medical device or decision-support tool. All patients are invented.
> Medical content is based on *ProcSense Medical Walkthrough v5.1* and is pending clinician validation.

## Repository

Working in a team? Read [CONTRIBUTING.md](CONTRIBUTING.md) and [docs/TEAM_GIT_WORKFLOW.md](docs/TEAM_GIT_WORKFLOW.md): branch, merge `origin/main`, `task ci`, then push straight to `main` (no pull requests). Run `task setup` once after cloning; the pre-push hook it installs runs `task ci` before any push to `main`.

The one and only remote: **https://github.com/ruhank-dev/procsense-fyp** (branch `main`; history restarted on 07 Oct 2026 — the earlier prototype was deliberately overwritten). `AGENTS.md` and `INSTRUCTIONS.md` stay local (git-ignored).

**Start with [docs/index.md](docs/index.md)**: it says where everything is, which file is the source of truth for what, and the rules every change follows.

## Repo layout

| Folder | What lives there |
|---|---|
| `src/ProcSense.Core` | Deterministic simulation engine (C#, netstandard2.1, no Unity) |
| `src/ProcSense.Cli` | Headless runner — play a case without Unity |
| `tests/` | xUnit tests; `tests/golden` golden runs; `tests/fixtures` test-only cases; `tests/samples` example action scripts |
| `content/` | Medical content package (every value traced in `docs/research/`) |
| `unity/` | Unity VR project (later) |
| `server/` | FastAPI + PostgreSQL (Supabase) backend (P1, milestone M17) |
| `tools/` | Python content/validation tools; `tools/blender/` procedural equipment scripts (later) |
| `docs/plans/` | Plan: tiers `p0/ p1/ p2/` → phases → steps — start at `docs/plans/README.md` |
| `docs/research/` | Proof of truth: source register, dataset register, open medical issues |
| `docs/CI.md` | Every CI/CD check, what it guards, and the roadmap of checks to add |
| `docs/deferred-tasks.md` | Register of postponed, outstanding and blocked work |
| `.github/` | GitHub Actions workflows (CI, release) and Dependabot |
| `docs/decisions/` | Architecture decision records |
| `docs/context/` | Original Master Plan, Implementation Guide, Medical Walkthrough (reference) |

## Requirements

Run `task doctor` (or `python tools/doctor.py` if Task or uv is not installed yet) to see what is installed and what is missing; it reads the wanted versions from the files named below, so this table and the check cannot drift apart. Open a new terminal after a winget install (PATH is only refreshed in new terminals).

| Tool | Needed for | Version (pinned in) | Install (Windows) |
|---|---|---|---|
| Git (with Git LFS) | everything | 2.40 or newer | `winget install --id Git.Git -e`, then `git lfs install` |
| .NET SDK | engine, CLI, tests | 9.x (`global.json`) | `winget install --id Microsoft.DotNet.SDK.9 -e` |
| uv | Python tools, backend | 0.5 or newer | `winget install --id astral-sh.uv -e` |
| Task | `task` commands | 3.x | `winget install --id Task.Task -e` |
| Docker Desktop | backend tests, service image, backup drill | any; the engine must be running | `winget install --id Docker.DockerDesktop -e` |
| Unity + Android Build Support | the VR app and Quest builds | 6000.5.10f1 (`unity/ProjectSettings/ProjectVersion.txt`) | Unity Hub > Installs > the version > Add modules > Android Build Support (OpenJDK, SDK and NDK) |
| Blender | equipment scripts | 4.5.14 (`tools/blender/VERSION`) | see `tools/blender/README.md` (or set `BLENDER_EXE`) |
| adb (Android platform tools) | installing on the Quest | any | `winget install --id Google.PlatformTools -e` |
| Meta Quest 3 | the headset | developer mode on | Meta Horizon phone app > Devices > Headset settings > Developer mode (needs a Meta developer account), then allow USB debugging in the headset |

Required for `task ci`: Git, .NET SDK, uv, Task (and Docker for the backend tests, which are skipped with a notice without it). After cloning: `task setup`, then `task doctor`, then `task ci`.

## Commands

| Task | Raw command |
|---|---|
| `task build` | `dotnet build ProcSense.sln` |
| `task test` | `dotnet test ProcSense.sln` |
| `task sim -- <args>` | `dotnet run --project src/ProcSense.Cli -- <args>` (`run` / `replay`) |
| `task content-validate` | `uv run --project tools tools/content/validate.py` — checks every content file against its schema and the source register |
| `task golden` | runs the golden runs in `tests/golden` through the CLI (`-- --update` rewrites the expected files deliberately) |
| `task check` | `uv run --project tools tools/ci/repo_checks.py` — project rules no compiler covers |
| `task doctor` | `uv run --no-project tools/doctor.py`: every tool, the version wanted and found, and how to install what is missing |
| `task setup` | `git config core.hooksPath .githooks` + tool check — installs the pre-push hook (runs `task ci` before `main` is updated) |
| `task ci` | `uv run --project tools tools/ci/run_all.py` — every check CI runs, locally |
| `task plans-index` | `uv run --project tools tools/plans/milestones.py` — rebuilds `docs/plans/milestones.md` from the step files and checks step references |
| `task data-fetch` | downloads the open source datasets the pictures are built from (about 3 GB, git-ignored; **only needed to rebuild**: every generated picture, report and table is already committed). See `tools/data/README.md` |
| `task data-check` / `task data-rebuild` | verifies the downloaded data against its manifest / rebuilds every generated picture, report, table and register from it |
| `task plans-check` | fails if `docs/plans/milestones.md` is out of date |
| `task clean` | `dotnet clean ProcSense.sln` |
