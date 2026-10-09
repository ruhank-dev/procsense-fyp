# p0-characters-s08 — Nurse enters the room and replaces the wristband

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

Added 09 October 2026 at the project owner's request ([deferred-tasks.md](../../../../deferred-tasks.md) DT-006).

## What
A visible 3D nurse who, when the Core starts a `WRISTBAND_REPLACEMENT` task (p0-simulation-workflow-s18), **comes into the patient bay from outside**, walks to the bedside,
checks the notes, and **replaces the incorrect (or missing) wristband on the patient's wrist**, then leaves or stays as the scene needs.

1. **Entry**: the nurse starts outside the bay (corridor side), opens/enters through the bay doorway on the Core's `ARRIVED` event, and walks a NavMesh path to `ANCHOR_NurseBedside`.
2. **Bedside**: "checking records" pose (notes/tablet in hand) while the Core is in `CHECKING_RECORDS`.
3. **Replacement**: IK/animation to the patient's wrist socket `ANCHOR_Wristband`; the old band mesh is removed and a new band mesh appears with the corrected text from the Core observable.
4. **Resolution**: nurse steps back, gives a short nod/line (voice: p0-voice-audio-s06), then walks out via `ANCHOR_NurseExit` or returns to her normal place.
5. **Missing band**: the same sequence, with no old band to remove; for a reported allergy she also attaches the allergy band (colour set by the institution decision).
6. **Greybox first**: a capsule nurse with labels uses the same controller in the walking skeleton; real avatar and animations replace it (p0-characters-s04).

## Why
The owner asked for a visible nurse who enters and fixes the problem, so the trainee sees consequences of a mismatch and learns that the correct response is to stop and have it resolved
(Walkthrough Part 11 Step 1; Guide L1-04 animation: nurse frowns, "Let me check", leaves and returns with a band).

## How to implement
1. `Scripts/Characters/Staff/NurseWristbandController.cs`: a view only. It subscribes to `NurseTaskChanged` and plays the matching state; **timing is owned by the Core** (cosmetic movement, same rule as p0-characters-s06).
2. Anchors from p0-environment-rooms: `ANCHOR_BayDoor` (outside → inside), `ANCHOR_NurseBedside`, `ANCHOR_NurseExit`; add them to `room_manifest_bay.json` and the real-room model.
3. Reuse the staff avatar and walk/gesture clips from p0-characters-s04/s05; add clips `Nurse_CheckNotes`, `Nurse_ReplaceWristband` (Mixamo or own), with IK target on the wrist socket.
4. Wristband swap: the band prefab (p0-blender-equipment-s06) with a text field bound to the observable; the nurse's replacement band is the same prefab with the new text.
5. If the path is blocked (trainee standing in the doorway), the nurse waits and says an "excuse me" line; the Core timer is not extended by animation.
6. Play Mode test: drive the Core to each nurse state and assert the nurse reaches the right anchor and the wrist band text equals the observable.

## Verification
Device simulator: report a mismatch on the mismatch fixture → nurse enters, replaces, the new band reads correctly. Headset: same, plus the missing-band fixture.

## Files changed
`unity/Assets/_Project/Scripts/Characters/Staff/*`, animation clips, prefabs, anchors in the room manifest, Play Mode test.

## Dependencies
p0-simulation-workflow-s18, p0-characters-s04 (staff avatars), p0-characters-s05 (animation mapping), p0-characters-s06 (NavMesh movement), p0-environment-rooms (bay doorway and anchors),
p0-blender-equipment-s06 (wristband asset), p0-vr-platform-s06 (scene director).

## Common issues
- The bay needs a real doorway/corridor side; the greybox bay must include one or the nurse cannot "enter from outside".
- Do not let the animation decide when the band changes — only the Core event does.

## Sources
Guide L1-04 and L1-01 animation column; Walkthrough Part 11 Step 1; owner instruction 09 October 2026.
