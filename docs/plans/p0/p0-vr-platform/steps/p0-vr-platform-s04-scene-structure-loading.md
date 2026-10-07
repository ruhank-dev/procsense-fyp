# p0-vr-platform-s04 — Scene structure and room loading

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Scene layout: `Boot` (loads content, shows progress) → `Sys_Core` (persistent: SimulationHost, XR rig, UI root, audio) → room scenes loaded additively (`Room_PatientBay`, `Room_Corridor`, `Room_OT`). A `LocationAnchorRegistry` maps Core location IDs (LOC-BAY, LOC-CORRIDOR, LOC-OT, B-door IDs) to scene anchors.

## Why
Moving between bay, corridor and theatre must never reload or reset the simulation.

## How to implement
1. `Scripts/Scenes/SceneLoader.cs`: additive load/unload with 0.3 s fade; keep `Sys_Core` (DontDestroyOnLoad).
2. `LocationAnchorRegistry` component in each room scene registers its anchors (`Anchor_<LOC>`); SceneDirector queries it.
3. Content loading on Android via UnityWebRequest from StreamingAssets (File IO fails on Android — B-M11 troubleshooting).
4. Play Mode test: load Boot → teleport across all three rooms → Core state hash unchanged.

## Verification
Device simulator: bay → corridor → OT → bay with no hitch > 50 ms; Play Mode test green.

## Files changed
`unity/Assets/_Project/Scripts/Scenes/*`, scenes `Boot`, `Sys_Core`, `Room_*`.

## Dependencies
p0-vr-platform-s03.

## Common issues
Observable null on first frame → create the Session in `Awake` before views (B-S7).

## Sources
Implementation Guide B-S6 worked example, B-S7, B-S8 scenes; B-M11 Step 4.
