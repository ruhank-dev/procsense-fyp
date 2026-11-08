# Quest 2 desktop optimisation round: what was done on this PC before any headset test

_8 November 2026 (UTC), owner decision D7 (ADR-0032) and the owner's follow-up of the same day (maximum possible Quest 2 optimisation and verification on the desktop first). Branch `quest-perf/d7-optimisations`. Companion of `quest2_performance_results.md`._

**Read this first.** Every number below was measured on the development PC (Windows, GTX 1660 SUPER, Unity 6000.5.10f1 Editor, Medium quality level = the Android tier, an eye buffer of 1832 × 1920 with 4× MSAA). They are **counts the renderer reports** (triangles, SetPass calls, shadow casters) and desktop CPU render times. **They are not Quest frame rates and are never converted into one.** What the headset will do with these savings is **Not measured on Quest**. Triangle and SetPass counts are also what a tile-based mobile GPU has to process, so a reduction is expected to help, by an amount only DC-13 can tell.

## 1. Method

- **Tool:** `DesktopRenderBenchmark` (PlayMode, explicit; `tools/unity/bench_desktop.ps1`). It boots the game, lets `RoomLoader` stream the three rooms together exactly as in play, moves a camera to each benchmark view, applies a diagnostic configuration (`PerfPresets`, the same list the headset build uses) and records, over 20 frames per cell, the renderer's own `ProfilerRecorder` counters (SetPass calls, triangles, vertices, shadow casters, memory) and the CPU time of `Camera.Render()`. With `-Png` it also writes each view's image.
- **Views:** the eight benchmark views of `quest2_sannan_benchmark_guide.md` plus **V9 theatre far end** (added because V7 stands in the theatre doorway, where the door is legitimately in view).
- **Not available on this PC:** the Editor's draw-call counter returned 0 (SetPass calls are the batching figure used); the driver reports no GPU frame time, so no GPU millisecond figure exists; the Editor's overdraw and Render Graph Viewer views are interactive and were not scripted; shader cost is covered by the build report (`quest2_performance_results.md` section 9). None of these is claimed.
- **Visual check:** per view, the original and the optimised image are compared pixel by pixel (threshold 8/255).

## 2. Result: original build against optimised build (desktop counts)

"Original" is the same camera, scenes and settings before today's changes (the 7 November state); "optimised" is production default B0 now.

| View | Original build: triangles | Original: SetPass calls | Optimised: triangles | Optimised: SetPass calls | Triangles change | Optimised: shadow casters | Desktop CPU render ms, original → optimised | City drawn? |
|---|---|---|---|---|---|---|---|---|
| V1 bay near wall | 1,044,260 | 126 | 407,752 | 28 | -61.0 % | 106 | 3.726 → 2.196 | no (hidden) |
| V2 bay overview | 1,267,232 | 194 | 1,265,662 | 176 | -0.1 % | 156 | 4.503 → 4.198 | yes |
| V3 bay window | 1,032,648 | 146 | 1,032,522 | 146 | -0.0 % | 90 | 4.063 → 3.603 | yes |
| V4 corridor long | 1,785,658 | 354 | 1,533,958 | 326 | -14.1 % | 76 | 7.342 → 6.372 | yes |
| V5 corridor people | 749,970 | 146 | 745,854 | 144 | -0.5 % | 74 | 3.772 → 3.708 | yes |
| V6 reception | 1,270,334 | 126 | 1,270,334 | 126 | +0.0 % | 244 | 3.876 → 3.815 | yes |
| V7 theatre | 611,326 | 68 | 608,494 | 64 | -0.5 % | 54 | 2.648 → 2.883 | yes |
| V8 corridor exterior | 471,870 | 80 | 471,800 | 80 | -0.0 % | 20 | 2.886 → 2.811 | yes |
| V9 theatre far end (new view) | 543,850 (city drawn, B16) | 64 | 152,994 | 40 | -71.9 % vs city always drawn | 44 | 2.766 → 2.265 | no (hidden) |

