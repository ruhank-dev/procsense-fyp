# Quest 2 diagnostics: Android emulator feasibility (9 Nov 2026)

Reproduce: `powershell -NoProfile -File tools\unity\android_emulator_probe.ps1 [-Install] [-Boot]`

## What was done
- New SDK root `C:\Users\RUHAN\AndroidEmuSdk` (5.3 GB on disk: emulator 1.1 GB, android-34 google_apis x86_64 image 4.2 GB, plus copies of Unity's cmdline-tools 159 MB and platform-tools 15 MB). Unity's own SDK was not modified.
- Installed with Unity's bundled JDK and sdkmanager: `emulator` and `system-images;android-34;google_apis;x86_64`. The standard Android SDK licences were accepted non-interactively (`sdkmanager --licenses` fed "y").
- AVD `probe34` (pixel_5) created in the default `%USERPROFILE%\.android\avd`.
- `emulator -accel-check`: "WHPX(10.0.26200) is installed and usable" (Windows Hypervisor Platform, no elevation needed, VBS/hypervisor already running). Emulator 37.2.13.
- Headless boot (`-no-window -no-audio -no-snapshot -gpu swiftshader_indirect -memory 2048 -cores 2`, hidden): boot completed in about 51 s, `adb -s emulator-5554` sees it (Android 14, x86_64 with arm64-v8a translation listed in the ABI list). Process qemu-system-x86_64: about 3.2 GB working set, about 2 cores busy shortly after boot. Shut down with `adb emu kill`; no qemu process left.
- All adb calls were addressed to `emulator-5554`; the phone was not touched.

## Can it add evidence for the Quest 2 build?
Quest 2 APK: ARM64, OpenXR, Vulkan/GLES on Adreno 650, 6 GB shared RAM. The emulator provides none of that: no XR runtime (OpenXR loader/Meta runtime absent, the app would fail XR init), GPU is the host (swiftshader CPU or host GL via ANGLE), CPU is x86 under WHPX. arm64 translation exists in the image but is slow, and it still lacks the XR runtime.

Could show, with a separate non-XR x86_64 (or arm64 with translation) diagnostic build:
- APK installs and starts, no missing native libs, no crash at scene load, shader compile/variant errors, Vulkan or GLES validation errors, scene/asset load time ordering, managed heap and texture memory totals (via `adb shell dumpsys meminfo`; values are x86 and host-driver dependent, so trend only), logcat warnings.

Could NOT show:
- FPS, frame time, GPU time, thermal or fill-rate behaviour (not representative of Adreno 650, tiled rendering, foveation, stereo/multiview cost, Quest compositor).
- Anything XR-specific: stereo single-pass instanced, ASW, tracking, OpenXR layer behaviour, Quest memory limits.
- ARM64-only code paths.

## Decision
Technically feasible (WHPX works, headless boot in under a minute), but low value for the performance question: the metrics that matter (frame time, GPU time, memory under XR) cannot be measured. Worth using only as a cheap smoke test of a non-XR build (loads, no shader errors, memory trend) if one exists. It is not evidence for Quest 2 FPS; keep using on-device captures and the desktop done_med_ms/triangle benchmarks (`tools/unity/bench_report.py`) as proxies. Disk can be recovered by deleting `C:\Users\RUHAN\AndroidEmuSdk` and `~\.android\avd\probe34*`.

## Free desktop GPU tools
| Tool | State | Notes |
|---|---|---|
| RenderDoc | not installed; `winget install BaldurKarlsson.RenderDoc` (v1.47.0, small, well under 500 MB) | `renderdoccmd` capture of the Unity Player standalone build: `renderdoccmd capture -w -d <dir> <player.exe> -batchmode` (set the Player to D3D11/Vulkan, not the Editor). Replay and export are possible headless with the Python module (`renderdoc.pyd`) or `renderdoccmd replay`; the UI is not needed. Not run (GPU benchmark rule). |
| NVIDIA Nsight Graphics | not installed; not in winget; developer.nvidia.com download needs an NVIDIA login (not done) | GTX 1660 SUPER (Turing, driver 621.14) is supported. Has a CLI (`ngfx`) for frame capture but the analysis is GUI-centric. |
| Unity Frame Debugger / Render Graph Viewer | bundled with the Editor | Need the Editor GUI window; deferred until the lead can open one. |
