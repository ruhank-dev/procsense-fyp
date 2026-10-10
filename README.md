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

## Repo layout

| Folder | What lives there |
|---|---|
| `src/ProcSense.Core` | Deterministic simulation engine (C#, netstandard2.1, no Unity) |
| `src/ProcSense.Cli` | Headless runner — play a case without Unity |
| `tests/` | xUnit tests; `tests/golden` golden runs; `tests/fixtures` test-only cases; `tests/samples` example action scripts |
| `content/` | Medical content package (every value traced in `docs/research/`) |
| `unity/` | Unity VR project (later) |
| `server/` | FastAPI + SQLite backend (later, P1) |
| `tools/` | Python content/validation tools; `tools/blender/` procedural equipment scripts (later) |
| `docs/plans/` | Plan: tiers `p0/ p1/ p2/` → phases → steps — start at `docs/plans/README.md` |
| `docs/research/` | Proof of truth: source register, dataset register, open medical issues |
| `docs/CI.md` | Every CI/CD check, what it guards, and the roadmap of checks to add |
| `docs/deferred-tasks.md` | Register of postponed, outstanding and blocked work |
| `.github/` | GitHub Actions workflows (CI, release) and Dependabot |
| `docs/decisions/` | Architecture decision records |
| `docs/context/` | Original Master Plan, Implementation Guide, Medical Walkthrough (reference) |

## Requirements

- .NET SDK 9 · Git
- [Task](https://taskfile.dev) (`winget install Task.Task`) and [uv](https://docs.astral.sh/uv/) (`winget install astral-sh.uv`)
- Later: Unity 6 + Android build support, Blender (for equipment scripts), Meta Quest

## Commands

| Task | Raw command |
|---|---|
| `task build` | `dotnet build ProcSense.sln` |
| `task test` | `dotnet test ProcSense.sln` |
| `task sim -- <args>` | `dotnet run --project src/ProcSense.Cli -- <args>` (`run` / `replay`) |
| `task content-validate` | `uv run --project tools tools/content/validate.py` — checks every content file against its schema and the source register |
| `task golden` | runs the golden runs in `tests/golden` through the CLI (`-- --update` rewrites the expected files deliberately) |
| `task check` | `uv run --project tools tools/ci/repo_checks.py` — project rules no compiler covers |
| `task setup` | `git config core.hooksPath .githooks` + tool check — installs the pre-push hook (runs `task ci` before `main` is updated) |
| `task ci` | `uv run --project tools tools/ci/run_all.py` — every check CI runs, locally |
| `task plans-index` | `uv run --project tools tools/plans/milestones.py` — rebuilds `docs/plans/milestones.md` from the step files and checks step references |
| `task data-fetch` | downloads the open source datasets the pictures are built from (about 3 GB, git-ignored; **only needed to rebuild**: every generated picture, report and table is already committed). See `tools/data/README.md` |
| `task data-check` / `task data-rebuild` | verifies the downloaded data against its manifest / rebuilds every generated picture, report, table and register from it |
| `task plans-check` | fails if `docs/plans/milestones.md` is out of date |
| `task clean` | `dotnet clean ProcSense.sln` |
