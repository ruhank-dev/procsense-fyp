# Final Quest and P3 verification (14 November 2026, UTC)

Evidence levels used: **Source** (read in code/settings), **Editor** (Unity EditMode/PlayMode test run), **Build** (Android build produced and checked), **Headset** (real Quest 2). No row below is Headset-verified. The 72 FPS target is **not** claimed.

Full per-requirement matrices (requirement, expected, actual, evidence, method, result, action): [audit/graphics_matrix.md](audit/graphics_matrix.md) (38 graphics rows) and [audit/p3_matrix.md](audit/p3_matrix.md) (64 P3 rows). They were written from the code, not from status text. This file records what the audit found, what was fixed and what remains.

## Executive summary

| | Graphics (38 rows) | P3 (64 rows) |
|--|--|--|
| Initial audit: PASS | 27 | 36 |
| PARTIAL | 5 | 16 |
| FAIL | 2 | 7 |
| NOT TESTED | 2 | 5 |
| NOT APPLICABLE | 2 (documented rejections) | 0 |

Counts are the initial audit (hand-tallied from the matrix tables). After the fixes below: graphics FAIL 2 to 0 (compositor layers now a documented rejection with a sharp-text alternative, 14 Nov 2026 UTC; performance levels see below), stripping PARTIAL to PASS, performance levels FAIL to PARTIAL (enabled in the config, effect unmeasured). For P3 the seven FAIL rows were the menu flow, the empty examination pool, the two missing content files, the hint-penalty mismatch, the Results/Debrief link and the notice UI; all are now implemented and Editor-tested (see below), so P3 FAIL is 0 at Editor level and every one still needs a headset or played-session check. Rows that depend on the headset stay PARTIAL or NOT TESTED.

**Final APKs:** `D:/ProcSenseWork/apk/overhaul/ProcSense-demo.apk` and `ProcSense-diag.apk` (hashes in the "Final build" section below).
**Walkthrough:** `D:/ProcSenseWork/videos/final/ProcSense-Quest-Overhaul-Walkthrough.mp4` (desktop Unity render, not Quest footage), contact sheet `D:/ProcSenseWork/videos/final/contact_sheet.jpg`.

## Defects the audit found, and fixes applied

