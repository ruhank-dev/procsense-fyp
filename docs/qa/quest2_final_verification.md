# Quest 2 optimisation: final visual QA tooling (lane E, 9 November 2026 UTC)

Desktop only. This file describes how the final walkthrough, comparison and QA reports are produced; it contains no Quest measurement.

## One command

```
pwsh -NoProfile -File tools/unity/final_qa.ps1 [-Optimised B29] [-Original B20] [-Stages tests,walk,diff,sweep,qa,video] [-Work D:\ProcSenseWork]
```

Hidden processes only; waits for `D:\ProcSenseWork\unity.lock` (one Unity at a time) and holds it only for the Unity stages. Temp and Gradle go to D:. Exit code 1 on a failed test or any QA finding in the blocking classes below. Offline re-check of frames already on disk (no Unity): `-Stages qa,video`.

| Stage | What runs | Output (under `D:\ProcSenseWork`) |
|---|---|---|
| tests | `ExteriorVisibilityTests`, `CharacterPerfTests`, benchmark smoke (original and optimised, 3 frames per view) | `unity\Logs\rt\fq_*.xml`, `reports\qa\bench_smoke.csv` |
| walk | 120 s path, 3600 frames, 1920x1080, original and optimised | `frames\final_walk\<id>\f#####.jpg` and `<id>_walk.csv` |
| diff | same path 640x360, `BENCH_DIFF=1`: every frame compared with the same pose with all culls off and with the original exterior | `frames\final_diff\<id>_walk.csv` |
| sweep | `Sweep_poses`, 4752 poses, doors closed and open | `frames\final_sweep` |
| qa | `walk_qa.py` on both walkthroughs (diff columns merged into the 1080p CSV) | `reports\qa\<id>_qa.md`, `_flags.csv`, `_summary.json`, contact sheets |
| video | full optimised, full original, side-by-side comparison, comparison with amplified difference | `videos\*.mp4` |

## What `walk_qa.py` detects

| Class | Rule | Blocks (exit 1) |
|---|---|---|
| blank / white / black / flat | mean luminance extreme, over 35% near-white or near-black pixels, or near-zero contrast | yes |
| missing geometry (`geom_cull`, `geom_orig`) | per-frame `diff_cull_pct` over 0.05% (a cull hid something) or `diff_orig_pct` over 0.25% (differs from the original exterior); needs the `BENCH_DIFF=1` CSV | yes |
| still-camera pop (`static_pop`) | camera motion below 0.002 per frame and frame-to-frame difference over 1.5: texture or LOD pop, shader flicker, late loading | yes |
| flicker | luminance sign alternates 4 or more times in 8 frames at over 0.4 grey on a still camera | yes |
| jump / pop | luminance or picture change that camera motion does not explain (robust z over 10) | no, reviewed on the contact sheets (fast spins and door sweeps are expected hits) |
| void | share of the sequence's modal colour; disabled when that colour is an ordinary surface | no |

Thresholds are options (`--max-cull-pct`, `--max-orig-pct`, `--static-d`, `--static-motion`, `--fail-on`). The static and geometry checks switch themselves off when the CSV lacks motion or diff columns.

## Validation without Unity

* `B20` frames in `frames\final_walk` (3600 frames, no diff columns): 0 blank, 0 geometry (check correctly reported as not enabled), 515 still-camera frames and 0 static pop, 0 flicker, 62 jump and 14 pop events (the same earlier flagged places).
* Synthetic geometry test (100 B29 frames, one injected `diff_cull_pct` of 0.2): 1 frame flagged `geom_cull`, exit code 1.
* Found and fixed: with no CSV every frame looked still, giving false still-camera pops; the check is now off without motion data.

## Path coverage (`QuestWalkthrough.Path`, read only)

Covered: slow pans, walking with head bob in the bay, the door (toggled), bay to corridor, corridor, theatre, the glass wall, the return, 150 and 180 degree snaps, one 360 degree look-around in the theatre (t 63 s, 4.5 s), three head heights, 0.25 s fast turns.

Requests for lane C (not edited here):
1. Add a bay-window segment (looking at the window and down at the street, like benchmark views V3 and V12), where city culling is most exposed.
2. Add a second 360 degree turn in the corridor next to the glass wall (the corridor has the most apertures).
3. Add a door-open stretch while the camera moves through the doorway with a fast turn (checks the doorway reset and the head-motion pad together).
4. After the DT-288 building fix, re-run `diff` and `sweep` and check the theatre east end poses; `diff_orig_pct` should stay under 0.25% there.

## Status

