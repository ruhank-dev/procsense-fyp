# Quest 2 performance analysis (static investigation, no headset)

_Investigation of 7 November 2026 (UTC) at `main` = `641f6b27`. Read-only: no project setting, scene, material, light or build was changed. No Quest headset was available (Sannan has it)._

## 0. What kind of evidence this report contains

| Kind | Present? |
|---|---|
| Frame rate measured on a Quest 2 | **No.** Nothing in this repository records one for the current environment |
| Frame rate reported by a teammate | **About 20-30 FPS**, reported verbally; conditions (build, room, firmware, temperature, metric tool) unknown. **Unverified** until DC-13 part A |
| Desktop/Editor measurement | Not used for any Quest conclusion; a PC GPU says nothing reliable about the Quest 2 tile-based GPU |
| Static analysis of settings, scenes and code | **Yes** (this report). Findings are **Confirmed** when read directly from files; costs are **estimates** or **unknown** |

Target: 72 Hz on Quest 2 (the runtime default; no code requests another refresh rate), that is **13.9 ms per frame** for CPU and GPU each, with headroom. 20-30 FPS would mean about 33-50 ms per frame: two to three and a half times over budget. Gaps that large usually come from several large costs at once, not from one effect.

## 1. Build and project settings audit

| Setting | Current value | Source | Quest-appropriate? |
|---|---|---|---|
| Unity | 6000.5.10f1 | `ProjectSettings/ProjectVersion.txt` | — |
| URP | 17.5.0; OpenXR 1.18.0; XRI 3.6.1; XR Management 4.7.0 | `Packages/manifest.json` | — |
| Quality level on Android | Medium (index 2) | `QualitySettings.asset` `m_PerPlatformDefaultQuality` | Every level uses the **same** pipeline asset, so the level changes little in URP |
| URP pipeline asset | `URP_Quest.asset` for every quality level and as the default; **shared with desktop** | `GraphicsSettings.asset`, `QualitySettings.asset` | **No:** desktop look and Quest cost cannot be tuned separately |
| Renderer | Forward, no renderer features (no SSAO, no decals), depth priming off | `URP_Quest_Renderer.asset` | Yes |
| **Intermediate texture** | **Always** (`m_IntermediateTextureMode: 1`) | `URP_Quest_Renderer.asset` | **No.** Unity: "Always … might have a significant performance impact on some platforms"; Meta/Unity: foveated rendering is not supported when intermediate render targets are used |
| Stereo rendering | Single-pass instanced (multiview on Android) | `OpenXR Package Settings.asset` (Android `m_renderMode: 1`), `ProjectSetup.cs:146` | Yes |
| Graphics API | Vulkan only | `ProjectSettings.asset` `m_BuildTargetGraphicsAPIs` (Android `15000000`) | Yes |
| Eye-buffer resolution | Runtime default (render scale 1.0, no `eyeTextureResolutionScale` set): **1440 × 1584 per eye** on Quest 2 per Meta's table | URP `m_RenderScale: 1`; Meta "OS render scale" page | Reasonable default (see section 3) |
| Dynamic resolution | Off (OpenXR feature off; camera `m_AllowDynamicResolution: 0`) | OpenXR settings, rig camera | Option |
| Fixed foveated rendering | **Off** (OpenXR Foveated Rendering feature disabled on Android) | OpenXR settings | **Unused saving** |
| Application SpaceWarp | Off | OpenXR settings | Option with caveats (section 4) |
| MSAA | 4x | `URP_Quest.asset` | Yes (Meta recommends 4x); cost rises with the intermediate texture resolve |
| HDR | Off (pipeline); camera flag on but ignored | `URP_Quest.asset`, sample rig prefab | Yes |
| Post-processing | Rig camera has **no serialized URP camera data**, so URP adds it with post-processing **off**; no runtime script switches it on | `PlayerRig.prefab` and the XRI sample rig (no `UniversalAdditionalCameraData`); code search | Cost-wise fine. **Hypothesis to verify:** the -0.4 EV exposure volume (VO-16) therefore does not apply in Play mode or on Quest; the desktop captures that included it used capture cameras with post on |
| Depth / opaque textures | Off / off | `URP_Quest.asset` | Yes |
| Additional lights | Per pixel, up to 4 per object, no additional-light shadows, light cookies on, light layers on | `URP_Quest.asset` | Costly if many realtime lights touch large surfaces (section 2) |
| Main light shadows | 2048, 1 cascade, distance 8 m, soft shadows (medium quality) | `URP_Quest.asset` | Moderate; soft medium uses several taps per pixel |
| Mixed lighting | Shadowmask; one connected bake for the three rooms: 8 directional lightmaps up to 2048 px at 64 texels/m, plus directionality and shadowmask textures | `PatientBay.lighting`, `Assets/Scenes/PatientBay/` | Baked lighting is right for Quest; directional lightmaps add a texture fetch per pixel |
| Reflection probes | Bay: 1 baked 512 + 1 custom 256; corridor: 3 baked 128; OT: 1 baked 128; box projection on, blending off | Scenes, `URP_Quest.asset` | Yes (DT-268 asks for a device check of the 512 HDR cube) |
| Batching | Static batching on, dynamic off, SRP Batcher on, GPU Resident Drawer off | `ProjectSettings.asset`, `URP_Quest.asset` | Yes; effectiveness unmeasured |
| Occlusion culling | **No occlusion data** in any scene | Scene folders | **Unused saving** (three rooms loaded at once) |
| LOD | **0 LOD Groups** in PatientBay, Corridor, OT | Scene files | Characters and repeated props render at full detail at every distance |
| Skinning | **CPU** (`gpuSkinning: 0`) | `ProjectSettings.asset` | Likely costly with the Rocketbox people; GPU skinning is an option |
| Animators | Rocketbox people: `CullUpdateTransforms`; `StaffIdleView` updates every 0.1 s on Android | `RocketboxSetup.cs:91`, VO-12 | Good start; distance LOD absent |
| Textures | 635 importers; about 446 have Android overrides (mostly ASTC 6x6 at 1024, some 2048 and two 4096); 42 have mipmaps off | `.meta` files | Mostly right; mip-less textures in 3D cause shimmering and bandwidth |
| Scripting | IL2CPP, ARM64, medium stripping, multithreaded rendering on | `ProjectSettings.asset` | Yes |
| Adaptive Performance | `m_UseAdaptivePerformance: 1` in the URP asset; only the built-in module is in the manifest, no provider | Manifest | No effect |