| # | Finding (evidence) | Fix | Verified at |
|--|--|--|--|
| 1 | The shipped player never created `MenuFlow`/`FlowPanel` (only tests did): menu, procedure choice, Submit to OT, Results, Retry unreachable; fixed case VAR-S1-A auto-started | `Scripts/Flow/MenuFlowInstaller.cs`, BootLoader hook, `SimulationHost.AutoStartFromConfig` guard (player starts the session from the menu; Editor/tests keep the auto-start), panels rebuild on session change, case title instead of id/seed, Debrief button and stage times on Results | Editor (`MenuFlowInstallerTests`, `MenuFlowTests`) |
| 2 | Examination had no playable case: 63 cases were training-only | `build_case_files.py --grant-assessment`: 63 cases raised to `APPROVED_FOR_ASSESSMENT`, labelled owner authorisation (ADR-0032 D1, INSTRUCTIONS 8i), **no clinician named or signed**; SC-4 and VAR-S1-B stay DRAFT; loader accepts non-P0 assessment only when the record cites ADR-0032 | Source + dotnet 1412 pass + Editor |
| 3 | `content/ui/training_guide.json`, `voice_feedback.json` missing; `Score100` charged hints in all modes | Files built by tools with validators registered in CI; `ResultGrade.From` takes the mode (hints charged in assessment only); Unity reader `Voice/VoiceFeedback.cs` (fallback phrases) shows and speaks the phrase from the router and push-to-talk (14 Nov 2026 UTC) | dotnet + python tests; Editor `VoiceFeedbackTests` 6/6 |
| 4 | Notice socket and banner not connected to the scene | `HeadsetNoticeSession` + `NoticeChipView` started/stopped by `ClinicalPanelsInstaller` with the session; reconnect backoff 2 s to 30 s; shows connected, reconnecting, offline, progress, quota, shutdown, TLS pin mismatch | Editor (fake transport tests); socket on a real Quest **not tested** |
| 5 | Examination dialogue disabled | Patient dialogue enabled in the examination (released facts only). Staff voices: off in the first fix; **owner decision D3 final (14 Nov 2026 UTC): enabled** under `content/ai/staff_exam_policy.json` (staff speak only when spoken to, examination-safe lines; the five practice lines that hinted were replaced by neutral ones; nurse readings are the engine's), applied by the server (`exam_policy.py`) and Unity (`StaffExamPolicy`); scoring and state hash proven identical with dialogue on, off and absent | Editor (`TeamConversationTests` 13/13), server (`test_dialogue_team.py`, `test_dialogue_exam_policy.py` 16/16 on PostgreSQL 17), python validator 5/5; **not a headset test** |
| 6 | Demo identity not in PostgreSQL path | Verified on real PostgreSQL 17: migrations 0001-0010, `ensure_rows`, upload, repeat upload (stored once), status read. Gap found: paired-headset upload was refused for missing consent. A temporary consent skip for the demo identity was tried and then **removed on the owner's final decision D2 (14 Nov 2026 UTC): no exception**. The demo user now answers the server's own consent questions on the headset (Login -> Consent -> Main Menu; `ConsentStep`, `POST /privacy/consent` with the device token, recorded `via="headset"`); missing or declined consent gives 403 `consent_required`, agreement lets the upload through to PostgreSQL; web-only privacy routes refuse the demo token | Real PostgreSQL 17 via Docker (desktop): `test_lan_demo_db.py` (consent missing, declined, granted, withdrawn, per headset, unauthorised), `test_lan_integration.py` over TLS; Unity EditMode `ConsentStepTests`; **not a headset test** |
| 7 | LAN flow unverified | Desktop integration against real uvicorn with self-signed TLS: pinned certificate accepted, different certificate refused (HTTP and socket), pairing, demo identity, speech/orders/ai-health offline fallbacks, notices, reconnect after restart, bad/revoked token, laptop down. Server gaps fixed: `websockets` dependency missing; `generation_progress` and `quota` notices never published (now wired); shutdown reaches clients as close code 1012 under uvicorn | Desktop only; **not a headset test** |
| 8 | Pink noise on glass, window city, benches, frames, TV, OT strip; corridor/reception/OT washed out | Root cause: the 14 Nov bake ran with `-nographics`, leaving the 4 baked reflection probes (Corridor x3, OT x1) filled with garbage (mean RGB about 2835/68/2225, values to 65504). Re-rendered the 4 probes with a graphics device (2 passes); same files/GUIDs, lightmaps and LightingData untouched; `BakeEverything` now refuses to bake without a graphics device | Editor + frames inspected, 0 magenta pixels |
| 9 | 34 deleted lightmap files | Verified intentional: bake changed to non-directional (no `_dir`), no mixed lights (no shadowmask), 8th atlas dropped; new LightingData references exactly the 7 existing `.exr` maps; all three scenes (PatientBay, Corridor, OT) reference the shared asset; nothing restored. Per-renderer lightmap index assignment cannot be read from YAML; confirmed visually in the walkthrough frames | Source + frames |
| 10 | Android XR performance settings off; stripping off; LOD cross-fade on | XR Performance Settings enabled; unused post-processing variants stripped; LOD cross-fade off in the Quest URP asset | Source + build |
| 11 | OpenXR validation refused the diagnostic build: Symmetric Projection needs Quest 2 or higher | Quest (1) target device disabled in the Android Meta feature (Quest 2 is the target) | Build |
| 12 | Stale tests of superseded decisions | `NeutralDaylightContractTests` rewritten to the P253 contract (all baked, non-directional, no realtime shadows, lightmaps present); equipment/folder shader asserts accept Simple Lit; exam-pool tests updated | Editor |

An interrupted diagnostic build had left Android scripting defines, frame-timing stats and the insecure-http option modified in `ProjectSettings.asset`; they were restored to the committed values (caught by `BuildScriptTests`).

## Graphics audit result (summary; rows in graphics_matrix.md)

PASS in source and Editor: Android Medium to `URP_Quest_Android`, Vulkan only, OpenXR multiview, URP forward, SRP batcher, HDR off, MSAA 1, no depth/opaque texture, depth priming off, no post-processing or motion blur, subsampled layout, symmetric projection, buffer discards, render regions, FFR High, eye buffer 0.9 with runtime scale 0.8 (no later script overwrites), dynamic resolution 0.7-0.9, ASTC overrides, all-baked lighting with shadows off on rig and characters, AppSW documented as rejected.

Added 14 Nov 2026 UTC (later the same day):
- **Config verifier** `tools/unity/verify_quest_config.py` (change-aware check `quest_config_check`) asserts the approved items from the project files. Its first run found that **row 10's XR Performance Settings fix was not in the asset** (`XrPerformanceSettingsFeature Android m_enabled: 0`, same as HEAD); it is enabled again. Result after the fix: 28 PASS, 2 WARN (default texture compression unset; frame timing stats off in the release APK, so dynamic resolution may hold 0.8 there), 0 FAIL. Source level only.
- **Compositor layers: investigated, not adopted** (graphics row "compositor layers for UI" is now NOT APPLICABLE with the reason recorded). `com.unity.xr.compositionlayers` 2.5.0 is available offline with the Editor and OpenXR 1.18 supports it, but its interactive-UI path adds a hidden camera and render texture per panel, quad overlays hide the controller ray and anyone in front of the panel (the fix needs a non-single-pass projection rig), and it cannot be verified off the headset. Implemented instead for the EMR tablet, EMR, monitor and menu panels: one shared dilated SDF material, a dynamic-resolution floor of 0.8 while such a panel is read, the menu's real 1.5 m reading distance for the text floor, and an estimated eye-buffer size report. Editor tests: see the graphics matrix section; **legibility is not claimed** and stays a headset check (item 2 below).

Still open (honest):
- **Compositor layers for UI text: not adopted** (documented rejection, sharp-text alternative in place). Prototype one quad layer (monitor) on a branch only if the headset shows illegible text with the alternative.
- Whether the Quest compositor honours the runtime render scale, and whether `FrameTimingManager` returns GPU time (dynamic resolution): **headset**.
- No fullscreen copies and text readability: **headset or frame capture**.
- Fallback profile if the XR display does not start is a logged warning only; Quest 2 is the sole target.
- LOD groups exist only on Staff (10) and PatientBay (4); Corridor/OT have none (add only if profiling shows vertex cost). 114 textures have no Android override; about 200 are capped at 2048.

## Remaining P3 gaps (rows in p3_matrix.md)

- Done 14 Nov 2026 UTC (Editor/Core): goals-inactive reasons in Core (DT-298, `Observation/Availability.cs`) and the P3-TRN-03 training board (`Scripts/UI/Training/`, hidden in the examination); both pathways covered by `PathwayCoverageTests`. Reconciliation of every open row: [audit/p3_matrix.md section 11](audit/p3_matrix.md). Done 14 Nov 2026 UTC (voice agent): the `voice_feedback.json` reader and the VOX-06 voice-only identity/allergy/consent examination tests (Core `VoiceOnlyExaminationTests`, PlayMode `SpokenExamVoiceOnlyTests`). Still open: the avatar per case (character assets), the stale FlowPanel refusal text (Flow owner), board summon icon and tick feedback (TRN-04/05).
- Notice banner on a Quest; socket and certificate pinning behaviour under IL2CPP.
- A played session over the LAN uploaded to the laptop database (the path is verified on desktop only).
- Clinician approval: **not claimed**. Case approvals are owner-attested; no clinician is named.
- Headset checks (DC-13/14/15) are open. P3-QA-04 is labelled Done in the register but has no headset evidence.
- The built APK was scanned for secrets: see the final build section.

## Remaining real-headset tests

1. Install the diagnostic APK; record GPU ms, FPS, dynamic scale and FFR level (DC-13). Confirm render scale and GPU performance level take effect.
2. Read text legibility on the EMR tablet, monitor and menu panel at scale 0.8.
3. Menu to scenario to Submit to OT to Results to Retry with controllers; confirm pointer works on the world-space panel.
4. Pair with the laptop; check certificate pinning (accept pinned, refuse other), notice chip states, offline and reconnect.
5. Voice: push-to-talk, spoken actions, examination patient dialogue, hint/submit by voice.
6. Visual pass of the baked rooms on the headset (reflection probes, exposure).

## Final build (15 November 2026, UTC)

Final verification run 14 Nov 2026 10:30 PM UTC to 15 Nov 2026 2:07 AM UTC, on the uncommitted tree of branch `quest-perf/gpu-overhaul` (engine 0.0.19). Not run on a headset.

- **Content.** `content/` copied into `unity/Assets/StreamingAssets/content` (the copy `BuildScript.CopyContent` makes); all 765 manifest entries match by SHA-256, no extra files; content hash `5e04ce0fe71ceb9e...` (after the fixes below).
- **dotnet Core.** 1,438/1,438 passed. A rerun while the full Python check set was running on the same machine failed two timing tests (`JsonLogicTests.Ten_thousand_evaluations_are_fast`, `CaseGraphTests.Two_hundred_goals_are_cheap_per_action`; the run took 5x longer); both pass on their own: load flake. Golden runs 222/222 and generated goldens 14/14.
- **Python checks** (`run_all.py --quick`): 187/194 after the fixes. The 7 left: `project rules` hygiene and its test `test_the_real_repository_is_clean` (only the two PatientBay lightmap EXRs over 10 MB; `git check-attr filter` says `lfs` for both, so they pass once staged through LFS), and five generated pages that were out of date because the register changed (step audit, medical review pack, register triage, engineering audit, execution queue), regenerated with this note. `verify_quest_config.py`: 28 PASS, 2 WARN (DT-306 owner items), 0 FAIL.
- **Server** (own PostgreSQL 17 container, removed afterwards): 544 passed, 3 skipped (live Supabase/Ollama checks), 0 failed; includes consent granted/declined/withdrawn, LAN TLS pinning (accepts the laptop certificate, refuses another), WebSocket notices and reconnect after a restart. Run before the rubric fix below.
- **Unity EditMode** (full): 310/310.
- **Unity PlayMode** (full): 337 total, 312 passed, 18 skipped, 7 failed. `CharacterPerfCullTests` 3/3 passed in the full run. Six failures (`ClinicalTextTests` x2, `ExamPresentationTests` x3, `NoRealtimeLightingTests` x1) are order-dependent: they pass 12/12 on their own and fail again when run after the capture probes `B58VisualCapture` and `BrightnessJumpProbe`, which are not `[Explicit]` (unlike the other capture tools), load Boot/PatientBay and leave runtime objects behind. The seventh is `TextureCropProbe` (a capture tool that needs `TEXPROBE_DIR`). Product code not at fault; DT-307. **Resolved 15 Nov 2026 UTC (DT-307 Completed):** the probes are `[Explicit]` capture tools with full scene cleanup (`BrightnessJumpProbe` removed as obsolete) and the three fixtures reset the scene first; full PlayMode rerun 336 total, 316 passed, 0 failed, 20 skipped (Explicit/Ignored); EditMode 310/310. Tests-only change; the shipped build is unaffected. A product finding from the old failure (runtime clinical key) is DT-308. Editor logs: no missing-script, missing-reference, GUID or texture warnings; all 8 build scenes loaded.
- **Smoke before the builds:** EditMode 7/7, PlayMode 16/16.

Defects fixed in this run (generated content that the lanes left stale; no test weakened):
1. `tools/golden/p0_scripts.py` now writes the 18 golden scripts the golden-fix lane edited by hand (DT-303: `document_treatment` before `decide_ready`, the vital-event SBAR calls, SC-31's critical potassium call); `--stale` passes and the scripts are unchanged byte for byte.
2. `tools/content/build_scoring.py`: rubric rule for the new `info.hint_used` item (P3-EXM-01; behaviour b16, 0 rubric points; the 5-point cost stays in `grades.json`); `content/scoring/rubric.json` regenerated.
3. `content/blocks/**` regenerated (300 blocks still said `APPROVED_FOR_TRAINING` after the case files moved to `APPROVED_FOR_ASSESSMENT`); manifest regenerated.
4. `docs/deferred-tasks.md`: DT-304 row moved into order (planning sync check).

Both APKs: zip test passed, package `com.procsense.fyp`, arm64-v8a only with `libil2cpp.so` and `libUnityOpenXR.so`, secret scan of 1,237 non-media entries each (no `sk-`, `AIza`, `service_role`, `OLLAMA_API_KEY`, JWT or private-key pattern), 767 `assets/content` files including `ui/interactions.json`, `ui/ui_messages.json`, `ai/staff_exam_policy.json`, `ui/voice_feedback.json`, content hash in the APK equal to `content/manifest.json`; the build report lists the PatientBay lightmaps (7 EXR) and `LightingData.asset`. The builds changed no tracked file; `ProjectSettings.asset` equals the committed version (no defines, `enableFrameTimingStats: 0`, `insecureHttpOption: 0`).

| APK | Bytes | SHA-256 |
|--|--|--|
| `D:/ProcSenseWork/apk/overhaul/ProcSense-demo.apk` | 252,380,183 | `6fe3dcd5975211cbb3262eff52773f04d902c7f65e1b903f427306d9c39d0ea2` |
| `D:/ProcSenseWork/apk/overhaul/ProcSense-diag.apk` | 252,429,883 | `44c9fab87ea2fcee8acd47b68436b75b18bd5a7e8dd328f3d233f73cf0cf92a7` |

These two APKs were rebuilt from commit 98da6c71 (engine 0.0.20, content hash f6fc71243e1abec1) after the first pair; smoke EditMode 7/7, PlayMode 16/16 before the build; working tree clean after the builds. GitHub CI (full dispatch run) passed on 98da6c71. Not run on a headset.

Previous build (14 Nov 2026, superseded): demo `2c269302...`, diag `b8f0dc10...`.
