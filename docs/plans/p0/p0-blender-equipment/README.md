# p0-blender-equipment

**Tier:** P0 (Step 15 / recovery items in p1-theatre-extension) · **Builds on:** p0-foundation, p0-medical-research-data (real references), p0-environment-rooms (placement) · **Feeds:** vr-interaction-ui, scenarios

## Goal
Every piece of equipment, machinery and prop the simulation needs, **generated procedurally by Blender Python scripts** —
clean, detailed, properly proportioned, clinically recognisable, VR-friendly, and runnable directly inside Blender to build the complete asset.

Full list: [equipment-list.md](equipment-list.md) (from walkthrough Part E + room dressing).
Free downloadable models found — **visual references only, never imported**: [free-model-candidates.md](free-model-candidates.md). **Every item is our own Blender Python script.**
Only category-A rooms (OT, patient bay) are dressed; diagnostic rooms are doors only (`../p0-environment-rooms/`).

## Quality bar (every asset)
- Real-world proportions from a **reference spec sheet** (dimensions, components, colours) — sourced and recorded like any other fact.
- Clean geometry: bevels on hard edges, sensible topology, separate components (drawers, wheels, lids, screens, cables, handles).
- Realistic PBR materials (plastic, brushed steel, rubber, glass, fabric); textures where detail matters (labels, decals, wear).
- VR-friendly triangle counts per asset class (budgets set by EXP-VR-1 / EXP-BL-4), plus LODs for large items.
- Organised: one collection per asset, meaningful object names, applied transforms, origins at a sensible pivot (floor contact or grip).
- **Interaction/attachment points** as named empties (see conventions) so Unity can wire grabbing, sockets, cables, screens.
- **Not** AI text-to-3D, ever.

## Pipeline

```
tools/blender/
├─ procsense_bpy/            shared helper library (the developer)
│  ├─ naming.py              EQ_/M_/GRAB_/SOCKET_ conventions
│  ├─ geometry.py            beveled boxes, rounded panels, tubes, cables, wheels, handles, screws
│  ├─ materials.py           PBR material library (plastics, metals, rubber, glass, fabric, screens)
│  ├─ textures.py            label/decal generation + baking
│  ├─ uv.py · lod.py         unwrap, LOD generation
│  ├─ points.py              interaction/attachment empties
│  └─ export.py              FBX export with Unity axis/scale settings
├─ assets/<category>/<asset_id>.py    one script per asset → builds one complete asset
├─ build_all.py              builds every asset (headless) → exports to unity/Assets/_Project/Art/Equipment/
├─ validate.py               checks tris, names, empties, scale, UVs, materials, non-manifold
└─ preview.py                renders 4 views + 1.7 m human scale reference for review
```

Run inside Blender (Scripting tab → open script → Run) **or** headless: `blender -b -P tools/blender/assets/monitoring/vital_signs_monitor.py`.

## Conventions
| Thing | Rule |
|---|---|
| Units / axes | Metres; Blender Z-up, exported for Unity Y-up |
| Object names | `EQ_<AssetId>_<Part>` e.g. `EQ_VitalMonitor_Screen` |
| Materials | `M_<Material>` shared from the library (e.g. `M_PlasticWhite`, `M_SteelBrushed`) |
| Interaction empties | `GRAB_*` (hand grip), `SOCKET_*` (where things attach, e.g. probe on finger), `ATTACH_*` (cable/tube ends), `SCREEN_*` (live UI surface), `PIVOT_*` (moving parts), `LABEL_*` (dynamic text, e.g. wristband name) |
| Readable text | Rendered live in Unity (TextMeshPro) at `LABEL_*` / `SCREEN_*` so case data (name, DOB, values) is real, not baked |
| Spec sheet | `docs/research/equipment/<asset_id>.md` — dimensions + reference source + components + states + interaction points |

## Decisions
| Decision | Chosen | Alternatives | Why | Revisit if |
|---|---|---|---|---|
| How assets are made | Procedural `bpy` scripts per asset + shared library | Hand-modelling; AI text-to-3D; paid pack | User decision; repeatable, editable, consistent style | — |
| Script style | Plain `bpy` mesh/modifier building | Script-generated Geometry Nodes trees | Easier to read/debug, exports cleanly | Many parametric variants needed (e.g. tubes/bags) → Geometry Nodes for those |
| Export format | FBX | glTF (.glb) via glTFast | Unity imports FBX natively | Material fidelity issues → glTF |
| Materials in Unity | Library of URP materials matching `M_*` names; bake only labels/decals/wear | Bake everything | Smaller textures, consistent look | Assets look flat → bake AO/detail maps |
| Build order | Pilot 3 assets → lock library + quality bar → rest by room, P0 first | All at once | Quality bar agreed before mass production | — |
| Free models | Visual references only; every asset scripted by us | Import good free models | User decision (07 Oct) | — |

## Experiments
| ID | Question | Method | Success metric |
|---|---|---|---|
| EXP-BL-1 | Can scripted assets meet the quality bar? | Pilot: vital-sign monitor, IV pole + infusion pump, patient wristband; previews + headset check | Team approves all 3 → quality bar locked |
| EXP-BL-2 | Unity URP materials vs baked textures | Same asset both ways on Quest | Visual vs memory trade-off chosen |
| EXP-BL-3 | Triangle budget per asset class | Profile a fully dressed theatre on Quest 2 | Budgets fixed in this README |
| EXP-BL-4 | Is `build_all` fast enough to run in CI? | Time full build headless | Decides commit-outputs vs rebuild (EXP-FND-1) |

## Exit
Every P0 item in `equipment-list.md` has: spec sheet, script, validated export, approved preview, placed in its room manifest.

## Risks
| Risk | Fallback |
|---|---|
| Organic/soft items (drapes, gowns, tubing, bags) look wrong procedurally | Cloth simulation baked in script; or team hand-edits that one asset (logged) |
| Small readable details (labels, gauges) | Dynamic text/UI in Unity instead of geometry |
| Dimensions unknown for an item | Reference from manufacturer datasheet/photos recorded in spec sheet; never guessed silently |

## Who does what
Developer: spec sheets (researched), library, every asset script, validator, previews, self-review of renders, export.
Team: run in Blender + headset, approve previews, give visual feedback; approve any free model before use.

## Sources
Walkthrough Part E; Implementation Guide B-S1, B-S9; Data Source Inventory D78–D79.
