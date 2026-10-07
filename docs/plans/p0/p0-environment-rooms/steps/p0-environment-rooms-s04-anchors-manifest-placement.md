# p0-environment-rooms-s04 — Anchors, room manifests and placement script

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Room manifests (data) listing every anchor — equipment slots, patient positions (bed, chair, trolley), NPC positions, teleport points, door anchors — and which asset goes where; an editor script places/updates assets from manifests so greybox and Blender assets swap by changing an ID.

## Why
Data-driven placement makes the rooms reproducible, reviewable in a diff, maintainable by the developer without the Editor, and lets greybox objects be swapped for Blender assets by changing one ID.

## How to implement
1. Reuse the `room_manifest_bay.json` format from s01; write `room_manifest_ot.json`, `room_manifest_corridor.json`.
2. `Editor/Rooms/PlaceFromManifest.cs`: idempotent (re-running gives identical scenes); objects it owns are tagged `ManifestOwned`.
3. Anchor naming: `Anchor_<LOC>`, `SLOT_<AssetId>_<n>`, `NPC_<Role>`, `TELEPORT_<Name>`, `DOOR_<Dest>` (T-EN2).
4. Manifest validator (in `validate.py` or a C# editor test): every anchor referenced by an equipment item exists; no two items share a slot.

## Verification
Re-running placement produces no diff; swapping an asset ID in a manifest updates the room.

## Files changed
`unity/Assets/_Project/Data/room_manifest_*.json`, editor script.

## Dependencies
p0-environment-rooms-s02.

## Common issues
Never hand-move a script-owned object — edit the manifest.

## Sources
B-S8 Step 5, T-EN2.
