# Weekly profiling route (p0-evaluation-performance-s03)

Every week, one person runs the same route on the Quest, the same way, so that this week's numbers can be set against last week's. The route itself, the build to use and the OVR Metrics set-up are in `docs/plans/p0/p0-vr-platform/perf/README.md`. This page covers the conditions, the order of work and what to do with the result.

## Conditions (the same every week)

- **Headset.** Start with the headset cool: off or idle for at least 15 minutes. Battery between 60 and 90 %. Charger unplugged.
- **Guardian.** The same stationary boundary every week, and the same room lighting.
- **Build.** The Demo build of the commit under test (`task unity-build -- demo`). Note the commit.
- **Recording.** OVR Metrics Tool records a CSV for the whole route (about 5 minutes).

## Steps

1. Run the route in the perf README, from boot to the debrief, without stopping.
2. Copy the CSV into `docs/plans/p0/p0-vr-platform/perf/`. Name it `YYYY-MM-DD-demo-<commit>.csv`.
3. Run the following command. It prints the row and the 1 % low fps, judges the gate and compares the row with last week's. With `--write` it adds the row to the table.
   ```
   uv run --project tools tools/unity/perf_capture.py docs/plans/p0/p0-vr-platform/perf/<file>.csv --by <name> --write
   ```
4. **If the gate fails, or a measure regresses by more than 5 %:**
   - Attach the Dev build with the Unity Profiler and repeat the worst part of the route.
   - Save the snapshot next to the CSV.
   - Open a fix step in the phase that owns the cause (`docs/plans/frontend/front_end_load_distribution.md` says who owns what), and link it in the row's notes.

## Gate (T-Q1, T-Q4)

- **Frame rate.** At least 72 fps on average.
- **Hitches.** No frame over 50 ms.
- **Memory.** At most 2.5 GB.

A capture without a frame-time column has its worst frame estimated from its slowest second, and the script says so.
