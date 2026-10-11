"""Runs inside Blender only. Build, validate, export, and neutral review renders."""
import importlib.util
import json
import math
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import procsense_bpy as geo

ROOT = HERE.parents[1]
OUT = ROOT / "build" / "blender"
PREVIEWS = ROOT / "build" / "previews"
REQUIREMENTS = json.loads((HERE / "requirements.json").read_text())


def validate(asset, requirements):
    errors = []
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    triangles = 0
    materials = set()
    for obj in meshes:
        if not obj.name.startswith(f"EQ_{asset}_"):
            errors.append(f"Invalid mesh name: {obj.name}")
        if any(abs(v-1) > .00001 for v in obj.scale) or any(abs(v) > .00001 for v in obj.rotation_euler):
            errors.append(f"Unapplied transform: {obj.name}")
        if not obj.data.uv_layers:
            errors.append(f"Missing UV: {obj.name}")
        obj.data.calc_loop_triangles()
        triangles += len(obj.data.loop_triangles)
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        if any(not e.is_manifold for e in bm.edges):
            errors.append(f"Non-manifold mesh: {obj.name}")
        bm.free()
        for mat in obj.data.materials:
            if mat is None or not mat.name.startswith("M_"):
                errors.append(f"Invalid material: {obj.name}")
            else:
                materials.add(mat.name)
    if triangles > requirements["triangles"]:
        errors.append(f"Triangle budget exceeded: {triangles}")
    if len(materials) > requirements["materials"]:
        errors.append(f"Material budget exceeded: {len(materials)}")
    for name in requirements["points"]:
        if name not in bpy.context.scene.objects:
            errors.append(f"Missing anchor: {name}")
    body = bpy.context.scene.objects.get(f"EQ_{asset}_Body")
    dimensions = list(body.dimensions) if body else []
    if not body or any(abs(a-b) > b*.05 for a,b in zip(dimensions, requirements["body"])):
        errors.append(f"Body dimension mismatch: {dimensions}")
    result = {"asset": asset, "blender": bpy.app.version_string, "triangles": triangles,
              "materials": sorted(materials), "body_metres": dimensions, "errors": errors}
    (OUT / f"{asset}.validation.json").write_text(json.dumps(result, indent=2)+"\n")
    if errors:
        raise ValueError("; ".join(errors))
    print("VALIDATED " + json.dumps(result))
    return result


def export(asset):
    # Metadata sidecar carries collider sizes without relying on FBX custom-property types.
    metadata = {"asset": asset, "anchors": [{"name": o.name,
                "size": {"x": o.get("size_x",0), "y": o.get("size_z",0), "z": o.get("size_y",0)}}
                for o in bpy.context.scene.objects if o.type == "EMPTY"],
                "materials": {"M_"+name: {"color": list(values[0]), "metallic": values[1], "roughness": values[2]}
                              for name, values in geo.PALETTE.items()}}
    target = OUT / f"{asset}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(target), use_selection=False, object_types={"MESH","EMPTY"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             use_custom_props=True, add_leaf_bones=False, bake_anim=False)
    (OUT / f"{asset}.equipment.json").write_text(json.dumps(metadata, indent=2)+"\n")


