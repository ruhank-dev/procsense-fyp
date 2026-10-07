# p0-medical-research-data-s09 — Equipment reference sheets for every Blender asset

Milestone: **M6 Environment & assets — rooms, equipment, characters, voice**

## What
One reference sheet per item in `equipment-list.md` (53 walkthrough items + interaction-relevant room objects): real dimensions with the source actually opened (manufacturer datasheet / catalogue / measured photo reference), components, materials and colours, states (on/off/fault/empty), interaction points (from the interaction spec), triangle-budget class.

## Why
Blender scripts must be built from real specifications, not invented shapes (INSTRUCTIONS §4g).

## How to implement
1. Template from p0-foundation-s03; files `docs/research/equipment/<asset_id>.md`.
2. Search manufacturer datasheets for typical devices (generic, no logos in the model); record URL + date opened; if only approximate → 'approx.' + method (e.g. scaled from a photo with a known reference).
3. Free-model references (visual only) may help with proportions/details — cite them as 'visual reference'.
4. Order: the 3 pilot assets first (vital-sign monitor on stand, IV pole + infusion pump, wristband), then OT machinery, then bedside items, then room objects.
5. Interaction points copied from `equipment-interaction-spec.md` rows.

## Verification
Every P0 asset has a sheet before its script starts; no dimension without a source note; you review the pilot three.

## Files changed
`docs/research/equipment/*.md`.

## Dependencies
p0-foundation-s03.

## Common issues
Typical dimensions vary by model — pick one representative device and say which.

## Sources
Walkthrough Part E; equipment-list.md; equipment-interaction-spec.md; free-model-candidates.md.
