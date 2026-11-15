# Quest 2 overhaul: headset test sheet (for Moiz and Sannan)

14 November 2026. One page. The desktop cannot tell us the Quest frame rate: **only headset numbers count**, and nothing here is verified until someone fills in the table at the end. Native 72 FPS is **not** claimed until it is measured.

## Baseline (Moiz, standalone Quest 2, production build before the overhaul)

34 FPS, 96% GPU utilisation, 24% CPU utilisation, 26.47 ms application GPU time, 72 Hz refresh, foveated rendering off. The GPU is the bottleneck. **Target: application GPU time below 13.89 ms (72 FPS), ideally 11 to 12 ms.**

## Which APK

`D:\ProcSenseWork\apk\overhaul\` (the file names and SHA-256 files are written when the build finishes):
- `ProcSense-demo.apk`: the production build with the aggressive profile as the default. This is the one that must reach 72 FPS.
- `ProcSense-diag.apk`: the same build plus the diagnostic readout and the preset list. It is the only build with the head-up display; the production build has none.

Install: `adb install -r <apk>`. Start the app, wait 5 minutes (shader warm-up and thermals), then measure.

## How to read the numbers

1. **OVR Metrics Tool** (Meta, on the headset; enable the on-screen overlay and the CSV log). Read: **App GPU Time (ms)**, **FPS**, **GPU Utilisation %**, **CPU Utilisation %**, **Stale/Dropped frames**, GPU level and CPU level. Record the median and the worst second.
2. The diagnostic APK's own readout (text in the lower right of your view): frame ms, CPU and GPU ms from the frame-timing manager, and the active preset id. It writes a CSV to the headset: `adb pull /sdcard/Android/data/com.procsense.fyp/files/perf/` (`diag_*.csv` rolling log, `sweep_*.csv` from the automatic sweep).
3. If a value looks wrong, capture with **RenderDoc for Meta Quest** and read the pass list (shadow passes should be absent now).

**Controls in the diagnostic APK:** left thumbstick click = next preset; right thumbstick click = next fixed test view; both clicks together = show or hide the readout; hold the left X button 1.5 s = automatic sweep of every preset (do not move during it).

**Same procedure every time:** same start pose, the same 60 s walk (bay, door, corridor, theatre, back), three runs after the 5 minute warm-up, headset charged and not hot, nobody else using the Wi-Fi.

## Presets to compare (the exact ids are listed in `docs/qa/quest_overhaul.md`, section A, when the rendering lane finishes)

| What to compare | How | What to write down |
|---|---|---|
| Old production against the aggressive profile | diag preset "original production" (B0) against the new default | GPU ms, FPS, and whether anything looks wrong |
| Foveation level | diag presets for foveation Low / Medium / High / off | GPU ms; peripheral blur you can notice |
| Render scale | diag presets 0.7 / 0.75 / 0.8 / 0.85 / 0.9 / 1.0 | GPU ms; can you read the patient record and monitor text? |
| MSAA | diag presets off / 2x | GPU ms; jagged edges on doors, bed rails, window frames |
| Exterior | diag presets original city / optimised city / new low-poly impostor city | GPU ms; skyline convincing from the glass? popping when you move sideways? |
| Dynamic resolution | on/off if the profile exposes it | smoothness when you turn quickly |
| Application SpaceWarp (ASW) | only if the rendering lane delivers it as an option | **not native 72 FPS**: write down separately; judge hands, characters, dust and light shafts for smearing |

## What to look for (answer yes/no and note where)

- **Text legibility:** patient record, monitor numbers, labels and signs readable at normal reading distance at the default scale?
- **Popping and white areas:** walls, corridor sections, the city or doors appearing late or flashing white when you turn fast or step through a door; hitching when entering another room.
- **Shadows missing or wrong:** the scene should show baked window-frame and furniture shadows; people and doors cast none (expected). Anything floating, glowing or black that should not be?
- **Light level:** is any room clearly darker or flatter than before? Do ceiling panels and lamps still look lit? Do the sun shafts and dust still read?
- **Characters:** correct pose when you look back at them after turning away; no frozen or reset animation; curtains and gown deform correctly.
- **Doors:** open and close, colliders stop you, no duplicate door visible.
- **Comfort:** any judder, double images or nausea at 72 Hz.

## Result table (copy, fill in, and send back)

| Config | App GPU ms (median / worst) | FPS | GPU % | CPU % | Dropped frames | Text readable | Popping / white | Notes |
|---|---|---|---|---|---|---|---|---|
| Production before overhaul (B0) | | | | | | | | |
| Aggressive default (production APK) | | | | | | | | |
| Best alternative found | | | | | | | | |
