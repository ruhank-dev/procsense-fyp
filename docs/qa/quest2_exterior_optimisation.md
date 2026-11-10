# Quest 2 exterior city: the optimised variant (P3-19 prototype)

_8 November 2026 (UTC). Owner direction of the same day (image-based exterior investigation). Branch `quest-perf/d7-optimisations`. Companion of `quest2_desktop_optimisation.md` and `quest2_performance_results.md`. Desktop measurements only: **no Quest frame rate exists for any number here and none is implied.**_

## 1. What the inventory showed (it changes the plan)

`quest2_exterior_inventory.md` (generated) lists every renderer of the city. The brief assumed hundreds of detailed building meshes; the project's city is **already built from textured boxes** (Blender `build_city.py`: towers are boxes with facade textures, normal and metal maps). Of the city's **196,844 triangles**:

| Part | Triangles | Share |
|---|---|---|
| Traffic and street furniture (805 vehicles, 108 poles; one merged mesh) | 90,654 | 46 % |
| Street and podium trees (104 trees: bark and leaf cards) | 64,970 | 33 % |
| Ground palette (road markings, curbs) | 14,210 | 7 % |
| Mid-distance towers, concrete | 10,820 | 5.5 % |
| Everything else: facades, near towers, roofs, lawn, terrace, roads, sidewalks, skyline rings | about 16,000 | 8 % |

So replacing towers with textured boxes would save almost nothing: **79 % of the cost is street-level traffic and trees**, at least 40 m below the glass. Second finding: all of it is **submitted whole**. The city is a handful of merged meshes whose bounds enclose the hospital, so frustum culling cannot reject any part of it while any part is in view; through a window the player sees a narrow cone, yet every car and tree of the 1 km city goes to the GPU.

## 2. What was built (all reversible; the original city is untouched)

1. **Distance LOD of the far traffic and trees, same seed and placement** (`unity/Tools/Environment/build_city.py`, `CITY_VARIANT=quest`). No random number is drawn differently, so every car and tree stands exactly where it stood (verified: running the generator without the variable gives the original triangle counts again). Cars beyond 70 m from the hospital lose the lofted detail (125 of 805 keep it); beyond 150 m the separate wheel blocks become one dark under-body block (257 cars); trees beyond 90 m lose twigs and keep 3 of 5 leaf cards, enlarged by 18 %, beyond 180 m keep 2 of 5, enlarged by 38 %. Traffic 90,654 to 57,100 triangles; trees 64,970 to 49,808. The choice is fixed per object, so **nothing changes while the player moves: no LOD popping**.
2. **Spatial chunks.** Traffic, trees, ground markings and mid-tower concrete are exported as 401 chunks of 80 m cells (`city_*_q.fbx`), each with the same materials and settings as the object it replaces (`QuestExteriorBuild`).
3. **Portal culling** (`ExteriorVisibility`, extends P3-PRF-21). Per camera and frame, for the room the camera is in: the screen rectangle of every window glass and every door opening (closed leaf plane) is found, the rectangles are united, and a view cone through that rectangle is built **from both eye positions** (plus 5 cm for head movement and 4 % padding). A chunk is drawn only if its bounds meet the cone. Apertures entirely behind the viewer are skipped; apertures that cross the eye plane are clipped against it. Conservative by construction: wider than what the headset sees.
4. **Tier switch** `QuestExterior` (default **Original everywhere** until the owner approves, P3-EXT-07). It only toggles `Renderer.enabled`; both variants sit in the scene. Diagnostic configurations **B17** (optimised exterior) and **B18** (optimised meshes without portal culling) are in the headset build.

Not done, with the reason: facade impostor atlases, far-layer cards and a flat panorama (buildings are 8 % of the cost and the far city already is the photographic skybox, so they would add risk to depth and reflections for no saving); baking the ground into a texture (ground markings are 7 % and chunking already culls them; a baked texture would lose the lane markings at this distance); any change to materials, normal maps or reflections (unchanged, so glass reflections and sun are exactly as approved).

