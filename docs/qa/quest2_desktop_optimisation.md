# Quest 2 desktop optimisation round: what was done on this PC before any headset test

_8 November 2026 (UTC), owner decision D7 (ADR-0032) and the owner's follow-up of the same day (maximum possible Quest 2 optimisation and verification on the desktop first). Branch `quest-perf/d7-optimisations`. Companion of `quest2_performance_results.md`._

> **Correction (8 November 2026, later the same day).** The first version of this report counted every frame twice (the benchmark camera rendered itself as well as being rendered by the benchmark), so its absolute triangle and SetPass numbers were twice the real ones. Sections 2 and 3 below were measured again with the fixed benchmark (`unity/Logs/bench/full1.csv`, `full2.csv`); percentages were not affected. The city is 196,844 triangles, not 394,000.

**Read this first.** Every number below was measured on the development PC (Windows, GTX 1660 SUPER, Unity 6000.5.10f1 Editor, Medium quality level = the Android tier, an eye buffer of 1832 × 1920 with 4× MSAA). They are **counts the renderer reports** (triangles, SetPass calls, shadow casters) and desktop CPU render times. **They are not Quest frame rates and are never converted into one.** What the headset will do with these savings is **Not measured on Quest**. Triangle and SetPass counts are also what a tile-based mobile GPU has to process, so a reduction is expected to help, by an amount only DC-13 can tell.

## 1. Method

- **Tool:** `DesktopRenderBenchmark` (PlayMode, explicit; `tools/unity/bench_desktop.ps1`). It boots the game, lets `RoomLoader` stream the three rooms together exactly as in play, moves a camera to each benchmark view, applies a diagnostic configuration (`PerfPresets`, the same list the headset build uses) and records, over 20 frames per cell, the renderer's own `ProfilerRecorder` counters (SetPass calls, triangles, vertices, shadow casters, memory) and the CPU time of `Camera.Render()`. With `-Png` it also writes each view's image.
- **Views:** the eight benchmark views of `quest2_sannan_benchmark_guide.md` plus **V9 theatre far end** (added because V7 stands in the theatre doorway, where the door is legitimately in view).
- **Not available on this PC:** the Editor's draw-call counter returned 0 (SetPass calls are the batching figure used); the driver reports no GPU frame time, so no GPU millisecond figure exists; the Editor's overdraw and Render Graph Viewer views are interactive and were not scripted; shader cost is covered by the build report (`quest2_performance_results.md` section 9). None of these is claimed.
- **Visual check:** per view, the original and the optimised image are compared pixel by pixel (threshold 8/255).

## 2. Result: before the 8 November optimisations against now (desktop counts)

"Before" is configuration B19 (occlusion off, the city always drawn: the 7 November state); "now" is production default B0. Same camera, scenes and settings.

| View | Before: triangles (B19) | Before: SetPass | Now: triangles (B0) | Now: SetPass | Triangles change | Now: shadow casters | Desktop CPU render ms, before → now | City drawn now |
|---|---|---|---|---|---|---|---|---|
| V1 bay near wall | 522,420 | 64 | 204,176 | 15 | -60.9 % | 53 | 7.6 → 4.3 | no (hidden) |
| V2 bay overview | 633,597 | 96 | 633,121 | 88 | -0.1 % | 78 | 8.0 → 8.7 | yes |
| V3 bay window | 516,621 | 74 | 516,247 | 73 | -0.1 % | 45 | 7.0 → 8.2 | yes |
| V4 corridor long | 892,821 | 176 | 766,987 | 163 | -14.1 % | 38 | 13.8 → 29.4 | yes |
| V5 corridor people | 375,011 | 73 | 372,943 | 72 | -0.6 % | 37 | 7.9 → 10.1 | yes |
| V6 reception | 635,167 | 63 | 635,167 | 63 | +0.0 % | 122 | 7.2 → 10.2 | yes |
| V7 theatre | 305,957 | 35 | 304,247 | 32 | -0.6 % | 27 | 5.1 → 6.9 | yes |
| V8 corridor exterior | 235,921 | 40 | 235,885 | 40 | -0.0 % | 10 | 5.0 → 6.6 | yes |
| V9 theatre far end | 273,341 | 34 | 76,497 | 20 | -72.0 % | 22 | 4.9 → 4.3 | no (hidden) |

