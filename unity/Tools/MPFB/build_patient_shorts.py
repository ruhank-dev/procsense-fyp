"""Derive opaque skinned shorts from the already-approved MPFB CC0 trousers."""
import json
from pathlib import Path

import bmesh
import bpy

root = Path(__file__).resolve().parents[3]
output = root / "unity/Logs/PatientCoverageReview"
output.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root / "build/mpfb/output/patient-source.blend"))
cloth = next(o for o in bpy.data.objects if o.type == "MESH" and "male_casualsuit04" in o.name)
rig = cloth.parent
cloth.shape_key_clear()
bm = bmesh.new()
bm.from_mesh(cloth.data)
remaining = set(bm.verts)
parts = []
while remaining:
    seed = remaining.pop()
    component, todo = {seed}, [seed]
    while todo:
        v = todo.pop()
        for edge in v.link_edges:
            other = edge.other_vert(v)
            if other in remaining:
                remaining.remove(other)
                component.add(other)
                todo.append(other)
    parts.append(component)
trousers = max((p for p in parts if max(v.co.z for v in p) < 1.2), key=len)
bmesh.ops.delete(bm, geom=[v for p in parts if p is not trousers for v in p], context="VERTS")
bmesh.ops.bisect_plane(bm, geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
                      plane_co=(0, 0, .70), plane_no=(0, 0, 1), clear_inner=True, dist=.00001)
bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=.00001)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(cloth.data)
bm.free()
cloth.data.update()
for v in cloth.data.vertices:
    # Bring only the outer waistband closer to the body. Retain the extra
    # crotch/thigh clearance so skin never breaks through the visible shorts.
    side = max(0, min(1, (abs(v.co.x) - .10) / .05))
    waist = max(0, min(1, (v.co.z - .85) / .10))
    side = side * side * (3 - 2 * side)
    waist = waist * waist * (3 - 2 * waist)
    v.co += v.normal * (.008 - .012 * side * waist)
for polygon in cloth.data.polygons:
    polygon.use_smooth = True
cloth.name = "PatientShorts"
cloth.data.materials.clear()
material = bpy.data.materials.new("PatientShorts_Navy")
material.diffuse_color = (.035, .075, .12, 1)
cloth.data.materials.append(material)
bpy.context.view_layer.objects.active = cloth
hem = cloth.modifiers.new("Opaque cloth and hem thickness", "SOLIDIFY")
hem.thickness = .0018
hem.offset = 0
bpy.ops.object.modifier_apply(modifier=hem.name)
bpy.ops.object.select_all(action="DESELECT")
rig.select_set(True)
cloth.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output / "patient_shorts.fbx"), use_selection=True,
                         object_types={"MESH", "ARMATURE"}, use_mesh_modifiers=False,
                         axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                         use_armature_deform_only=True, bake_anim=False, path_mode="STRIP")
cloth.data.calc_loop_triangles()
report = {"source": "existing MPFB 2.0.18 CC0 male_casualsuit04 trousers",
          "triangles": len(cloth.data.loop_triangles), "vertices": len(cloth.data.vertices),
          "skinGroups": len(cloth.vertex_groups), "hemHeightBindPoseMetres": .70,
          "opaque": True, "clothThicknessMetres": .0018}
(output / "shorts-validation.json").write_text(json.dumps(report, indent=2))
print("PATIENT_SHORTS_COMPLETE", json.dumps(report))
