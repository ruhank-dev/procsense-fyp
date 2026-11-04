"""Blender review renders of a rebuilt prop (Cycles, CPU, soft studio light): front-3/4, front, side and a close view.

  blender-4.5.14 -b --factory-startup -P unity/Tools/Environment/preview_prop.py -- ASSET [view ...]

Reads unity/Logs/BayRebuild/ASSET.blend, writes unity/Logs/BayRebuild/preview/ASSET-<view>.png. These are Blender
review renders for modelling only, never evidence of the look in Unity or on Quest.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
import os
OUT = Path(os.environ["PREVIEW_DIR"]) if os.environ.get("PREVIEW_DIR") else ROOT / "unity" / "Logs" / "BayRebuild"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
asset = argv[0]
views = argv[1:] or ["q34", "front", "side"]
bpy.ops.wm.open_mainfile(filepath=str(OUT / (asset + ".blend")))
(OUT / "preview").mkdir(exist_ok=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.device = "CPU"
sc.cycles.samples = 48
sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 1400, 900
sc.view_settings.view_transform = "Standard"
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (.78, .80, .82, 1); bg.inputs[1].default_value = 1.1
meshes = [o for o in sc.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; size = (hi - lo).length
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); sc.collection.objects.link(sun)
sun.data.energy = 2.5; sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
floor_z = lo.z
bpy.ops.mesh.primitive_plane_add(size=size * 6, location=(ctr.x, ctr.y, floor_z - .001))
fl = bpy.context.object
m = bpy.data.materials.new("floor"); m.use_nodes = True; m.node_tree.nodes["Principled BSDF"].inputs[0].default_value = (.55, .55, .56, 1)
fl.data.materials.append(m)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
dirs = {"q34": (-.6, -1, .55), "front": (0, -1, .15), "closeL": (-.2, -1, .25), "closeDesk": (.25, -1, .55), "closeMouse": (.0, -1, .9), "closeR": (.2, -1, .25), "side": (1, 0, .15), "back": (.5, 1, .4), "top": (0, -.2, 1)}
ABS_VIEWS = {"deskTop": ((.38, -.80, 1.50), (0, -.05, 1.04), 52), "deskMouse": ((.52, -.36, 1.22), (.27, -.10, 1.02), 42), "monitorFront": ((0, -1.0, 1.36), (0, .1, 1.30), 42)}
for v in views:
    if v in ABS_VIEWS:
        pos, look, fov = ABS_VIEWS[v]
        cam.data.lens = 36 / (2 * math.tan(math.radians(fov) / 2))
        cam.location = Vector(pos)
        cam.rotation_euler = (Vector(look) - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = str(OUT / "preview" / f"{asset}-{v}.png")
        bpy.ops.render.render(write_still=True)
        continue
    d = Vector(dirs.get(v, dirs["q34"])).normalized()
    dist = size * 1.5 if not v.startswith("close") else (size * .55 if v == "closeDesk" else size * .22 if v == "closeMouse" else size * .32)
    focus = ctr + (Vector((0, 0, -.25)) if v == "closeDesk" else Vector((.30, -.1, -.45)) if v == "closeMouse" else Vector((-.55, 0, .02)) if v == "closeL" else Vector((.55, 0, .02)) if v == "closeR" else Vector((0, 0, 0)))
    cam.location = focus + d * dist
    cam.rotation_euler = (focus - cam.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = str(OUT / "preview" / f"{asset}-{v}.png")
    bpy.ops.render.render(write_still=True)
print("PREVIEW", asset, views)