def preview(asset):
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = scene.render.resolution_y = 1024
    scene.render.resolution_percentage = 100
    scene.world.color = (.25,.25,.25)
    scene.view_settings.view_transform = "AgX"
    meshes = [o for o in scene.objects if o.type == "MESH"]
    coords = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = Vector(tuple(min(c[i] for c in coords) for i in range(3)))
    high = Vector(tuple(max(c[i] for c in coords) for i in range(3)))
    centre = (low+high)/2
    span = max(high-low)
    geo.box("StudioFloor", (200,200,.01), (0,0,-.006), "PlasticGrey" if asset == "wristband" else "PlasticWhite", 0)
    for position, power, size in [((-3,-4,5),700,4),((3,1,4),900,3),((0,3,5),500,2)]:
        bpy.ops.object.light_add(type="AREA", location=position)
        light = bpy.context.object
        light.data.energy, light.data.shape, light.data.size = power,"DISK",size
        light.rotation_euler = (centre-light.location).to_track_quat("-Z","Y").to_euler()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = "ORTHO"
    camera.data.clip_start = .001
    camera.data.ortho_scale = span*1.35
    PREVIEWS.mkdir(parents=True, exist_ok=True)
    views = {"front": (0,-1,.08), "side": (1,0,.08), "top": (0,0,1), "three_quarter": (1,-1,.65)}
    for name, direction in views.items():
        camera.location = centre+Vector(direction).normalized()*max(3,span*3)
        camera.rotation_euler = (centre-camera.location).to_track_quat("-Z","Y").to_euler()
        scene.render.filepath = str(PREVIEWS / f"{asset}_{name}.png")
        bpy.ops.render.render(write_still=True)
    # Separate human-scale view keeps wristband close-ups readable.
    x = -.7
    geo.box("ScaleTorso", (.34,.17,.56), (x,.13,1.18), "PlasticGrey", .04)
    geo.box("ScaleHead", (.16,.16,.22), (x,.13,1.59), "PlasticGrey", .03)
    for offset in [-.095,.095]:
        geo.box("ScaleFoot"+str(offset), (.11,.22,.07), (x+offset,.10,.035), "PlasticGrey", .01)
        geo.rod("ScaleLeg"+str(offset),(x+offset,.13,.04),(x+offset,.13,.9),.055,"PlasticGrey")
        geo.rod("ScaleArm"+str(offset),(x+offset*2.4,.13,.78),(x+offset*2,.13,1.42),.035,"PlasticGrey")
    geo.rod("OneMetreScale", (.45,0,0), (.45,0,1), .004,"AccentBlue")
    for i in range(11):
        geo.rod("ScaleTick"+str(i),(.43,0,i*.1),(.47,0,i*.1),.002,"AccentBlue")
    centre = Vector((-.2,0,.95))
    camera.data.ortho_scale = 2.6
    camera.location = centre+Vector((1,-4,1))
    camera.rotation_euler = (centre-camera.location).to_track_quat("-Z","Y").to_euler()
    scene.render.filepath = str(PREVIEWS / f"{asset}_human_scale.png")
    bpy.ops.render.render(write_still=True)


def selftest():
    geo.reset("test")
    body = geo.box("Body", (1,1,1), (0,0,.5))
    req = {"body": [1,1,1], "triangles": 5000, "materials": 1, "points": []}
    validate("test", req)
    # Each failure is independent; restore the original before the next mutation.
    cases = [(lambda: setattr(body,"name","Wrong"), lambda: setattr(body,"name","EQ_test_Body"), "Invalid mesh name"),
             (lambda: setattr(body,"scale",(2,1,1)), lambda: setattr(body,"scale",(1,1,1)), "Unapplied transform"),
             (lambda: req.update(triangles=1), lambda: req.update(triangles=5000), "Triangle budget"),
             (lambda: req.update(points=["MISSING"]), lambda: req.update(points=[]), "Missing anchor"),
             (lambda: req.update(body=[2,1,1]), lambda: req.update(body=[1,1,1]), "Body dimension")]
    for mutate,restore,expected in cases:
        mutate()
        try:
            validate("test",req)
        except ValueError as error:
            assert expected in str(error), str(error)
        else:
            raise AssertionError("Validator accepted " + expected)
        restore()
    validate("test",req)
    print("PASS validator positive + 5 independent rejection cases")


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    command, selection = sys.argv[sys.argv.index("--")+1:]
    if command == "selftest":
        selftest()
        return
    assets = list(REQUIREMENTS) if selection == "all" else [selection]
    for asset in assets:
        if command in ("build","all"):
            geo.reset(asset)
            if asset == "hello_cube":
                geo.box("Body",(1,1,1),(0,0,.5),bevel=0)
            else:
                spec = importlib.util.spec_from_file_location(asset, HERE / "assets" / REQUIREMENTS[asset]["script"])
                module = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(module)
                module.build()
            bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f"{asset}.blend"))
        else:
            bpy.ops.wm.open_mainfile(filepath=str(OUT / f"{asset}.blend"))
        req = REQUIREMENTS.get(asset, {"body":[1,1,1],"triangles":12,"materials":1,"points":[]})
        validate(asset, req)
        if command in ("build","all"):
            export(asset)
        if command in ("preview","all"):
            preview(asset)


main()
