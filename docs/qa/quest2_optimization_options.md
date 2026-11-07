# Quest 2 optimisation options (ranked, not applied)

> **Status update, 7 November 2026 (UTC), D7:** option 1 (separate Android asset) and option 2 (intermediate texture Auto on Android) are **done** on branch `quest-perf/d7-optimisations` with no desktop change; option 3 (foveation) is **available at level 0**, levels 0.5/1.0 are diagnostic configurations B4/B5; option 10 (shafts and dust on Quest, stereo-safe) is **done**, cost measured with B3; options 4, 5, 7, 11, 16 have diagnostic configurations (B6, B7, `diag-gpuskin`, B10, B11/B12). The full feature table with "Measured cost" columns is now `quest2_performance_results.md` section 6; this file keeps the original ranking for history.

_7 November 2026 (UTC). Companion to `quest2_performance_analysis.md`. Nothing here is applied; the owner chooses after the DC-13 measurements (decision D7 in `docs/plans/p3/P3_FINAL_GAME_INTEGRATION_ROADMAP.md`). Constraint from the owner: keep the hospital geometry, details, high-quality textures, realistic materials, lighting direction, sunlight shapes, soft shafts, moving dust, shadows, reflections, characters, equipment and overall look. Options that reduce any of these are marked and are not recommended without approval._

## How to read "estimated benefit"

No Quest measurement of this project exists yet, so **no row claims a number of FPS**. Benefit is given as one of:
- **Doc:** the effect is documented by Unity or Meta (qualitative; the size depends on the scene).
- **Reasoned:** follows from the confirmed settings and how a tile-based GPU works; size unknown.
- **Unknown:** needs the headset.
Every "Recommended" row is measured with a diagnostic toggle in DC-13 part B before it is applied for real.

## Options table