## 3. Results (desktop counts, Medium quality, 1832 × 1920 eye buffer, 4× MSAA, GTX 1660 SUPER)

"Original" is the production exterior with the room-aware city culling of the previous round; "optimised" is the same with B17. Views V10 to V12 look down at the street and exist only in the desktop benchmark.

| View | Triangles original | Triangles optimised | Change | City part, original (B0 minus B8) | City part, optimised (B17 minus B8) | City saving | SetPass original / optimised | Desktop CPU `Camera.Render` ms original / optimised |
|---|---|---|---|---|---|---|---|---|
| V1 bay near wall | 204,170 | 204,450 | +0 % | 0 | 278 | city not drawn in either | 15 / 15 | 3.8 / 3.5 |
| V2 bay overview | 632,849 | 487,759 | -23 % | 196,542 | 51,452 | -74 % | 88 / 91 | 8.5 / 6.4 |
| V3 bay window | 516,253 | 348,259 | -33 % | 196,530 | 28,536 | -85 % | 73 / 73 | 5.6 / 5.6 |
| V4 corridor long | 766,965 | 601,197 | -22 % | 196,840 | 31,072 | -84 % | 161 / 163 | 12.4 / 10.5 |
| V5 corridor people | 372,941 | 236,529 | -37 % | 196,816 | 60,404 | -69 % | 72 / 76 | 5.8 / 5.8 |
| V6 reception | 635,167 | 483,563 | -24 % | 196,844 | 45,240 | -77 % | 63 / 63 | 6.2 / 5.6 |
| V7 theatre | 304,247 | 123,593 | -59 % | 195,428 | 14,774 | -92 % | 32 / 30 | 4.7 / 4.3 |
| V8 corridor exterior | 235,907 | 89,897 | -62 % | 196,840 | 50,830 | -74 % | 40 / 42 | 4.1 / 5.0 |
| V9 theatre far end | 76,497 | 76,497 | +0 % | 0 | 0 | city not drawn in either | 20 / 20 | 5.2 / 4.0 |
| V10 glass wall down | 197,451 | 86,771 | -56 % | 184,554 | 73,874 | -60 % | 27 / 31 | 3.3 / 4.2 |
| V11 glass wall oblique | 205,907 | 86,829 | -58 % | 196,844 | 77,766 | -60 % | 28 / 32 | 3.9 / 4.6 |
| V12 bay window down | 219,335 | 89,703 | -59 % | 196,800 | 67,168 | -66 % | 26 / 28 | 3.7 / 5.1 |

- The city's own cost falls **60 to 92 %** wherever a window is in view; where no window can show the city nothing changed (it was already off).
- SetPass calls rise by 2 to 4 in window views (chunks are separate objects with the same few materials; the SRP batcher keeps them cheap).
- Desktop CPU time is within run-to-run noise (about ±1 ms) in the window views and a little higher in the three street-facing views (+0.9 to +1.4 ms): the portal test and 401 more renderers to cull. **Whether that matters on the Quest's CPU is only measurable on the headset (DC-13: B17 against B0, CPU time).**
- Memory: no texture changed (0 MB). The diagnostic build holds both variants (about 4 MB of extra mesh source); once a variant is approved the unused one can be dropped from the Quest build.
- **No GPU time** exists on this PC (the driver reports none) and the Editor's draw-call counter reads 0, so GPU milliseconds, overdraw and draw calls are not claimed.

## 4. Visual comparison

Same camera, same frame, original against optimised; pixels differing by more than 8/255 and by more than 32/255 (of 3.5 million). Original, optimised and difference mask for V3, V8, V10 and V11 are in `docs/qa/images/exterior/`.

