# Quest 2 performance: verified configuration, changes made, results and what only the headset can tell

_7 November 2026 (UTC), owner decision D7 (ADR-0032), branch `quest-perf/d7-optimisations`. Companion of `quest2_performance_analysis.md` (static analysis of the same day, corrected where section 2 says so), `quest2_optimization_options.md`, `quest2_missing_light_effects.md`, `quest2_sannan_benchmark_guide.md` and the generated `quest2_static_render_stats.md`._

> **8 November 2026 (UTC):** the desktop optimisation round (baked occlusion, room-aware exterior-city culling, nine-view desktop benchmark, the rejected city split) is documented in [`quest2_desktop_optimisation.md`](quest2_desktop_optimisation.md). Its numbers are desktop counts; the Quest columns below stay "Not measured on Quest".

## 0. Evidence classes used in this file

| Class | Meaning | Present today |
|---|---|---|
| **Verified configuration** | Read in the project files or counted by a tool from the scenes | Yes (sections 2, 4, 5) |
| **Measured on Quest** | A number recorded on a Quest 2 with OVR Metrics Tool or the diagnostic build's CSV | **No.** Every Quest cell reads "Not measured on Quest" until DC-13 |
| **Measured on desktop** | A number from this PC (GTX 1660 SUPER); says nothing reliable about the Quest 2's mobile tile-based GPU | Only image comparisons (section 3), no timings used |
| **Estimate** | Reasoned from documentation and the configuration; no number is given | Table 6 |
| **Hypothesis** | Needs the headset to confirm or reject | Marked in each row |

Teammate report, kept separate: **about 20-30 FPS** on the Quest 2 with the build of early November (conditions unknown). Unverified.

## 1. What was changed (branch `quest-perf/d7-optimisations`)

| Change | Files | Visible on desktop? | Visible on Quest? |
|---|---|---|---|
| Quest render tier: Android's quality level (Medium) uses its own pipeline asset; renderer 0 has intermediate texture **Auto** (URP renders straight into the OpenXR eye buffer when nothing needs the copy); renderer 1 is the original, used only by diagnostic configurations B1/B2 | `Assets/Settings/URP_Quest_Android*.asset`, `ProjectSettings/QualitySettings.asset`, `ProjectSetup.cs` | No (desktop assets untouched; section 3) | Should be identical (no post-processing, no feature needed the copy); to confirm in DC-13 |
| Shafts and dust: stereo macros in both shaders (`multi_compile_instancing`, instance id, stereo output, eye index in the fragment) | `SunShaft.shader`, `SunDustMote.shader` | No (macros are empty without stereo) | Yes: the effects return, in both eyes (to confirm) |
| `DesktopOnly` no longer hides the effects on Android; tier switch `ShowOnAndroid` (default on; diagnostic toggle B3) | `DesktopOnly.cs` | No | Yes (restores the approved look) |
| OpenXR Foveated Rendering feature on for Android, eye tracking off (Quest 2 has none), level 0 by default | `OpenXR Package Settings.asset`, `QuestRenderTier.cs` | No | No at level 0; levels 0.5 and 1.0 are diagnostic configurations B4/B5 |
| Diagnostic APK variants `diag` (CPU skinning, as production) and `diag-gpuskin` | `BuildScript.cs`, `Diagnostics/*`, `run_unity.py`, `Taskfile.yml` | — | Diagnostic builds only |
| Static workload tool | `QuestRenderStats.cs` | — | — |

Nothing was deleted; no scene, material, texture, light, lightmap or prefab was changed.

## 2. Verified project configuration (lighting, shadows, post-processing)

**Lights (all three scenes read by a YAML audit and the Editor tool):**

