# p0-blender-equipment-s11 — OT machinery 1

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Our own procedural Blender Python assets for: Operating table + positioning padding (#40), Ceiling surgical lights + control panel (#44), Anaesthesia machine + check log (#41), OT patient monitor + capnograph (#9/#42). Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

## Why
Every item the trainee touches must exist as a clean, detailed, interactive asset; no AI text-to-3D and no imported free models (user decisions).

## How to implement
1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Operating table + positioning padding (#40)** — column base, table top in sections (head, back, seat, legs) each `PIVOT_*`, side rails, arm boards, hand control `SCREEN_TableControl`; gel pads with `SOCKET_Pad_n`.
6. **Ceiling surgical lights + control panel (#44)** — ceiling mount, spring arms with joints `PIVOT_Arm_n`, two light heads with LED arrays (emissive + spotlight anchor `LIGHT_Head_n`), sterile handle `GRAB_Handle`; wall control panel `SCREEN_LightControl` (on/off, intensity).
7. **Anaesthesia machine + check log (#41)** — workstation body, drawers `PIVOT_Drawer_n`, ventilator screen `SCREEN_Vent`, flowmeters, vaporiser, breathing circuit + bag, APL valve, check-log clipboard `SCREEN_CheckLog`, `ATTACH_Circuit`.
8. **OT patient monitor + capnograph (#9/#42)** — larger arm/boom-mounted monitor `SCREEN_OTMonitor` with module slots; capnography module + sampling line `ATTACH_EtCO2`.

## Verification
Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

## Files changed
`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

## Dependencies
p0-blender-equipment-s04, s05; p0-environment-rooms-s06 (OT anchors)

## Common issues
Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.

## Sources
Walkthrough Part E; equipment-list.md; equipment-interaction-spec.md; Implementation Guide B-S1 (budgets, naming), B-S9 (states).
