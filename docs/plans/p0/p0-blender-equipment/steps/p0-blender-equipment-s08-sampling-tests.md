# p0-blender-equipment-s08 — Sampling and bedside tests

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Our own procedural Blender Python assets for: Blood collection set + tourniquet (#16), Specimen tubes + labels (#17), Blood culture bottles (#18), Urine pot, dipstick, pregnancy test (#19), Sharps bin (#33), Specimen drop / pneumatic-tube station. Each is clinically recognisable, built from its reference sheet, VR-ready, and carries the interaction points its row in `equipment-interaction-spec.md` needs.

## Why
Every item the trainee touches must exist as a clean, detailed, interactive asset; no AI text-to-3D and no imported free models (user decisions).

## How to implement
1. Write/confirm the reference sheet for each item (p0-medical-research-data-s09); free-model references are visual only.
2. One script per asset: `tools/blender/assets/<category>/<asset_id>.py` using `procsense_bpy` helpers; runs from Blender's Scripting tab or headless (`blender -b -P …`).
3. Organisation: collection per asset, objects `EQ_<AssetId>_<Part>`, shared `M_*` materials, applied transforms, origin at floor contact / natural grip; separate parts for anything that moves or changes state.
4. `task blender-build -- <asset>` → `task blender-validate` → `task blender-preview`; the developer reviews the 4-view previews with the 1.7 m reference, then you review in Blender and in the headset.
5. **Blood collection set + tourniquet (#16)** — needle holder, butterfly with tubing, tourniquet strap (two states: loose / applied).
6. **Specimen tubes + labels (#17)** — tube family with colour-coded caps per tube type (types per content), `LABEL_Tube` surface, fill-level shape key, tube rack.
7. **Blood culture bottles (#18)** — aerobic/anaerobic bottle pair with cap colours, `LABEL_Bottle`, fill state.
8. **Urine pot, dipstick, pregnancy test (#19)** — pot with lid `PIVOT_Lid`; dipstick with pad colour material slots (driven by result); cassette test with result window material.
9. **Sharps bin (#33)** — yellow bin with lid flap `PIVOT_Lid`, opening `SOCKET_Sharps`, fill line.
10. **Specimen drop / pneumatic-tube station** — wall station with carrier, door `PIVOT_Door`, `SOCKET_Carrier`, send button `SCREEN_Status`.

## Verification
Per asset: validator green (triangles within class budget, naming, required empties, scale in metres, UVs, no non-manifold), previews approved by you, imports into Unity as a working prefab via the import pipeline, and the interaction-spec row works with the greybox stand-in replaced.

## Files changed
`tools/blender/assets/<category>/*.py`, `docs/research/equipment/*.md`, exported FBX in `unity/Assets/_Project/Art/Equipment/` (rebuildable). Starting budgets (B-S1/B-S8, tunable by EXP-BL-3): small props ≤ 5k tris, large machines ≤ 20k, textures ≤ 1K (≤ 2K for towers/monitors), ~512 px/m texel density; per view ≤ 150 batches, ≤ 750k tris; ≥ 72 fps.

## Dependencies
p0-blender-equipment-s04 (quality bar locked), s05 (import pipeline)

## Common issues
Soft/organic shapes (bags, tubing, cloth, gloves) → cloth/curve simulation baked inside the script; readable text (labels, screens, gauges where numbers matter) → live `LABEL_`/`SCREEN_` surfaces in Unity, not baked geometry; inverted normals → recalculate outside before export; FBX unit 1.0 with applied scale.

## Sources
Walkthrough Part E; equipment-list.md; equipment-interaction-spec.md; Implementation Guide B-S1 (budgets, naming), B-S9 (states).
