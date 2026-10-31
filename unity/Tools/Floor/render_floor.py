"""Cycles study render of the PatientBay wood floor: the same atlas, UV layout and cameras as the Unity scene, in a simplified room (walls, ceiling light panels, window, bed and cabinet blocks).
Run:  blender -b -P render_floor.py -- <outdir> [samples] [width] [views,...]
Unity -> Blender axes: B = (-Ux, -Uz, Uy). The plane's default UVs then equal the Unity floor's UVs (u grows with -Ux, v with -Uz).
Parity choices: one specular lobe (Principled, IOR 1.5, no clear-coat), roughness = 1 - smoothness alpha, normal map strength .6, Standard view transform."""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT = Path(args[0]); OUT.mkdir(parents=True, exist_ok=True)
SAMPLES = int(args[1]) if len(args) > 1 else 96
WIDTH = int(args[2]) if len(args) > 2 else 1600
ONLY = args[3].split(",") if len(args) > 3 else None
TEX = Path(__file__).resolve().parents[2] / "Logs" / "FloorBuild" / "out"
VARIANT = __import__("os").environ.get("FLOOR_BLENDER_VARIANT", "wood")
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene


def U(x, y, z):
    return Vector((-x, -z, y))


def mat(name, base=(.8, .8, .78, 1), rough=.8, emission=None):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if emission:
        e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = emission[:4]; e.inputs["Strength"].default_value = emission[4]
        nt.links.new(e.outputs[0], out.inputs[0]); return m
    b = nt.nodes.new("ShaderNodeBsdfPrincipled"); b.inputs["Base Color"].default_value = base; b.inputs["Roughness"].default_value = rough
    nt.links.new(b.outputs[0], out.inputs[0]); return m


def floor_mat():
    m = bpy.data.materials.new("wood"); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); b = nt.nodes.new("ShaderNodeBsdfPrincipled"); nt.links.new(b.outputs[0], out.inputs[0])
    b.inputs["Metallic"].default_value = 0; b.inputs["IOR"].default_value = 1.5
    if VARIANT == "vinyl":
        b.inputs["Base Color"].default_value = (.58, .55, .48, 1); b.inputs["Roughness"].default_value = .2; return m
    a = nt.nodes.new("ShaderNodeTexImage"); a.image = bpy.data.images.load(str(TEX / "floor_albedo_smooth.png")); a.image.colorspace_settings.name = "sRGB"
    a.image.alpha_mode = "CHANNEL_PACKED"; a.interpolation = "Cubic"
    nt.links.new(a.outputs["Color"], b.inputs["Base Color"])
    inv = nt.nodes.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0
    nt.links.new(a.outputs["Alpha"], inv.inputs[1]); nt.links.new(inv.outputs[0], b.inputs["Roughness"])
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = bpy.data.images.load(str(TEX / "floor_normal.png")); n.image.colorspace_settings.name = "Non-Color"; n.interpolation = "Cubic"
    nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = .6
    nt.links.new(n.outputs["Color"], nm.inputs["Color"]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    return m


def box(name, size, centre_u, material):
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(*centre_u)); o = bpy.context.object; o.name = name
    o.scale = (size[0], size[2], size[1]); o.data.materials.append(material); return o


# --- room (Unity extents x -2.5..2.5, z -2..2, ceiling 2.8)
bpy.ops.mesh.primitive_plane_add(size=1, location=U(0, 0, 0)); fl = bpy.context.object; fl.name = "Floor"; fl.scale = (5, 4, 1); fl.data.materials.append(floor_mat())
paint = mat("paint", (.80, .76, .68, 1), .9); ceil = mat("ceiling", (.85, .84, .80, 1), .9); lower = mat("lower wall", (.46, .56, .50, 1), .85)
box("wall_back", (5, 2.8, .1), (0, 1.4, 2.05), paint); box("wall_front", (5, 2.8, .1), (0, 1.4, -2.05), paint)
box("wall_left", (.1, 2.8, 4), (-2.55, 1.4, 0), paint); box("wall_right", (.1, 2.8, 4), (2.55, 1.4, 0), paint)
box("ceiling", (5, .1, 4), (0, 2.85, 0), ceil)
for nm, (x, z), sz in (("panel_a", (.35, -.6), (1.2, .6)), ("panel_b", (-.85, .95), (1.2, .6))):
    box(nm, (sz[0], .02, sz[1]), (x, 2.79, z), mat(nm, emission=(1, .93, .82, 1, 9)))
    bpy.ops.object.light_add(type="AREA", location=U(x, 2.76, z)); li = bpy.context.object; li.data.size = sz[0]; li.data.size_y = sz[1]; li.data.shape = "RECTANGLE"; li.data.energy = 140
    li.data.color = (1, .93, .82); li.rotation_euler = (0, 0, 0); li.data.energy = 0           # the emissive panel itself lights the room
