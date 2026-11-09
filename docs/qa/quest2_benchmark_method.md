# Quest 2 desktop benchmark method and visual defects (round 3, lane C)

Desktop numbers only. **They rank work; they are not Quest frame rates, and no Quest FPS was measured.** Written 9 November 2026 (UTC).

## 1. What exists now

| Tool | What it does |
|---|---|
| `StereoBenchmark.Benchmark_stereo` (`unity/Assets/Tests/PlayMode/StereoBenchmark.cs`) | Renders **both eyes** of a headset frame (two cameras 64 mm apart, each with its own 1832 x 1920 target and 4x MSAA; `BENCH_EYE_SCALE` scales both, 1.4 gives a GPU-heavy load). Workloads: `static` (five of the review views), `turn` (a 360 degree head turn in 60 frames, about 360 degrees per second, in bay, corridor and theatre) and `walk` (1.4 m/s along the corridor with a gentle head turn). |
| `bench_desktop.ps1 -Filter StereoBenchmark -Rounds N -Frames N -EyeScale x -Workloads ...` | Runs it hidden; writes `unity/Logs/bench/<tag>.csv` (one row per config x cell x round) and `<tag>.frames.csv` (every frame). The old single-eye `DesktopRenderBenchmark` is unchanged. |
| `tools/unity/bench_stereo_report.py` | Paired comparison against a base configuration; see section 2. |

Per cell and configuration the test records: wall time between frames; time until the GPU has finished both eyes (1-pixel readback of each eye); CPU time to submit both eyes; FrameTimingManager main-thread, render-thread and GPU times when the Editor reports them (main thread: yes; render thread and GPU: **not reported by this Editor**, so those columns are blank and the report says "unavailable"); setpass calls, triangles, vertices, shadow casters, memory; and **`pipe_med_ms`**, the GPU-bound time: eight stereo frames are queued back to back and waited for once, and time / 8 is taken, which removes the Editor's own frame loop from the number.

## 2. Noise control and statistics

* 30 warm-up frames per cell (shader variants, culling caches, animators) before measuring.
* **Interleaved A/B:** for every cell the configurations run back to back and the order rotates every round, so slow drift in machine load hits every configuration alike.
* Process priority above normal; `BENCHLOAD` lines log the busiest processes.
* **Contention flag (`other_busy`):** the test counts other Unity Editors, ffmpeg, Blender, java and il2cpp processes at the start and end of every cell; `bench_stereo_report.py` drops cells measured with any of them running (`--keep-busy` keeps them). The first version counted the test runner's own helper, so every row of the `stereo_clean` run is marked 1 (that run was clean because this lane held the Unity lock; it is analysed with `--keep-busy`). The corrected counter was verified in the later runs: all 240 cells of `fast_6` carry 0.
* Statistics: per cell and round a paired ratio configuration / base; the report gives the geometric mean ratio with a 95% bootstrap interval over (cell, round) pairs, the coefficient of variation over rounds, and an exact sign test on rounds. A difference is called **real only if the interval excludes 1 and the sign test p is below 0.05, which needs at least 6 rounds when all agree** (5 rounds give p = 0.0625, 3 rounds give 0.25).

## 3. What the runs showed

Clean run (`stereo_clean`: B20, B28, B29, B30; 8 cells = 5 static views and 3 rapid-turn cells; 5 rounds, interleaved; both eyes at 1832 x 1920 with 4x MSAA; GTX 1660 SUPER; nothing else rendering). **Noise: coefficient of variation over rounds 2 to 3%**; the earlier shared runs had 16 to 47%.

| Config | Triangles (mean of cells) | SetPass calls | CPU submit, both eyes (ms) | GPU-bound frame `pipe_med_ms` (ms) | Ratio to B20 (95% interval) | Rounds faster than B20 |
|---|---|---|---|---|---|---|
| B20 original exterior, occlusion on | 1,190k | 201 | 8.5 | 13.65 | 1.000 | |
| B28 optimised exterior + room cull, occlusion off | 946k (-20%) | 214 | 9.7 | 15.23 | 1.059 (1.033 to 1.087) | 0 of 5 |
| B29 B28 + character culler | 946k | 214 | 9.6 | 15.14 | 1.055 (1.031 to 1.078) | 0 of 5 |
| B30 B29 with baked occlusion on | 899k (-24%) | 204 | 9.2 | 15.01 | 1.055 (1.035 to 1.075) | 0 of 5 |

