# Quest 2: why the sunlight shafts and the dust are missing

> **Update, 7 November 2026 (UTC), owner decision D7 (ADR-0032): fixed on branch `quest-perf/d7-optimisations`.** F1/F2: `DesktopOnly` no longer hides the effects on Android; it is now a tier switch (`ShowOnAndroid`, default on; diagnostic configuration B3 hides them to measure their cost). DC-12 now asks for the effects in both eyes. F3: both shaders now carry `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` and, in the fragment, `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` (the shaft uses the eye's own camera position); EditMode test `QuestRenderTierTests`. The appearance is unchanged on desktop (capture comparison in `quest2_performance_results.md` section 3). F5: the diagnostic APK build compiles them for Vulkan (section 9 of the results file). **Still to verify on the headset (DC-13):** both-eye display (F4) and cost (F10). The text below is the investigation as written before the fix.

_Investigation of 7 November 2026 (UTC) at `main` = `641f6b27`. Read-only: no setting, scene, shader or build was changed. No headset was available; nothing here was observed on a Quest. Each finding is marked **Confirmed** (read in code or settings, no doubt about what it does) or **Hypothesis** (needs a device test, listed in DC-13 part B)._

## 1. Summary

The shafts and the dust are not failing on Quest. **They are switched off on Android on purpose**, by a script written during the visual overhaul so that the headset would not pay for them before they were measured. Their two shaders would also need a small change before they can draw correctly in both eyes on Quest. Neither effect is expensive by design (no ray marching, no depth texture), but the shafts are large additive transparent surfaces, so their cost on the Quest's tile-based GPU is fill rate, and it has never been measured.

## 2. What the effects are

| Effect | Where it is built | What it is |
|---|---|---|
| Sunlight shafts ("cones") | `unity/Assets/Editor/Environment/CorridorSun.cs` (`ShaftsFor`), called for the bay by `PatientBaySun.cs` | Several overlapping prism meshes per window opening, merged into one mesh per opening (corridor 3 objects `Shafts z…`, bay 1), drawn with `ProcSense/Sun Shaft` (`unity/Assets/Shaders/SunShaft.shader`): additive (`Blend One One`), no depth write, both faces, transparent queue +6, value noise along the sun direction, fade by view angle, along the beam and near the camera. No depth texture, no ray marching |
| Dust motes | Same builder | One `ParticleSystem` per strong prism (corridor 6, bay 2), 150 particles each, size 5-20 mm, world space, slow velocity and noise, billboards with `ProcSense/Sun Dust Mote` (`SunDustMote.shader`): additive soft dot |

Both live under one parent object named `Door sun shafts` in each room.

## 3. Findings

| # | Finding | Class | Evidence |
|---|---|---|---|
| F1 | The parent `Door sun shafts` carries the `DesktopOnly` component, whose `Awake` calls `gameObject.SetActive(false)` when `Application.platform == RuntimePlatform.Android`. The whole subtree (shafts and dust) is therefore inactive in every Android build, including the Quest APK | **Confirmed** | `unity/Assets/Scripts/Views/DesktopOnly.cs`; `CorridorSun.cs:197` (`host.AddComponent<ProcSense.VR.Views.DesktopOnly>()`); the shader header comment says "the host object carries DesktopOnly, so Quest never draws it" |
| F2 | This was a deliberate, documented decision: the headset check DC-12 says "The soft sunshafts and floating dust are desktop-only by design and must NOT appear in the headset" | **Confirmed** | `docs/testing/external_testing.json` (DC-12 procedure step 3) |
| F3 | Both shaders lack the single-pass-instanced stereo macros (`UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`) and `#pragma multi_compile_instancing`. The Android OpenXR render mode is single-pass instanced (`m_renderMode: 1` in `OpenXR Package Settings.asset`, set by `ProjectSetup.cs:146`). Unity's documentation says custom shaders need these macros for single-pass instanced rendering; URP's own shaders already have them | **Confirmed** (missing macros) | Shader files; Unity manual, "Single-pass instanced rendering and custom shaders" (https://docs.unity.com/en-us/engine/6000.5/manual/xr/graphics/stereo-rendering/single-pass-instancing) |
| F4 | If F1 alone were removed, the shafts and dust would most likely draw in one eye only, at the wrong position in the other eye, or not at all on Quest (Vulkan multiview) | **Hypothesis** | Follows from F3; to confirm in DC-13 part B with the diagnostic APK |
| F5 | The objects and their materials are in the APK: the scenes contain them (inactive at run time on Android), the materials reference the two shaders, so the shaders are included and compiled for Vulkan | **Confirmed** (by scene references); compile success on Vulkan **Hypothesis** until a build log is read | Scenes `PatientBay.unity`, `Corridor.unity` |
| F6 | No depth or opaque texture is needed (the beam ends where opaque geometry writes depth; `m_RequireDepthTexture: 0`), so the effects do not depend on a URP texture Quest lacks | **Confirmed** | `URP_Quest.asset`, shader code |
| F7 | Additive blending with `Blend One One` and `return half4(col, 1)` cannot produce an invisible result from alpha; brightness comes from `_Intensity` (0.06 default) × vertex strength × noise × fades. With HDR off on Android the result is clamped to the 8-bit target; the effect is faint but not zero | **Confirmed** (math) | Shader code; `m_SupportsHDR: 0` |
| F8 | The dust materials reference a saved texture asset (`Assets/Textures/Environment/soft_dot.png`, by GUID), so the texture is in the build; a missing texture is ruled out | **Confirmed** | `Assets/Materials/Environment/Sun dust mote.mat`, `Sun dust mote bay.mat` |
| F9 | No quality level, camera culling mask or layer removes them: they use the default layer, the rig camera renders all layers, and every quality level uses the same URP asset | **Confirmed** | `QualitySettings.asset` (one `customRenderPipeline` guid), `PlayerRig.prefab` |
| F10 | Cost: never measured on Quest. The corridor's shaft meshes can cover a large part of the view when the player stands in a beam; each covered pixel runs two 3D value-noise evaluations (16 hashes) per prism layer, so overlapping prisms multiply the cost. On a tile-based GPU this is fill rate (overdraw), not geometry | **Hypothesis** (no number) | Shader code; measured in DC-13 part B |

## 4. Why the dust is "missing"

Same cause as the shafts (F1): the particle systems are children of the same `DesktopOnly` host, so they are inactive on Android. The particle shader has the same missing stereo macros (F3). Particle count (at most 1,200 in the corridor) and the billboard size are small; their fill cost is expected to be low but is not measured.

## 5. Keeping the current look on Quest (no redesign)

1. Add the four stereo macros and `multi_compile_instancing` to both shaders (same visual output on desktop).
2. Replace `DesktopOnly` on this host by a tier switch that keeps the effects on Quest when the Quest tier allows them (P3-PRF-11).
3. Measure the cost in DC-13 part B with the diagnostic toggle, in the worst view (standing in a corridor beam) and the typical view.
4. Only if the cost is too high: reduce overlapping prism layers per opening and particle counts on the Quest tier while matching the desktop look at headset distance, and compare side-by-side captures with the owner. No change to the desktop version.

## 6. Unresolved questions

- Does the shader compile without warnings for Vulkan (read the build log of the next APK)?
- F4 above.
- The frame-time cost of each effect on Quest 2 (DC-13 part B).
