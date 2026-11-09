# Quest 2 optimisation, round 2 (9 November 2026, UTC)

Branch `quest-perf/d7-optimisations`. Desktop measurements only: **no Quest frame rate, GPU time or overdraw was measured, and none is claimed.** The reported 20 to 30 FPS on the headset stays unverified until Sannan runs the diagnostic APK (DC-13). Owner approval of the optimised exterior (P3-EXT-07) was given on 9 November 2026; approval to merge into `main` is still pending the owner's review of the walkthrough video.

## 1. What is active by default on Android now

| Setting | Default on Quest | Desktop / Editor |
|---|---|---|
| Exterior city | optimised variant (far traffic and trees simplified, 401 chunks, portal cone culling); the original stays in the scene as the recoverable comparison variant (`QuestExterior`, diagnostic B20) | original city |
| Room-level portal culling (`RoomVisibility`) | on | off |
| Baked occlusion data (`QuestCullingProfile`) | **off** (data asset kept in the project, reversible) | on |
| Character culler (`CharacterPerfCuller`: off-screen skinning and animator culling with locked bounds) | on | off |
| Sun shafts and dust | on (approved look); shader early-out and half-precision vertex colour | on |
| Skinning | CPU (unchanged: no desktop difference measured, see 4.5) | CPU |
| Android quality levels | only level 2 Medium (`URP_Quest_Android`) is built into the Android player; desktop levels excluded (smaller shader set) | all levels |
| Android texture overrides | eleven patient-monitor textures ASTC (was uncompressed, about 245 MB, now about 55 MB) | unchanged |
| Android shader stripping | `QuestShaderStripping` removes variants that cannot occur (debug display, probe volumes, SSAO, DBuffer, Forward+, unused fog modes, LOD cross-fade, realtime lightmaps) | none |

Foveation (0), eye scale (1.0), MSAA (4x), shadows, lights and the look of every object are unchanged.

## 2. Findings that changed decisions

1. **Door glass was culling the city (fixed).** The per-frame comparison against an unculled render found the city missing behind the vision pane of the MRI/MRCP door. `ExteriorVisibility` now treats "Door vision glass" as an aperture. Worst per-frame difference on that stretch: 0.745% of pixels before, 0.052% after.
2. **Baked occlusion changes ceiling shading (occlusion switched off on Quest).** With occlusion on, the corridor's eight realtime ceiling-fill lights are culled differently, which shifts the per-object light selection: up to 1.0% of pixels (up to 65/255) differ near ceiling fixtures when looking up in the corridor near the theatre door. Switching the realtime fills off removes the difference; so does switching occlusion off. The approved look never had baked occlusion, so it is off on Quest. `RoomVisibility` replaces its cross-room saving without touching lights.
3. **A city glass facade clips into the theatre (hidden, pre-existing).** With every cull off, a blue glass building intersects the theatre's east end (x about 11.5). It is invisible with the optimised culling because the theatre has no window. Reported for the owner: it is a defect of the city placement, not of this work.
4. **Doorway frame (fixed by the environment review).** `PortalCull` left city chunks hidden while the camera stood between rooms; they are reset now. A head-motion allowance (`MotionPad`) widens the cone for the predicted pose the compositor renders.

## 3. Visibility verification (automated, desktop)

* Walkthrough path (`QuestWalkthrough`, 120 s, 3600 frames: bay, door, corridor, theatre, glass wall, return; slow pans, 150 degree and 180 degree snaps, one full 360, three head heights, walking head bob): every frame of the production configuration is compared with the same pose rendered with every cull off.
* Pose sweep (`Sweep_poses`): 4,752 poses (all rooms, 12 headings, 3 pitches, doors closed and open). Production candidate B28: **24 poses differ by more than 0.05% of pixels, all of them the theatre east end of item 3; the largest difference anywhere else is 0.037%.**
* `walk_qa.py` flags no blank frames; its 17 events on the earlier frame set were fast spins and door sweeps, none a render defect.

## 4. Desktop measurements

Machine: Ryzen 5 2600, GTX 1660 SUPER, Unity 6000.5.10f1 Editor, URP 17.5, one 1832 x 1920 eye, 4x MSAA, Medium quality level (the Quest renderer asset). "Frame completion" is the time from submitting the render until a read-back of the result returns, i.e. submit time plus GPU time on this card. It is **not** a Quest frame time. The machine was shared with other work, so every figure is the minimum of three runs of 40 frames; run-to-run spread was 2x to 4x, so differences under about 10% are not significant.

