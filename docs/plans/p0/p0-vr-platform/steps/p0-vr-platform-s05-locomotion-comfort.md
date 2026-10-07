# p0-vr-platform-s05 — Locomotion and comfort (EXP-VR-3)

Milestone: **M2 Walking skeleton 2 — records + diagnostics loop**

## What
Teleport anchors at bedside/OT stations, teleport areas in the corridor (free roaming), 45° snap turn, fade on teleport, seated mode; comfort experiment teleport vs smooth locomotion.

## Why
Medical trainees are often not gamers — comfort first; the corridor must be roamable (approved).

## How to implement
1. XRI Teleportation Area on corridor floors; Teleportation Anchors at stations (`TELEPORT_*` from room manifests).
2. Snap turn 45°, vignette optional, seated height offset.
3. EXP-VR-3: 3+ testers, short comfort questionnaire; record result; smooth locomotion only if testers ask.
4. Teleport arc colour indicates valid/invalid targets; fade 0.2–0.3 s.
5. Comfort questionnaire: 3 questions (nausea, disorientation, eye strain) after 10 minutes.

## Verification
EXP-VR-3 recorded; T-U4 all anchors reachable; seated mode works.

## Files changed
XR rig prefab `unity/Assets/_Project/Prefabs/XR/XROrigin.prefab` (teleport/turn settings), teleport areas in room scenes, `docs/plans/p0/p0-vr-platform/README.md` (EXP-VR-3 result).

## Dependencies
p0-vr-platform-s04.

## Common issues
Teleport targets on uneven floors or stairs fail → use flat teleport areas only. Snap-turn angle too large disorients — 45° is the default; let users pick 30°.

## Sources
Implementation Guide B-S6 Step 4, T-U4, T-U5; vr-platform README EXP-VR-3.
