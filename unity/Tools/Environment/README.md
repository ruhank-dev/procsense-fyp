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