Ready: `walk_qa.py` (extended), `final_qa.ps1` (parse-checked; Unity stages not yet run, by design, until the lead says integration is done), `walk_video.ps1` (unchanged, used by the video stage). Nothing was committed, pushed or run through CI.

## Fast and incremental pipeline (10 November 2026 UTC)

### Where the hour went (r4 chain, measured from the log timestamps)

| Stage | Time | Cause |
|---|---|---|
| walk, B20 + B29 at 1920x1080 (Unity) | 14.4 min | about 86 ms per frame, one config after the other |
| diff, B29 at 640x360 (Unity) | 6.1 min | about 73 ms per frame |
| qa, both configs | 14.6 min (one config alone 427 s) | one Python process decoded 5010 full-size JPEGs per config into float32 arrays, strictly serial |
| video, four clips | 7.3 min | four software x264 encodes (`-preset medium`) one after another |

The Unity capture is mostly fixed cost per frame: the quick run (640x360) took 4.6 min for B20 against 7.2 min per config at 1080p, so only about a third of the capture time depends on resolution and JPG writing. The rest is the two `yield return null` frame waits, the repeated `cam.Render()` calls and the readbacks inside `QuestWalkthrough.Walkthrough` (lane C's file, not edited here). Removing the waits and the extra renders is the next saving for the capture itself; that is an estimate and was not measured.

### What changed

| Piece | Change |
|---|---|
| `walk_qa.py` | JPEGs are decoded at half size (DCT scaling), one worker process per logical CPU minus 2; per-frame features are cached by directory, file size and modification time in `D:\ProcSenseWork\frame_cache\feat`, so unchanged frames are never decoded twice. `--exact` decodes at full size, `--no-cache` and `--workers` exist. All detectors are unchanged. One rule was sharpened without losing sensitivity: still-camera pop needs the frame difference to exceed both the limit and 1.5 times the median of its neighbours (steady dust motion on a still camera is not a pop; a real spike still is). |
| `walk_video.ps1` | `h264_nvenc` (GTX 1660 SUPER) when a one-frame test encode works, otherwise libx264; `-Encoder auto/nvenc/x264`; `-Segments` builds the clip ranges from the CSV segment names; full mode no longer needs a second frame directory. |
| `frame_cache.py` (new) | content-hash cache of rendered frame sets in `D:\ProcSenseWork\frame_cache`. Key = hash of scenes, shaders, scripts, materials, prefabs, URP and project settings, the walkthrough test (content for files up to 1 MB, size and time for textures and models) plus config, resolution, diff and JPG options. Commands: key, get, put, link (directory junction), merge (overlay a partial run onto the newest full set), prune. |
| `final_qa.ps1` | `-Mode quick` (640x360, difference columns from the same run, tag `_quick`) and `-Mode final` (default); cache lookup before every render; `-Segments doors,windows,turns` (or segment names) renders only those segments and merges them onto the cached full set; QA runs one process per config in parallel; the four videos encode in parallel and start in the background while the diff and sweep Unity stages run; `-Stages walk,qa` style lists work through `pwsh -File`. |

### Measured, before and after (existing r4 frames; no Unity except the quick runs)

| Item | Before | After |
|---|---|---|
| QA of one config, 5010 frames at 1080p | 427 s | 27 s (23 s decoding), 4 s when the frame features are cached, 89 s with `--exact` |
| QA flags | 73 flagged frames | identical frame and flag lists (max difference in D 0.04, in mean luminance 0.01); `--exact` identical too |
| Videos (four clips, 1080p, 167 s and 60 s) | 435 s | 82 s; NVENC against libx264 on the same frames: PSNR 45.3 dB, same duration |
| Walk, B20 + B29 | 14.4 min | 0 s (2 s) on a cache hit; a quick-mode full render of both configs with differences took 11 min |
| Unchanged re-run, quick mode, walk + qa | - | 5 s |
| Only the doors segment changed, quick mode, walk + qa + video | 678 s (full quick run) | 139 s (two Unity starts of about 1 min each, 270 frames per config) |
| QA of the r4 frames under the sharpened rule | 0 flags | 0 flags (r3 frames still report the black frame 4692) |

### Typical use

* Iterating on a change: `-Mode quick -Stages walk,qa,video` (add `-Segments doors` when only the doors changed).
* Final: `-Mode final` once, with the cache reusing anything unchanged.
* After a path-only change, skip the sweep: `-Stages walk,diff,qa,video`.

Known limits: the cache key covers the whole walkthrough test file, so any edit to it re-renders everything unless `-Segments` is given; the segment merge assumes the changed content stays inside the named segments (the merged set is the earlier render outside them); the frame sets are kept until `frame_cache.py prune`.
