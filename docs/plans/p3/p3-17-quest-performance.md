# p3-17-quest-performance — Quest 2 frame rate without losing the look (PRF)

Status: In progress (P3; planned 7 November 2026, owner decision D7 the same day, ADR-0032). D7 authorises safe, visual-preserving Quest optimisations on an isolated branch, measured first; anything that changes the look needs comparison captures and the owner's approval. Implemented on branch `quest-perf/d7-optimisations` (7 Nov 2026 UTC): the Quest render tier, stereo-safe shafts and dust restored on Quest, foveation available at level 0, two diagnostic APKs, static workload measurements. **No Quest frame rate has been measured**; DC-13 (Sannan) produces the numbers. Reports: `docs/qa/quest2_performance_results.md` (results and comparison tables), `docs/qa/quest2_static_render_stats.md` (generated), `docs/qa/quest2_performance_analysis.md`, `docs/qa/quest2_missing_light_effects.md`, `docs/qa/quest2_optimization_options.md`, `docs/qa/quest2_sannan_benchmark_guide.md`.

## 1. Objective and final user-facing behaviour

On a standalone Quest 2 the hospital looks like the approved environment, including the sunlight shafts and drifting dust in both eyes, and runs at a stable 72 frames per second (13.9 ms per frame) in every room and view. Assets, materials, lighting direction, shadows and reflections stay as approved; savings come from rendering the same content more efficiently.

## 2. Current implementation status

