# p0-foundation-s00 — Repo foundation

Milestone: **M0 Foundation**

## What
Create the empty-but-working skeleton of the ProcSense repo: folder structure, a .NET solution that builds,
one smoke test, a Taskfile, git setup, the medical research register, and the shared `.memory/` log.

## Why
Previous work was lost; we restart clean. Every later step (Core kernel, content package, Unity, AI, backend)
needs a fixed home and a one-command build/test before any feature code is written.

## Folder structure

```
ProcSense/
├─ AGENTS.md · INSTRUCTIONS.md · CLAUDE.md     rules (auto-loaded)
├─ README.md · Taskfile.yml · ProcSense.sln
├─ .gitignore · .gitattributes · .editorconfig
├─ src/
│  ├─ ProcSense.Core/      deterministic simulation engine (C#, netstandard2.1, NO Unity references)
│  └─ ProcSense.Cli/       headless runner: run a case without Unity (net9.0)
├─ tests/
│  └─ ProcSense.Core.Tests/  xUnit tests for Core
├─ content/                medical content package (CSV/JSON) — every value traced to docs/research
├─ unity/                  Unity 6 URP project (created from Unity Hub in a later step)
├─ server/                 FastAPI + SQLite backend (P1, later step)
├─ tools/                  Python tools: content pipeline, validators (uv, later step)
├─ docs/
│  ├─ context/             original project docs (read-only reference)
│  ├─ plans/               step plans (this folder)
│  ├─ decisions/           ADRs — why we chose X
│  └─ research/            proof of truth: source register, dataset register, issues
└─ .memory/                shared daily work log (git-ignored)
```

## How to implement
1. Root config: `.gitignore` (dotnet, Unity, Python, `.memory/`, `.env*`), `.gitattributes` (LF + Unity binary/LFS hints), `.editorconfig`.
2. `dotnet new sln` + three projects; Core targets `netstandard2.1` with `LangVersion 9.0` (the C# level Unity 6 supports).
3. One smoke test: Core must not reference `UnityEngine` (the Core must stay engine-free and deterministic — B-R11).
4. `Taskfile.yml` with `build`, `test`, `run-cli`, `clean`.
5. `docs/research/` register files with headers only — no clinical values yet.
6. ADR-0001: where the Core lives (`src/`, consumed by Unity as a local package later).
7. `git init` (no commit — the user commits).

## Verification
- `dotnet build ProcSense.sln` → 0 errors, 0 warnings.
- `dotnet test` → smoke test passes.
- `dotnet run --project src/ProcSense.Cli` → prints the version banner.

## Files changed
Root config files, `ProcSense.sln`, `src/`, `tests/`, folder READMEs, `docs/plans/`, `docs/decisions/`, `docs/research/`, `.memory/`.

## Dependencies
None — first step.

## Common issues
- `task` / `uv` not installed → install with `winget install Task.Task` and `winget install astral-sh.uv`; until then use the raw `dotnet` commands.
- Unity can only use code inside `Assets/` or a package → solved later by making `src/ProcSense.Core` a local UPM package (ADR-0001).
