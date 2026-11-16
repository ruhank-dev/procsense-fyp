# Quest 2 GPU overhaul, working notes (branch quest-perf/gpu-overhaul)

Baseline from the headset (Moiz): about 34 FPS, 96% GPU, 24% CPU, 26.47 ms application GPU time at 72 Hz, foveation off. Target: native 72 FPS, GPU time under 13.89 ms (ideally 11 to 12 ms). Desktop numbers do not predict the Quest. Each agent adds its own section below (re-read the file before editing).

## B. Lighting and shaders (Agent B)

Goal: zero realtime shadow maps and zero realtime light calculations in the three rooms. Tool: `unity/Assets/Editor/Setup/BakeEverything.cs` (`Apply`, `Bake`, `Report`, `MaterialsDryRun`, `MaterialsApply`); every change is scene, light, material or lighting-settings data, so `git checkout` reverts it.

| Change | Before | After |
|---|---|---|
| Lights not baked (enabled) | 13 (2 Mixed suns with shadows, 8 realtime corridor fills, 3 realtime "Soft clinical key" directionals) | **0** (suns, fills and keys set to Baked; the 3 weak overhead keys, which existed only for realtime contact shadows, are switched off) |
| Dynamic (non-static) renderers casting shadows | 265 of 423 | **0** (shadow casting and receiving off; static renderers keep casting so the baked shadows stay) |
| Lighting settings (`PatientBay.lighting`, shared by all three rooms) | realtime environment lighting on, directional lightmaps | realtime environment lighting **off**, **non-directional** lightmaps (one texture fetch less per pixel) |
| Sun shaft shader | `ProcSense/Sun Shaft`: about 16 hashes per pixel, double-sided | `ProcSense/Sun Shaft Baked`: one 128x128 tileable noise sample, back faces culled, half precision; same property names, so the two shaft materials switched in place |
| Dust motes | up to 150 per system | at most 60 per system (emission scaled), unlit shader unchanged |
| Lit materials in the three rooms | 222 | **52 moved to Simple Lit** (opaque, non-metallic, not glossy: posters, prints, plants, fabrics, ceiling tiles, wall paint, props); 170 stay Lit (glass, floors, metals, monitors, characters, city, glossy or transparent) |
| Runtime guard | none | `QuestLightingGuard` (Android): at every scene load, lights that are not baked get no shadows and non-static renderers cast none |
| Reflection probes | 6 (baked or custom cubemaps, 512 / 256 / 128) | unchanged: already static |
| Light probes | one group per room (132 + 108 + 108) | unchanged: characters and doors (non-static) are lit by probes |

**Bake with a GPU device (14 Nov 2026 UTC fix).** The 14 Nov bake ran `-batchmode -nographics` (Null device): lightmaps were fine, but the 4 baked reflection probes (Corridor x3, OT x1; `ReflectionProbe-0..3.exr`) came out as uninitialised memory (magenta/blue noise up to 65504), which drew pink noise on glass, posters, chrome, the TV and the window city, and blew glossy floors and benches out to near-white in Corridor, reception and OT. The bay looked right because its probe is a custom cubemap. Fix: `BakeEverything.BakeReflectionProbes` (batch mode without `-nographics`, two passes, writes the same files, lightmaps and LightingData untouched); `Bake` and `BakeReflectionProbes` now refuse to run on the Null device.

Test: `NoRealtimeLightingTests` (every enabled light is baked, no moving renderer casts a shadow, the guard fixes late additions). It needs the re-bake below, because a light reports its baked type only after a bake.

**Needed in the Quest URP asset (owned by Agent A, not edited here):** with no realtime lights at all, `m_MainLightShadowsSupported` can be 0 and `m_AdditionalLightsRenderingMode` 0 (disabled); that also lets URP strip every shadow and additional-light shader variant on Android.

Deliberately not done: no realtime lights are kept "for realism"; the doors stay non-static and are lit by probes; character and city materials are left to Agents D and C.

## A. Quest rendering profile (Agent A, 14 Nov 2026)

