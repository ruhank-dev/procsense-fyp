# Quest 2 benchmark guide (for Sannan)

_Written 7 November 2026 (UTC), updated the same day for the diagnostic APKs (P3-PRF-01, owner decision D7). This is the procedure behind headset item **DC-13** in `docs/testing/SANNAN_EXTERNAL_TESTING.md`. Write results in `docs/testing/results/DC-13.md` (you own that file). Nothing measured on a PC counts as a Quest result. Parts A and B use the diagnostic APKs; part C repeats A after the optimisations the owner approves._

## 1. What you need

- Quest 2 with developer mode on, USB cable, a Windows PC with the repository, Unity 6000.5.10f1 with Android modules, adb (`winget install Google.PlatformTools`), Meta Quest Developer Hub (MQDH).
- **OVR Metrics Tool** on the headset (Meta Horizon Store, free; or from MQDH). Meta's guide: https://developer.oculus.com/documentation/unity/ts-ovrmetricstool
- The headset fully charged and at room temperature.

## 2. Build and install

```
git fetch
git switch quest-perf/d7-optimisations
powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1
task unity-build -- diag
task unity-build -- diag-gpuskin
task unity-desktop
adb devices
adb install -r artifacts\unity\ProcSense-diag.apk
```

`diag` is a release build (like Demo) with production CPU skinning; `diag-gpuskin` is the same with GPU skinning (install it later for the skinning comparison; both share the package name, so installing one replaces the other). Record: commit hash (`git rev-parse HEAD`), APK size and its `.sha256`, headset firmware version (Settings → System → Software update), OVR Metrics Tool version. `task unity-desktop` puts the Editor back on Windows after the build (`docs/new_unity_environment.md`).

## 3. The diagnostic controls in the headset

