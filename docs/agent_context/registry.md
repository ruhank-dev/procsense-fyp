# Agent registry (update on every spawn/finish)

| Agent | Task | Prompt IDs | Owned files | Status | Output | Depends on | Tests |
|--|--|--|--|--|--|--|--|
| a739d826 (sonnet) | Server NET-11/12/15 | P253, P256 | server/ LAN files, Taskfile demo-laptop | running | pytest | none | targeted pytest |
| af76e7d4 (sonnet) | DemoLoginView hook | P253 | unity Auth/MenuFlow/DemoLoginView | running | EditMode tests | NET-11 server (contract only) | Unity run after (queued) |
| a0d4aa6e (sonnet) | VOX-06/13 spoken equivalents | P253 | voice/exam-action files | running | dotnet+EditMode | none | Unity run after (queued) |

Smoke 14 Nov: EditMode 7/7, PlayMode 16/16 (`D:/ProcSenseWork/tmp/smoke_14nov.txt`); not rerun unless relevant change.

## Queue (dependency-aware)
1. Unity smoke: `python tools/unity/quest_overhaul_smoke.py smoke` (heavy lane, coordinator) - no deps.
2. Parallel light lanes (disjoint files): DemoLoginView hook; spoken exam equivalents VOX-06/13; NET-11/12/14/15 - no Unity needed beyond compile; run change-check per lane.
3. After 1 and 2: flag file, `final` APK build (heavy lane), then single commit, push, one CI run, wait for approval.

## Efficiency log
Record per agent: elapsed time, tool calls, duplicated reads. Token figures only if the harness reports them.
