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