| Group | Count | Mode | Shadows | Notes |
|---|---|---|---|---|
| Ceiling panels (area) | bay 2, corridor 8, OT 6 | Baked | soft (bay, OT) / none | In the lightmaps |
| Window and reception daylight (area) | bay 1, reception 6 | Baked | soft / none | In the lightmaps |
| Wall sconces and reception desk downlights (spot) | corridor 18 + 2, OT 10 | Baked | soft (desk: none) | In the lightmaps |
| Headwall reading light, sunlit side rooms (area, point) | 1, 3 | Baked | none / soft | In the lightmaps |
| "Baked sconce light" points | 14 (corridor 9, OT 5) | Baked | soft | **Disabled** (intensity 0) |
| Sun through the bay window | 1 | **Mixed** directional | soft | Realtime part: URP main light candidate |
| Corridor sun | 1 | **Mixed** directional | soft | The second mixed directional becomes a per-pixel additional light |
| "Soft clinical key" | 3 (one per room) | **Realtime** directional | none | Intensity 0.05 (bay), 0.08 (corridor), 0.2 (OT); **`RoomLoader.UpdateLighting` keeps only the current room's lit**, so 1 at a time (correction: the 7 Nov analysis counted 3 at once) |
| Corridor "Ceiling fill" points | 8 | **Realtime** | none | Intensity **0.04** each, range 5 m: nearly invisible, but each is a per-pixel light for every object it reaches (bay and corridor views include up to 8) |
| Bay and OT "Ceiling fill" points, room lights | 6 | Realtime | none | **Disabled** |

- **Lightmaps in the Android build:** the connected bake (`Assets/Scenes/PatientBay/LightingData.asset`, Lightmap-N, ReflectionProbe-N) is referenced by the scene and therefore packed; the build report next to the APK lists the largest packed assets (section 5).
- **Shadowmask:** Android's quality level uses **Shadowmask** mode (static objects take their shadows from the baked shadowmask; only dynamic objects cast realtime shadows); desktop's Ultra uses Distance Shadowmask. URP main-light shadows: 2048, one cascade, 8 m, soft shadow quality medium.
- **Post-processing:** the renderer's `postProcessData` is empty, so post-processing never runs, in Play mode or on Quest. The -0.4 EV exposure volume of ADR-0031 therefore never applied outside the capture tools (DT-275 confirmed). No cost on Quest.
- **Realtime work per frame (verified):** main light (one mixed sun) with a 2048 soft shadow map of dynamic casters, the other mixed sun as an additional directional light, one clinical key, and up to 8 realtime point fills of 0.04 intensity. The Quest pays per-pixel cost for 3 directional lights and the fills that reach the view, for a few percent of light. **Estimate:** removing or baking the fills and the clinical key (configuration B6) is visually small and may save GPU time; Not measured on Quest.

## 3. Desktop look unchanged (measured on this PC)

Captures of the 24 QA views of PatientBay and the corridor before and after the change, same cameras (`qa_capture.ps1`, `compare_captures.py`), 1600 px wide:

| Set | Views | Mean absolute difference (0-255) | Largest single view | PSNR range |
|---|---|---|---|---|
| PatientBay | 12 | 0.01-0.97 | pb04 patient face 0.97 (the breathing patient and dust motes moved) | 32.6-52.6 dB |
| Corridor | 12 | 0.001-0.35 | co11 floor 0.35, co08 bulletin board 0.35 (dust, idle people) | 35.7-73.4 dB |

The difference images show only the animated patient, idle people and drifting dust motes; walls, lighting, materials and the city are pixel-identical. The project's earlier clean-clone comparison found 0.0-0.27 for identical content.

## 4. The three rooms, the city and what each view asks the GPU to draw (verified counts)

From `quest2_static_render_stats.md` (generated, approximate Quest 2 frustum for both eyes):

- All three rooms are loaded and **every active renderer is submitted for culling every frame**; nothing stops the rooms the player is not in from being drawn except frustum culling. Without occlusion data a wall does not hide what is behind it from culling, so the GPU still processes the vertices of hidden rooms (pixels are rejected by the depth test).
- **The city is the largest single block:** 28 merged meshes, **196,844 triangles**, 16 materials. Its bounds are so large that it is inside the frustum of **every** view, including the theatre (V7) and the bay wall (V1): about 40-88 % of the triangles of each view are city triangles, most of them behind walls.
- Triangles per view (both eyes together; multiview draws each once per eye in one pass): 224 k (V8) to 824 k (V4 corridor long). Unbatched draws: 134 to 979. Realtime or mixed lights touching the view: 6 to 11.
- Shadow casters within 8 m of the camera: 249 to 610 renderers; with Shadowmask mode only the dynamic ones should render into the shadow map (to confirm with a RenderDoc or OVR GPU profiler capture in DC-13 part B).

