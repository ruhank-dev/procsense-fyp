# p0-environment-rooms-s05 — Patient bay: S1 ward / S2 ED dressing

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
One pre-op patient bay shell dressed from data as a day-surgery ward bay (S1: patient seated on bed in own clothes, folder, nurse) or an ED cubicle (S2: patient on trolley, wall monitor, triage clipboard, relative) — interaction-relevant objects only.

## Why
Approved P1: one bay, two dressings; matches walkthrough Step 1 'what the trainee sees'.

## How to implement
1. `room_manifest_bay_S1.json`, `room_manifest_bay_S2.json` sharing the shell; the case's scenario selects one.
2. Objects from equipment-list.md (bay rooms) + interaction-relevant room objects only.
3. S1 dressing: day-surgery bay, bed, bedside locker/table only if interacted with, folder on the bed table, nurse position; S2 dressing: ED cubicle, trolley, wall monitor, triage clipboard, relative position.
4. Shared anchors for everything common (monitor, outlets, sink) so interactions are written once.

## Verification
Loading VAR-S1-A shows the ward dressing and VAR-S2-PC the ED dressing; all spec interactions work in both.

## Files changed
`unity/Assets/_Project/Data/room_manifest_bay_base.json`, `room_manifest_bay_S1.json`, `room_manifest_bay_S2.json`, `Room_PatientBay` scene.

## Dependencies
p0-environment-rooms-s04.

## Common issues
Two dressings drifting apart → keep shared anchors in one base manifest that both variants extend.

## Sources
Walkthrough Step 1 (S1 day-surgery bay vs S2 ED cubicle — what the trainee sees); p0-environment-rooms README (approved one-bay decision).