### 4.1 Workload (exact, deterministic)

Triangles per view against the original (B20 = original exterior and baked occlusion as of 8 November): B17 optimised exterior -38.6% (geometric mean over 12 views; -59% in the street-facing and window views, 0% where the city is already hidden); B28 (production: optimised exterior, room culling, no baked occlusion) -34.6%. Without baked occlusion some views draw more of the room they stand in (V1 +44%, V4 -4% instead of -21%).

### 4.2 Frame completion, desktop (minimum of 3 runs, geometric mean over 12 views)

| Configuration | Frame completion | Triangles | SetPass calls | Note |
|---|---|---|---|---|
| B20 original exterior | 0% | 0% | 0% | baseline |
| B17 optimised exterior + occlusion | -12.5% | -38.6% | +3.8% | best desktop number, keeps the occlusion side effect |
| B28 production candidate | -11.0% | -34.6% | +18.4% | look-neutral |
| B29 = B28 + character culler | -5.6% | -34.6% | +18.5% | the V4 value (51.9 ms) is most likely an artefact of the benchmark's manual `Camera.Render` calls fooling the culler's visibility test; on the continuous walkthrough B29 is within 0.5 ms of B28 (not proven) |
| B3 shafts and dust off (measurement) | -13.7% | 0% | 0% | the two effects cost about 14% of desktop frame completion; kept (approved look) |
| B6 realtime fills off (measurement) | -3.5% | 0% | 0% | not applied (changes the look) |
| B26 skinned meshes not updated off-screen | -5.9% | +2.1% | +0.9% | included in the character culler |
| B13 MSAA 2x (measurement) | -0.9% | 0% | 0% | not applied (changes the look) |

On the continuous 120 s path at 1280 x 720 the median frame completion was 9.2 ms (B20), 9.8 ms (B28), 10.2 ms (B29): the desktop card is not geometry-bound at that size and the culling logic costs about 0.4 ms of CPU in the Editor, so **the triangle reduction does not translate into a desktop frame-time gain there. Whether it helps the Adreno 650 and the Quest CPU is exactly what only the headset can show.**

### 4.3 Not applied after measurement

Shadow distance 5 m, hard shadows, shadow map 1024, additional lights capped at 2 or off, eye scale 0.9 and 1.1, MSAA 2x: each changes the approved look or soft-edge quality (B21 to B25, B11 to B13); kept as diagnostic presets only. The early timings of these in the first noisy run are not used.

### 4.4 Memory

Monitor textures: about 245 MB uncompressed to about 55 MB ASTC on Android (a desktop capture with the same ASTC format differs from the original by a mean of under 0.8/255 and under 0.7% of pixels, mostly dust and animation). Other Android texture settings audited: 446 of 635 textures already carry an ASTC override; no texture has Read/Write enabled.

### 4.5 CPU against GPU skinning

Two runs each of B28 and B29 with Player Settings GPU skinning on and off: ratio 1.00 (frame completion and CPU render time), no measurable difference on the desktop. CPU skinning stays (status quo, no evidence for a change). `ProcSense-diag-gpuskin.apk` is rebuilt so the headset can decide.

### 4.6 Tools that could not be used

GPU time from `FrameTimingManager` is not reported by the Editor, so "frame completion" is used instead; transparent overdraw has no counter (the shaft and dust cost is read from B3); RenderDoc and Nsight are not installed (RenderDoc is available through winget); the Frame Debugger, Rendering Debugger and Render Graph Viewer need the Editor GUI and are deferred so the desktop stays under the owner's control. The Android emulator (Android Studio command-line tools, x86_64 image, WHPX acceleration works here) boots hidden but has no XR runtime, no Adreno GPU and a translated ARM path, so it adds nothing for Quest performance questions (`quest2_emulator_feasibility.md`).

## 5. Estimated Quest effect (conditional, not a measurement)

The earlier reports quote 20 to 30 FPS on the headset (unverified). A frame at 72 FPS has 13.9 ms. If, on the headset, the work removed here is GPU-bound in proportion to triangles and the same share of the frame as on the desktop, a 10% to 13% cut of the frame time would move 25 FPS (40 ms) to about 28 FPS (36 ms); it would not by itself reach 72. These numbers assume that frame time scales with the desktop ratios, which is not established for a tile-based mobile GPU. The 190 MB texture saving and the removed shader variants reduce memory pressure and load stalls rather than frame time.

