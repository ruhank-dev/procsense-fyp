# p0-blender-equipment-s02 — Helper library `procsense_bpy`

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
Shared Blender Python helpers so each asset script is short and consistent: naming (`EQ_`/`M_`/empties), geometry (bevelled boxes, rounded panels, tubes along curves, cables, castor wheels, handles, screws, buttons, screen bezels), PBR material library (`M_PlasticWhite`, `M_PlasticGrey`, `M_SteelBrushed`, `M_Chrome`, `M_RubberBlack`, `M_Glass`, `M_ScreenOff`, `M_FabricBlue`…), label/decal baking, UV unwrap, LOD generation (decimate), interaction empties, FBX export with Unity axes (Y-up, 1 unit = 1 m, apply transforms).

## Why
Consistent style and fewer lines per asset; one place to fix a modelling technique.

## How to implement
1. Build only the helpers the 3 pilot assets need first; grow them as real assets need (Ponytail/YAGNI).
2. Helpers are plain functions in a few modules (`naming.py`, `geometry.py`, `materials.py`, `uv.py`, `lod.py`, `points.py`, `export.py`).
3. Self-check script `tools/blender/selftest.py` builds one of each helper headless and asserts names/scale.
4. Helpers documented with a one-line docstring and a usage example in `selftest.py`.

## Verification
`task blender-build -- selftest` passes headless.

## Files changed
`tools/blender/procsense_bpy/*.py`, `tools/blender/selftest.py`.

## Dependencies
p0-blender-equipment-s01.

## Common issues
Normals inverted after boolean ops → recalculate outside in the helper.

## Sources
B-S1 Steps 3–5.
