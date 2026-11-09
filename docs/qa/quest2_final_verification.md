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