**Can rooms stay loaded without being rendered?** Yes: `Renderer.forceRenderingOff` (or disabling renderers and lights per room, or occlusion culling) keeps the scenes, colliders, scripts and simulation alive while removing them from rendering. Configuration B7 measures it; P3-PRF-08 (door-portal room visibility) and P3-PRF-09 (occlusion bake, connected like the lighting bake) implement it after measurement. The city needs splitting per block or impostors so frustum culling can skip it (P3-PRF-18, p3-19).

## 5. Render resolution, foveation and the build (verified)

- **Eye buffers:** OpenXR runtime default, render scale 1.0, no `eyeTextureResolutionScale` override: **1440 × 1584 per eye** on Quest 2 (Meta's table; panel 1832 × 1920). Both eyes ≈ 4.56 million pixels, 4x MSAA on top. This is not "1080p" or "4K": a flat 1080p game shades 2.07 million pixels once per frame at 60 Hz; the Quest shades two views from two positions at 72 Hz with 4 samples each at the edges of triangles, on a mobile GPU, and the lenses then warp the image. The project does **not** render above the default.
- **Options measured by the diagnostic build:** eye scale 0.9 (B11) and 1.1 (B12), foveation 0.5 (B4) and 1.0 (B5), MSAA 2x (B13). Sharpness of clinical text is judged on headset photos (P3-PRF-13). Dynamic resolution stays off until a measurement shows the app is GPU-bound after the other changes.
- **Texture compression:** about 446 of 635 textures have Android overrides (mostly ASTC 6x6, 1024; some 2048, two 4096); the city skybox imports 4K on Android.
- **Build facts of the diagnostic APK:** see section 9.

## 6. Feature and setting table (every optimisation opportunity)

| Feature/setting | Current configuration | Measured cost | Proposed optimization | Estimated benefit | Visual difference | Confidence |
|---|---|---|---|---|---|---|
| Intermediate texture | Desktop renderer: Always. **Quest tier: Auto (done on the branch)** | Not measured on Quest (B1/B2 vs B0) | Keep Auto on Quest | Unity: Always "might have a significant performance impact"; removes a full-screen MSAA resolve and copy per eye per frame; enables foveation | None expected | Configuration verified; benefit documented qualitatively, size unknown |
| Shafts and dust on Quest | **Restored (done on the branch)**, stereo-safe | Not measured on Quest (B3 vs B0) | Keep; only if too costly, fewer overlapping prisms on Quest with owner approval | Cost is fill rate in sunlit views; worst case standing in a corridor beam | Restores the approved look | Shader fix verified by compile and tests; both-eye display a hypothesis until DC-13 |
| Fixed foveated rendering | Feature on, **level 0** | Not measured on Quest (B4, B5) | Level 0.5 if B4 helps and edges are acceptable | Meta/Unity: lower shading rate at the lens edges; needs Auto above | Slight blur at the periphery, mostly hidden by the lenses | Hypothesis until measured |
| Realtime corridor fills (8 × 0.04) and clinical key | Realtime, per-pixel | Not measured on Quest (B6) | Bake or remove on Quest after a capture comparison | Fewer per-pixel lights on most surfaces | Very small (0.04 intensity) | Configuration verified; benefit estimate |
| Second mixed directional sun | Per-pixel additional light everywhere | Not measured on Quest | Light layers or per-room enabling of the sun that is not the main light | Fewer per-pixel lights | None if done per room | Estimate |
| Main-light realtime shadows | 2048, soft medium, 8 m | Not measured on Quest (B10, measurement only) | Hard or low-quality soft shadows only with owner approval | Fewer shadow samples | Harder edges of dynamic shadows | Estimate |
| Rooms not in view | All drawn (frustum only) | Not measured on Quest (B7) | Door-portal room visibility (P3-PRF-08), occlusion bake (P3-PRF-09) | Fewer draws and vertices | None | Estimate; counts verified |
| City exterior | 28 merged meshes, 197 k triangles, inside every frustum | Not measured on Quest (B8 = upper bound) | Split per block for culling (P3-PRF-18); image-based middle and far bands (p3-19, owner approval) | Up to the city's whole share where it is hidden | None for splitting; to be judged for impostors | Counts verified; benefit estimate |
| Characters and skinning | CPU skinning, no LODs | Not measured on Quest (B9; `diag` vs `diag-gpuskin`) | GPU skinning if measured better; animator culling; LODs | CPU time | None | Hypothesis |
| LOD groups | None in the three scenes | Not measured on Quest | LODs for people, dense props and city blocks where visible at distance | Vertices | None at viewing distance | Estimate |
| Occlusion culling | No data | Not measured on Quest | Bake the three rooms together | Draws and vertices | None (watch popping) | Estimate |
| MSAA | 4x | Not measured on Quest (B13) | Keep 4x (Meta recommends it) unless B13 shows a large gain | Bandwidth | More aliasing at 2x | Estimate |
| Eye-texture scale | 1.0 | Not measured on Quest (B11, B12) | Keep 1.0 unless measured | Pixels | Softer or sharper text | Estimate |
| Draw calls and batching | SRP Batcher on; custom shaders' compatibility unchecked; 134-979 unbatched draws per view | Not measured on Quest | SRP Batcher compatibility, material sharing | CPU render thread | None | Estimate |
| Post-processing | Off on the renderer | None (verified off) | Keep off on Quest | — | None | Verified |
| Light cookies and light layers | Enabled in the asset; URP Lit is the largest item in the APK (212.8 MB of variants) | Not measured on Quest | Disable on the Quest tier if no light uses them (check), strip unused variants | Smaller build, less shader memory and load time | None if unused | Build size verified; runtime effect estimate |
| Textures without mipmaps | 42 | Not measured on Quest | Mipmaps where used in 3D | Bandwidth, less shimmer | Better | Estimate |
| Application SpaceWarp | Off | — | Not now (dust, shafts, glass UI lack motion vectors) | Large on paper | Artefacts on transparent effects | Documented limitation |

## 7. Build comparison table

| Build | Average FPS | 1% low FPS | CPU frame time | GPU frame time | Visual comparison | Status |
|---|---|---|---|---|---|---|
| Dev APK of early November (main, shafts and dust off, intermediate Always) | Reported about 20-30 (teammate; unverified) | Not measured on Quest | Not measured on Quest | Not measured on Quest | Approved look minus shafts and dust | Reported only |
| `diag` configuration B1 (reproduces the early-November rendering) | Not measured on Quest | Not measured on Quest | Not measured on Quest | Not measured on Quest | Same as above | APK built (section 9), awaiting DC-13 |
| `diag` configuration B0 (this branch's production default) | Not measured on Quest | Not measured on Quest | Not measured on Quest | Not measured on Quest | Approved look with shafts and dust | APK built, awaiting DC-13 |
| `diag` B4/B5 (foveation), B6 (fills off), B7 (current room only), B8 (no city), B9 (no people), B11-B13 | Not measured on Quest | Not measured on Quest | Not measured on Quest | Not measured on Quest | Measurement configurations; B4-B9 and B11-B13 change the look | APK built, awaiting DC-13 |
| `diag-gpuskin` B0 (GPU skinning) | Not measured on Quest | Not measured on Quest | Not measured on Quest | Not measured on Quest | Same as B0 | APK built (467,133,027 bytes, SHA-256 `2f281720c6f47e5719a4027d73cab37361e40979849ab0a7351b6ea110a8ab15`), awaiting DC-13 |
| Final optimised build | Not measured on Quest | Not measured on Quest | Not measured on Quest | Not measured on Quest | Must equal the approved look | After DC-13 and the owner's choices |

### Measured sweep (filled by `tools/unity/perf_sweep.py --write` from the headset CSVs)

<!-- measured:begin -->
Not measured on Quest yet (DC-13 part B).
<!-- measured:end -->

## 8. What can and cannot be measured without the headset

| Tool | Possible here? | What it gives | Limit |
|---|---|---|---|
| Static scene counts (`QuestRenderStats`) | **Done** | Renderers, triangles, draws, materials, lights, shadow casters per view | Counts, not time |
| Capture comparison | **Done** (desktop) | Proof that the desktop look did not change | Desktop GPU, not Quest |
| Android build and its report | **Done** (section 9 facts) | Shaders compile for Vulkan, APK size, largest assets | No runtime numbers |
| Unity Frame Debugger, Render Graph Viewer | In the Editor only | The passes and draws of the Editor render, including whether the intermediate copy appears | Editor uses the desktop GPU and, unless the target is Android, the desktop tier; not a Quest frame |
| Unity Profiler on the desktop | Yes | CPU script costs (skinning, scripts, animators) on a desktop CPU | A Ryzen 5 is several times faster than the Quest 2's XR2; ratios only |
| Android emulator | Not useful | — | No Vulkan multiview, no OpenXR, no Adreno 650: results would mislead |
| Another Android phone | Possible as a rough proxy | Whether the APK starts, shader compile on a mobile GPU | Different GPU, no XR runtime, not Quest numbers |
| OVR Metrics Tool, diagnostic CSV, RenderDoc for Oculus / OVR GPU profiler | **Headset only** (Sannan) | FPS, CPU/GPU frame times, levels, throttling, per-pass GPU times | — |

Nothing in sections 2-5 is presented as a Quest frame rate.

## 9. Build facts (diagnostic APK)

Built 7 November 2026 (UTC) on this PC, branch `quest-perf/d7-optimisations` (Unity 6000.5.10f1, Vulkan, IL2CPP ARM64, release build). Measured from the build itself, not from a headset:

| Fact | `ProcSense-diag.apk` |
|---|---|
| Result | Succeeded, 0 errors, 27 warnings, 1,444 s |
| APK file | 467,133,043 bytes (the early-November Dev APK, a development build, was 493,771,980 bytes) |
| SHA-256 | `5ea151e546c17bae9ba1e39eba563cbf6bf90d2b1e9f7122c271561ab3a245b8` |
| Shader variants compiled for Vulkan | 45,056 in total; `ProcSense/Sun Shaft` 2 and `ProcSense/Sun Dust Mote` 2 (instancing off/on), no warnings for either |
| Largest packed assets (uncompressed size in the build report) | URP `Lit.shader` 212.8 MB (its variants: the largest single item; light cookies and light layers are enabled in the asset and multiply variants, option "light cookies and layers on Android" in section 6); city sky `city_skyline_8k.hdr` 44.7 MB (Android override 4096, format 22 in the importer: worth an ASTC HDR check); `receptionist.fbx` 41.1 MB (the densest character file); Rocketbox people 12-21 MB each; the patient's idle body 16.6 MB; PatientBay lightmaps up to 11.2 MB |

These sizes say how much memory and loading the build carries; they are not frame times. The report is regenerated next to every APK (`artifacts/unity/<apk>.report.txt`).

## 10. Ranked proposals (by likely value, before measurement)

1. Quest tier with intermediate texture Auto (**done**, no visual change).
2. Restore shafts and dust stereo-safely (**done**, restores the look; cost measured with B3).
3. Room visibility and city culling (P3-PRF-08, P3-PRF-18): no visual change; the city alone is up to 197 k hidden triangles per view.
4. Foveation 0.5 (B4): small peripheral change; approve on headset photos.
5. Bake or remove the 0.04-intensity realtime fills and handle the second sun per room (B6): very small visual change; capture comparison.
6. Occlusion bake (P3-PRF-09): no visual change.
7. GPU skinning if `diag-gpuskin` measures better (P3-PRF-10): no visual change.
8. Batching and SRP Batcher compatibility (P3-PRF-12): no visual change.
9. Image-based middle and far city (p3-19): owner approval of comparisons required.
10. Resolution, MSAA or shadow quality changes (B10-B13): only if still short of 72 FPS, with approval.

## 11. Only Sannan can verify (DC-13, DC-15)

Every FPS and frame time in this file; whether the shafts and dust show in both eyes at the right place; whether intermediate texture Auto is visually identical on the device; foveation edge quality; GPU vs CPU skinning; thermal behaviour (cool and warm); the network round trips of DC-15.