## 6. Headset-only checks for Sannan (DC-13 and DC-14)

1. B20 (original), B0 (production default), B17, B28, B29, B30 in V3 and V8 and in the corridor: average FPS, 1% low, OVR CPU and GPU levels (CPU matters: three culling components run per frame).
2. Fast head turns at each door and window: no popping city, room or character; both eyes.
3. GPU skinning (`ProcSense-diag-gpuskin.apk`) against CPU skinning.
4. Foveation 0.5 and 1.0, eye scale 0.9 and 1.1: only with the owner's check of text sharpness.
5. Symmetric Projection and Multiview Render Regions (Final Pass) A/B (not enabled: they change the eye-buffer projection).
6. Sun shafts and dust in both eyes on the device (Vulkan, single-pass instanced).
7. Texture look of the patient monitor with ASTC (UI text sharpness).

## 7. Reproduce

```
powershell -File tools\unity\bench_desktop.ps1 -Configs B20,B17,B28,B29 -Tag mine -Frames 40      (-Stereo, -Skin cpu|gpu)
python tools\unity\bench_report.py unity\Logs\bench\mine.csv --base B20
BENCH_CLIP_DIR=<dir> BENCH_CONFIGS=B29 BENCH_DIFF=1 -> QuestWalkthrough.Walkthrough / Sweep_poses (PlayMode, Explicit)
python tools\unity\walk_qa.py <frames dir>      powershell -File tools\unity\walk_video.ps1 -Mode all ...
```

## 8. Builds, shader check and what was not done

* **APKs** (Android, IL2CPP, ARM64, Vulkan, release; built 9 Nov 2026, copies and SHA-256 in `D:\ProcSenseWork\apk`): `ProcSense-demo.apk` (production default), `ProcSense-diag.apk` (diagnostic: presets B0 to B30, original and optimised exterior), `ProcSense-diag-gpuskin.apk` (same with GPU skinning). Each is 363 MB, **down from 471 MB** before this round; the player build is 1.77 GB, down from 1.98 GB. The URP Lit shader packs to 110 MB, down from 213 MB. `QuestShaderStripping` removed 22,604 of 45,208 Lit variants (all `LOD_FADE_CROSSFADE`; the project has no LOD groups) and 64 of 128 Unlit variants; the Android-only quality-level exclusion removed the desktop pipeline's variants. The APK contains the optimised city meshes (`city_traffic_q`, `city_trees_q`, `city_ground_q`, `city_mid_q`) and the compiled sun-shaft and dust shaders. The build report lists one error line that earlier builds also carried (the Editor's skinned-mesh message, DT-282); the build succeeded.
* **Sun-shaft shader check:** the same 40 frames of the bay path rendered with the old and the new shader differ only in individual drifting dust motes and the patient's breathing (the diff map shows no change in the light cones). The two shaders compile for Android and are stereo-instanced.
* **Not measured on a headset or by a desktop counter:** Quest FPS, GPU time, transparent overdraw, render passes and fullscreen copies (the render graph viewer needs the Editor GUI; the Android URP renderer has no renderer features and intermediate texture Auto, per the configuration audit).
* **Static batching and mesh combining:** not applied. 127 of 296 bay, 618 of 1113 corridor and 188 of 213 theatre renderers are already batching-static; combining the rest at load needs readable meshes (13 of 138 models are readable, and making all readable doubles their memory), and the SRP Batcher already removes most state changes. `StaticBatchingAtLoad` stays opt-in and unused.
* **Image-based distant buildings, impostors:** not built. The buildings are already textured boxes (8% of the city's triangles); the saving came from culling and simplifying traffic and trees (`quest2_exterior_optimisation.md`). An impostor would change parallax and window depth for a saving of at most that 8%.
* **Android emulator:** feasible but not useful for Quest performance (section 4.6); installed and removed again.
* **Continuous integration:** the full local CI (`task ci`) was started once on this branch and stopped part-way by the developer's instruction (it was not final integration); **it has not completed and no pass is claimed.** Targeted tests run this round: `ExteriorVisibilityTests`, `CharacterPerfTests`, `PatientIdleTests`, `SpeechFaceTests`, `MpfbPatientTests`, `GownPoseTests`, `ClothMotionTests` (28 of 28 passed); the doc and planning checks pass. The full CI runs once at final integration (the pre-push hook is that run). GitHub Actions do not run on this branch.