Reading: the savings are large where the player cannot see the city or the other rooms (V1 -61 %, V9 -72 %, V4 -14 % from occlusion and the room-aware city culling together) and **zero where a window or glass wall legitimately shows the city** (V2, V3, V5 to V8: -0.6 % to +0.0 %). That is by design: nothing visible was removed. The remaining cost in those views is the city itself (196,844 triangles visible through windows, 84 % of V8's) and the characters; the city part is addressed by the optimised exterior variant (`quest2_exterior_optimisation.md`), which needs the owner's approval; characters need it too.

## 3. Every configuration (triangles / SetPass calls)

| Configuration | V1 | V2 | V3 | V4 | V5 | V6 | V7 | V8 | V9 |
|---|---|---|---|---|---|---|---|---|---|
| B19 Before the 8 Nov optimisations (occlusion off, city always drawn) | 522,420 / 64 | 633,597 / 96 | 516,621 / 74 | 892,821 / 176 | 375,011 / 73 | 635,167 / 63 | 305,957 / 35 | 235,921 / 40 | 273,341 / 34 |
| B0 Optimised production default | 204,176 / 15 | 633,121 / 88 | 516,247 / 73 | 766,987 / 163 | 372,943 / 72 | 635,167 / 63 | 304,247 / 32 | 235,885 / 40 | 76,497 / 20 |
| B16 Exterior culling off (city always drawn) | 399,304 / 26 | 633,441 / 88 | 516,261 / 73 | 767,013 / 163 | 372,959 / 72 | 635,167 / 63 | 304,247 / 32 | 235,923 / 40 | 271,925 / 32 |
| B15 Occlusion culling off | 325,286 / 49 | 634,193 / 95 | 516,335 / 73 | 892,815 / 176 | 374,973 / 73 | 635,167 / 63 | 305,663 / 34 | 235,925 / 40 | 76,497 / 20 |
| B6 Realtime fills off | 204,158 / 15 | 632,821 / 88 | 516,247 / 73 | 766,985 / 163 | 372,955 / 72 | 635,167 / 63 | 304,247 / 32 | 235,891 / 40 | 76,497 / 20 |
| B7 Only current room drawn (measurement only) | 131,948 / 13 | 632,457 / 85 | 516,205 / 71 | 503,613 / 116 | 288,667 / 54 | 462,579 / 59 | 287,939 / 30 | 235,919 / 40 | 64,233 / 18 |
| B8 City hidden everywhere (measurement only) | 204,166 / 15 | 436,315 / 74 | 319,433 / 59 | 570,143 / 146 | 176,105 / 58 | 438,323 / 49 | 108,819 / 20 | 39,069 / 27 | 76,497 / 20 |
| B9 Characters hidden (measurement only) | 71,878 / 13 | 489,737 / 83 | 372,577 / 68 | 568,761 / 149 | 338,177 / 67 | 404,125 / 56 | 269,467 / 27 | 235,909 / 40 | 59,107 / 18 |
| B14 Candidate set: foveation 0.5, fills off, current room only (measurement only) | 131,950 / 13 | 632,447 / 85 | 516,195 / 71 | 503,613 / 116 | 288,663 / 54 | 462,579 / 59 | 287,939 / 30 | 235,913 / 40 | 64,233 / 18 |
| B17 Optimised exterior city (P3-EXT, see `quest2_exterior_optimisation.md`) | 203,876 / 14 | 488,039 / 91 | 347,957 / 72 | 601,201 / 163 | 236,549 / 76 | 483,563 / 63 | 123,593 / 30 | 89,909 / 42 | 76,497 / 20 |
| B18 Optimised exterior without portal culling | 203,876 / 14 | 497,079 / 90 | 378,503 / 75 | 613,709 / 163 | 236,541 / 76 | 483,669 / 63 | 155,869 / 32 | 111,581 / 44 | 76,497 / 20 |

Rows marked "measurement only" change what is drawn and exist to show where the cost sits; they are not proposed settings. B15 keeps the exterior culling on and switches only occlusion off; B16 keeps occlusion on and switches only the exterior culling off; B19 switches both off.

## 4. Visual comparison

| View | Original against optimised: mean absolute difference (of 255) | Pixels differing by more than 8 | What differs |
|---|---|---|---|
| V1 | 0.0000 | 0 | nothing |
| V2 | 0.027 | 2,947 | idle patient breathing, dust |
| V3 | 0.071 | 7,698 | idle patient breathing, dust |
| V4 | 0.027 | 2,712 | corridor people idle motion |
| V5 | 0.141 | 15,594 | corridor people idle motion |
| V6 | 0.087 | 11,645 | corridor people idle motion |
| V7 | 0.436 | 44,557 | theatre staff idle motion (the difference mask outlines the people only, checked by eye) |
| V8 | 0.003 | 443 | dust |

In a controlled comparison inside one session (optimisation on against off, same frame of animation) V8 and V9 are pixel-identical and the other views differ only on animated people. **No static surface changed in any view.** This is a desktop render; the headset's colour, foveation and MSAA resolve are not part of it.

## 5. Each optimisation (finding, change, evidence, visual impact, risk, how to undo)

### 5.1 Baked occlusion culling: done
- **Finding:** no scene had occlusion data and no `LODGroup` exists; frustum culling alone lets everything behind a wall through (V1 draws 318,244 triangles of other rooms and city while looking at a wall).
- **Change:** `OcclusionBake` (`Assets/Editor/Setup/OcclusionBake.cs`) opened the three rooms together and marked large opaque static fixtures Occluder + Occludee static and other authored-static renderers Occludee (`quest2_occlusion_bake.md`); glass, alpha-cut and curtain materials are never occluders; the city and anything outside the hospital stay out of the bake (the first attempt including them ran out of memory in Umbra's generator). One data asset, `Assets/Scenes/PatientBay/OcclusionCullingData.asset` (12 MB), is referenced by all three scenes. Bake time 389 s.
- **Evidence:** triangles with occlusion on (B16) against off (B19): V1 -24 % (522,420 to 399,304 with the city drawn), V4 -14 % (892,821 to 767,013); other views within a fraction of a percent. SetPass V1 64 to 26, V4 176 to 163.
- **Visual impact:** none measured (section 4). **Risk:** a wrongly marked occluder would make objects vanish; verified on the nine views only, so the headset walk-through (DC-13) must also look for popping. **Undo:** `git checkout` the three scene files and delete the data asset, or switch `Camera.useOcclusionCulling` off (diagnostic B15).

### 5.2 Room-aware exterior-city culling: done
- **Finding:** the city's meshes cannot be rejected by frustum or occlusion culling (their bounds enclose the hospital), so about 197 k triangles were drawn in the theatre and behind every solid wall. The city can only be seen through a window or a door.
- **Change:** `ExteriorVisibility` (`Assets/Scripts/Views/ExteriorVisibility.cs`; on by default on Android only, off on desktop and in the Editor): before a camera renders it finds the camera's room; if no window glass of that room and no door of that room is inside a wider-than-headset view test (112° vertical, aspect 1.15, apertures expanded 0.5 m), the city renderers are switched off for that camera's frame. A camera outside every room (a doorway transition) always shows the city. It uses `Renderer.forceRenderingOff` only and restores the authored state. Test: `ExteriorVisibilityTests`.
- **Evidence:** V1 -49 % triangles (399,304 to 204,176), V9 -72 % (271,925 to 76,497); V2-V8 unchanged because a window or door is in view. Desktop CPU render time V1 6.7 to 4.3 ms (a desktop number).
- **Visual impact:** none: V8 and V9 pixel-identical, V1 identical to the original. **Risk:** a window entering the view from a very oblique angle must reappear without delay; the margin is generous (112° tested against about 97° for the headset) but this is the first thing to watch on the headset (DC-13: turn quickly towards the bay window and the corridor glass). **Undo:** `ExteriorVisibility.Enabled = false` (diagnostic B16) or remove the component.

### 5.3 Splitting the city into spatial chunks: tried, no benefit, reverted
Splitting the 7 big city renderers into 559 chunks (120 × 80 m cells; 24 m cells made 3,605) kept every triangle but **added nothing on its own** over 5.1: the chunks next to the hospital are never occluded because the occlusion data does not cover the city, and it added a 45 MB mesh asset. The tool and the asset were removed; the original city was never changed. **Update, later on 8 November:** the same idea became worthwhile once the draw decision used the view cone through each window (portal culling) instead of occlusion; see `quest2_exterior_optimisation.md`.

### 5.4 Not changed, with the reason

| Area | Finding | Decision |
|---|---|---|
| City visible through windows | 196,844 triangles in V2-V8 (V8: 84 % of the frame) | The only large saving left in window views; **done as a separate, switchable variant** in `quest2_exterior_optimisation.md` (far traffic and trees simplified, chunks, portal culling): the city's cost falls 60 to 92 % on the desktop. The default stays the original until the owner approves (D7). |
| Characters | Hiding all skinned meshes removes up to 231,042 triangles per view (B9: -24 in V8, 231,042 in V6); CPU skinning is the build default | GPU skinning is built as `diag-gpuskin`; decision after DC-13. Replacing character models is prohibited without approval. |
| Realtime lights | B6 (fills off) changes triangles by 0 and SetPass by 0 to 2 | Lowest value on desktop counts; the per-pixel cost can only be judged on the headset. Not applied. |
| Static batching flags | 349 of 414 bay objects and 316 of 608 corridor objects are not authored static; SetPass is 28-326 per view | Some objects move (drop, interact); needs a per-object review, so no blanket change (P3-PRF-12). |
| LOD groups | None in the project | Needs authored lower-detail meshes; nothing was decimated automatically on approved assets. |
| Foveation, intermediate texture, shafts and dust, resolution | Headset-only questions | Unchanged today (see `quest2_performance_results.md`). |
| Skinned mesh warning | The Editor logs `First two bone weights should have non-zero sum` | Traced: the XR Interaction Toolkit starter-asset mesh `Pinch_Pointer_LOD0` (546 vertices, no bone weights) on the player rig's poke pointer; all 340 other skinned meshes are clean (`skinned_bone_weight_scan.md`). Package sample content, harmless; DT-282 closed. |

## 6. What only the headset can tell

Everything above reduces work the renderer is asked to do; **whether the Quest 2 now holds 72 FPS is unknown**. Sannan runs DC-13 with the new diagnostic APKs: compare B0 against B15 (occlusion off) and B16 (exterior culling off) in views V1, V4 and V9, walk through every door looking for popping, and send the CSVs.
