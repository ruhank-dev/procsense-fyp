# p0-vr-platform-s01 — Unity project and Quest setup

Walking skeleton 1 · step 10 of 15

## What
Create the Unity 6 LTS (URP) project in `unity/`, configured for Meta Quest, with an XR rig (teleport + snap turn), the XR Device
Simulator for headset-free testing, and a first build running on the Quest.

## Why
Everything visual sits on this; building to the headset early avoids late packaging and performance surprises.

## Prerequisites (you)
Unity Hub + Unity 6 LTS with **Android Build Support** (OpenJDK + Android SDK/NDK); Meta Quest in **developer mode** (Meta Horizon app);
USB cable or Air Link; Git LFS installed.

## How to implement (Implementation Guide B-S6)
1. New project from the URP template into `unity/`; Editor settings: Force Text serialization, visible meta files.
2. Packages: OpenXR + Meta Quest feature group, Meta XR Core SDK (platform), XR Interaction Toolkit 3.x (+ Starter Assets, XR Device Simulator), TextMeshPro, Newtonsoft JSON.
3. Android: IL2CPP, ARM64, Vulkan, ASTC; minimum API level per current Meta requirement (check Project Validation).
4. XR Plug-in Management → OpenXR (Android) → Meta Quest feature group + Oculus Touch profile; fix all Project Validation warnings.
5. Rig: XRI Starter Assets XR Origin; teleport anchors only; snap turn 45°; fade on teleport.
6. `DevOnly` scene with the XR Device Simulator.
7. `Boot` scene + `Sys_Core` scene (empty for now; SimulationHost lands in s03).
8. Build & Run to Quest; record a 60 s idle OVR Metrics baseline (start of EXP-VR-1).
9. Taskfile: `task unity-build` (batchmode build script, `Editor/BuildScript.cs`).

## Verification
- In the Editor with the device simulator: you can look around and teleport.
- On the Quest: app launches, both controllers tracked, teleport works, stable frame rate in an empty scene (T-U1…T-U4).

## Files changed
`unity/` (ProjectSettings, Packages/manifest.json, Assets/_Project/Scenes, Assets/_Project/Editor/BuildScript.cs), `.gitattributes` (LFS patterns), `Taskfile.yml`, README.

## Dependencies
p0-foundation; installs above.

## Common issues
Black screen → Meta feature group / Vulkan; controllers not tracked → interaction profile missing; build fails stripping → `link.xml` (B-S6 troubleshooting).

## Sources
Implementation Guide B-S6 Steps 1–6, tests T-U1–T-U5; Master Plan §25 (VR environment).
