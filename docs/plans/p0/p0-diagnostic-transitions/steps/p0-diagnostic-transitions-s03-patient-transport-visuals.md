# p0-diagnostic-transitions-s03 — Patient goes to the door and back

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
The patient goes to the right door by the trainee's chosen mode (walk / wheelchair / trolley + escort), disappears inside, and returns when Core reports `PatientReturned`; the trainee may accompany to the door.

## Why
This is the visible half of the B-room loop: trainees must see the patient leave by the mode they chose, go through the right door, and come back — the choice of walk / wheelchair / trolley + escort is part of what is scored.

## How to implement
1. SceneDirector + PatientMover; wait overlay while inside (s06).
2. Mode visuals: walk (patient agent), wheelchair (patient seated, porter pushing), trolley (patient lying, porter pushing, escort following, portable monitor attached).
3. Door sequence: leaves open → patient enters → leaves close → patient hidden at the `DOOR_<Dest>_Inside` anchor → reverse on return.
4. Accompanying trainee is teleported to the corridor side of the door.

## Verification
Play Mode test for each mode to the CT door; replay of the exported session gives the same end state.

## Files changed
`unity/Assets/_Project/Scripts/Diagnostics/DoorSequence.cs`, `Scripts/Characters/Movement/PatientMover.cs` (modes), door 'inside' anchors in `room_manifest_corridor.json`, Play Mode tests.

## Dependencies
p0-vr-platform-s06; p0-characters-s06.

## Common issues
Patient visually 'arrives' before Core says so → Unity must wait for the Core state, never the other way round.

## Sources
Implementation Guide B-S7 Step 4 (SceneDirector), B-R16 Steps 2, 5; B-S3 worked example (porter push).