## 2. Scene load at run time

`RoomLoader` (Continuous mode) loads **PatientBay, Corridor and OT together** and keeps them loaded; nothing disables the rooms the player cannot see. Static counts read from the scene files:

| Scene | Mesh renderers (+ prefab instances) | Realtime/mixed lights switched on | Skinned renderers in the scene file | Particle systems |
|---|---|---|---|---|
| PatientBay | 118 (+35) | 1 mixed directional sun with soft shadows; 1 realtime directional without shadows | 12 (patient, gown, curtains, hidden old rig parts) | 2 (dust, off on Android) |
| Corridor | 431 (+76) | 1 mixed directional sun with soft shadows; 1 realtime directional without shadows; **8 realtime point lights** | in prefab instances (Rocketbox people) | 6 (dust, off on Android) |
| OT | 135 (+21) | 1 realtime directional without shadows | in prefab instances (2 theatre doctors) | 0 |

**Confirmed:** with all rooms loaded, the frame has 2 mixed directional suns (only one can be URP's main light and cast the realtime shadow; the other becomes a per-pixel additional light), 3 unshadowed realtime directional lights and 8 realtime point lights. A directional additional light touches **every** object, so in the bay every pixel can run up to 4 additional-light evaluations on top of the main light, lightmap, directional lightmap, shadowmask, reflection probe and PBR textures. **Estimate:** this per-pixel lighting, with 4x MSAA and the forced intermediate texture, is a strong candidate for the GPU time. **Unknown until measured:** the share of each.

## 3. Render resolution

- Quest 2 panels are 1832 × 1920 per eye; Meta's default eye buffer at render scale 1.0 is **1440 × 1584 per eye** (about 2.28 megapixels per eye, 4.56 for both). Matching the panel would need a scale of about 1.24 (Meta, "OS render scale").
- The project does not raise the scale (URP render scale 1.0, no override), so it renders at the default, not above it. **Confirmed.**
- Each eye is rendered from its own viewpoint into a texture-array layer in one pass (multiview); this is not "a 1080p or 4K video": cost scales with the pixels of both eyes, the samples per pixel (4x MSAA) and the work per pixel.
- With the intermediate texture forced on, URP renders into its own MSAA target and then resolves and copies into the eye buffer the OpenXR runtime provides; with Auto and no post-processing it can render straight into the eye buffer. **Confirmed by settings; cost unknown until measured.**
- Lowering resolution is not the first lever here: the default is already below the panel. Foveated rendering lowers the shading rate only at the lens edges, where the Quest 2 optics blur anyway.

## 4. Most likely bottlenecks (ranked, all to be confirmed by DC-13)

| Rank | Candidate | Why it is likely | Evidence class |
|---|---|---|---|
| 1 | GPU fill: per-pixel lighting with extra realtime directional and point lights across three loaded rooms | Light counts confirmed; directional lights touch every pixel | Confirmed setup, cost hypothesis |
| 2 | GPU bandwidth: forced intermediate texture with 4x MSAA (resolve + copy every frame) and no foveation | Setting confirmed; foveation blocked by it | Confirmed setup, cost hypothesis |
| 3 | GPU/CPU: three rooms always rendered (no room visibility, no occlusion data, no LODs), 680+ mesh renderers plus 130+ prefab instances | Counts confirmed | Confirmed setup, cost hypothesis |
| 4 | CPU: CPU skinning of the Rocketbox people, patient, gown and curtains (with blendshapes and secondary bones) | `gpuSkinning: 0` confirmed | Confirmed setup, cost hypothesis |
| 5 | GPU: soft shadow sampling of the 2048 main-light shadow on all receivers within 8 m | Setting confirmed | Hypothesis |
| 6 | CPU: script and UI polling (`PracticeOnly.Update`, tablet and views reading state every frame) | Code confirmed | Small, hypothesis |

## 5. Settings never tuned for Quest specifically

The shared pipeline asset; intermediate texture Always; foveation off; additional-light count and realtime point lights; CPU skinning; no occlusion culling; no LODs; no room visibility streaming; soft shadow quality; light cookies and light layers enabled without a measured need; 42 textures without mipmaps.

## 6. Redundant passes and cameras

- **Confirmed:** one camera renders the XR view (the rig's Main Camera). The login rig is destroyed before the application rig loads (`BootLoader.cs`). Capture cameras exist only in Editor tools.
- **Confirmed:** no renderer feature adds passes. The intermediate texture adds a resolve/copy pass (section 3).
- **To verify on device:** no second camera is active in the built APK (the RenderDoc/OVR GPU profiler capture in DC-13 part B shows the passes).

## 7. Unresolved questions (for DC-13)

1. Measured CPU and GPU frame times per room and view, cool and warm, and the GPU/CPU levels the system chose.
2. Is the app GPU-bound, CPU-bound or both (OVR Metrics shows both times)?
3. Effect of each diagnostic toggle (see `quest2_optimization_options.md` and the guide).
4. Does the 512 px HDR reflection cube render correctly (DT-268)?
5. Is the exposure trim visible on the headset (post-processing hypothesis above)?

## 8. Sources

- Meta, "OS render scale" (Quest 2 default 1440 × 1584 per eye, panel 1832 × 1920): https://developers.meta.com/horizon/documentation/unity/os-render-scale/
- Unity, URP Universal Renderer, Intermediate Texture: https://docs.unity3d.com/6000.6/Documentation/Manual/urp/urp-universal-renderer.html
- Unity, foveated rendering support (intermediate render targets limitation): https://docs.unity.com/en-us/engine/current/manual/xr/graphics/foveated-rendering/support
- Unity OpenXR foveated rendering feature: https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.19/manual/features/foveatedrendering.html
- Unity, single-pass instanced rendering and custom shaders: https://docs.unity.com/en-us/engine/6000.5/manual/xr/graphics/stereo-rendering/single-pass-instancing
- Meta, Unity best practices and draw call analysis: https://developers.meta.com/horizon/documentation/unity/unity-best-practices-intro/ , https://developer.oculus.com/documentation/unity/po-draw-call-analysis
- Meta, OVR Metrics Tool: https://developer.oculus.com/documentation/unity/ts-ovrmetricstool

All pages accessed 7 November 2026; recorded in `docs/research/research_log.md`.