| View | Mean abs. difference (of 255) | Pixels > 8 | Pixels > 32 | What differs |
|---|---|---|---|---|
| V1 bay near wall | 0.000 | 0 | 0 | nothing |
| V2 bay overview | 0.054 | 3,050 | 1,250 | patient breathing, drifting dust |
| V3 bay window | 0.116 | 9,711 | 3,838 | patient breathing, dust (window region checked by crop: tower facades identical) |
| V4 corridor long | 0.051 | 2,663 | 752 | corridor people, dust |
| V5 corridor people | 0.292 | 25,385 | 9,749 | corridor people, dust (this view does not look at a window) |
| V6 reception | 0.026 | 3,268 | 374 | people, dust |
| V7 theatre | 0.161 | 18,639 | 5,629 | theatre staff idle motion |
| V8 corridor exterior | 0.000 | 1 | 0 | nothing (identical) |
| V9 theatre far end | 0.000 | 0 | 0 | nothing |
| V10 glass wall looking down | 0.003 | 422 | 88 | a few leaf-edge pixels of far trees |
| V11 glass wall oblique | 0.013 | 1,882 | 534 | leaf-edge pixels of far trees, dust |
| V12 bay window looking down | 0.007 | 591 | 353 | dust |

In V10 and V11, which look straight down at the street, the plaza, trees and towers are indistinguishable by eye at full size. Building silhouettes, windows, facade textures, sun, haze and depth are untouched because those meshes and materials are not part of the variant. **Not covered by a desktop render:** the headset's colour, foveation and MSAA resolve, depth in both eyes, and head-motion parallax. The LOD only affects objects 70 m and more away, where the two eyes' views of a point differ by about 0.05 degrees (0.064 m / 70 m; the Quest 2 shows about 20 pixels per degree, so about 1 pixel) and a 20 cm head movement shifts it by under 3 pixels; the chunks are the original geometry, so their parallax is exactly the original's. A stereo pair (left and right eye, 64 mm) and a head-movement sequence are still to be captured (P3-EXT-05), and the headset check (P3-EXT-06) decides depth.

## 5. Risks and how to undo

| Risk | Where it would show | Mitigation / undo |
|---|---|---|
| A chunk missing in a window view (cone too tight) | A tree, car or ground patch vanishing while turning towards a window (DC-13) | The cone is wider than the headset and padded; B18 turns portal culling off, B16 turns all exterior culling off; `ExteriorVisibility.PortalCullEnabled = false` |
| Far cars or trees look simpler up close | Looking straight down from a window onto the nearest street | Radii are constants in `build_city.py`; regenerate with larger radii; or keep Original |
| Extra CPU for the portal test and 401 renderers | Quest CPU time (not measurable here) | Compare B17 against B0 CPU time (DC-13); B18 isolates the test |
| The variant is the default by mistake | A Quest build with the optimised exterior before approval | `QuestExterior.Current` defaults to Original; test `Quest_variant_is_isolated_and_reversible` |

Undo everything: `git checkout unity/Assets/Scenes/Corridor.unity` and delete `Assets/Models/Environment/City/*_q.fbx`; or never select B17 or `QuestExterior.Variant.Optimised`.

## 6. Correction of an earlier measurement error

The desktop benchmark camera stayed enabled while the benchmark also called `Camera.Render()`, so every frame was rendered and counted twice. **All absolute triangle counts in the 7 and 8 November reports before this one were twice the true values** (the city is 196,844 triangles, not 394,000). Ratios and percentages were unaffected (constant factor). The benchmark now disables the camera and `quest2_desktop_optimisation.md` carries the corrected numbers. The earlier statement that splitting the city into chunks gave no benefit (5.3 there) was true for occlusion culling alone; with portal culling the chunks are what makes the saving possible.

## 7. What only the headset can tell, and the decision

Sannan runs DC-13 with the new diagnostic APK: B0 (original), B17 (optimised) and B18, looking down from the corridor glass wall and the bay window and comparing with B0 by eye; turning quickly towards each window looking for a chunk appearing late; sending the CSVs with CPU time. **The owner decides whether the optimised exterior becomes the Quest default (P3-EXT-07) after seeing the side-by-side images and the headset result.** Until then the default stays Original.
