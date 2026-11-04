"""Quick Workbench preview (front + side) of a baked civilian: blender -b <key>_baked.blend -P preview_civilian.py -- <out.png>"""
import sys, math
import bpy
from mathutils import Vector
out = sys.argv[sys.argv.index("--") + 1]
sc = bpy.context.scene; sc.render.engine = "BLENDER_WORKBENCH"; sc.render.resolution_x, sc.render.resolution_y = 900, 700
sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"; sc.world.color = (.7, .72, .75)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 40
paths = []
for name, loc, tgt in (("front", (0, -3.2, 1.0), (0, 0, .85)), ("side", (-3.2, 0, 1.0), (0, 0, .85)), ("three", (-2.3, -2.3, 1.2), (0, 0, .85))):
    cam.location = loc; d = Vector(tgt) - Vector(loc); cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = out.replace(".png", f"_{name}.png"); bpy.ops.render.render(write_still=True); paths.append(sc.render.filepath)
print("PREVIEW", paths)