| # | Optimisation | Current status | Performance impact | Estimated FPS / frame-time benefit | Visual impact | Difficulty | Recommended? |
|---|---|---|---|---|---|---|---|
| 1 | Separate Android URP asset and renderer (desktop keeps today's) | One shared asset | Enables every Quest-only change below without touching the desktop look | None by itself | None | Low | **Yes, first** |
| 2 | Intermediate texture **Auto** on the Android renderer | Always | Removes the forced extra MSAA target resolve/copy per frame when no pass needs it; unblocks foveation | Doc + Reasoned; size Unknown (likely significant on a bandwidth-limited mobile GPU) | None | Low | **Yes** |
| 3 | Fixed foveated rendering (OpenXR feature, level by script) | Off | Lower shading rate at the lens edges | Doc (Meta/Unity); typical gains are in GPU time at medium/high levels; size Unknown here | Slight blur at periphery, largely hidden by the lens; text panels stay central | Low (needs #2) | **Yes** (start medium; owner checks text at the edges) |
| 4 | Realtime light budget per room on Android: one main sun only; extra realtime directionals off or baked; corridor's 8 realtime points baked or limited to the room the player is in | 3 extra realtime directionals + 8 points live at once | Fewer per-pixel light evaluations on every fragment | Reasoned; potentially large; Unknown | None if their contribution is already baked or tiny (check each light: several are fill/bounce helpers) | Medium | **Yes** (light by light, with capture comparison) |
| 5 | Room visibility: disable renderers and lights of rooms not visible from the player's room (door portals), keep scenes loaded | All three rooms render | Fewer draw calls, vertices and culling work | Reasoned; Unknown | None | Medium | **Yes** |
| 6 | Occlusion culling bake (three rooms) | None | Hides objects behind walls in the same room set | Reasoned; Unknown | None (watch for popping) | Low-Medium | **Yes** (after #5) |
| 7 | GPU skinning (Android) | CPU skinning | Moves skinning off the CPU | Reasoned; CPU benefit Unknown | None | Low (one setting + test) | **Yes** (measure CPU before/after) |
| 8 | LOD Groups for characters and dense props (corridor people, benches, plants) | None | Fewer vertices at distance | Reasoned; Unknown | None at viewing distance when transitions are tuned; cross-fade is already enabled in URP | Medium (LOD meshes needed: Rocketbox ships lower-poly variants for some avatars; else decimation) | **Yes**, after measurement shows vertex/CPU load |
| 9 | Animator and script rate limits (distance-based animation update, event-driven UI instead of per-frame polling) | Partial (0.1 s staff idle on Android) | CPU | Reasoned; small to moderate | None | Low-Medium | **Yes** |
| 10 | Shafts and dust on Quest: stereo-safe shaders, tier switch instead of `DesktopOnly`, merged mesh per opening, fewer overlapping layers on Quest if needed | Off on Android | Adds cost (fill rate) to bring the effects back | Unknown (measure the worst view: standing in a beam) | **Restores** the approved look on Quest | Low-Medium | **Yes** (owner wants them) |
| 11 | Soft shadow quality low (or hard shadows) on Android; shadow distance tuned per room | Medium soft, 8 m | Fewer shadow taps per pixel | Reasoned; Unknown | Slightly harder shadow edges of dynamic objects (baked shadows unchanged) | Low | Measure; apply only with owner approval |
| 12 | Disable light cookies and light layers on Android if unused | On | Fewer shader variants and keywords | Reasoned; small | None if unused (check) | Low | **Yes**, after a usage check |
| 13 | Mipmaps on the 42 mip-less textures used in 3D (UI and lightmap-like cases excluded) | Off on 42 | Less bandwidth, less shimmer | Reasoned; small | Better (less aliasing) | Low | **Yes** |
| 14 | Texture budget review (ASTC 6x6/1024 for most; 2048/4096 overrides reviewed; ASTC 8x8 for large low-frequency surfaces) | Mostly done | Memory and bandwidth | Reasoned; small to moderate | Minimal if chosen per texture with captures | Medium | Partly (keep hero textures) |
| 15 | SRP Batcher compatibility of custom shaders; material sharing on repeated props; static batching flags | SRP Batcher on; compatibility unchecked | Fewer state changes / CPU render time | Doc (Meta draw-call analysis: material switches cost CPU) ; Unknown | None | Medium | **Yes** |
| 16 | Render scale / eye buffer: keep 1.0 (default 1440 × 1584) and add dynamic resolution only if still GPU-bound after 2-6 | 1.0, dynamic off | Fewer pixels when under load | Doc; Unknown | Softer image when it drops | Low | Later, only if needed |
| 17 | Application SpaceWarp (half-rate rendering with synthesised frames) | Off | Large GPU headroom | Doc (Meta); Unknown | Artefacts on transparent surfaces, particles (dust) and custom shaders without motion vectors; would need motion-vector passes in custom shaders | High | **Not now** (conflicts with the shafts/dust and the glass UI) |
| 18 | Depth submission to the runtime (for reprojection quality) | None | Better reprojection when frames drop | Doc; small | None | Low | Optional |
| 19 | Reflection probe resolution on Android (bay 512 → 256 if the device check of DT-268 shows a cost) | 512 / 256 / 128 | Memory/bandwidth | Unknown | Slightly softer reflections | Low | Only if measured |
| 20 | Characters: fewer people in the corridor on Quest (VO-12 density switch) | All placed | CPU/GPU | Reasoned | **Changes the scene** | Low | **Not without owner approval** |
| 21 | Lower texture resolution globally, remove props, simplify materials, drop shadows | — | Large | — | **Changes the look** | — | **No** (owner constraint) |
| 22 | Physics: cloth and secondary motion (`CurtainCloth`, `GownSecondaryMotion`) update rate on Android | Every frame | CPU | Reasoned; small | None at 30-45 Hz update for slow cloth | Low | Measure first |
| 23 | Object pooling for UI panels, printouts, audio sources | Partly (audio pooled) | CPU/GC spikes | Reasoned; small | None | Low | Yes, with P3 UI |
| 24 | Build configuration: IL2CPP master configuration, script debugging off in Demo builds, profiler only in Dev | Dev/Demo/Study builds exist | CPU | Small | None | Low | Check the Demo build settings |

## Recommended order (subject to DC-13)

1. Measure the unchanged Dev APK (DC-13 part A).
2. Diagnostic APK toggles for options 2, 3, 4, 5, 7, 10, 11, 16 (DC-13 part B), each alone and combined.
3. Apply in this order if measurements agree: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 10 → 15 → 12 → 13 → 8 → 9 → 22 → 11 (only with approval) → 16 (only if still GPU-bound).
4. Re-measure (DC-13 part C). Target: stable 72 FPS in every benchmark view on a warm headset.

Options 20 and 21 are listed for completeness and are not part of the plan unless the owner asks.

## Sources

Same list as `quest2_performance_analysis.md` section 8, plus Unity OpenXR SpaceWarp overview: https://docs.unity3d.com/Packages/com.unity.xr.openxr@1.15/manual/features/spacewarp/spacewarp-overview.html and Meta Application SpaceWarp for Unity: https://developers.meta.com/horizon/documentation/unity/unity-asw/ (accessed 7 November 2026).