box("window", (.02, 1.1, 1.7), (-2.48, 1.55, .3), mat("window", emission=(.85, .93, 1.0, 1, 3)))
bpy.ops.object.light_add(type="AREA", location=U(-2.3, 1.55, .3)); wl = bpy.context.object; wl.data.size = 1.7; wl.data.size_y = 1.1; wl.data.shape = "RECTANGLE"; wl.data.energy = 0
wl.data.color = (.88, .94, 1.0); wl.rotation_euler = (0, math.radians(90), 0)
# bed and cabinet blocks stand where the real ones stand, so the contact-shadow behaviour can be judged
frame = mat("frame", (.72, .72, .72, 1), .5); cover = mat("cover", (.35, .5, .72, 1), .9)
bx, bz = 0.0, 0.945
box("bed_slab", (1.1, .2, 2.0), (bx, .66, bz), cover)
box("headboard", (1.1, .6, .06), (bx, .95, bz + .98), frame)
for sx in (-1, 1):
    for sz in (-1, 1):
        box("bed_leg", (.06, .5, .06), (bx + sx * .5, .28, bz + sz * .92), frame)
box("cabinet", (.5, .75, .5), (-2.2, .38, -1.0), mat("cab", (.8, .78, .72, 1), .6))
# monitor stand: pole and star base
box("pole", (.04, 1.1, .04), (1.1, .65, 1.6), mat("steel", (.8, .82, .85, 1), .2))
for k in range(5):
    a = k * math.tau / 5
    box("leg", (.05, .05, .27), (1.1 + .13 * math.cos(a), .04, 1.6 + .13 * math.sin(a)), mat("leg", (.5, .5, .52, 1), .5)).rotation_euler.z = -a + math.radians(90)

# --- camera / render
sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.cycles.max_bounces = 8; sc.cycles.glossy_bounces = 6
sc.render.resolution_x = WIDTH; sc.render.resolution_y = int(WIDTH * 9 / 16)
sc.view_settings.view_transform = "Standard"
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs["Color"].default_value = (.5, .5, .5, 1); w.node_tree.nodes["Background"].inputs["Strength"].default_value = .15
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam


def point(p, t, fov):
    cam.data.sensor_fit = "VERTICAL"; cam.data.sensor_height = 36; cam.data.lens = 18 / math.tan(math.radians(fov) / 2); cam.data.clip_start = .02
    cam.location = U(*p); cam.rotation_euler = (U(*t) - U(*p)).to_track_quat("-Z", "Y").to_euler()


def auto_exposure(target=.55):
    import numpy as np
    point((2.38, 1.62, -1.84), (-0.62, 0.95, 1.15), 80)
    keep = (sc.render.resolution_x, sc.render.resolution_y, sc.cycles.samples)
    sc.render.resolution_x, sc.render.resolution_y, sc.cycles.samples = 480, 270, 16; sc.view_settings.exposure = 0
    sc.render.filepath = str(OUT / "_exp.png"); bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(str(OUT / "_exp.png")); px = np.array(im.pixels[:]).reshape(-1, 4)[:, :3]
    lum = (px * [.2126, .7152, .0722]).sum(1).mean()
    sc.view_settings.exposure = float(math.log2(target / max(lum, 1e-4)))
    sc.render.resolution_x, sc.render.resolution_y, sc.cycles.samples = keep
    print("AUTO EXPOSURE", round(lum, 3), "->", round(sc.view_settings.exposure, 2), "EV")


auto_exposure()
views = {
    "doorway": ((2.38, 1.62, -1.84), (-0.62, 0.95, 1.15), 80),
    "closeup": ((.9, .85, -.61), (-.7, 0, -1.4), 55),
    "low_reflection": ((1.95, .32, -1.6), (-1.2, 0, 1.0), 62),
    "under_bed": ((bx, .14, -.61), (bx, .11, bz), 72),
    "overview": ((2.3, 2.2, -1.7), (-.3, 0, .4), 70),
    "macro": ((1.2, .55, -.9), (.5, 0, -.2), 45),
}
for name, (p, t, fov) in views.items():
    if ONLY and name not in ONLY:
        continue
    cam.data.sensor_fit = "HORIZONTAL"; cam.data.sensor_width = 36
    cam.data.lens = 36 / (2 * math.tan(math.radians(fov * 16 / 9 / 2))) if False else 18 / math.tan(math.radians(fov) / 2)   # Unity's fov is vertical; convert via the vertical sensor
    cam.data.sensor_fit = "VERTICAL"; cam.data.sensor_height = 36; cam.data.lens = 18 / math.tan(math.radians(fov) / 2)
    cam.data.clip_start = .02
    cam.location = U(*p)
    cam.rotation_euler = (U(*t) - U(*p)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)
