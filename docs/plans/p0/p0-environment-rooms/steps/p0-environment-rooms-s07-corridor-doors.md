# p0-environment-rooms-s07 — Walkable corridor and diagnostic doors

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
A roamable corridor connecting the patient bay, the OT and the B doors (CT Scan, Imaging, X-ray, Laboratory, MRCP) plus the specimen drop / tube station; B doors physically block the trainee (short message) while the patient/porter layer passes.

## Why
Approved decision: corridor roamable; B rooms not enterable.

## How to implement
1. Greybox corridor from a manifest first (M2); real corridor from the supervisor model later (s02).
2. Door anchors `DOOR_CT`, `DOOR_IMAGING`, `DOOR_XRAY`, `DOOR_LAB`, `DOOR_MRCP` with sign-plate slots.
3. Trainee-blocking collider on each B door + proximity message; separate physics layer for patient/porter.
4. NavMesh areas through doors for patient/porter only.

## Verification
In the headset: you can roam the corridor and can't pass any B door; the patient walks through.

## Files changed
`unity/Assets/_Project/Data/room_manifest_corridor.json`, `Room_Corridor` scene, door prefabs, NavMesh data.

## Dependencies
p0-environment-rooms-s01.

## Common issues
NavMesh through doors must exist for patient/porter but the trainee's teleport areas must stop at the door.

## Sources
p0-environment-rooms README (approved P7 alternative: walkable corridor, B rooms not enterable); p0-diagnostic-transitions README.