Reading: the savings are large where the player cannot see the city or the other rooms (V1 −61 %, V9 −72 % against the city always drawn, V4 −14 % from occlusion) and **zero where a window or glass wall legitimately shows the city** (V2, V3, V5-V8). That is by design: nothing visible was removed. The remaining cost in those views is the city itself (about 394,000 triangles visible through windows, 83 % of V8) and the characters, which are the next candidates (section 5.4) and need the owner's approval because they would change what is drawn.

## 3. Every configuration (triangles / SetPass calls)

| Configuration | V1 | V2 | V3 | V4 | V5 | V6 | V7 | V8 | V9 |
|---|---|---|---|---|---|---|---|---|---|
| B0 Optimised production default | 407,752 / 28 | 1,265,662 / 176 | 1,032,522 / 146 | 1,533,958 / 326 | 745,854 / 144 | 1,270,334 / 126 | 608,494 / 64 | 471,800 / 80 | 152,994 / 40 |
| B16 Exterior culling off (city always drawn) | 798,608 / 52 | 1,266,264 / 176 | 1,032,514 / 146 | 1,533,982 / 326 | 745,920 / 144 | 1,270,334 / 126 | 608,494 / 64 | 471,806 / 80 | 543,850 / 64 |
| B15 Occlusion culling off | 650,572 / 98 | 1,268,424 / 190 | 1,032,658 / 146 | 1,785,628 / 352 | 749,978 / 146 | 1,270,334 / 126 | 611,326 / 68 | 471,886 / 80 | 152,994 / 40 |
| B6 Realtime fills off | 408,300 / 30 | 1,266,204 / 176 | 1,032,474 / 146 | 1,533,994 / 326 | 745,938 / 144 | 1,270,334 / 126 | 608,494 / 64 | 471,834 / 80 | 152,994 / 40 |
| B7 Only current room drawn (measurement only) | 263,308 / 24 | 1,264,874 / 172 | 1,032,370 / 142 | 1,007,234 / 232 | 577,344 / 108 | 925,158 / 118 | 575,878 / 60 | 471,790 / 80 | 128,466 / 36 |
| B8 City hidden everywhere (measurement only) | 407,752 / 28 | 872,586 / 150 | 638,872 / 118 | 1,140,228 / 294 | 352,154 / 116 | 876,646 / 98 | 217,638 / 40 | 78,086 / 54 | 152,994 / 40 |
| B9 Characters hidden (measurement only) | 143,750 / 26 | 978,894 / 166 | 745,166 / 136 | 1,137,458 / 298 | 676,324 / 134 | 808,250 / 112 | 538,934 / 54 | 471,834 / 80 | 118,214 / 36 |
| B14 Candidate set: foveation 0.5, fills off, current room only (measurement only) | 263,904 / 26 | 1,264,916 / 172 | 1,032,408 / 142 | 1,007,260 / 232 | 577,386 / 108 | 925,158 / 118 | 575,878 / 60 | 471,842 / 80 | 128,466 / 36 |