Completion time of both eyes including the Editor loop (`done_med_ms`): 20.5 ms (B20), 21.1 (B28), 21.1 (B29), 21.3 (B30): the same direction (+4 to +5%, intervals exclude 1). The rapid-turn cells show the largest gap: T-corridor 11.8 ms (B20) against 13.8 to 14.4 (candidates); T-theatre is equal (10.0 against 9.6 to 9.8). Frame-time spikes are not worse for any candidate (turn p99: 39.0 ms for B20 against 39.0 to 43.7; maximum 45 against 41 to 52 ms).

What this means, as far as the data goes:

1. **On this desktop the candidate configurations are about 5% slower than the original exterior, consistently (all 5 rounds, every candidate), although they draw 20 to 60% fewer triangles on the views where culling bites** (V7 theatre 610k to 250k, V10 street 395k to 174k). With 5 rounds the sign test cannot go below p = 0.0625; the bootstrap intervals exclude 1.
2. The counters point to the cause: SetPass calls go up (the optimised exterior is split into 401 chunk renderers), and the CPU submit of both eyes goes up by about 1 ms (the culling scripts run once per camera; this benchmark has two cameras per frame, where the headset's single multiview pass culls once). The desktop card at 4x MSAA 1832 x 1920 is limited by per-batch and CPU work, not by triangles, so removed triangles do not show.
3. This does **not** say what the Quest does: its tile-based GPU pays for triangles in binning and vertex work differently, and the culling cost is not doubled there. It says the saving has to be proven on the headset (DC-13 and DC-14), and that the culling CPU cost must not exceed what it saves: lane B's culling work is the thing to reduce, then repeat this run.
4. The earlier shared runs (`stereo_a`, `stereo_b`: another lane's Unity Editor was rendering on the same GPU) gave 20 to 115 ms and a coefficient of variation of 16 to 47%; their ratios, in either direction, are not evidence and are not used. They are the reason for the contention flag.

Not measured: Quest GPU and CPU time; overdraw (nothing on this machine reports it); the Render Graph pass count (the viewer is a window, no headless equivalent; the Frame Debugger call returned -1 headless); per-pass GPU time.

### 3.1 Repeat with lane B's fast paths, 6 rounds (stereo `fast_6`, 240 cells, no other process running; the contention counter was fixed first)

`ExteriorVisibility.CollectStats` was switched on in the first benchmark and in the earlier clean run; it **disables the pose cache of the fast path**, so those runs did not exercise it fully. It is off now. `FastPath`, `RoomVisibility.FastPath` and `CharacterPerfCuller.FastTick` are on (default); `B28-slow` is B28 with all three off.

| Config | GPU-bound frame (ms) | Ratio to B20 (95% interval) | Rounds faster | CPU submit, both eyes (ms) | Ratio to B20 |
|---|---|---|---|---|---|
| B20 | 14.17 | 1.000 | | 9.00 | 1.000 |
| B28 | 15.41 | 1.050 (1.018 to 1.079) | 0 of 6, p = 0.031 | 10.10 | 1.088 (1.036 to 1.141) |
| B29 | 15.60 | 1.048 (1.014 to 1.082) | 0 of 6, p = 0.031 | 10.05 | 1.090 |
| B30 | 15.17 | 1.061 (1.037 to 1.087) | 0 of 6, p = 0.031 | 9.37 | 1.096 |
| B28-slow | 15.55 | 1.061 (1.030 to 1.093) | 0 of 6, p = 0.031 | 9.98 | 1.095 |

* **B28 and B29 still lose to B20, now with p = 0.031** (all 6 rounds, bootstrap interval above 1): +5% GPU-bound time, +9% CPU submit.
* **The fast paths change nothing measurable here:** B28 against B28-slow is 15.41 against 15.55 ms (GPU-bound) and 10.10 against 9.98 ms (submit): the culling scripts are not where the extra cost is.
* **Where it is (decomposition run `decomp`, 5 rounds, B20 / B17 / B27 / B28 / B30):** B17 (optimised exterior + baked occlusion, no room or character cull) is already +5.9% GPU-bound (1.038 to 1.079) and +9.5% submit; B27 (occlusion off) +4.7% and +10.7%; B28 (room cull) +7.1% and +11.4%; B30 +6.7% and +13.2%. So the **optimised exterior itself costs about 1 ms of CPU submit and about 0.8 ms of GPU-bound time per stereo frame; room cull, character culler and occlusion add 0 to 2% on top.** It draws 35% fewer city triangles (V10 street 395k to 174k) and still costs more here.
* **SetPass is not the explanation.** The SetPass difference seen earlier (201 against 214) is baked occlusion, not the city: B20 has occlusion on, B28 has it off, and B30 (occlusion on) has 204. The visible-renderer dump (`fast_a.visible.csv`) shows what the optimised city does instead: the same 26 materials but more renderers (street view V10: city palette 2 renderers become 38, concrete 3 become 13, leaf 1 becomes 10, bark 1 becomes 10; 57 renderer-material pairs become 121). Giving all chunks of a material the same `rendererPriority` (so the opaque sort draws them together, experiment `-grp`) changed neither the SetPass count (200.6 and 213.5, identical with and without) nor the time (B28 15.73 against B28-grp 15.39 ms, interval spanning 1). So the extra cost is per renderer (Unity's own culling, SRP Batcher nodes and draw submission for 401 chunks), not material switches.
* **Cheap fix?** Not found in the test: sorting by material does not help. The remaining options need a rebuild of the Quest city meshes (fewer, larger chunks, for example 100 instead of 401) with a new walkthrough check, and should be measured against the Quest, not this desktop: the chunking exists so the room-aware culling can drop whole city regions, which matters for the headset's vertex and binning work that this card does not have.

### 3.2 Coarser chunks for the optimised city (decision: 320 m cells, 44 chunk renderers)

`build_city.py` now reads `CITY_CHUNK_M` (default stays 80). Variants were built with Blender, installed with `QuestExteriorBuild.Run` and measured against B20 with the two-eye benchmark (B28, 5 rounds each, interleaved):

| Chunk cell | Chunk renderers | B28 triangles | GPU-bound frame against B20 | CPU submit against B20 |
|---|---|---|---|---|
| 80 m (before) | 401 | 946k | +5.0% (6 rounds, p = 0.031) | +8.8% |
| 160 m | 133 | 967k | +2.3% (1.003 to 1.044) | +4.8% |
| 320 m | 44 | 971k | +2.1% (1.000 to 1.044) | +3.2% |
| 2000 m (one renderer per material) | 20 | 974k | +2.9% (1.007 to 1.051) | +3.8% |

* **Merging removes about half of the loss; the rest is not the chunk count** (20 chunks are no better than 44). The triangle cut hardly changes (946k to 971k; V10 street 174k to 192k) because it comes from the simplified traffic, tree and mid-tower meshes; the cone culling of whole 80 m cells saved about 3%.
* **Kept: 320 m.** Final 6-round run (`final6`, B20 / B28 / B29, fast paths on): GPU-bound frame B28 +3.8% (1.019 to 1.058, 0 of 6 rounds faster, p = 0.031), B29 +3.4%; CPU submit +4.4% and +5.0%; both-eyes completion +2.1%. **No variant beats B20 on this desktop**; the best compromise is 320 m (the optimised exterior now costs about half of what it did), with the same triangles (971k against 1,190k for B20, -18%). The remaining difference also contains the baked occlusion that B20 has on and B28 has off. Whether the triangle cut pays on the Quest's tile GPU is the open headset question (DC-13/DC-14).
* **Visibility check with the 320 m city:** complete sweep (B29, 4,752 poses): worst 0.027%, none above 0.05%. Full walkthrough diff (B29, 5,010 frames, 640 x 360, all segments including the three new ones): worst 0.008% against every cull off, 0.047% against the original exterior (glass wall, as before); fewest triangles in any frame 632,757.
* **Files:** `unity/Tools/Environment/build_city.py` (`CITY_CHUNK_M`), the four `city_*_q.fbx` and `Corridor.unity` (Quest variant rebuilt; the original city variant is untouched). Backups of the 80 m versions: `D:\ProcSenseWork\city_backup\quest_80` (also in git). To go back: `CITY_VARIANT=quest CITY_CHUNK_M=80`, then `QuestExteriorBuild.Run`.

 on this machine

| Tool | Finding |
|---|---|
| Meta XR Simulator, Meta Quest Link, OpenXR runtime | Not installed (no `Program Files\Meta`, no Oculus folder, no registered OpenXR runtime); the project has `com.unity.xr.openxr` but nothing to run it. Two explicit eye renders stand in for the stereo pass. This does **not** exercise Multiview, Symmetric Projection or Vulkan on Adreno. |
| FrameTimingManager GPU time and render-thread time | Not reported by the Editor on this card. |
| Frame Debugger event count | Tried through reflection (`FrameDebuggerUtility`) in the first round; returned -1 in batch mode. Render Graph Viewer is a window and cannot run hidden. |
| RenderDoc | Not installed (DT-288 in `docs/deferred-tasks.md`). |

## 5. Still to run

1. A GPU-heavy repeat (`-EyeScale 1.4 -Workloads static`) to see whether the candidates win once the card is the limit; it was queued and cancelled to free the Unity lock for the other lanes.
2. A repeat of the 6-round run after the per-renderer cost of the optimised exterior is reduced (section 3.1), for example with fewer, larger city chunks.
3. Done after the first version of this list: the complete 4,752-pose sweep and the full-path walkthrough diff with deterministic doors (section 7.1). Still open: rendering the new path segments at 1080p for the review video, and a headset check.

## 6. Deterministic walkthrough (round 3)

`SwingDoor` moves in `FixedUpdate`, so its phase depended on render speed and on whether an earlier configuration had already toggled it. The walkthrough and the pose sweep now set the door angle directly from the path time (`DoorAngleAt(t - (key.t - 3))`: closed, then the same 160 degrees-per-second sweep as the game, then open) through `SetDoorAngle` (angle, swing target and transform), so every configuration, run and render speed sees identical door phases, including in segment-filtered runs. Three path segments were added after the existing 120 s (the old keys keep their times): bay window (benchmark views V3 and V12, 124 to 134 s), a return through the bay door into the corridor (134 to 144 s), a full 360 degree turn next to the corridor glass wall (144 to 158 s) and a walk through the open bay door with fast turns (158 to 167 s). The path is now 167 s. (The first version of the return went in a straight line from the window to the corridor and left the building for one frame; the full-path diff run caught it at t = 135.13 s, 44% of pixels, camera outside the hospital. The path now goes through the door; that frame was a path error, not a culling error.) A second review found 47% black at t = 156.4 s (the straight walk from the glass wall to the bay door start crossed the bay wall) and a door leaf filling the view at t = 165.3 s; three keys (155.2, 156.8, 157.5 s) now walk down the middle of the corridor and through the door, and the last key looks into the bay. Segment-only render of glass360 and doorfast (690 frames): none more than 30% black, the 156.4 s and 165.3 s frames show corridor and bay. New environment variables: `BENCH_SPOTS="x,z;x,z"` (the sweep tests only these spots) and `BENCH_SAVE=1` (the sweep saves production and all-culls-off pictures of the closed-door poses at multiples of 90 degrees).

## 7. Visual defects

### 7.1 Theatre east end: glass facade inside the room (P3-EXT-11) - fixed at the source

* **Cause found:** not a city tower. `build_city.py` builds the hospital's own tower shell (`Facade5` glass, x -2.9 to 11.0, z -10 to 14.5) up to y = 3.9, above the corridor floor (y = 0), so its east face (x = 11.0) stood inside the theatre, whose east end reaches x = 13.45. With every cull off it showed as a blue glass wall from x > 11 (sweep poses at x = 11.5: 20 to 39% of pixels differed from the production render).
* **Fix:** the shell now stops at y = -0.45, the underside of the hospital's floor slabs (`SHELL_TOP` in `unity/Tools/Environment/build_city.py`). No face of it is inside any room; the skyline, the street, the podium and the view down from the glass wall are unchanged. `city_near.fbx` was regenerated with Blender 4.5.14. The build is deterministic: rebuilding with the old value (3.9) reproduced the committed FBX except for 355 header bytes, and the other four city FBXs were not touched.
* **Both exterior variants:** the optimised exterior replaces only traffic, trees, ground palette and mid-distance concrete; `city_near` is shared, so the original and the optimised exterior are both fixed.
* **Verification (Unity, production against every cull off):** 360 poses at x = 7.5, 9.5, 11.5, 12.5 and 13.1 in the theatre (36 headings and pitches, doors closed and open): worst difference **0.000%** (before: up to 38.7%). Pictures at x = 11.5 and 13.1 in four directions show no blue glass. The glass-wall and bay-window path segments rendered at 1920 x 1080 against the earlier frames: median per-frame difference 0.0%; the largest differences (frames 834 to 846, up to 13%) are drifting dust and the breathing sun shaft in a corridor shot, not the city.
* **Backup:** the previous `city_near.fbx` is in `D:\ProcSenseWork\city_backup\city_near_before.fbx` (and in git).
* **Complete sweep after the fix (`Sweep_poses`, B29, 4,752 poses, doors closed and open): no pose differs by more than 0.05% of pixels; worst 0.040% (corridor, x 3.2, z 9, looking back and down); every theatre pose at x 11 or more: 0.000%.** Before: 24 poses above 0.05%, all at the theatre east end. Report: `D:\ProcSenseWork\frames\final_sweep2\B29_sweep.csv`.
* **Full walkthrough with the fix and the deterministic doors (B29, 640 x 360, every frame compared with the same pose with every cull off and with the original exterior; 4,800 frames of the first run plus 1,290 frames of the new segments after the path correction):** worst difference from every-cull-off 0.008% (new segments), worst against the original exterior 0.047% (glass wall, as before); no blank or missing-geometry frame (fewest triangles in a frame: 387,717). The one frame above 0.05% in the first run (t = 135.13 s, 44%) was the path leaving the building (section 6), not culling.

### 7.2 Baked occlusion and the corridor ceiling lighting - decision: stays off

* Re-measured with the current production candidate (B30 = optimised exterior, room cull and character culler, occlusion on) against the same configuration with occlusion off: 1,080 poses (corridor x 3.2, 4.5, 5.8 at six z each, theatre three spots, bay three spots; 12 headings, 3 pitches): **worst 0.176% of pixels, mean 0.0013%, 9 poses above 0.05%**, all in the corridor. The earlier "up to 1.0%" came from a worse pose and an earlier configuration.
* `Light_occluder_probe` shows **no ceiling-fill light inside an occluder** (the corridor fills at y = 2.95 hang below the ceiling), so the cause is not a light buried in geometry. What remains is the light selection: Unity drops a point light from the visible list when its whole bounding sphere is hidden, and the per-object limit of four lights then picks other lights for some ceiling fixtures.
* **Tried:** enlarging the fills' bounding sphere (`Light.boundingSphereOverride`, 14 m; range and intensity untouched) with occlusion on. It removed the occlusion differences at the corridor poses (0.176% to 0.000%) but created new ones against the occlusion-off picture (12 poses above 0.05%, worst 0.674% at pitch -25, z 6 to 9): keeping lights in the list that frustum culling would drop changes the selection the other way, i.e. the approved picture itself depends on which fills the view keeps. Data: `docs/qa/quest2_occlusion_light_pad_experiment.csv`. The helper was removed because it does not help.
* Bake settings (smallest occluder and hole) were not changed: they decide which renderers occlude, and no light lies inside one, so they cannot change the light-bounds decision. A re-bake was not run.
* **Why it stays off:** occlusion adds only 5 to 9% fewer triangles on top of room and exterior culling (B30 against B29: 899k against 946k mean; V4 1,474k to 1,337k) with no desktop gain (15.0 ms against 15.1 ms, equal within noise); it keeps a 12 MB LFS asset; and it leaves a real, small lighting difference against the approved picture. Reversible: `QuestCullingProfile.OcclusionEnabled = true` and preset B30.
