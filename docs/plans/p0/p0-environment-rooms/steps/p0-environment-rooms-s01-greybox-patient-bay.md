# p0-environment-rooms-s01 — Greybox patient bay

Walking skeleton 1 · step 13 of 15 · blocked until p0-vr-platform-s01

## What
A simple placeholder patient bay (until the supervisor's model is integrated): floor, walls, bed/trolley, patient stand-in (capsule or
basic mannequin seated on the bed), wristband on the wrist, notes folder on the bed table, monitor on a stand, a nurse stand-in,
tablet holder, teleport anchor at the bedside. Every object has a **text label** and the named anchor it will keep when real assets replace it.

## Why
Walking skeleton first (approved P3): prove the loop before art. Anchors defined now are where the Blender assets will go later.

## How to implement
1. Claude writes an **editor script** `Editor/Greybox/BuildPatientBay.cs` that generates the greybox scene from a small data file
   (`room_manifest_bay.json`: anchor name → position/rotation → placeholder shape → label). Re-runnable.
2. Anchors: `ANCHOR_Bed`, `ANCHOR_PatientSeat`, `ANCHOR_Wristband` (child of patient wrist), `ANCHOR_NotesFolder`, `ANCHOR_Monitor`,
   `ANCHOR_Nurse`, `ANCHOR_TabletHolder`, `TELEPORT_Bedside`.
3. Scale: real-world metres (bed height ~0.6 m, etc.); a 1.7 m reference capsule to check scale in the headset.
4. Baked simple lighting.

## Verification
You open the scene in the headset: bed/patient/monitor are at believable sizes and reachable from the bedside teleport point; labels readable.
Send Claude a screenshot from the Editor for review.

## Files changed
`unity/Assets/_Project/Scenes/Skeleton1_PatientBay.unity`, `unity/Assets/_Project/Editor/Greybox/BuildPatientBay.cs`, `unity/Assets/_Project/Data/room_manifest_bay.json`.

## Dependencies
p0-vr-platform-s01.

## Common issues
Things feel too big/small in VR → adjust the manifest, re-run the script (never hand-move objects that the script owns).

## Sources
Implementation Guide B-S8 Step 5 (anchors); walkthrough Step 1 'what the trainee sees' (S1 day-surgery bay); p0 README build order (skeleton 1).
