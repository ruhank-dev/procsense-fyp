# p0-characters-s04 — Staff avatars (people physically present)

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Characters for the roles physically present in A rooms: ward/ED nurse, triage nurse, anaesthetist, surgeon, scrub nurse, circulating/ODP, porter (recovery nurse in P1) — role-coloured uniforms for recognisability, Humanoid rigs, NPC controller (waypoints/NavMesh, look-at, gestures).

## Why
Only people physically present get avatars (approved principle); trainees must recognise roles instantly.

## How to implement
1. Base meshes from the chosen set; uniforms scripted/re-textured per role (B-S3 Step 1).
2. `Scripts/Characters/NPCController.cs`: positions from room manifests (`NPC_<Role>`), look-at the trainee when speaking, gesture triggers from staff intents.
3. LOD ×2, ≤ 25k tris each (B-S3 config).
4. Role recognisability check: show 5 testers each avatar without labels; ≥ 4/5 name the role correctly.
5. Name badges with role text (live `LABEL_` surface) as a backup cue.

## Verification
Each role placed and recognisable in the headset; T-ST-A2: 5 NPCs in the OR within budget.

## Files changed
`unity/Assets/_Project/Art/Characters/Staff/*`, outfit Blender scripts `tools/blender/assets/outfits/*.py`, `Scripts/Characters/NPCController.cs`, room manifests (NPC anchors).

## Dependencies
p0-characters-s01.

## Common issues
Two NPCs on the same path → avoidance priorities (T-ST-A4).

## Sources
B-S3 Steps 1–4, T-ST-A1–A4; walkthrough NPC roster (1.4).
