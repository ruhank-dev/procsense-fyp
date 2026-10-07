# p0-blender-equipment-s05 — Unity import pipeline (EXP-BL-2)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
FBX import preset; URP material library matched by `M_*` names; an asset postprocessor that converts named empties into components — `GRAB_*` → XRGrabInteractable attach point, `SOCKET_*` → XRSocketInteractor, `ATTACH_*` → cable/tube endpoint, `SCREEN_*` → world-space canvas anchor, `PIVOT_*` → hinge/slider config, `LABEL_*` → TextMeshPro surface; prefab variants generated automatically. Experiment: URP shared materials vs baked textures on Quest.

## Why
Assets only become useful when they're interactive in Unity; doing that wiring automatically from named empties means ~60 assets need zero manual setup and re-imports never lose work.

## How to implement
1. `Editor/Import/EquipmentPostprocessor.cs` (AssetPostprocessor) + `EquipmentPrefabBuilder.cs`.
2. Material library `Art/Materials/M_*.mat`; missing name → import warning.
3. Colliders: convex mesh or primitives from `COL_*` helper objects.
4. EXP-BL-2 on the pilot monitor: memory + visual comparison; record decision.

## Verification
The three pilot assets become working prefabs (grab, socket, screen) without manual setup; re-import keeps component settings.

## Files changed
`unity/Assets/_Project/Editor/Import/*`, `unity/Assets/_Project/Art/Materials/*`.

## Dependencies
p0-blender-equipment-s04; p0-vr-platform-s03.

## Common issues
Socket steals items → interaction layer masks (B-S9 troubleshooting).

## Sources
B-S1 Steps 5–6; B-S9 Steps 1–2.
