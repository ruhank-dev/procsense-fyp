"""Review renders of a staff character blend (Cycles CPU, soft studio light): full body, 3/4 and a head close-up. Not evidence of the Unity look.
  blender -b --factory-startup -P unity/Tools/MPFB/preview_staff.py -- <blend> <outdir> [views]   views: full,q34,head,hands,back
"""
import math, sys
from pathlib import Path
import bpy
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:]
blend, out = Path(argv[0]), Path(argv[1]); views = argv[2].split(",") if len(argv) > 2 else ["full", "q34", "head"]
out.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(blend))
sc = bpy.context.scene
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 40; sc.cycles.use_denoising = True
sc.render.resolution_x, sc.render.resolution_y = 900, 1200
sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True; sc.world.node_tree.nodes["Background"].inputs[0].default_value = (.55, .58, .62, 1); sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
def light(name, loc, energy, size):
    d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.size = size; o = bpy.data.objects.new(name, d); o.location = loc; sc.collection.objects.link(o)
    c = o.constraints.new("TRACK_TO"); c.target = tgt; c.track_axis = "TRACK_NEGATIVE_Z"; c.up_axis = "UP_Y"
tgt = bpy.data.objects.new("tgt", None); tgt.location = (0, 0, 1.0); sc.collection.objects.link(tgt)
light("key", (1.8, -2.2, 2.6), 380, 2.0); light("fill", (-2.2, -1.6, 1.4), 140, 2.5); light("rim", (0, 2.2, 2.4), 200, 1.5)
rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
top = max((o.matrix_world @ Vector(c)).z for o in rig.children if o.type == "MESH" for c in o.bound_box)
face_y = -1.0
cams = {"full": ((0, -4.6, top * .52), (0, 0, top * .50), 32), "q34": ((2.4, -3.6, top * .52), (0, 0, top * .5), 32),
        "head": ((.35, -1.1, top - .1), (0, 0, top - .09), 40), "hands": ((.9, -1.5, top * .55), (0, 0, top * .55), 40), "back": ((0, 4.6, top * .52), (0, 0, top * .5), 32)}
for v in views:
    p, l, f = cams[v]
    cd = bpy.data.cameras.new(v); cd.lens = f * 1.6 if False else 60 if v in ("head",) else 50; co = bpy.data.objects.new(v, cd); sc.collection.objects.link(co); co.location = p
    c = co.constraints.new("TRACK_TO"); t2 = bpy.data.objects.new("t_" + v, None); t2.location = l; sc.collection.objects.link(t2); c.target = t2; c.track_axis = "TRACK_NEGATIVE_Z"; c.up_axis = "UP_Y"
    sc.camera = co; sc.render.filepath = str(out / f"{blend.stem}_{v}.png"); bpy.ops.render.render(write_still=True)
print("PREVIEW_DONE", top)