- **Measured on Quest:** nothing. A teammate reported about 20-30 FPS; unverified until DC-13 records it.
- **Done on the branch (desktop look unchanged, Editor-tested):** Android quality level (Medium) uses `URP_Quest_Android` whose renderer has intermediate texture **Auto** (desktop's `URP_Quest` keeps Always); `SunShaft.shader` and `SunDustMote.shader` carry the single-pass-instanced stereo macros; `DesktopOnly` no longer hides the effects on Android (tier switch `ShowOnAndroid`, default on); OpenXR Foveated Rendering enabled for Android without eye tracking, level 0 (`QuestRenderTier`); `ProjectSetup` keeps the tier; diagnostic APK variants `diag` and `diag-gpuskin` with 15 run-time configurations, 8 teleport views, a readout and CSV logs (`QuestPerfDiagnostics`); build report next to each APK; `QuestRenderStats` workload per view.
- **Confirmed by inspection (7 Nov 2026):** lighting is mostly baked (every ceiling panel, sconce, window and reception daylight); realtime: 2 mixed directional suns with soft shadows (bay and corridor), one "Soft clinical key" realtime directional at a time (RoomLoader keeps only the current room's, intensity 0.05 to 0.2), 8 corridor realtime point "Ceiling fill" lights at intensity 0.04; post-processing is disabled on the renderer (`postProcessData` empty), so the -0.4 EV volume of ADR-0031 never applies in play (DT-275 confirmed); Android quality uses Shadowmask mode (desktop Distance Shadowmask); the city's 28 merged meshes (196,844 triangles) are inside the view frustum from every benchmark view, including the theatre; CPU skinning; no occlusion data; no LOD groups.

## 3. Existing code, assets, scenes and systems to reuse

`BuildScript.cs` (variants), `EnvironmentReimport.SwitchToDesktop`, `ProjectSetup.cs` (OpenXR and pipeline setup), `RoomLoader` (current room, clinical key rule), `RoomLayout.Contains`, profiler markers from p0-vr-platform-s07, `StaffIdleView.UpdateInterval`, Android texture overrides, `CorridorSun`/`PatientBaySun`/`CityExterior` builders, `tools/unity/perf_capture.py` (OVR Metrics CSV gate), `unity/Tools/QA` capture and compare tools.

## 4. Missing functionality

Real measurements; per-room light budget; room visibility streaming; occlusion data; city bounds or impostors (p3-19); GPU skinning decision; resolution policy; acceptance measurements.

## 5. Architecture and integrations

- **Two render tiers, one scene:** desktop keeps `URP_Quest.asset` and `URP_Quest_Renderer.asset` untouched; Android's quality level uses `URP_Quest_Android.asset` (a copy with every setting equal except renderer 0 = `URP_Quest_Android_Renderer`, intermediate texture Auto; renderer 1 = the original renderer, used only by diagnostic configuration B1/B2 to measure the old path). `QuestRenderTierTests` keeps the copy equal and the desktop asset unchanged.
- **Diagnostic APK:** `task unity-build -- diag` (and `diag-gpuskin`), release builds with `PROCSENSE_PERF_DIAG`; production variants never contain it (EditMode test). Controls and outputs are in the benchmark guide.
- **Measure, then change:** every visible optimisation is a diagnostic configuration first; applied for real only after DC-13 and, if visible, the owner's approval.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-PRF-01 | Diagnostic APK: build variants, define, run-time configurations B0-B14, readout, CSV logs, teleport views V1-V8, automatic sweep | Ruhan | — | APK builds; production builds free of the define (test); on-device behaviour checked in DC-13 | In progress |
| P3-PRF-02 | Baseline on the Quest: DC-13 part A with the diagnostic APK in configuration B1 (the 7 Nov rendering) and B0 (this branch), rooms and views, cool and warm | Sannan | P3-PRF-01 | Result file `docs/testing/results/DC-13.md` part A | Planned |
| P3-PRF-03 | Diagnostic matrix: DC-13 part B (automatic sweep in V2, V4, V5, V3, V8; both APKs for skinning) | Sannan | P3-PRF-01, P3-PRF-02 | Part B recorded with the sweep CSVs | Planned |
| P3-PRF-04 | Analysis (`tools/unity/perf_sweep.py` fills the results tables) and owner approval of each visible change, in measured order | Owner | P3-PRF-03 | Decision recorded with the numbers | Planned |
| P3-PRF-05 | Android render tier: own pipeline asset and renderer (intermediate texture Auto), desktop asset untouched, `ProjectSetup` keeps it | Ruhan | — | Desktop captures identical before/after (`docs/qa/quest2_performance_results.md` section 3); tests green; merged after DC-13 shows no regression | In progress |
| P3-PRF-06 | Fixed foveated rendering: OpenXR feature on for Android without eye tracking, level set by `QuestRenderTier` (0 until approved) | Ruhan | P3-PRF-05 | Level visible in OVR Metrics; owner accepts edge sharpness from headset photos; production level set | In progress |
| P3-PRF-07 | Light budget on Android: corridor realtime fills (intensity 0.04) and the clinical key baked or culled per room, keeping the baked look | Ruhan | P3-PRF-04 | Per-room realtime light count within the budget; capture comparison accepted | Planned |
| P3-PRF-08 | Room visibility streaming: renderers and lights of rooms the player cannot see are not drawn (door portals), rooms stay loaded | Ruhan | P3-PRF-04 | Draws in the bay drop by the corridor's and OT's share (B7 measured); no popping at doors | Planned |
| P3-PRF-09 | Occlusion culling bake for the three rooms (connected, like the lighting bake) | Sannan | P3-PRF-04 | Occlusion data committed; no popping in the walkthrough | Planned |
| P3-PRF-10 | Characters: GPU skinning on Android if `diag-gpuskin` measures better, Animator culling, corridor crowd update rate | Sannan | P3-PRF-04 | Skinning decision from measured CPU/GPU times | Planned |
| P3-PRF-11 | Quest shafts and dust: stereo-safe macros, tier switch instead of the Android ban, cost measured (B3), Quest variant only if needed and approved | Ruhan | — | Visible in both eyes on Quest (DC-13); cost within budget; desktop look unchanged | In progress |
| P3-PRF-12 | Draw-call and batching pass: SRP Batcher compatibility of custom shaders, static batching flags, material sharing on repeated props | Sannan | P3-PRF-04 | Draws per view recorded before/after with `QuestRenderStats` and on device | Planned |
| P3-PRF-13 | Resolution policy: eye-texture scale (B11/B12) and optional dynamic resolution, chosen for sharp clinical text | Ruhan | P3-PRF-05 | Chosen scale recorded with headset photos of text panels | Planned |
| P3-PRF-14 | Re-measure on Quest 2 and accept: DC-13 part C; stable 72 FPS in every benchmark view, warm | Sannan | P3-PRF-05, P3-PRF-06, P3-PRF-07, P3-PRF-08, P3-PRF-09, P3-PRF-10, P3-PRF-11, P3-PRF-12, P3-PRF-13 | Part C recorded; owner accepts | Planned |
| P3-PRF-15 | Static render workload per benchmark view (`QuestRenderStats`, `task unity-quest-stats`) | Ruhan | — | `docs/qa/quest2_static_render_stats.md` generated | Done |
| P3-PRF-16 | Lighting audit of every Light component (mode, shadows, intensity, run-time rule) and the shadowmask and post-processing settings | Ruhan | — | Results doc section 2 | Done |
| P3-PRF-17 | Results tables (features × measured cost; builds × FPS) kept current from the device CSVs by `tools/unity/perf_sweep.py` | Ruhan | P3-PRF-03 | Tables filled with DC-13 numbers; "Not measured on Quest" removed only where measured | In progress |
| P3-PRF-18 | City bounds: the city's 28 merged meshes are drawn from every room; split them per block (or impostors, p3-19) so frustum culling can skip them | Ruhan | P3-EXT-02 | City triangles in V7 (theatre) and V1 drop to what is actually visible; captures identical | Planned |

## 7. Dependencies and execution order

Diagnostic APK (done) → baseline and sweep on the headset → analysis and approvals → tier merge → foveation level, lights, room visibility, city bounds, occlusion, skinning, batching, resolution → final measurement.

## 8. Frontend components

Diagnostic readout (diagnostic builds only).

## 9. Backend and API changes

None.

## 10. AI and voice integration

None on the headset (no model runs there). Audio capture cost is measured in DC-15.

## 11. Unity objects, prefabs, animations and assets

`URP_Quest_Android.asset`, `URP_Quest_Android_Renderer.asset`, `QuestRenderTier`, `Diagnostics/*` (Perf window, presets, toggles, diagnostics), `QuestRenderStats`; later occlusion data and impostors.

## 12. Performance

This plan. Budgets after measurement are written into `docs/qa/quest2_performance_results.md`.

## 13. Error handling and edge cases

Thermal throttling (measure warm); foveation artefacts on text panels; intermediate texture forced again by a future renderer feature or post-processing (test on the Android renderer); desktop regressions (capture comparison); Editor left on the Android target after a build (`task unity-desktop`).

## 14. Testing

`QuestRenderTierTests` and `BuildScriptTests` (EditMode); desktop capture comparison; headset measurements DC-13.

## 15. Acceptance criteria

1. DC-13 part C shows a stable 72 FPS in every benchmark view on a warm Quest 2, or the owner accepts a documented exception.
2. Desktop captures are unchanged.
3. Shafts and dust appear in both eyes in the headset.

## 16. Definition of Done

All PRF tasks Done; results files written by Sannan; the reports updated with measured numbers in place of estimates.