Baseline from the headset (Moiz): about 34 FPS, 96% GPU, 24% CPU, 26.47 ms application GPU time, 72 Hz, foveation off, 4x MSAA, eye scale 1.0. The bottleneck is pixel work, so every change below cuts GPU pixels. Nothing here is measured on a headset yet: the targets (13.89 ms for 72 FPS, 11 to 12 ms for headroom) are unverified, and 72 FPS is not claimed.

| Setting | Old | New | Where | Status |
|---|---|---|---|---|
| MSAA | 4x | **1x (off)** | `URP_Quest_Android.asset` `m_MSAA` | applied; 2x/4x remain diagnostic presets B81/B82 |
| Eye buffer scale | 1.0 | **0.9 allocated, drawn at 0.8** (`XRSettings.eyeTextureResolutionScale` 0.9 plus `renderViewportScale` 0.889) | `QuestPerformanceProfile` (run time, Android player only) | applied; headset check for text sharpness |
| Dynamic resolution | none | **0.7 to 0.9 of native**, from the GPU frame time (FrameTimingManager), 6 samples over 12.5 ms lower it by 0.03, 120 samples under 10 ms raise it by 0.02, 30-sample cooldown | `QuestPerformanceProfile.Controller` | applied; it moves the viewport scale only (no buffer re-allocation); whether the compositor honours the viewport scale on this Vulkan path is a headset check |
| Fixed foveated rendering | off (0) | **High (1.0)** | `QuestRenderTier.DefaultFoveation`, applied at start by the profile driver | applied; Quest 2 has no eye tracking, so no eye-tracked foveation |
| Subsampled Layout (Vulkan) | off | **on** | OpenXR `FoveatedRenderingFeature Android` `enableSubsampledLayout` | applied; needs Vulkan (it is) and foveation |
| Symmetric Projection | off | **on** | OpenXR `MetaQuestFeature Android` `m_symmetricProjection` | applied; Vulkan + multiview; not combinable with SpaceWarp (off) |
| Multiview Render Regions | None | **Final Pass** | `m_multiviewRenderRegionsOptimizationMode` 0 to 1 | applied; there is no post-processing, so the forward pass is the final pass |
| Optimize Buffer Discards | on | on | `m_optimizeBufferDiscards` | unchanged |
| Performance levels | SustainedHigh hint | unchanged | `QuestRuntimeSettings` | unchanged |
| Application SpaceWarp | off | **off** (feature not enabled in the build) | `SpaceWarpFeature Android` | see below |

Unchanged on purpose and verified: Vulkan only, multiview, URP Forward, Render Graph (the only path in 6000.5), Intermediate Texture Auto, depth and opaque textures off, HDR off, SRP Batcher on, no post-processing, no camera stacking, no extra cameras (see `quest2_config_matrix.md`).

The URP asset `m_RenderScale` stays 1.0 on purpose: the scale is applied through the XR eye texture and viewport scale, and setting both would multiply them.

### Text readability at 0.8
The monitors, labels and UI are world-space, drawn into the same eye buffer, so they lose the same 20% as everything else; foveation High only reduces the periphery. OpenXR compositor layers (separate, native-resolution quads for text) need the `com.unity.xr.compositionlayers` package, which is **not installed**; adding a runtime dependency for it was not done. If monitor text is not sharp enough on the headset, the order of remedies is: raise `StartScale` toward 0.9 (preset B76), enlarge the monitor text, or add the composition-layers package as a separate decision.

### Application SpaceWarp (optional, off)
The OpenXR package has `SpaceWarpFeature` (`XR_FB_space_warp`) and a run-time switch `SpaceWarpFeature.SetSpaceWarp(bool)`. It needs the feature enabled **in the build**, produces synthesised frames (about 36 rendered plus extrapolated, not native 72 FPS), needs motion vectors and depth (extra GPU cost), is not combinable with Symmetric Projection, and adds compositor cost with Subsampled Layout. It is therefore **not enabled** in the production build. Preset B84 calls `TrySetSpaceWarp(true)` and does nothing (returns false) in a build without the feature. Moving hands, characters, transparent UI, dust and sun shafts are the artefact risks (transparent surfaces write no motion vectors). Native 72 FPS comes first.