Rows marked "measurement only" change what is drawn and exist to show where the cost sits; they are not proposed settings. B15 keeps the exterior culling on and switches only occlusion off; B16 keeps occlusion on and switches only the exterior culling off.

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
- **Finding:** no scene had occlusion data and no `LODGroup` exists; frustum culling alone lets everything behind a wall through (V1 draws 390 k triangles of other rooms and city while looking at a wall).
- **Change:** `OcclusionBake` (`Assets/Editor/Setup/OcclusionBake.cs`) opened the three rooms together and marked large opaque static fixtures Occluder + Occludee static and other authored-static renderers Occludee (`quest2_occlusion_bake.md`); glass, alpha-cut and curtain materials are never occluders; the city and anything outside the hospital stay out of the bake (the first attempt including them ran out of memory in Umbra's generator). One data asset, `Assets/Scenes/PatientBay/OcclusionCullingData.asset` (12 MB), is referenced by all three scenes. Bake time 389 s.
- **Evidence:** triangles with occlusion on against off: V1 −23 % (1.04 M to 0.80 M with the city drawn), V4 −14 % (1.79 M to 1.53 M); other views 0 to −0.5 %. SetPass V1 −45 %, V4 −8 %.
- **Visual impact:** none measured (section 4). **Risk:** a wrongly marked occluder would make objects vanish; verified on the nine views only, so the headset walk-through (DC-13) must also look for popping. **Undo:** `git checkout` the three scene files and delete the data asset, or switch `Camera.useOcclusionCulling` off (diagnostic B15).

### 5.2 Room-aware exterior-city culling: done
- **Finding:** the city's meshes cannot be rejected by frustum or occlusion culling (their bounds enclose the hospital), so about 394 k triangles were drawn in the theatre and behind every solid wall. The city can only be seen through a window or a door.
- **Change:** `ExteriorVisibility` (`Assets/Scripts/Views/ExteriorVisibility.cs`; on by default on Android only, off on desktop and in the Editor): before a camera renders it finds the camera's room; if no window glass of that room and no door of that room is inside a wider-than-headset view test (112° vertical, aspect 1.15, apertures expanded 0.5 m), the city renderers are switched off for that camera's frame. A camera outside every room (a doorway transition) always shows the city. It uses `Renderer.forceRenderingOff` only and restores the authored state. Test: `ExteriorVisibilityTests`.
- **Evidence:** V1 −49 % triangles (798,608 to 407,752), V9 −72 % (543,850 to 152,994); V2-V8 unchanged because a window or door is in view. Desktop CPU render time V1 3.7 to 2.2 ms (a desktop number).
- **Visual impact:** none: V8 and V9 pixel-identical, V1 identical to the original. **Risk:** a window entering the view from a very oblique angle must reappear without delay; the margin is generous (112° tested against about 97° for the headset) but this is the first thing to watch on the headset (DC-13: turn quickly towards the bay window and the corridor glass). **Undo:** `ExteriorVisibility.Enabled = false` (diagnostic B16) or remove the component.

### 5.3 Splitting the city into spatial chunks: tried, no benefit, reverted
Splitting the 7 big city renderers into 559 chunks (120 × 80 m cells; 24 m cells made 3,605) kept every triangle but **added nothing** over 5.1: the chunks next to the hospital are never occluded because the occlusion data does not cover the city, and it added a 45 MB mesh asset. The tool and the asset were removed; the original city was never changed. Recorded so nobody repeats it.

### 5.4 Not changed, with the reason

| Area | Finding | Decision |
|---|---|---|
| City visible through windows | About 394 k triangles in V2-V8 (V8: 83 % of the frame) | The only large saving left in window views. Options: distance LOD of the 91 k-triangle traffic and 65 k-triangle trees (street level, far below the windows) or the image-based city of plan p3-19. **Needs the owner's approval** (D7): both change what is drawn. Not applied. |
| Characters | Hiding all skinned meshes removes up to 462 k triangles per view (B9: 0 in V8, 462 k in V6); CPU skinning is the build default | GPU skinning is built as `diag-gpuskin`; decision after DC-13. Replacing character models is prohibited without approval. |
| Realtime lights | B6 (fills off) changes triangles by 0 and SetPass by 0 to 2 | Lowest value on desktop counts; the per-pixel cost can only be judged on the headset. Not applied. |
| Static batching flags | 349 of 414 bay objects and 316 of 608 corridor objects are not authored static; SetPass is 28-326 per view | Some objects move (drop, interact); needs a per-object review, so no blanket change (P3-PRF-12). |
| LOD groups | None in the project | Needs authored lower-detail meshes; nothing was decimated automatically on approved assets. |
| Foveation, intermediate texture, shafts and dust, resolution | Headset-only questions | Unchanged today (see `quest2_performance_results.md`). |
| Skinned mesh warning | The Editor logs `First two bone weights should have non-zero sum` once per run for a skinned mesh | Not yet traced to an asset; recorded as DT-282. |

## 6. What only the headset can tell

Everything above reduces work the renderer is asked to do; **whether the Quest 2 now holds 72 FPS is unknown**. Sannan runs DC-13 with the new diagnostic APKs: compare B0 against B15 (occlusion off) and B16 (exterior culling off) in views V1, V4 and V9, walk through every door looking for popping, and send the CSVs.
