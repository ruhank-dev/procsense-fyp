# Room anchors and manifests (p0-environment-rooms-s04)

The rooms are Sannan's. This page and the manifests describe them as they are. Anything they propose is for Sannan to agree before the manifests are used to build a room.

## Files

- **Manifests.** `unity/Assets/Data/Rooms/<room>.json`, one per room: `bay`, `corridor`, `ot` and `recovery`. Each holds the room's anchors and, for every equipment or environment prefab in it, a slot and the asset in it.
- **Editor tool.** `unity/Assets/Editor/Rooms/RoomManifests.cs`. It has two menu items:
  - **ProcSense > Rooms > Export room manifests from the scenes.** Writes the manifests from the scenes and changes no scene.
  - **ProcSense > Rooms > Place the open room from its manifest.** Rebuilds the manifest's slots and assets under one `Manifest owned` root, marked by the `ManifestOwned` component. It replaces what it built before and touches nothing outside that root, so running it twice gives the same scene.
- **Checks.** `unity/Assets/Tests/EditMode/RoomManifestTests.cs` checks that:
  - every manifest is valid;
  - placing twice gives the same scene;
  - an asset swaps by changing its id;
  - a bad name, a missing slot, a shared slot and a missing prefab are each caught.

  A room that has changed since its manifest was exported gets a warning, not a failure, because the rooms change in the editor.

## Naming rule (T-EN2)

| Anchor | Pattern | Example in the rooms now |
|---|---|---|
| Location | `Anchor_<LOC>` | (none yet) |
| Equipment slot | `SLOT_<asset id>_<n>` | `SLOT_bp_cuff_1` (written by the export) |
| Character | `NPC_<Role>` | (none yet) |
| Teleport point | `TELEPORT_<Name>` | `TELEPORT_Bedside`, `TELEPORT_Exit`, `TELEPORT_Tablet`, `TELEPORT_BayDoor`, `TELEPORT_OTDoor`, `TELEPORT_Station` |
| Door | `DOOR_<Dest>` or `DOOR_<Dest>_Inside` / `_Outside` | `DOOR_ct_Inside`, `DOOR_ct_Outside`, and the same for lab, MRI, ultrasound and X-ray |

## What the export found (29 October 2026)

| Room | Anchors | Slots with an asset | Notes |
|---|---|---|---|
| Bay | 3 teleport | 20 (the monitor, cuff, probe, folder, bed, headwall, cabinet and the dressing) | |
| Corridor | 2 teleport, 10 door | 6 | |
| OT | 2 teleport | 8 (operating table, anaesthesia unit, light, trolleys) | |
| Recovery | none | none | The scene has no prefab instances or anchors yet |

## To agree with Sannan before the manifests build a room

1. **The panel anchors.** `ClinicalPanelsInstaller` looks for `EmrPanelAnchor`, `EventCardsAnchor`, `DiagnosticsAnchor` and `NurseSubtitleAnchor`. They predate the rule. They could become `Anchor_EmrPanel` and so on, but only if both sides move together.
2. **Character positions.** For example `NPC_Nurse` at the bay door and `NPC_Anaesthetist` in the OT. Character placement is Sannan's (characters s01 to s08).
3. **Location anchors.** `Anchor_<LOC>` for each Core location, so a transfer knows where to put the patient. The ids are those of `content/locations/locations.json`: `patient_bay`, `corridor`, `ultrasound`, `xray`, `ct`, `mri`, `theatre_reception` and `theatre`.
4. **Adopting the manifests.** When a room should be built from its manifest, its hand-placed prefabs move under `Manifest owned`. From then on, they are moved by editing the manifest, never by hand.

Until then the scenes are the source and the manifests record them. Re-export after a room changes.
