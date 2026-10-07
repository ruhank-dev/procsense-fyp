# p0-blender-equipment-s03 — Validator and preview renders

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
`validate.py`: triangle count vs the asset's budget class, naming rules, required empties per the interaction spec, scale (bounding box vs reference sheet dimensions ±5 %), applied transforms, UVs present, no non-manifold edges, material count. `preview.py`: renders front/side/top/three-quarter views with a 1.7 m human reference and a scale bar into `build/previews/`.

## Why
Quality enforced automatically, and reviews (the developer's and yours) happen from images without opening Blender.

## How to implement
1. Budget classes in `tools/blender/budgets.json` (starting values from B-S1; EXP-BL-3 tunes them).
2. Required empties per asset read from `tools/blender/requirements.json` (generated from the interaction spec).
3. Preview: Eevee, neutral studio lighting, 1024 px, consistent camera framing.
4. Validator output is machine-readable (JSON) so CI and the import pipeline can use it.

## Verification
Validator fails a deliberately bad cube (wrong name, unapplied scale, too many tris); previews render for `hello_cube`.

## Files changed
`tools/blender/validate.py`, `preview.py`, `budgets.json`, `requirements.json`.

## Dependencies
p0-blender-equipment-s02.

## Common issues
Bounding-box scale checks fail for items with cables/tubing — measure the main body object only (`EQ_<Asset>_Body`).

## Sources
Implementation Guide B-S1 T-3D1, T-3D2 and troubleshooting; p0-blender-equipment README conventions.
