"""Presentation-only padded/curled cuff; keeps existing wrapped geometry and anchor names.

Run with the repository's Blender 4.5.14, factory startup. Outputs to unity/Logs/CuffBuild.
Shared Blender helpers and original fitted wrapped mesh are read-only dependencies.
"""
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
import procsense_bpy as geo

OUT = ROOT / "unity" / "Logs" / "CuffBuild"
OUT.mkdir(parents=True, exist_ok=True)
geo.reset("bp_cuff")
spec = importlib.util.spec_from_file_location("original_cuff", ROOT / "tools/blender/assets/monitoring/bp_cuff.py")
original = importlib.util.module_from_spec(spec)
spec.loader.exec_module(original)
original.build()
for name in ["EQ_bp_cuff_Body", "EQ_bp_cuff_Closure"]:
    bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)


def surface(theta):
    return (.17 * math.sin(theta), .004 + .17 * (1 - math.cos(theta)))


def shell(part, start, end, width, thickness, mat, lift=0, count=32):
    vertices = []
    for i in range(count + 1):
        x, z = surface(start + (end-start)*i/count)
        for dy, dz in [(-width/2, 0), (width/2, 0), (-width/2, thickness), (width/2, thickness)]:
            vertices.append((x, dy, z + dz + lift))
    faces = []
    for i in range(count):
        a, b = i*4, (i+1)*4
        faces.extend([(a,b,b+1,a+1), (a+2,a+3,b+3,b+2),
                      (a,a+2,b+2,b), (a+1,b+1,b+3,a+3)])
    faces.extend([(0,1,3,2), (count*4,count*4+2,count*4+3,count*4+1)])
    mesh = bpy.data.meshes.new(part)
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new(part, mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    return geo.finish(obj, part, mat)


body = shell("Body", -.9, .9, .16, .008, "FabricNavy")
closure = shell("Closure", .52, .87, .142, .002, "RubberBlack", .009, 10)
# Contrasting bound edges, rather than a single flat rectangle.
for side in [-1, 1]:
    points = []
    for i in range(25):
        x, z = surface(-.9 + 1.8*i/24)
        points.append((x, side*.077, z+.009))
    geo.tube("Bound_edge"+str(side), points, .0015, "AccentBlue")
# Small metal retaining loop and hose connector.
x, z = surface(-.78)
loop = [(x-.012,-.062,z+.014),(x+.012,-.062,z+.014),
        (x+.012,.062,z+.014),(x-.012,.062,z+.014)]
geo.tube("Retaining_loop", loop+[loop[0]], .0022, "Chrome")
geo.rod("Hose_grommet", (.045,-.076,.024), (.045,-.089,.024), .007, "RubberBlack", 16)
geo.rod("Hose_connector", (.045,-.088,.024), (.045,-.106,.024), .004, "PlasticGrey", 16)
geo.point("ATTACH_HoseLoose", (.045,-.106,.024))
# A simple printed index mark; no clinical sizing or pressure claims.
geo.box("Index_bar", (.038,.002,.0005), (-.01,-.032,.013), "PlasticWhite", 0)
geo.box("Index_tick", (.002,.025,.0005), (-.01,-.021,.013), "PlasticWhite", 0)

# Preserve the two original loose mesh names so serialized scene references survive.
parts = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.name != "EQ_bp_cuff_Wrapped" and o != closure]
bpy.ops.object.select_all(action="DESELECT")
for obj in parts:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body
bpy.ops.object.join()
body.name = "EQ_bp_cuff_Body"
anchor = bpy.data.objects["COL_Body"]
anchor.location.z = .04
anchor["size_x"], anchor["size_y"], anchor["size_z"] = .28, .16, .08
triangles = 0
for obj in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
    obj.data.calc_loop_triangles()
    triangles += len(obj.data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    assert all(e.is_manifold for e in bm.edges), obj.name
    bm.free()
    assert obj.data.uv_layers, obj.name
assert triangles < 5000, triangles
for name in ["GRAB_Cuff", "SOCKET_Arm", "ATTACH_Hose", "COL_Body"]:
    assert name in bpy.data.objects
bpy.ops.wm.save_as_mainfile(filepath=str(OUT / "bp_cuff.blend"))
bpy.ops.export_scene.fbx(filepath=str(OUT / "bp_cuff.fbx"), object_types={"MESH","EMPTY"},
    use_mesh_modifiers=True, add_leaf_bones=False, axis_forward="-Z", axis_up="Y",
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
    bake_anim=False, path_mode="STRIP")
(OUT / "validation.json").write_text(json.dumps({"triangles":triangles,"manifold":True,
    "uvs":True,"anchors_preserved":True,"wrapped_mesh":"unchanged"},indent=2)+"\n")
metadata = json.loads((ROOT / "unity/Assets/Models/Equipment/bp_cuff.equipment.json").read_text())
for item in metadata["anchors"]:
    if item["name"] == "COL_Body":
        item["size"] = {"x":.28,"y":.08,"z":.16}
if not any(a["name"] == "ATTACH_HoseLoose" for a in metadata["anchors"]):
    metadata["anchors"].append({"name":"ATTACH_HoseLoose","size":{"x":0,"y":0,"z":0}})
(OUT / "bp_cuff.equipment.json").write_text(json.dumps(metadata,indent=2)+"\n")
print("PASS padded cuff", triangles, "triangles")