### Diagnostic presets (runtime-switchable in the diagnostic build; `PerfPresets.cs`)
B70 old production baseline (4x MSAA, 1.0, foveation off), B71 aggressive profile explicit, B72 to B75 foveation off/low 0.25/medium 0.5/high 1.0, B76 to B80 fixed scales 0.9/0.85/0.8/0.75/0.7, B81 MSAA 2x, B82 MSAA 4x, B83 no dynamic resolution, B84 SpaceWarp. B0 is the aggressive profile on Android; older B-presets are deltas against it. The metrics overlay is `QuestPerfDiagnostics`, compiled only with `PROCSENSE_PERF_DIAG` (diagnostic builds), not in normal gameplay.

## F. Networking and AI (Agent F, 14 Nov 2026)

State inspected, not trusted from labels: every P3-NET task was "Planned"; the server routes (ASR, speech action, orders, dialogue, TTS, evaluate, debrief, scenario packages, `/ai/health`) already exist and are tested.

| Task | Result |
|---|---|
| P3-NET-01 (record D2) | Already recorded by ADR-0032 (D2); no new ADR needed |
| P3-NET-02 address policy | Done: `ServiceSettings.IsAcceptable` accepts plain http to a private IPv4 address (10/8, 172.16/12, 192.168/16) only when `AllowPrivateHttp` is on (PROCSENSE_DEMO, PROCSENSE_DEV or a development build); names and public addresses are refused; `BuildScript` sets `insecureHttpOption` per variant (study: not allowed; dev: development only; demo and diagnostic: always allowed) and restores it. EditMode test added (run result below) |
| P3-NET-04 `task demo-laptop`, `demo-laptop-check` | Done (`tools/setup/demo_laptop.py`): checklist, LAN addresses, pairing code, server on 0.0.0.0 with the LAN profile. QR code not done |
| P3-NET-05 firewall helper | Done (`tools/setup/firewall_rule.ps1`); not run on a clean machine |
| P3-NET-07 pairing | Done on the server (`server/lan/pairing.py`, `/pairing/*`, middleware `LanPairing`): six-digit code, hashed device tokens kept across restarts, five wrong codes lock a minute, revoke from the laptop only. Headset side (code entry screen, token storage) is Moiz's UI work, not done |
| P3-NET-10 version compatibility | Done on the server: `/health` fields, `content/ai/compat.json`, 426 on an unsupported engine header, test that fails when CoreInfo.Version is not in the table. Headset sending the headers: not done |
| P3-NET-09 time budgets, request ids | Done: `LanHttp` (wraps `UnityHttp`, used by `AuthService.Current` so every AI client gets it): per-kind budgets (health 2 s, ASR 6, action/orders 8, dialogue 10, TTS 4, never above the caller's wait), request ids, one in flight per kind, late answers marked `Superseded` and dropped, one retry for a failed GET, never for a POST. EditMode tests (21/21 with AuthServiceTests) |
| P3-NET-07 headset side | Done: `PairingClient`, `DeviceTokenStore` (token kept on the device only), `PairingPanel.Open(parent, baseUrl, closed)` keypad built from `UiKit` (no prefab, one canvas, default UI material). Entry point sent to Agent E; Settings/Main Menu must call it |
| P3-NET-10 headset side | Done: `LanHttp` sends `X-ProcSense-Build/Engine/Content` and `X-ProcSense-Device`; `PairingClient.CheckCompatibility` + `Banner` (engine or API mismatch: voice off, menus work). Showing the banner/chip is Agent E / P3-UIX-11 |
| P3-NET-13 model-name check | Done in `task demo-laptop-check`: every model in `content/ai/model_routes.json` must be listed by Ollama (cloud names match with or without `-cloud`); 3 tests in `tools/ci/test_demo_laptop.py` |
| P3-NET-11 demo identity | Server side done 14 Nov (reviewed change to the auth dependencies: `server/lan/demo_identity.py`, `GET /pairing/demo-identity`; tests without a database; database path and headset play untested). Earlier note:  every AI route resolves a database user and organisation (`require_member`), so a database-less synthetic identity for a paired headset is a change to the authentication dependencies and needs a review. Works today by signing in against the laptop's own Postgres (device-code flow) |
| P3-NET-12 WebSocket, P3-NET-15 HTTPS | Server side done 14 Nov (`/ws/headset/{session_id}`, `server/lan/tls.py`, `task demo-laptop -- --tls`, fingerprint in the pairing reply); headset pinning and socket client need Unity (queued). P3-NET-14 headset package cache | Deferred: the plan marks them optional ("the headset works identically without it"); the authored case pool is on the device, so the demo does not need them |
| Voice flow review | Checked in code, no change needed: a spoken action goes through `TabletView.PressId`, the same call a button press makes, so the engine decides and scores it identically; `PushToTalk` shows the offline banner and the tablet menu at once when `VoiceHealth` is offline or ASR is unavailable; local-model proposals need a confirmation; repeated recognitions within 4 s are handled once |
| P3-NET-03, 06, 08, UIX-11 | Moiz-owned (runtime address, discovery listener, status model, chip): not touched |
| P3-NET-17 / DC-15 | Needs the headset and Wi-Fi: Sannan |

Tests: `server/tests/test_lan_pairing.py` (8 cases) and `test_health.py` pass; the laptop checklist runs and honestly reports Ollama not running. Real blockers that stay external: the LAN round trip needs a headset on the same Wi-Fi, a private network profile and Ollama signed in or `OLLAMA_API_KEY`; nothing here was tried over a real network.

## E. Gameplay and integration (Agent E, 14 Nov 2026)

Inventory: `docs/qa/quest_overhaul_ruhan_inventory.md` (generated by `tools/plans/ruhan_inventory.py`; name search, so it shows existence, not correctness). Of the 84 Ruhan-owned P3 tasks the index labels almost all Planned; the code shows the Core flow pieces (session planner, picker, grade, summary, hint and submit verbs) were missing, and the LAN/performance items were already in progress.

Implemented now (pure Core, xUnit, no engine or golden change):

| Task | What | Where | Test |
|---|---|---|---|
| P3-SCN-02 | Case catalogue generated from the 65 case files (procedure S1 cholecystectomy / S2 appendicectomy, tier, status copied, stacked, setting, sex, age band, label, avatar profile key), `--check` in `task ci`, in `task content-refresh` and in the manifest | `tools/content/case_catalogue.py`, `content/ui/case_catalogue.json` | `ScenarioFlowTests` |
| P3-SCN-03/04 | `ScenarioPicker`: mode, procedure, level, seed, recent ids to a case; never a draft or below the mode's status (`ContentGate`); beginner gets no stacked case while another exists; the last three not repeated unless the pool is too small (reason returned); uniform over 10,000 seeds | `src/ProcSense.Core/Flow/ScenarioPicker.cs` | 17 tests |
| P3-SCN-05 | `BriefingFacts`: name, age, sex, setting and the patient's own reason for admission only | `Flow/BriefingFacts.cs` | reflection test on the fields |
| P3-SCN-09 (Core) | Retry keeps case and seed; new scenario draws a new seed and another case | `ScenarioPicker.Retry/NewScenario` | tests; the golden replay hash test is still to do |
| P3-SES-06 (Core) | `SessionRequest`, `SessionPlanner` (refusal codes, seed drawn and recorded, log header fields) | `Flow/SessionRequest.cs` | `SessionPlannerTests` |
| P3-RES-10 | `ResultGrade.From(ScoreReport, correctPathCredits, hints)`, `GradeTable` read from `content/scoring/grades.json` (D4: A 90, B 80, C 70, D 60, pass mark 60, hint 5); a critical failure or a FAIL result is never a pass | `Flow/ResultGrade.cs` | tests |

The examination pool is still empty: no case is `APPROVED_FOR_ASSESSMENT`, and I did not raise any approval (P3-SCN-07 and P3-SCN-12 need the clinician's signed review, DT-237). `ContentGate` is used unchanged, so approving cases later changes data only.

Not done (needs engine, Unity scene or other-owner work; none marked complete):
- Engine verbs `request_hint` (EXM-01) and `submit_patient` (EXM-05), `Session.Check` (SES-02): they change the engine and need the golden hashes and engine version handled with the rules-engine owner's review; not attempted without that.
- `ResultSummary.From` (RES-02), stage timing (EXM-08), the correct-path credit table per case (needs the golden correct scripts run through `Scorer`).
- Unity: menu, briefing card, results panel, voice routing to the same gateway, training guide and contextual interaction content (TRN-01, CTX-01), save/resume (SES-09), start-time validation with the avatar check (SCN-08, needs the avatar profiles of P3-AST-03). Environment scenes were not touched.
- Voice and menu equivalence (VOX-13) and the headset items (DC-13/14/15) stay with the headset testers.

## D. Characters and animation (Agent D)

**D scene edits applied** (Unity batch `CharacterQuestOptimiser.Run`, 14 Nov 2026): prefabs under `Assets/Prefabs/Characters` and the three scenes (PatientBay, Corridor, OT) were changed; Agent B can bake.

What changed (flags and materials only; no mesh, rig or script changed):
- **No realtime shadows on any skinned renderer** (characters, gown, curtains): `shadowCastingMode` Off, `receiveShadows` off, `reflectionProbeUsage` Off, light probes on (BlendProbes). 151 renderers changed. The baked patient contact shadow stays (the `Patient baked shadow proxy` is a bake-time-only tool, renderer off at runtime).
- **Cheaper character materials:** `_EnvironmentReflections` and `_ReceiveShadows` off on the 180 URP materials those renderers use (two fewer samples per pixel and no shadow-map variants; specular highlights kept).
- **LODGroups on 11 multi-part people** (staff prefabs, MPFB patient, unpacked corridor receptionist): LOD0 all parts above 30% screen height (about 2.5 m); LOD1 (12%) drops teeth, tongue, brows and lashes (teeth alone are about 7,000 triangles); LOD2 (4%) also drops shoes and accessories; culled below 4%. Facial blendshapes sit on the body mesh, which every LOD keeps, so speech and expression state is never lost. Cross-fade off (its variants are stripped on Android); the bands act as hysteresis.
- **Animation cost:** unchanged and already handled by `CharacterPerfCuller` (animator culling with held pose, locked bounds; 12 tests). There is no Unity `Cloth` component in the project (curtain and gown motion is CPU script code), so no GPU cloth cost to remove. Skinning is two bones at the Quest quality level and the earlier sort/normalise fix keeps every mesh valid (`SkinWeightTests`).

Audit (before): Rocketbox people are single meshes of 6.7 to 9.8 thousand triangles; the MPFB/staff people are 9 to 17 skinned parts and 46 to 65 thousand triangles; three curtains are 21 thousand skinned triangles each; the patient gown 16 thousand.

Tests (Android build target, targeted): `CharacterPerfTests`, `CharacterPerfCullTests`, `SkinWeightTests`, `EnvironmentIntegrationTests`, `ModeledDoorTests`: 15 of 15 pass.

Not done, with reasons:
- **Decimated body meshes (LOD by triangle count).** Unity 6000.5 has Mesh LOD (`ModelImporter.generateMeshLods`, `Renderer.meshLodSelectionBias`, `QualitySettings.meshLodThreshold`; probe output in `D:\ProcSenseWork\reports\meshlod_probe.txt`), but whether it supports meshes with blendshapes and skinning here is unverified and it reimports 28 models. Candidate for a later step, after a headset check shows characters matter.
- **Curtains (3 x 21,000 skinned triangles) and gown:** large on screen but static most of the time; converting idle curtains to unskinned meshes needs a script change in `CurtainCloth` (Agent C/B scope on the bake). Candidate.
- **Hair-card transparency:** hair and lashes are already opaque URP/Lit in these prefabs; the single transparent character material is the Rocketbox opacity set on the m162 patient.
- **P3 character and animation tasks:** P3-ANI-01..12 are owned by Sannan and Moiz; only P3-PRF-24 (character culler, done) and the animation budgets part of P3-ANI-11 (culling plus the LODs above) fall to this lane; the ANI-11 CPU measurement is for the headset (DC-13).
- **Texture sizes for characters:** the character textures are 2048 on all Rocketbox/staff materials; Android overrides belong to Agent C's `AndroidTextureBudget` (character list left to its rules).

## C. Environment, exterior and textures (Agent C, 14 Nov 2026)

**C scene edits applied** (Corridor, PatientBay, OT are saved with them; Agent B may bake now). Nothing else of mine touches scenes.

- **Impostor exterior (Android default).** `QuestExterior.Variant.Impostor` = the optimised chunk geometry drawn with `ProcSense/Exterior Unlit` (`Assets/Shaders/ExteriorUnlit.shader`): one texture sample, sun/shade from the normal and street-canyon darkening from the height computed per VERTEX, no lights, shadows, normal or metal maps, no keywords, SRP Batcher compatible, optional alpha test for leaf cards. 16 unlit materials (`Assets/Materials/Environment/City/Impostor`) replace the Lit ones on 67 city renderers; the 8k HDR sky is replaced by a 2048 x 1024 LDR panorama (`city_skyline_ldr_2048.png`, made by `unity/Tools/Environment/hdr_to_ldr.py`; the camera has HDR off, so the picture is the same) with an ASTC 6x6 Android override: the sky goes from a 4096 x 2048 RGB9E5 texture (about 44 MB with mips) to about 1 MB. `Original` and `Optimised` are still selectable and restore the original Lit materials and sky exactly (test `Impostor_variant_swaps_to_the_cheap_materials_and_back`). Portal/chunk culling now runs for every non-original variant (`ExteriorVisibility`: `Current != Original`). Builder: `ExteriorImpostorBuild.Run` (re-runnable). **For Agent A:** add a preset flag for `QuestExterior.Variant.Impostor` in PerfToggles/PerfPresets (the existing `OptimisedExterior` still means Optimised).
- Honest scope: the exterior is NOT rebuilt as billboards/atlased boxes: the desktop run showed the old optimised city was neutral, the cost on the headset is per-pixel (Lit with normal/metal maps over every window pixel), so the cheap surface was the change that matters; far impostors and a baked panorama of the mid layers are not built. Whether the Impostor variant beats Optimised on the headset is a Quest A/B (diagnostic variants Original / Optimised / Impostor).
- **Texture budget** (`AndroidTextureBudget.Apply`, report `docs/qa/quest_texture_budget.md`): Android-only maximum sizes by use; 103 textures lowered (effective Mpx: Hospital/PBR 119.0 to 51.4, City 38.3 to 5.1, Environment 25.2 to 3.2, PatientBayWood 33.6 to 2.1 before the floor colour map was put back to 2048, FinalLook 28.8 to 16.3, PatientBayLook 24.0 to 14.5, Corridor 22.6 to 13.6, Bed 11.2 to 3.3); ASTC 6x6 colour/data, 5x5 normals; anything text-bearing (monitor, label, poster, sign, board, chart, screen, print, UI, records) and all characters are excluded; desktop import settings untouched.
- **Prop LODs** (`PropLodBuild.Run`, report `docs/qa/quest_prop_lods.md`): 4 LOD groups (3 levels, screen-height switching 22% / 7%, culled below 1.5%) on unlightmapped readable static props over 2,500 triangles. Most heavy props are lightmapped (35 skipped) or unreadable (9 skipped), so LODs are a small part of the saving. Note for Agent B/D: `Patient baked shadow proxy` (38,872 triangles) has a LOD group but should simply go once realtime shadows are removed.
- **Not done in this pass:** a Sweep_poses/Walkthrough re-run (culling logic is unchanged apart from the equivalent `!= Original` test), billboard/atlas exterior rebuild, LODs for lightmapped props (needs the bake to carry LOD lightmaps), `Window exterior backdrop` in the bay is inactive and was left alone.

### E (continued): engine verbs and the result summary (14 Nov 2026)

Engine version 0.0.18 (`CoreInfo.Version`, `tests/golden/engine_version.lock` updated): the existing 190 golden runs pass unchanged (their hashes did not move); 32 new goldens were added.

| Task | What | Evidence |
|---|---|---|
| P3-EXM-01 | `request_hint`: next pending goal, two tiers (think about / do), limit 3 (`HINT_LIMIT`), logged, never changes clinical state; the examination records `info.hint_used` and the 5-point cost is applied by `ResultGrade` (once per hint, assessment only) | `ExamVerbsTests` (determinism through replay, state hash unchanged, cost only in assessment, fourth refused) |
| P3-EXM-05 | `submit_patient`: outcome proceed (default) or transfer_request, `abandoned` flag; goals still open are missed through the existing path; a case not yet ended resolves to SAFETY_FAILURE; all later actions and time are refused (`SESSION_SUBMITTED`) | 32 goldens for the 16 P0 cases: correct run + submit gives the same verdict and end state as the correct run; submit at the start is FAIL with the missed goals |
| P3-EXM-08 | stage durations and total minutes in `ResultSummary` (recorded only, no time pressure) | `ResultSummaryTests` |
| P3-RES-02 | `ResultSummary.From`: verdict, end state, points, criticals, completed and missed goals, hints (used and charged), times, first recommendations from the Debrief, headline ("Not completed" when abandoned); grade only when the case has a correct-path baseline (`content/scoring/correct_path_credits.json`, generated by `procsense-sim credits`, 45 of 63 playable cases; the other 18 have no correct golden and show the verdict without a percentage) | `ResultSummaryTests` (equals Scorer for three cases; the correct run scores 100) |
| P3-SES-02 | `Session.Check(action)`: validates in a throw-away copy rebuilt from the session's own log (`Session.Checker` set by the starter); the session, log hash and clock are untouched | agrees with Apply on the first 14 actions of one golden log; it is O(log length), for button states not per frame; agreement on every golden log is still to do |
| P3-SCN-08 | `SessionStarter`: manifest check, review-status gate, avatar availability; a case that cannot start is skipped with its reason; a manifest mismatch refuses the start (`CONTENT_REFUSED`); a chosen case is never replaced | `SessionStarterTests` (the avatar profiles themselves are P3-AST-03) |

Still not done: Unity menu, briefing, hint, submit, results and settings screens (the rows are Moiz's: P3-MNU, P3-EXM-03/04/06, P3-RES panel, P3-UIX), the ActionGateway (P3-SES-03, Moiz) that these verbs plug into, voice and menu equivalence (P3-VOX-13, needs the gateway and the fake recogniser), save and resume (P3-SES-09, needs the Unity lifecycle), `Session.Check` agreement on every golden log, and the retry golden replay (P3-SCN-09). `SessionConfig`, `ModeChoice` and `SimulationHost` still start a fixed case; the planner and starter above are what the menu should call.
| P3-SES-09 (Core) | `SessionResume.Open`: a saved log is replayed to its last action and continues (same hash); refused with a fixed code when the engine version, content version or hash differ, the log is malformed, or the case is no longer allowed for the mode | `SessionResumeTests`; the Unity flush on pause/quit and the Resume/Discard buttons are not built |

Unity screens, the ActionGateway and voice/menu equivalence were not started: the Unity lock was held by the lighting bake, and adding unverified Unity C# risks breaking compilation for every lane. They are next, once the lock is free, built on `SessionPlanner`, `SessionStarter`, `ResultSummary` and the engine verbs above.
