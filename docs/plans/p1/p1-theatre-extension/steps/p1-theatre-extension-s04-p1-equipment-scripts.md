# p1-theatre-extension-s04 — P1 equipment: specimen container, recovery setup

Milestone: **M11 P1 content — theatre extension, extended cases, composer, Pulse**

## What
Blender Python assets: specimen container with screw lid (`PIVOT_Lid`) and `LABEL_Specimen`; recovery bay set-up reusing monitor, oxygen and suction assets.

## Why
Step 15 (Sign Out) requires the specimen to be labelled and read aloud; the recovery handover needs a believable recovery set-up — both reuse the P0 pipeline so quality stays consistent.

## How to implement
1. Same pipeline, quality bar and validator as P0 assets; reference sheet first.
2. Specimen container: clear pot with lid thread, label area, biohazard bag variant.
3. Recovery setup: reuse monitor/oxygen/suction prefabs placed via the recovery manifest.

## Verification
Validator + previews approved; specimen labelling interaction works in the headset.

## Files changed
`tools/blender/assets/ot/specimen_container.py`, `docs/research/equipment/specimen_container.md`, recovery manifest placements.

## Dependencies
p0-blender-equipment-s04, s05.

## Common issues
Specimen label text must be live (patient name from Core) so the 'read aloud including the patient's name' check works.

## Sources
Equipment list #52–#53 (walkthrough Part E); Implementation Guide B-S1 Step 7 (specimen pot), B-S12 Step 3.
