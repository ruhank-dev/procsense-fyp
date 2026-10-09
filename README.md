# ProcSense

VR training simulator (Meta Quest 2/3, Unity 6) for **pre-operative preparation**: the trainee takes an invented
patient from arrival to the WHO **Time Out** for two scenarios —
**S1** planned laparoscopic cholecystectomy and **S2** emergency appendectomy — across 15 walkthrough steps.

> Educational simulation only — not a medical device or decision-support tool. All patients are invented.
> Medical content is based on *ProcSense Medical Walkthrough v5.1* and is pending clinician validation.

## Repository

The one and only remote: **https://github.com/ruhank-dev/procsense-fyp** (branch `main`; history restarted on 07 Oct 2026 — the earlier prototype was deliberately overwritten). `AGENTS.md` and `INSTRUCTIONS.md` stay local (git-ignored).

## Repo layout

| Folder | What lives there |
|---|---|
| `src/ProcSense.Core` | Deterministic simulation engine (C#, netstandard2.1, no Unity) |
| `src/ProcSense.Cli` | Headless runner — play a case without Unity |
| `tests/` | xUnit tests |
| `content/` | Medical content package (every value traced in `docs/research/`) |
| `unity/` | Unity VR project (later) |
| `server/` | FastAPI + SQLite backend (later, P1) |
| `tools/` | Python content/validation tools; `tools/blender/` procedural equipment scripts (later) |
| `docs/plans/` | Plan: tiers `p0/ p1/ p2/` → phases → steps — start at `docs/plans/README.md` |
| `docs/research/` | Proof of truth: source register, dataset register, open medical issues |
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
| `task run-cli` | `dotnet run --project src/ProcSense.Cli` |
| `task content-validate` | `uv run --project tools tools/content/validate.py` — checks every content file against its schema and the source register |
| `task plans-index` | `uv run --project tools tools/plans/milestones.py` — rebuilds `docs/plans/milestones.md` from the step files and checks step references |
| `task clean` | `dotnet clean ProcSense.sln` |
