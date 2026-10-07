# p0-characters-s06 — Patient walking, wheelchair, trolley and porter escort

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
NavMesh agents for the patient and porter: patient walks (stable), sits in a wheelchair pushed by the porter, or lies on a trolley pushed by the porter (IK hands on handles) with an escort nurse following; enter/exit B doors.

## Why
The door-transition design (patient walks to CT and back) and transfer to theatre need believable, visible movement — and the transport *choice* (walk vs wheelchair vs trolley + escort) is scored, so the trainee must see the difference.

## How to implement
1. Greybox capsules first (M2) using the same controller; real characters swap in later.
2. `Scripts/Characters/Movement/PatientMover.cs` modes: Walk, Wheelchair, Trolley; driven by SceneDirector.
3. Porter push with IK hand targets on `GRAB_Push` empties (T-ST-A3).
4. Patient agent speed/gait from behaviour mode (in pain → slower); escort nurse keeps beside the trolley.
5. Collision/avoidance with other NPCs (B-S3 T-ST-A4).

## Verification
Each transport mode plays end to end to every B door and to the OT.

## Files changed
`Scripts/Characters/Movement/*`.

## Dependencies
p0-vr-platform-s06.

## Common issues
Keep movement cosmetic — Core owns timing.

## Sources
B-S3 worked example (porter push), T-ST-A3; B-R16.
