# Quest 2 benchmark guide (for Sannan)

_Written 7 November 2026 (UTC). This is the procedure behind headset item **DC-13** in `docs/testing/SANNAN_EXTERNAL_TESTING.md`. Write results in `docs/testing/results/DC-13.md` (you own that file). Nothing measured on a PC counts as a Quest result. Part A needs only the normal Dev APK and can be done now; part B needs the diagnostic APK (P3-PRF-01, not built yet); part C repeats A after the optimisations._

## 1. What you need

- Quest 2 with developer mode on, USB cable, a Windows PC with the repository (`main`), Unity 6000.5.10f1 with Android modules, adb (`winget install Google.PlatformTools`), Meta Quest Developer Hub (MQDH).
- **OVR Metrics Tool** installed on the headset (Meta Horizon Store, free; or from MQDH). Meta's guide: https://developer.oculus.com/documentation/unity/ts-ovrmetricstool
- A stopwatch or phone timer, and the headset fully charged and at room temperature.

## 2. Build and install

```
git switch main
git pull
powershell -ExecutionPolicy Bypass -File tools\setup\restore_environment.ps1
task unity-build -- dev
adb devices
adb install -r artifacts\unity\ProcSense-dev.apk
```

Record: commit hash (`git rev-parse HEAD`), APK size and its `.sha256`, headset firmware version (Settings → System → Software update), OVR Metrics Tool version. Before building, make sure the Unity Editor is on the Android platform only for the build (the build script switches it; switch back to Windows afterwards, see `docs/new_unity_environment.md`).

## 3. Set up OVR Metrics Tool

1. Open OVR Metrics Tool on the headset.
2. Turn on the **performance overlay (HUD)** and choose the "Advanced" or custom set with: FPS, App CPU time, App GPU time, CPU level, GPU level, GPU utilisation, stale frames, battery temperature (or device temperature), and CPU/GPU throttling flags.
3. Turn on **Record CSV** (report mode). Each run writes a CSV; you will copy it to the PC with MQDH's File Manager (or `adb pull` from the folder the tool shows).
4. Do not change the Quest's own refresh-rate setting during the test; the app runs at the default 72 Hz.

## 4. Thermal states

- **Cool:** headset off and on charge at room temperature for at least 15 minutes; start measuring within 2 minutes of launching.
- **Warm:** after 15 minutes of continuous play (walk the three rooms); then repeat the same measurements.

## 5. Part A: the unchanged environment (do now)

For each viewpoint, stand still for **30 seconds**, head looking at the described direction, then slowly turn 90° left and back for another 30 seconds. Note average FPS, worst FPS, App CPU ms, App GPU ms, CPU/GPU level, stale frames and whether throttling is shown. Take one headset screenshot (Meta button → Camera → Screenshot) per viewpoint.

| View | Where to stand | Look at |
|---|---|---|
| V1 Baseline-near-empty | In the bay, face the blank wall beside the door, 0.5 m from it | The wall (few objects in view) |
| V2 Bay overview | Foot of the patient's bed | Patient, monitor, window |
| V3 Bay window | Next to the window | Out to the city exterior |
| V4 Corridor sunlit | Corridor, in the middle of the sunlit floor patch near the west doors | Down the corridor toward the reception |
| V5 Corridor people | Near the waiting benches with the seated people | The people and benches |
| V6 Reception | In front of the reception desk | Desk and TV |
| V7 Theatre | OT, at the foot of the operating table | Table, lights, staff |
| V8 Corridor exterior | Corridor end facing the exterior glass | City view |

Also record: time from launch to the first room frame; any pink, black or missing materials; any flicker.

## 6. Part B: diagnostic configurations (after P3-PRF-01)

The diagnostic APK has a wrist panel with toggles and teleport buttons to V1-V8. For **V2, V4 and V5** (cool, then warm), record the same numbers for each line below, changing one thing at a time from the default:

| Config | Change |
|---|---|
| B0 | Default (same as part A) |
| B1 | Intermediate texture Auto (Android renderer) |
| B2 | B1 + foveation medium; B2h: B1 + foveation high |
| B3 | Extra realtime directional lights off |
| B4 | Corridor realtime point lights off |
| B5 | Other rooms' renderers off (only the current room drawn) |
| B6 | Characters hidden (cost of people and skinning) |
| B7 | GPU skinning on |
| B8 | Main-light soft shadows → hard |
| B9 | Shafts and dust on (Quest stereo-safe version) |
| B10 | Render scale 0.9 and 1.1 (to see if GPU-bound) |
| B11 | All recommended (B1 + B2 + B3 + B4 + B5 + B7) |

Also: in B9, check both eyes (close one eye at a time) that the shafts and dust appear in the same place.

## 7. Recording

Optionally record a short video of each part with the Quest's own recorder (Meta button → Camera → Record video) or MQDH casting; note that recording itself costs performance, so do not record during the numbered measurements.

## 8. What to send back

1. `docs/testing/results/DC-13.md` with: build facts (section 2), a table per part (view × cool/warm × numbers), screenshots (in `docs/testing/results/DC-13/`), notes on anything visual.
2. The OVR Metrics CSV files in `docs/testing/results/DC-13/csv/` (they are small).
3. Push on your branch and tell Ruhan; do not change any project setting in the same commit.

## 9. Pass rule (part C)

Stable 72 FPS (no sustained drops, stale frames near zero) in V1-V8, warm, with the approved look (shafts and dust visible in both eyes after P3-PRF-11). If a view cannot reach it, write which view and the numbers; the owner decides.
