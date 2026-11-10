# Audit lane 1: URP configuration, rendering and lighting settings

Round 4 audit, 10 November 2026. Scope: every URP and rendering setting on the Quest 2 (Android) production tier, `URP_Quest_Android` with `URP_Quest_Android_Renderer`. **Desktop evidence only: no Quest frame rate, GPU time or overdraw was measured, and none is claimed.**
"Frame completion" = submit plus GPU time on the GTX 1660 SUPER (not a Quest frame time). Old figures are from `quest2_optimisation_round2.md` section 4.2 (one eye, minimum of 3 runs, spread 2x to 4x, so differences under about 10% are not significant). New figures come from the two-eye benchmark (`StereoBenchmark.cs`, 6 interleaved rounds, noise 2 to 3% on a quiet machine) run by lane 4; they are filled in section 4 when lane 4 reports. **A cell that says "pending" has no measurement yet; nothing here is guessed.**

## 1. What was added (this lane's files only)

`PerfPresets.cs` and `PerfToggles.cs` (diagnostic build and benchmark only; run-time switches on the in-memory pipeline asset, never saved, authored values are restored when the preset no longer asks for a change). New presets, each equal to B20 plus one setting:

| Preset | Setting | Field switched |
|---|---|---|
| B31 / B32 | eye scale 0.8 / 1.2 | XR eye texture scale (B11 0.9 and B12 1.1 already existed) |
| B33 | MSAA off (1x) | `urp.msaaSampleCount` = 1 (B13 = 2x already existed) |
| B34 / B35 | main-light shadow cascades 2 / 4 | `urp.shadowCascadeCount` (authored 1) |
| B36 | shadow map 4096 | `mainLightShadowmapResolution` (authored 2048; B23 = 1024 exists) |
| B37 | shadow distance 12 m | `shadowDistance` (authored 8 m; B21 = 5 m exists) |
| B38 | additional lights per vertex | `m_AdditionalLightsRenderingMode` PerPixel (1) to PerVertex (2) |
| B39 | up to 8 additional lights per object | `maxAdditionalLightsCount` (authored 4; B24 = 2 exists) |
| B40 / B41 | depth texture on / opaque texture on | `supportsCameraDepthTexture` / `supportsCameraOpaqueTexture` (authored off) |
| B42 | SRP Batcher off | `GraphicsSettings.useScriptableRenderPipelineBatching` |
| B43 | dynamic batching on | `supportsDynamicBatching` (authored off) |
| B44 | HDR colour buffer on | `supportsHDR` (authored off) |
| B45 | reflection probes off | `ReflectionProbe.enabled` (measurement only) |
| B47 / B48 | anisotropic filtering off / per texture | `QualitySettings.anisotropicFiltering` (authored Forced On) |
| B49 / B52 | anisotropy capped at 4x / 2x | `Texture.SetGlobalAnisotropicFilteringLimits(-1, max)` |
| B46 | depth priming forced | `UniversalRenderer.depthPrimingMode` (authored Disabled) |
| B50 | runtime static batching of the interiors (lane 2 request) | `StaticBatchingAtLoad.Combine()` once per loaded scene; **irreversible for the process, so it needs its own launch** (alternate fresh launches of B20-only and B50-only, three pairs); only meshes with Read/Write enabled are eligible (13 of 138 models), so the test shows the ceiling of what is batchable today |
| B51 | SRP Batcher off + GPU instancing on every material (lane 2 request) | `Material.enableInstancing` (authored values restored); compare against B42 alone |
| B55 | city hidden + baked occlusion off (lane 2 request) | no-city base for the pair B15 / B27 |

`ChangesLook` is updated: cascades, per-vertex lights, HDR and probes-off count as look-changing; depth texture, opaque texture, SRP Batcher, dynamic batching and depth priming are look-neutral.
**Not yet compile-checked in Unity** (the Unity lock was held by lane 4; the API members were verified by reading the URP 17.5 package source: all setters exist). A compile error would show in the first launch log.

The exact benchmark requests are in `benchmark_requests.md` (section lane1). Two harness points lane 4 has to handle, because they would otherwise give meaningless numbers:

1. `StereoBenchmark` creates the eye render targets with `antiAliasing = 4` fixed and sizes them once from `BENCH_EYE_SCALE`; it ignores `preset.Msaa` and `preset.EyeScale`. MSAA and eye-scale presets need per-preset RTs (or separate launches), and `rt_mem` must differ between B13, B33 and B20 to prove the setting took effect.
2. The benchmark eye is 1832 x 1920 (3.52 M pixels). Unity reports the Quest 2 runtime default as 1440 x 1584 (2.28 M pixels, scale 1.0); 1832 x 1920 is the maximum. The desktop therefore carries about 1.54 times the pixel load of a default-scale Quest eye. A second launch with `BENCH_EYE_SCALE=0.786` (1440 x 1509 is the nearest) gives the default-scale workload and tells whether any ranking depends on resolution.

## 2. Per-technique table (this lane's techniques)

Status words: Implemented, Tested and rejected, Already optimal, Unsupported, Pending (benchmark requested), Quest-only.

| # | Technique | What it does | Original (Android production) | Alternatives tested (desktop) | Status | Measured effect | Visual difference | Final choice and why |
|---|---|---|---|---|---|---|---|---|
| 1 | MSAA | Anti-aliasing samples per pixel; resolve on tile memory on Quest | 4x | 2x (B13, mono bench): frame completion -0.9% (not significant); 1x (B33): pending | Already optimal as authored (4x is the setting Meta's Quest guidance recommends; 8x was not tested: this project has no 8x preset and the desktop card would not predict the Adreno) | -0.9% for 2x, within noise | 2x has visibly more edge stair-stepping on thin geometry and text borders | Keep 4x. On a tile GPU the resolve is cheap and the desktop number is within noise. A/B on the headset only if frame time is fill-bound. |
| 2 | Fixed foveated rendering | Lower shading rate in the periphery | level 0 (feature enabled, level 0) | B4 (0.5), B5 (1.0) exist | Quest-only | Desktop GPU has no foveation: any number would be noise | Periphery blur; clinical text must stay central | Keep 0 until the headset A/B (section 3); this is the setting most likely to matter on a fill-bound Quest |
| 3 | Dynamic foveation (eye tracked) | Moves the foveation centre with the eyes | not available | none | Unsupported (Quest 2 has no eye tracking) | n/a | n/a | n/a |
| 4 | Resolution scale | XR eye texture scale | 1.0 | 0.9 (B11), 1.1 (B12), 0.8 (B31), 1.2 (B32): B11, B12, B31, B32 pending | Pending (+ Quest-only) | pending; frame cost scales roughly with pixel count when fill-bound | 0.9 softens clinical text; 1.2 sharper | Keep 1.0 for now; Sannan picks 1.0 vs 0.9 on the headset if the GPU frame time is over budget |
| 5 | Shadow distance | Range of the main-light shadow map | 8 m | 5 m (B21) measured in the first noisy run only, discarded; 12 m (B37) pending | Pending | pending | 5 m drops contact shadows on far room edges | Keep 8 m (the room sizes and the existing approval) unless B37/B21 show a clear gain |
| 6 | Shadow cascades | Splits the shadow map by distance | 1 | 2 (B34), 4 (B35) pending | Already optimal (1 cascade is cheapest; 8 m range is small) | pending | none expected at 8 m | Keep 1 |
| 7 | Shadow map resolution | Texels of the main-light shadow map | 2048 | 1024 (B23): first noisy run discarded; 4096 (B36) pending | Pending | pending | 1024 softens the contact edge | Keep 2048 |
| 8 | Soft shadows | Filtered shadow edges (quality Medium) | on, Medium | hard (B22): first noisy run discarded | Tested and rejected for the look; number pending | pending | harder, aliased shadow edges | Keep soft |
| 9 | Main-light realtime shadows | Mixed directional suns cast realtime shadows | on | off (B10, measurement only) | Pending (batch 1) | pending | removes dynamic shadows from people and carts | Keep |
| 10 | Additional-light rendering mode | How the corridor and key fill lights are shaded | **per pixel** (PerPixel = enum 1) | per vertex (B38) pending; off (B25, measurement) pending | Pending | pending (B6 realtime fills off: -3.5% measured in the one-eye run) | per vertex would flatten the highlights and falloff of the 8 corridor fills; look diff pending | Keep per pixel unless B38's diff is invisible AND it saves at least 5% |
| 11 | Per-object additional-light limit | Lights each object samples | 4 | 2 (B24) pending; 8 (B39) pending | Pending | pending | 2 changes ceiling shading where 3 or 4 fills overlap (see occlusion finding) | Keep 4 |
| 12 | Realtime vs baked lighting | What is lightmapped vs realtime | baked + 8 realtime corridor fills + realtime clinical key | fills off (B6) -3.5% in the one-eye run | Already optimal as authored (lighting was baked and approved) | -3.5% when fills are off | fills off changes the look | Keep. Converting the fills to baked is a re-bake and a look change; not pursued. |
| 13 | Depth texture | Depth pre-copy used by soft effects | off | on (B40) pending | Already optimal (off) | pending (expected extra copy/pass cost) | none | Keep off; no effect uses it |
| 14 | Opaque texture | Colour copy for refraction-style materials | off | on (B41) pending | Already optimal (off) | pending | none | Keep off; no transparent material reads it (lane D audit) |
| 15 | Intermediate texture | Renders to an offscreen RT then blits | Auto (0) | Always: B2 (legacy renderer) exists | Pending (batch 1) | pending | none | Keep Auto (lets URP render straight to the swapchain when no pass needs a copy) |
| 16 | Depth priming | Depth pre-pass to cut overdraw | Disabled (renderer `m_DepthPrimingMode: 0`) | Forced (B46) pending | Already optimal (disabled; usually a loss on tile GPUs) | pending | none | Keep disabled |
| 17 | SRP Batcher | Cuts per-draw CPU state setup | on | off (B42, measurement) pending | Already optimal (on) | pending: shows CPU submit and SetPass cost if it were off | none | Keep on |
| 18 | Dynamic batching | CPU merges small meshes | off | on (B43) pending | Already optimal (off; SRP Batcher already batches) | pending | none | Keep off unless B43 lowers CPU submit |
| 19 | GPU instancing | One draw for repeated meshes | 162 materials flagged, 272 not | B51 (SRP Batcher off + instancing on everything) against B42 pending: with the batcher on, compatible shaders ignore instancing, so B20 vs B20 + instancing would show nothing | Pending (expected: rejected, the SRP Batcher already covers it) | pending | none | Keep as authored unless B51 beats B20 itself (not just B42)|
| 20 | HDR | Float colour buffer | off | on (B44) pending | Already optimal (off; doubles buffer bandwidth) | pending (also `rt_mem`) | none expected without post-processing | Keep off |
| 21 | Post-processing | Bloom, tone mapping, colour grading passes | **none**: `postProcessData` empty on the renderer, no Volume components in any scene, no camera requests it | no toggle needed | Already optimal | no pass to remove | none | Keep none |
| 22 | Light layers | Per-light rendering layer masks | on | off was rejected by lane A: the skyline renderers use layer mask 2 so lights do not reach them; off would light the skyline | Tested and rejected (visible) | n/a | skyline lit | Keep on |
| 23 | Reflection probes | Probe sampling for reflections (blending off, box projection on) | present | off (B45, measurement only) pending | Pending (measurement only) | pending | reflections fall back to the skybox | Keep as authored; reflections are part of the approved look |
| 24 | Camera stacking | Extra base+overlay camera passes | none | n/a | Already optimal | no extra passes | n/a | none |
| 25 | Render Graph, forward rendering | URP 17.5 uses the render graph; forward | Forward, render graph | n/a (the only path in this Unity version) | Already optimal | n/a | none | none |
| 27 | Static batching (runtime) | Merges immovable meshes so one draw covers many | partly authored (127 of 296 bay, 618 of 1113 corridor, 188 of 213 theatre renderers already batching-static) | B50 pending (own launch) | Pending | pending: draw calls, SetPass, CPU submit and mesh memory | none | Keep off unless B50 lowers CPU submit and the memory growth is acceptable on 6 GB |
| 28 | Anisotropic filtering | Sharper textures at grazing angles; costs texture bandwidth | Quality level Medium (the only Android level): `anisotropicTextures: 2` = Forced On; 295 textures ask for 8x, 143 for 4x, 24 for 3x, 162 for 1x | off (B47), per texture (B48), capped 4x (B49), capped 2x (B52): pending | Pending (+ Quest-only) | pending | blur on floors, walls and ceilings seen at an angle | Keep as authored until B47 to B52 show a gain; strongest candidate for a free win on the headset |
| 29 | Opaque downsampling | Resolution of the opaque copy | 2x bilinear (`m_OpaqueDownsampling: 1`) | none | Already optimal (irrelevant while the opaque texture is off) | none | none | none |
| 30 | Store actions optimisation | Discard vs store of render-target contents | Auto (`m_StoreActionsOptimization: 0`: discard unless injected passes need the contents) | none (no injected passes: no renderer features) | Already optimal | n/a | none | none |
| 31 | Native render pass | Keeps passes in tile memory | automatic through the Render Graph since 6000.5 (`useNativeRenderPass` is obsolete) | none | Already optimal (cannot be switched) | n/a | none | none |
| 32 | Render Graph compatibility mode | The old non-render-graph path | removed in this version: `RenderGraphSettings.enableRenderCompatibilityMode` is hard-coded false | none | Unsupported (cannot be enabled) | n/a | n/a | n/a |
| 33 | Tile-only mode | Renderer data field `m_TileOnlyMode` | false | none (semantics not confirmed from the package source) | Quest-only (exploratory, see packs row A16) | n/a | unknown | Keep false |
| 26 | Adaptive Performance flag | URP flag `m_UseAdaptivePerformance` | on, but no provider package installed | n/a | Already optimal (no-op) | none | none | none |

Items marked pending in rows 4 to 18 have preset ids and a launch string ready; the one-eye results quoted come from the earlier noisy run and are explicitly weak.

## 3. Quest-only A/B configurations for Sannan

The complete pack, one table with exact fields, expected effect, how to read it and rollback, is in `docs/qa/quest2_sannan_ab_packs.md`. Summary:

Runtime-switchable in the diagnostic APK (left thumbstick click cycles presets; the HUD and the CSV log the preset id; each is look-labelled "measurement only" when `ChangesLook`):

| A/B | Presets | What to record |
|---|---|---|
| Foveation | B0 (0) / B4 (0.5) / B5 (1.0) | GPU frame ms (OVR Metrics or `adb shell setprop debug.oculus.gpuLevel`), FPS, edge sharpness at monitors/signs |
| Eye scale | B31 (0.8) / B11 (0.9) / B0 (1.0) / B12 (1.1) | same, plus text legibility on the patient monitor |
| MSAA | B0 (4x) / B13 (2x) / B33 (1x) | GPU ms |
| Additional lights | B0 (per pixel) / B38 (per vertex) / B24 (2 per object) | GPU ms; corridor ceiling highlights |
| Shadows | B0 / B21 (5 m) / B22 (hard) / B23 (1024) / B10 (off) | GPU ms |
| Passes | B0 / B40 (depth tex) / B41 (opaque tex) / B46 (depth priming) / B2 (intermediate Always) | GPU ms |
| Culling | B20 / B28 / B29 / B30 | CPU and GPU ms (the city and culling A/B) |

Build-time (need a rebuild with the existing path `python tools/unity/run_unity.py` and variant `diag`, one APK per value; the fields are in `unity/Assets/XR/Settings/OpenXR Package Settings.asset`, feature `MetaQuestFeature Android`):

| Setting | Field (value now) | A/B value | Notes |
|---|---|---|---|
| Symmetric Projection | `m_symmetricProjection: 0` | 1 | Vulkan, multiview, Quest 2 are compatible; changes the eye projection (check the edges of the view), no desktop proxy |
| Multiview Render Regions | `m_multiviewRenderRegionsOptimizationMode: 0` | 1 (FinalPass), 2 (AllPasses) | Needs Symmetric Projection; reduces pixels shaded outside each eye region; check the periphery |
| Optimize Buffer Discards | `m_optimizeBufferDiscards: 1` (Android feature value) | 0 | Already on; the headset gain is not measurable on the desktop |
| Subsampled layout | n/a here | enable only together with foveation > 0 | no gain at level 0 |
| Performance levels | `XrPerformanceSettingsFeature Android` enabled; `QuestRuntimeSettings.RequestLevels` | Level SustainedHigh vs off | check thermals over 10 minutes |

Every A/B is a headset measurement; none can be answered on the desktop.

## 4. Desktop measurements: results

Pending lane 4 (batches in `benchmark_requests.md`). Filled in this section when lane 4 reports: per preset, ratio to B20 with a 95% bootstrap interval, rounds faster, CPU submit, SetPass, draw calls, triangles, `rt_mem`.

## 5. Recommendations so far (no measurement needed)

* Keep: MSAA 4x, intermediate texture Auto, no depth or opaque texture, depth priming disabled, HDR off, SRP Batcher on, dynamic batching off, no post-processing, 1 cascade, 2048 shadow map at 8 m, soft shadows, per-pixel additional lights with a limit of 4, light layers on. All were verified in the asset files and all are the safest for the approved look.
* Needs the headset: foveation, eye scale, Symmetric Projection, Multiview Render Regions, performance levels, per-vertex lights.
* Decision rule for adopting any change: a measured gain with a 95% interval clear of zero on the two-eye benchmark AND a pixel difference of at most 0.05% at the 12 views with text crops unchanged. Nothing meets this yet; nothing is adopted.

## 6. Findings from the extra configuration check (project files and package source)

* **Anisotropic filtering** is Forced On at the Android quality level, with 295 textures at 8x: a candidate saving on the headset (B47 to B52).
* **Non-power-of-two textures:** 45 textures use `nPOTScale: 1` (To Nearest), including posters, charts, the bulletin board, the reception screen and the TV (1024 x 1448 becomes 1024 x 1024). Android overrides cap posters 01 to 07 and 10 to 12 at 512 and charts and prints at 1024 (ASTC 6x6). If any carries text that must be readable this is below the quality bar; packs rows A18 and A19 test it. Not changed here (texture lane).
* **Sky:** `city_skyline_8k.hdr` is a 2D HDR texture capped at 2048 with default Android compression; ASTC HDR support on the Quest 2 GPU is unverified (packs row A17).
* **Render Graph compatibility mode, native render pass:** not switchable in 6000.5 (always the render graph, native render pass automatic).

## 7. B58 on Android (production change, static verification)

10 November 2026. **What "current Android settings" means at runtime** (from `PerfToggles`, `QuestExterior`, `QuestCullingProfile`, `RoomVisibility`, `CharacterPerfCuller`): the Android player runs the optimised exterior (`QuestExterior.DefaultVariant`), baked occlusion off (`QuestCullingProfile.OcclusionDefault`), room portal culling on (`RoomVisibility.Enabled` defaults to `Application.isMobilePlatform`; it only acts while `ExteriorVisibility.Enabled` is also true, which is its default), and the character culler on (`CharacterPerfCuller.DefaultEnabled`). The desktop preset B20 forces the original city but leaves the desktop defaults (baked occlusion ON, no room culling, no character culler), so **B20 is not an Android-equivalent base**. Equivalent Android presets exist for the optimised city (B29, and B56 = B29 + B58) and are now defined for the original city (B61, B62).

### Comparison set (all have baked occlusion off; ids unique, `tools/unity/check_preset_ids.py`: 64 presets, 64 unique ids, including B60 to B65)

| Id | City | Occlusion | Room cull | Character cull | Lights and shadows | Meaning |
|---|---|---|---|---|---|---|
| B15 | original | off | desktop default (off) | off | authored | the occlusion-free desktop base |
| B60 | original | off | off | off | B58 (2 per object, hard) | B15 + B58 |
| B61 | original | off | on | on | authored | **original city with the real Android settings** |
| B62 | original | off | on | on | B58 | B61 + B58 |
| B29 | optimised | off | on | on | authored | the Android production configuration |
| B56 | optimised | off | on | on | B58 | B29 + B58 |
| B63 | original | off | off | off | 3 per object, hard | selective alternative |
| B64 | original | off | off | off | 2 per object, soft | B24 on the B15 base |
| B65 | original | off | off | off | authored limit 4, hard | B22 on the B15 base |

One launch string for lane 4: `B20,B15,B60,B61,B62,B29,B56,B63,B64,B65` (6 rounds; B20 is the historical base only).

### Exact production change (not applied; lane A owns the URP assets, the coordinator applies it after the benchmark and the pixel diff)

File `unity/Assets/Settings/URP_Quest_Android.asset` (the only pipeline asset the Android player uses; Android is excluded from the other quality levels):

| Field | Old | New |
|---|---|---|
| `m_AdditionalLightsPerObjectLimit` (line 49; runtime name `maxAdditionalLightsCount`) | 4 | 2 |
| `m_SoftShadowsSupported` (line 67) | 1 | 0 |

`m_SoftShadowQuality: 2` can stay (ignored when soft shadows are off). Light components stay as authored.

### Why it is technically safe (from the scene files)

* **Soft shadows:** URP uses soft filtering only when the asset supports it AND the light's shadow type is Soft. Turning the asset flag off makes every light hard regardless of its own setting, so the per-light "Soft" type (2) on the Mixed suns does not override it. The only realtime shadow casters are the two Mixed directional suns (`Sun through window` in the bay, `Corridor sun`); the additional lights cast no realtime shadows (every point and spot light is either baked or has shadows off). The hard-shadow look change is therefore limited to the edge softness of the sun shadows within 8 m.
* **Additional lights, what is really realtime** (parsed from the three scene files; `m_Lightmapping` 4 = realtime, 2 = baked, 1 = mixed): baked lights (sconces, ceiling panels, daylight, headwall) are lightmap-only and are never in the per-object list. The realtime additional lights are the eight corridor `Ceiling fill` point lights (intensity 0.04, range 5 m, x 4.5, y 3.0, z from -8.3 to 12.7 in 3 m steps) and the directional `Soft clinical key` (intensity 0.05, 0.08 and 0.2 in the bay, corridor and theatre; enabled only in the room the player is in, by `RoomLoader`). The `Ceiling fill` lights of the bay (2) and the theatre (3) and the `Room light` lights are authored disabled.
* **Where more than 2 realtime additional lights overlap an object** (sample grid over each room, floor to 2.4 m):

| Room | Fills enabled | Points with more than 2 fills in range | Counting the clinical key |
|---|---|---|---|
| Corridor | 8 | floor 42%, 0.9 m 70%, 1.6 m 79%, 2.4 m 81% (2 fills minimum everywhere; 3 typical; 4 at head height in 6% to 12%) | more than 2 at 99% to 100% of points |
| Patient bay | 0 | 0% | at most 1 (the key) |
| Theatre | 0 | 0% | at most 1 (the key) |

So the limit of 2 only changes shading **in the corridor** (and for bay or theatre objects within 5 m of a corridor fill through the wall; the fills have no shadows): there the per-object list drops the third to fifth light. The dropped lights are weak (fill intensity 0.04, key 0.08), so the expected change is small but not zero, and URP's choice of which two to keep (distance and intensity ranking, with the directional key included) is not predictable from the files. **The pixel diff of B56 against B29 and B62 against B61 at the corridor views is the deciding evidence; nothing is adopted before it.** The theatre and bay are unaffected by the limit.
* **Earlier finding that this touches:** the occlusion investigation found that changing which fills a corridor object receives alters ceiling shading by up to 1.0% of pixels; capping at 2 changes the same selection, so the same ceiling-fixture region must be checked.
