# p3-17-quest-performance — Quest 2 frame rate without losing the look (PRF)

Status: Planned (P3, 7 November 2026). Investigation done, no optimisation implemented. Reports: `docs/qa/quest2_performance_analysis.md`, `docs/qa/quest2_missing_light_effects.md`, `docs/qa/quest2_optimization_options.md`, `docs/qa/quest2_sannan_benchmark_guide.md`. Nothing here is applied before the owner approves it (D7).

## 1. Objective and final user-facing behaviour

On a standalone Quest 2 the hospital looks like the approved environment, including the sunlight shafts and drifting dust, and runs at a stable 72 frames per second (13.9 ms per frame) in every room and view. Assets, materials, lighting direction, shadows and reflections stay as approved; savings come from rendering the same content more efficiently.

## 2. Current implementation status

- **Measured on Quest:** nothing. A teammate reported about 20-30 FPS; that number is unverified until DC-13 records it.
- **Confirmed by settings (static):** one URP asset for desktop and Quest (MSAA 4x, render scale 1.0, HDR off, 2048 soft main-light shadows, 4 per-pixel additional lights per object, light cookies and layers on); renderer forces an intermediate texture (`m_IntermediateTextureMode: 1`); OpenXR on Android uses single-pass instanced (multiview) on Vulkan; foveated rendering, dynamic resolution, performance settings and SpaceWarp features are off; CPU skinning (`gpuSkinning: 0`); no occlusion culling data; all three rooms loaded together; realtime lights live together: 3 unshadowed realtime directional lights, 2 mixed suns with soft shadows, 8 realtime point lights (corridor).
- **Confirmed by code:** shafts and dust are deactivated on Android by `DesktopOnly`; their shaders lack the single-pass-instanced stereo macros.

## 3. Existing code, assets, scenes and systems to reuse

`BuildScript.cs` (Dev/Demo/Study/desktop), `EnvironmentReimport.SwitchToDesktop`, `ProjectSetup.cs` (OpenXR settings), profiler markers from p0-vr-platform-s07, `StaffIdleView.UpdateInterval` (0.1 s on Android), Android texture overrides (about 440 textures already ASTC), `DesktopOnly`, `CorridorSun`/`PatientBaySun` builders, `SunShaft.shader`, `SunDustMote.shader`.

## 4. Missing functionality

A diagnostic APK with runtime toggles and an FPS readout; real measurements; a Quest render tier separate from desktop; foveation; a light budget per room; room visibility streaming; occlusion data; GPU skinning; stereo-safe Quest versions of the shafts and dust; a resolution policy; acceptance measurements.

## 5. Architecture and integrations

