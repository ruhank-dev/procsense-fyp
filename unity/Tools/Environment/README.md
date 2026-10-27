# Bedside presentation props

`build_bedside.py` generates the patient-bay headwall, bedside cabinet and tablet
table using the repository's procedural Blender helpers. Use Blender 4.5.14:

```powershell
& '<Blender 4.5.14 executable>' -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_bedside.py
```

Outputs are ignored under `unity/Logs/BedsideBuild`: editable Blender sources,
FBX files and `validation.json`. The generator checks closed manifold geometry,
UV presence and a 5,000-triangle limit per prop. Current counts are 2,616,
1,476 and 1,340 triangles respectively. Static geometry is combined by material.
Dimensions are in metres; they are illustrative presentation dimensions.

`ProcSense.EditorTools.HospitalBedside.ImportAndApply()` imports through the
connected Editor, generates secondary lightmap UVs, assigns existing hospital
materials and creates nested model prefabs. Run only for the initial installation:
it refuses existing furnishings and unsaved scenes. Later adjustments must preserve
the existing asset GUIDs and authored scene objects. Rebake only PatientBay after
changing static geometry.

The headwall ports, cabinet drawers and adjustment hardware are decorative.
The existing clinical tablet, folder, patient and monitoring interactions retain
their bindings. Review corrections make the tablet display upright and move the
cabinet, folder and legacy notes anchors together to the bedside at x=-0.85,
z=1.40. The rear cap rail has a 1.05-metre gap behind the headboard to avoid
surface overlap. The old notes-stand collider retains its footprint at the new position;
three simple box colliders replace the old tablet post. The reading light is baked
and adds no realtime light. Quest performance still requires device profiling.

## Full room dressing

Use the repository's pinned Blender 4.5.14 from the repo root:

```powershell
& ./build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_room_dressing.py
```

The generator reads the shared geometry library without changing it. It creates
11 metre-based presentation models and checks closed geometry, UVs and triangle
budgets. Reproducible `.blend` sources, FBXs and `validation.json` are written
under ignored `unity/Logs/RoomDressing`. Dimensions are illustrative furniture
envelopes, not manufacturer or clinical specifications.

`HospitalRoomDressing.Import()` creates the imported models, shared materials and
nested prefabs through the connected Unity Editor. `Apply("PatientBay")`,
`Apply("Corridor")` and `Apply("OT")` place them in the existing saved rooms.
These are guarded initial-install operations: they reject Play, baking, dirty
scenes and existing dressing. They do not regenerate room shells. For later art
corrections, reimport an existing FBX through AssetDatabase with its `.meta`
preserved, or edit an existing prefab/scene through the Editor.

Bay furniture keeps the MPFB pose, mattress, bedding, sensor targets, tablet,
notes folder and existing solid bed footprint. Legacy visible bed geometry is
hidden. The lowered foot rails preserve access to the arms. The static privacy
curtain is parked on the left side; no cloth or curtain interaction is added.
Corridor seating and a linen trolley occupy the right wall. The OT gains a
segmented table, overhead surgical light, anaesthesia presentation unit, Mayo
stand, trolley, cupboard and hygiene fixtures. Equipment added by this batch is
static presentation furniture; screens remain inactive and no clinical action
or readiness state is implied.

Floor furniture has simple BoxColliders. Ceiling fixtures and the parked curtain
are visual dressing. Reuse shared hospital materials and lightmap UVs; bake each
room separately using its existing lighting configuration. Do not add overlapping
room shells for a bake. Review the bay from both sides and foot/head ends, the
corridor in both directions and the OT around its table before acceptance.

The earlier `build_bedside.py` creates the headwall, cabinet and tablet table;
`HospitalBedside` performs their initial import. Preserve their existing GUIDs
and interaction transforms when editing the surrounding furniture.

## Clinical bay detail pass

`build_clinical_details.py` imports the validated exporter from the now
import-safe `build_room_dressing.py`. Run it with the same pinned Blender command
above, changing the script filename. Eight models are generated under ignored
`unity/Logs/ClinicalDetails`: sink cabinet and recessed bowl, glove and towel
dispensers, sharps container, charting counter, visitor chair, clock and bumper.
All pass the exporter's closed-geometry/UV/5000-triangle checks.

