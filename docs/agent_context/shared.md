# Shared agent context (short; read this instead of re-deriving; updated 14 Nov 2026 UTC)

**Archive.** `docs/previous_prompt.md`: read CURRENT ACTIVE CONTEXT only, then grep an ID (`## 253.`). Strategy: P256 (this file), workflow P254 (agent cap superseded by P256).
**Detail.** `docs/qa/overhaul_checkpoint.md`, `docs/qa/quest_overhaul.md` (lanes A-F), `docs/qa/quest_overhaul_headset_sheet.md`.

## State
- Repo root `procsense-fyp-main/procsense-fyp-main`; branch `quest-perf/gpu-overhaul`, HEAD 7b446e28 (= origin/main), nothing committed, ~601 uncommitted paths (preserve all). Backup `D:/ProcSenseWork/backup/overhaul_20261114_1744/`.
- Goal: Quest 2 from 34 FPS / 26.47 ms GPU to 72 FPS (<13.89 ms). Never claim 72 FPS without headset results.
- Done+tested: Android profile, all-baked lighting, Impostor exterior, textures, character LODs, engine v0.0.18, LAN pairing, MenuFlow 7/7.
- Open: APK build, visual review of baked scenes, visibility sweep, DemoLoginView hook, spoken exam equivalents (VOX-06/13), NET-11/12/14/15, headset checks (Moiz, Sannan: theirs), final CI.

## Layout
`unity/` (Unity project, Assets/Tests), `engine/` + Core (dotnet), `server/`, `tools/unity/` (smoke, run_unity.py), `tools/ci/` (rules.yml, change-check), `docs/plans/p3/` (P3 step files).

## Rules
- One heavyweight Unity op at a time (lock `D:/ProcSenseWork/unity.lock`); background only, never steal focus; big files on D:.
- Change-aware tests only (`task change-check`); no full CI, commit, push or main merge until final integration and approval.
- No AI attribution anywhere tracked; UTC times; no secrets in Git; keep Sannan's and Moiz's work.
- Docs upkeep each turn (previous_prompt, deferred-tasks, context).

## Dynamic agent policy (P256)
- No fixed cap; start ~3 concurrent; raise only if tasks are independent, files disjoint, RAM/GPU fine, time saved; lower on conflict/duplication/saturation.
- Scripts for mechanical work; cheap model (haiku/sonnet) for search, routine edits, docs; strong model for Unity/shader/clinical/integration.
- Agent brief = task, file paths, prompt IDs, needed sections of this file, dependencies, expected output, targeted tests. Reply = files changed, done, tests, issues, notes.
- Unity/bakes/builds/benchmarks go through one queue; edits, planning, analysis run in parallel with disjoint file ownership.
- Check `registry.md` before spawning; reuse results. Do not invent token metrics.