A small yellow readout sits at the lower right of your view: configuration, view, room, FPS, 1% low FPS, CPU and GPU ms (from the device's frame timing), foveation, eye scale, skinning.

| Control | Does |
|---|---|
| Left thumbstick click | Next configuration (B0 → B19 → B0) |
| Right thumbstick click | Teleport to the next benchmark view (V1 → V9) |
| Hold left **X** for 1.5 s | Automatic sweep: every configuration at the current view, 3 s settle + 10 s measure each (about 3.5 minutes); stand still |
| Both thumbsticks clicked together | Hide or show the readout |

Every second a row goes to `perf/diag_<UTC time>.csv`; each sweep step adds a row to `perf/sweep_<UTC time>.csv`. Both are in the app's folder: `/sdcard/Android/data/<package>/files/perf` (find the package with `adb shell pm list packages | findstr -i procsense`).

## 4. OVR Metrics Tool and thermal states

1. Turn on the **performance overlay (HUD)** with: FPS, App CPU time, App GPU time, CPU level, GPU level, GPU utilisation, stale frames, temperature, CPU/GPU throttling. Turn on **Record CSV**. OVR Metrics stays the reference for levels and throttling; the diagnostic CSV gives per-configuration rows.
2. Do not change the Quest's refresh rate; the app runs at the default 72 Hz.
3. **Cool:** headset off and on charge for at least 15 minutes; measure within 2 minutes of launch. **Warm:** after 15 minutes of continuous play (walk the three rooms); then repeat.

## 5. Views (right thumbstick click teleports there)

| View | Where (world metres, the QA capture views) | Looks at |
|---|---|---|
| V1 bay near wall | (0, 1.6, -1.4) | the TV wall 0.55 m away (few objects) |
| V2 bay overview | (2.2, 1.65, -1.8), the bay door | patient, bed, monitor, window |
| V3 bay window | (1.8, 1.5, -0.6) | the window and the city |
| V4 corridor long | (4.5, 1.7, 13.6), north end | down the corridor to the reception |
| V5 corridor people | (3.2, 1.55, 5.6) | the nurses and benches |
| V6 reception | (4.5, 1.6, -1) | desk and TV |
| V7 theatre | (7, 1.7, 5.2), the OT door | table, lights, staff |
| V8 corridor exterior | (4.5, 1.6, 9) | the north glass and the city |
| V9 theatre far end | (9.5, 1.7, 5.2) | the theatre looking away from its door; the city must not be drawn (about 72 % fewer triangles on desktop) |

## 6. Part A: baseline (old rendering and new default)

1. Configuration **B1** (the early-November rendering: intermediate texture Always, shafts and dust off). For each view V1-V9: 30 s still, then 30 s slowly turning 90° left and back. Note average FPS, 1% low, App CPU ms, App GPU ms, CPU/GPU level, stale frames, throttling; one headset screenshot per view (Meta button → Camera → Screenshot).
2. Configuration **B0** (this branch: Quest renderer with intermediate texture Auto, shafts and dust on): the same.
3. In B0, close one eye at a time in V2 and V4: the shafts and dust must be in the same place for both eyes. Note any pink, black or missing material, flicker, or effect visible in one eye only.

## 7. Part B: the configuration sweep

In **V2, V3, V4, V5 and V8**, cool and then warm: hold left X for 1.5 s and stand still until the readout says "Sweep done". Then install `ProcSense-diag-gpuskin.apk` and repeat the sweep in V4 and V5.

| Config | What changes (from B0) | Changes the look? |
|---|---|---|
| B0 | Production default of this branch. **Since 9 Nov 2026 on Android:** optimised exterior city, room portal culling (`RoomVisibility`) and city portal culling, character culler, no baked occlusion (see section 11) | No |
| B1 | Legacy renderer (intermediate texture Always) and shafts/dust off: the early-November rendering | Shafts off |
| B2 | Legacy renderer only | No |
| B3 | Shafts and dust off | Yes |
| B4 | Foveation medium (0.5) | Edges slightly softer |
| B5 | Foveation high (1.0) | Edges softer |
| B6 | Realtime fill points and the clinical key off | Very slightly |
| B7 | Only the current room drawn (city stays) | Other rooms vanish through doors |
| B8 | City exterior hidden (skybox stays) | Yes |
| B9 | Characters hidden and paused | Yes |
| B10 | Main-light realtime shadows off | Dynamic shadows gone |
| B11 | Eye-texture scale 0.9 | Softer |
| B12 | Eye-texture scale 1.1 | Sharper |
| B13 | MSAA 2x | More jagged edges |
| B14 | Candidate set: foveation 0.5 + fills off + current room only | Slightly |

Configurations that change the look are for measurement only; nothing is adopted without the owner.

## 8. What to send back

1. Pull the logs: `adb pull /sdcard/Android/data/<package>/files/perf docs/testing/results/DC-13/csv`. Also copy the OVR Metrics CSVs (MQDH File Manager) into the same folder.
2. `docs/testing/results/DC-13.md`: build facts (section 2), the part A table (view × B1/B0 × cool/warm), notes on the eyes check and anything visual, screenshots in `docs/testing/results/DC-13/`.
3. Optional: `uv run --project tools tools/unity/perf_sweep.py docs/testing/results/DC-13/csv/sweep_*.csv` prints the part B table; Ruhan writes it into `docs/qa/quest2_performance_results.md` with `--write`.
4. Push on your branch and tell Ruhan; do not change any project setting in the same commit.

## 9. Pass rule (part C)

Stable 72 FPS (no sustained drops, stale frames near zero) in V1-V9, warm, with the approved look (shafts and dust visible in both eyes). If a view cannot reach it, write which view and the numbers; the owner decides.

## 9. Added 8 November 2026 (UTC): desktop optimisations to confirm on the headset

The desktop round (`quest2_desktop_optimisation.md`) added **baked occlusion culling** (all three rooms) and **room-aware culling of the exterior city** (`ExteriorVisibility`, on by default on Quest). The diagnostic builds carry two switches to measure them: **B15** (occlusion culling off) and **B16** (city always drawn). Please:

1. In V1, V4 and V9 compare B0 against B15 and B16 (30 s each, cool). Note average FPS, 1 % low and OVR CPU/GPU levels. Hypothesis, not a result: B0 is faster in V1 and V9 and equal in V4/V8 where a window is in view.
2. **Look for popping.** Walk slowly through the bay door, the corridor and the theatre door in B0, turning quickly towards the bay window, the corridor glass walls and open doors. The city and any wall or object must never flicker, vanish or appear late. If it does: note where, then send a screenshot-free description of the position and head direction.
3. Send the CSVs as before.

## 10. Added 8 November 2026 (UTC): the optimised exterior city (B17 and B18)

`quest2_exterior_optimisation.md` describes a Quest variant of the exterior city (far traffic and trees simplified from the same seed, the city split into 401 chunks, only the chunks inside the view cone through a window or door drawn). It looked identical to the original in every desktop capture, but **only the headset can say whether it is faster and whether depth looks right.** The default stays the original (B0). The diagnostic builds add:

| Configuration | What it is |
|---|---|
| B17 | Optimised exterior city (reduced far traffic and trees, chunks, portal culling) |
| B18 | Optimised exterior without the portal culling (reduced meshes only) |

1. In V3 and V8 compare B0 against B17 and B18 (30 s each, cool): average FPS, 1 % low, OVR CPU and GPU levels. The CPU level matters here: the portal test and the extra renderers cost CPU. Hypothesis, not a result: B17 is lighter on the GPU in V3 and V8.
2. **Look down.** Stand at the corridor glass wall (V8 position) and at the bay window, look down at the street, the plaza, the trees and the traffic in B0 and then B17. Report any difference you can see: simpler cars, thinner trees, anything missing, anything flickering, in either eye.
3. **Look for chunks appearing late.** In B17 turn quickly towards each window and each glass wall from the side and from a few metres back. A tree, a car, a patch of ground or a building must never pop in or out.
4. Send the CSVs as before. Do not press anything that changes the default: the approved look stays B0 until the owner decides.

## 11. Added 9 November 2026 (UTC): optimisation round 2, presets B20 to B30

The Android default changed (`quest2_optimisation_round2.md`): **B0 now means the optimised exterior city, room portal culling, city portal culling and the character culler, with baked occlusion off** (it was the original exterior in section 6; the older B0 text above describes the 7 Nov build). Owner approval of the optimised exterior was given on 9 Nov 2026 for the development branch only; merging into `main` is not approved yet. Everything below was measured on the desktop only; **no Quest frame rate exists**, so these comparisons are what the headset has to settle (DC-13, DT-283).

| Config | What it is | Compare |
|---|---|---|
| B20 | Original exterior city (the recoverable comparison variant) | the old look and cost; the baseline for the others |
| B21 | Shadow distance 5 m | measurement only; changes the look |
| B22 | Hard realtime shadows | measurement only; changes the look |
| B23 | Shadow map 1024 | measurement only; changes the look |
| B24 | At most 2 additional lights per object | measurement only; changes the look |
| B25 | Additional lights off | measurement only |
| B26 | Skinned meshes not updated off-screen | CPU level; people must not pop or freeze |
| B27 | Optimised exterior, baked occlusion off | against B17: does removing occlusion cost anything |
| B28 | Optimised exterior + room portal culling, baked occlusion off | the production candidate; against B20 |
| B29 | B28 + character culler | against B28: CPU level and any popping people |
| B30 | B29 with baked occlusion forced on | against B29: what occlusion adds on the headset (the owner decides, DT-285) |

What to do: in V3, V8 and the corridor (V4) run B20, B0, B17, B28, B29 and B30 (30 s each, cool; average FPS, 1 % low, OVR CPU and GPU levels; CPU matters because three culling components run each frame). Turn the head fast at each door and window and check for popping city, rooms or people, in both eyes. Then repeat B0 with `ProcSense-diag-gpuskin.apk` (GPU skinning) against `ProcSense-diag.apk`. Foveation (B4, B5), eye scale (B11, B12), Symmetric Projection and Multiview Render Regions only with the owner's check of text sharpness and nothing is adopted without the owner.