Use `ProcSense.EditorTools.ClinicalBayDetails.Import()` through the connected
Editor for initial import, then `Apply()` for the existing bay and
`ApplyCorridor()` for the existing corridor. These reject Play, baking, dirty
scenes and duplicate dressing. Later corrections must preserve existing GUIDs.
The bay's sage lower panels, muted floor and woven fabric are local material
clones; the OT and its shared materials are preserved. A procedural 256px normal
map uses mipmaps and Android ASTC 6x6. Bay chair/sink compound box colliders leave
the seat and basin recess open; fixtures and the computer are static props.
The analogue clock is decorative and does not represent Core simulation time.
The existing tablet, patient and monitoring targets retain their workflow.

After placement, rebake only Bay and Corridor, inspect front/reverse views and
verify arrivals, teleport pads, dropped items and sensor access. Device profiling
requires an attached Quest; mesh counts and Editor tests do not establish FPS.

## Supported sensor table

`build_sensor_table.py` uses the same pinned Blender/exporter to generate the
compact rounded worktop, stainless frame, castors and lower shelf. Sources,
FBX and manifold/UV/budget validation are under ignored `Logs/SensorTableBuild`.
Initial installation uses `ProcSense.EditorTools.SensorEquipmentTable.Install()`
through the connected Editor; it rejects dirty scenes, Play, baking and an
existing table prefab. It retains the old Sensor tray object with renderer and
collider disabled, and creates the reusable bedside prefab with simple compound
colliders. No runtime interaction component is added to the fixed table.

The worktop remains exactly 0.8425m high beneath the unchanged probe/cuff return
anchors. Its full visible surface and low rims have colliders; the open space
between the legs remains open. Reimport later art revisions with the existing
FBX GUID, rebake only PatientBay and review both sensor attach/remove/return
flows plus dropped-item contact. Do not rerun the initial sensor/scene builder.

## Realistic hospital bed (frame, blue mattress cover, pillow)

Pinned Blender 4.5.14 and Python only; generated sources and maps are ignored under `unity/Logs/HospitalBed`.

| Script | What it makes |
| --- | --- |
| `build_hospital_bed.py` (+ `bed_frame_parts.py`) | Frame FBX in one mesh: moulded boards and rails with open grips, blue inlays, lime tabs, keypad and handset, an open diagonal lift. UVs point at a swatch atlas (`frame_albedo.png`, `frame_metal_smooth.png`), so plastic, painted metal, stainless, rubber and accents keep their own smoothness and metallic value in one draw call. |
| `build_bed_cover.py` (+ `bed_cover_fields.py`) | Mattress cover mesh (quad-grid top stitched to the wrapped edge) displaced by an irregular fold field, plus baked albedo (smoothness in alpha), normal and occlusion maps and a tileable weave normal. |
| `build_bed_pillow.py`, `bake_bed_pillow.py` | Pillow by offline cloth simulation (pressure-inflated case with a seam flange, pressed by the real head, neck and hair proxy exported from the patient), then its mesh and maps. Nothing simulates at runtime. |
| `render_bed_preview.py` | Lit and wireframe Blender previews of what ships. |
| `HospitalBedUpgrade` (Editor) | Applies the result in place with GUIDs kept: frame FBX, the existing mattress mesh asset (now the cover), the pillow OBJ, textures (ASTC, mipmaps) and three materials. |
| `HospitalBedReview` (Editor) | Batch camera set (including patient-hidden and low views), patient-to-bed clearance, the renderers near the bed head, and the head proxy export. |

Measured constraints: the patient rests 2.6 mm above the flat cover top (y = 0.700), so folds never rise inside the body footprint
(`data/patient_bed_footprint.csv`) and the pillow top stays under the measured head underside. The mattress keeps its original
0.95 x 2.00 m footprint. The head board stands against the oak wall panel face (z = 1.926) and the wall stops end at the upper wall
plane (z = 1.950). The bed is dynamic and probe-lit, so PatientBay is not rebaked; room lighting is a separate review.
Run Unity batch jobs with a waiting process and one instance at a time. Editor counts and screenshots do not establish Quest frame time.

### Working order for each new asset (one object at a time)
References and constraints, plan with numbers, Blender model, geometry inspection (wireframe, manifold and winding checks,
the validator), materials and textures, Editor import with GUIDs kept, real Unity screenshots from the same cameras as the
"before" set, objective checks (clearance, neighbouring renderers, tests), optional external design review of a
reference-versus-ours image, revision, then the owner's approval before the next object.
