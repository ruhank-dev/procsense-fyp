# Quest GPU overhaul: checkpoint (14 Nov 2026, UTC)

State at writing: branch `quest-perf/gpu-overhaul`, HEAD `7b446e28` (= origin/main when branched), **nothing committed**. The path count from the first checkpoint (601 uncommitted paths: 423 modified, 144 new, 34 deleted) was not re-counted after the 14 Nov additions. Backup of every changed file from the first checkpoint: `D:/ProcSenseWork/backup/overhaul_20261114_1744/` (`changed_files.tar.gz` 638 files, `deleted_files.txt` 34 paths recoverable from Git HEAD, `status.txt`). Details per lane: `docs/qa/quest_overhaul.md` (sections A-F), headset sheet `docs/qa/quest_overhaul_headset_sheet.md`.

## Finished and tested (latest run results)
- **Rendering profile (A):** MSAA 1, runtime scale 0.8 (eye buffer 0.9), FFR High, dynamic resolution 0.7-0.9, Subsampled Layout, Symmetric Projection, Multiview Render Regions (Final Pass); presets B70-B87, B0 = Impostor on Android; URP asset main-light shadows off (set by coordinator). Tests: profile 4/4 PlayMode, 10/10 EditMode, smoke PlayMode 16/16.
- **Lighting (B):** all lights baked, bake ran, dynamic renderers cast no shadows, 8 unlit statics moved to light probes. Latest EditMode 7/7 and PlayMode 3/3 (`D:/ProcSenseWork/tmp/bakeB_edit3.txt`).
- **Exterior and textures (C):** Impostor exterior (unlit shader, LDR sky), 104 Android texture overrides, 4 prop LODs; exterior tests passed.
- **Characters (D):** LODs on 11 people, shadows and reflection probes off, player rig shadows off; 15/15 targeted tests (Android target).
- **Engine (E):** v0.0.18 verbs (hint, submit, Session.Check, ResultSummary), 222 goldens, session planner/picker/grades; `dotnet test` 1,405/1,405 on the full run before the last small additions (SessionResume tests 3/3 filtered). `content/ai/compat.json` engine_versions now list 0.0.17 and 0.0.18.
- **Networking (F):** pairing, version checks, time budgets, headers, Ollama model check; server tests 12 pass (earlier run), Unity AuthService+LanHttp 21/21.
- **Networking, server side (14 Nov):** P3-NET-11 demo identity (`GET /pairing/demo-identity`; a device token sent as Bearer resolves to a synthetic demo user; LAN profile and synthetic deployment only), P3-NET-12 WebSocket `/ws/headset/{session_id}` (only the shutdown notice is wired), P3-NET-15 HTTPS (`task demo-laptop -- --tls`, certificate fingerprint returned by `/pairing/exchange`). Server tests: 25 pass.
- **Flow UI (E):** MenuFlow, FlowPanel, resume/discard buttons; `MenuFlowTests` 7/7 (Standalone).
- **Smoke (14 Nov):** 7/7 EditMode, 16/16 PlayMode.
- **Targeted run (14 Nov):** EditMode 38/38, PlayMode 27/27, covering the new `PairedDemoSignInTests`, `SpokenExamRulesTests` and regressions.
- **Spoken equivalents, core (VOX-06/13, 14 Nov):** the examination allows speech (decision D3); hint and submit by voice are routed through MenuFlow; critical physical verbs are guarded so speech never gives completion credit for them; 3 Core tests pass.

## Written or compiled only, untested or incomplete
- **Not Completed, and no claim of working on a headset:** NET-11, NET-12 and NET-15 headset sides, the `DemoLoginView` sign-in hook (compiled and tested in EditMode only), NET-14 headset package cache, the Postgres path, and the played-session check (a session played over the LAN, uploaded and verified on the laptop).
- **VOX-06/13 spoken equivalents:** built and unit-tested in Core. The voice/menu equivalence on the headset is not shown, so the rows stay In progress in `docs/deferred-tasks.md`.
- **OpenXR:** the `OpenXR Android Quest (1)` target device is disabled in `unity/Assets/XR/Settings/OpenXR Package Settings.asset`, because Symmetric Projection needs Quest 2 or higher. The Quest 2 target is the one in use.
- **APK:** rebuild in progress. No APK from this work has been run on a headset.
- No visual review of the baked scenes; visibility sweep and walkthrough **not re-run** after the exterior change; text sharpness at scale 0.8 is a headset check.
- Full `dotnet test` not re-run after the latest Core additions.
- Not done by design: clinician-approved exam pool, ActionGateway (Moiz), headset validation (DC-13/14/15), full CI.

## Next commands
1. Done 14 Nov: `python tools/unity/quest_overhaul_smoke.py smoke` (7/7 EditMode, 16/16 PlayMode).
2. When the APK rebuild finishes: create `D:/ProcSenseWork/overhaul_ready.flag`, then `python tools/unity/quest_overhaul_smoke.py final` (production + diagnostic APK to `D:/ProcSenseWork/apk/overhaul`).
3. Commit once on the branch (check `git status` first; the Rocketbox `.mat` changes and `Lightmap-*` files are expected), push the branch, run `gh workflow run ci.yml --ref quest-perf/gpu-overhaul`, then wait for the owner's approval before touching `main`.
4. Headset (Sannan, DC-15): install the APK, pair with the laptop, run one LAN-profile session, and record the result in `docs/testing/results/DC-15.md`. Only then move DT-292 to DT-296 towards Completed.