- **Two render tiers, one scene:** desktop keeps `URP_Quest.asset` as it is today (renamed only if the owner wants); Android gets its own pipeline asset and renderer through the Android quality level, so desktop captures and the approved desktop look do not change.
- **Diagnostic APK:** a separate build target (`BuildScript.BuildQuestDiagnostic`, scripting define `PROCSENSE_PERF_DIAG`) adds a wrist panel to switch, at runtime, the items of the options table (render scale, foveation level, MSAA, shafts/dust Quest variant, realtime extra lights, main shadow, characters, other rooms' renderers, intermediate texture via renderer swap) and shows frame time, so Sannan measures many configurations with one install. The production Dev/Demo builds do not contain it.
- **Measure, then change:** every optimisation is applied only after DC-13 shows its effect, one at a time, with before/after numbers in the results file.

## 6. Implementation tasks

| ID | Task | Owner | Depends on | Done when | Status |
|---|---|---|---|---|---|
| P3-PRF-01 | Diagnostic APK: build method, define, wrist toggle panel, frame-time readout, fixed benchmark viewpoints (teleport buttons), stereo-safe test quad scene | Ruhan | — | APK builds; toggles verified in the Editor; production builds unchanged (define absent) | Planned |
| P3-PRF-02 | Baseline on the current Dev APK (no changes): DC-13 part A (rooms, views, cool and warm) | Sannan | — | Result file `docs/testing/results/DC-13.md` part A | Planned |
| P3-PRF-03 | Diagnostic matrix: DC-13 part B with the diagnostic APK | Sannan | P3-PRF-01, P3-PRF-02 | Part B recorded | Planned |
| P3-PRF-04 | Analysis and owner decision D7: which options to apply, in which order, with the measured gains | Owner | P3-PRF-03 | Decision recorded with the numbers | Planned |
| P3-PRF-05 | Android render tier: separate URP asset and renderer for the Android quality level (intermediate texture Auto, settings per D7); desktop asset untouched | Ruhan | P3-PRF-04 | Desktop captures identical before/after (`compare_captures.py`) | Planned |
| P3-PRF-06 | Fixed foveated rendering on Quest (OpenXR feature on Android, level set at runtime per D7) | Ruhan | P3-PRF-05 | Level visible in OVR Metrics; edge quality accepted by the owner in headset photos | Planned |
| P3-PRF-07 | Light budget on Android: duplicate realtime directional lights and corridor realtime point lights baked or culled per room, keeping the baked look | Ruhan | P3-PRF-04 | Per-room realtime light count ≤ the D7 budget; desktop unchanged | Planned |
| P3-PRF-08 | Room visibility streaming: renderers and lights of rooms the player cannot see are disabled (door portals), rooms stay loaded for instant transitions | Ruhan | P3-PRF-04 | Draw calls in the bay drop by the corridor's and OT's share (measured) | Planned |
| P3-PRF-09 | Occlusion culling bake for the three rooms (with the room visibility of P3-PRF-08) | Sannan | P3-PRF-04 | Occlusion data committed; no popping in the walkthrough | Planned |
| P3-PRF-10 | Characters: GPU skinning on Android, Animator culling, corridor crowd LOD and update rate | Sannan | P3-PRF-04 | Skinning time measured lower in DC-13 re-run | Planned |
| P3-PRF-11 | Quest shafts and dust: stereo-safe macros in both shaders, a Quest variant (merged prism mesh per window, fewer prisms, fewer particles, same look at headset distance), `DesktopOnly` replaced by a tier switch | Ruhan | P3-PRF-04 | Visible in both eyes on Quest; cost within the D7 budget; desktop look unchanged | Planned |
| P3-PRF-12 | Draw-call and batching pass: SRP Batcher compatibility of custom shaders, static batching flags, material sharing on repeated props | Sannan | P3-PRF-04 | Draw calls per view recorded before/after | Planned |
| P3-PRF-13 | Resolution policy: eye-buffer scale and optional dynamic resolution per D7 | Ruhan | P3-PRF-05 | Chosen scale recorded with images | Planned |
| P3-PRF-14 | Re-measure on Quest 2 and accept: DC-13 part C; target 72 FPS stable in every benchmark view, warm device | Sannan | P3-PRF-05, P3-PRF-06, P3-PRF-07, P3-PRF-08, P3-PRF-09, P3-PRF-10, P3-PRF-11, P3-PRF-12, P3-PRF-13 | Part C recorded; owner accepts | Planned |

## 7. Dependencies and execution order

Baseline (PRF-02) can start today with the existing APK; the diagnostic APK in parallel; analysis and decision; then the tier first (it unlocks foveation), then lights, streaming, occlusion, characters, shafts, batching, resolution; final measurement.

## 8. Frontend components

Diagnostic wrist panel (development builds only).

## 9. Backend and API changes

None.

## 10. AI and voice integration

None on the headset (no model runs there). Audio capture cost is measured in DC-13 part B.

## 11. Unity objects, prefabs, animations and assets

Android URP asset and renderer, diagnostic panel prefab, occlusion data, Quest shaft meshes and particle settings.

## 12. Performance

This plan. Budgets after D7 are written into `docs/qa/quest2_optimization_options.md` and checked in DC-13 part C.

## 13. Error handling and edge cases

Thermal throttling (measure warm); foveation artefacts on text panels (panels stay in the centre region or level lowered); intermediate texture forced again by a future renderer feature (CI check on the Android renderer setting); desktop look regressions (capture comparison).

## 14. Testing

Desktop capture comparison for every change; EditMode check of Android settings (intermediate texture Auto, foveation feature on, shaders stereo-safe); headset measurements DC-13.

## 15. Acceptance criteria

1. DC-13 part C shows a stable 72 FPS in every benchmark view on a warm Quest 2, or the owner accepts a documented exception.
2. Desktop captures are unchanged.
3. Shafts and dust appear in both eyes in the headset.

## 16. Definition of Done

All PRF tasks Done; results files written by Sannan; the reports updated with measured numbers in place of estimates.
