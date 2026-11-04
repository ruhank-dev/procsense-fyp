"""High-rise city skyline for the PatientBay window (upper-floor view, neutral daylight).

Renders an equirectangular patch that matches the engine's exterior backdrop mapping exactly (longitude -55..55 degrees, latitude -20..45 degrees, u = (az+55)/110, v = (el+20)/65),
so the image drops into 'PatientBay window exterior view.mat' with no remapping. Everything is procedural and original: no third-party imagery.
Usage: blender -b --factory-startup -P build_city_skyline.py -- <out.png> [seed] [samples] [width]
"""
import math
import random
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
out = argv[0] if argv else "skyline.png"
seed = int(argv[1]) if len(argv) > 1 else 7
samples = int(argv[2]) if len(argv) > 2 else 48
W = int(argv[3]) if len(argv) > 3 else 2048
H = int(W * 65 / 110)
rnd = random.Random(seed)

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
sc.render.engine = "CYCLES"
sc.cycles.samples = samples
sc.cycles.use_denoising = True
try:
    sc.cycles.device = "GPU"
    prefs = bpy.context.preferences.addons["cycles"].preferences
    prefs.compute_device_type = "OPTIX"
    prefs.get_devices()
    for d in prefs.devices:
        d.use = True
except Exception:
    sc.cycles.device = "CPU"
sc.render.resolution_x, sc.render.resolution_y = W, H
sc.render.film_transparent = False
sc.view_settings.view_transform = "Standard"
sc.view_settings.look = "None"
sc.view_settings.exposure = -1.0

# ---- sky: neutral clear daylight with a few soft clouds ----------------------------------------------------------------------------------------------------
world = bpy.data.worlds.new("sky"); sc.world = world; world.use_nodes = True
nt = world.node_tree; nt.nodes.clear()
sky = nt.nodes.new("ShaderNodeTexSky"); sky.sky_type = "NISHITA"
sky.sun_elevation = math.radians(38); sky.sun_rotation = math.radians(250); sky.air_density = 1.0; sky.dust_density = 1.6; sky.sun_size = math.radians(.6); sky.sun_intensity = .25
bg = nt.nodes.new("ShaderNodeBackground"); bg.inputs["Strength"].default_value = .55
outn = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(sky.outputs[0], bg.inputs[0]); nt.links.new(bg.outputs[0], outn.inputs[0])

# ---- camera: panoramic patch, 70 m above street level ----------------------------------------------------------------------------------------------------
cam_d = bpy.data.cameras.new("cam"); cam = bpy.data.objects.new("cam", cam_d); sc.collection.objects.link(cam); sc.camera = cam
cam_d.type = "PANO"
try:
    cam_d.panorama_type = "EQUIRECTANGULAR"
    cam_d.longitude_min, cam_d.longitude_max = math.radians(-55), math.radians(55)
    cam_d.latitude_min, cam_d.latitude_max = math.radians(-20), math.radians(45)
except AttributeError:
    cam_d.cycles.panorama_type = "EQUIRECTANGULAR"
    cam_d.cycles.longitude_min, cam_d.cycles.longitude_max = math.radians(-55), math.radians(55)
    cam_d.cycles.latitude_min, cam_d.cycles.latitude_max = math.radians(-20), math.radians(45)
cam.location = (0, 0, 55)
cam.rotation_euler = (math.radians(90), 0, 0)       # looks along +Y, level


def mat(name, base, rough=.5, metal=0.0, spec=.5, emit=None, emit_str=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = base; b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    if emit is not None:
        b.inputs["Emission Color"].default_value = emit; b.inputs["Emission Strength"].default_value = emit_str
    return m


concrete = [mat(f"conc{i}", c, .75) for i, c in enumerate([(.62, .60, .56, 1), (.72, .70, .66, 1), (.55, .56, .58, 1), (.78, .74, .68, 1), (.66, .62, .58, 1)])]
glass = [mat(f"glass{i}", c, .08, .0) for i, c in enumerate([(.16, .30, .46, 1), (.22, .38, .52, 1), (.12, .22, .34, 1), (.30, .44, .56, 1), (.10, .18, .26, 1)])]
for g in glass:
    g.node_tree.nodes["Principled BSDF"].inputs["Metallic"].default_value = .6
ground = mat("ground", (.10, .11, .10, 1), .9)
green = mat("park", (.06, .13, .05, 1), .95)


_hz = {}


def hazed(m0, r):
    """Copy of the material blended toward the sky colour by distance (aerial perspective), cached per 60 m band."""
    k = (m0.name, int(r // 60))
    if k not in _hz:
        m = m0.copy(); b = m.node_tree.nodes["Principled BSDF"]
        f = 1 - math.exp(-r / 1500.0)
        c = b.inputs["Base Color"].default_value
        haze = (.34, .46, .62)
        b.inputs["Base Color"].default_value = (c[0] + (haze[0] - c[0]) * f, c[1] + (haze[1] - c[1]) * f, c[2] + (haze[2] - c[2]) * f, 1)
        b.inputs["Roughness"].default_value = min(1.0, b.inputs["Roughness"].default_value + .6 * f); b.inputs["Metallic"].default_value *= (1 - f)
        windows(m, f)
        _hz[k] = m
    return _hz[k]


def windows(m, haze_f):
    """Facade grid in world space (floors every 3.6 m, bays every 2.4 m): dark glazing between lighter spandrel lines, faded by the aerial haze."""
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]
    geo = nt.nodes.new("ShaderNodeNewGeometry"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); comb = nt.nodes.new("ShaderNodeCombineXYZ"); add = nt.nodes.new("ShaderNodeMath")
    add.operation = "ADD"
    nt.links.new(geo.outputs["Position"], sep.inputs[0]); nt.links.new(sep.outputs["X"], add.inputs[0]); nt.links.new(sep.outputs["Y"], add.inputs[1])
    nt.links.new(add.outputs[0], comb.inputs["X"]); nt.links.new(sep.outputs["Z"], comb.inputs["Y"])
    br = nt.nodes.new("ShaderNodeTexBrick"); br.offset = 0.0; br.squash = 1.0
    br.inputs["Scale"].default_value = 1.0; br.inputs["Mortar Size"].default_value = .10; br.inputs["Mortar Smooth"].default_value = .02
    br.inputs["Brick Width"].default_value = 2.4; br.inputs["Row Height"].default_value = 3.6
    base = b.inputs["Base Color"].default_value
    br.inputs["Color1"].default_value = (base[0] * .45, base[1] * .5, base[2] * .6, 1); br.inputs["Color2"].default_value = (base[0] * .6, base[1] * .66, base[2] * .75, 1)
    br.inputs["Mortar"].default_value = (min(1, base[0] * 1.5), min(1, base[1] * 1.5), min(1, base[2] * 1.5), 1)
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.inputs[0].default_value = max(0.0, .85 - haze_f * .9)
    mix.inputs[6].default_value = base
    nt.links.new(comb.outputs[0], br.inputs["Vector"]); nt.links.new(br.outputs["Color"], mix.inputs[7]); nt.links.new(mix.outputs[2], b.inputs["Base Color"])


def box(x, y, w, d, h, m, z0=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=(x, y, z0 + h / 2))
    o = bpy.context.active_object; o.scale = (w, d, h)
    o.data.materials.append(m)
    return o


# ground plane and a few park patches
bpy.ops.mesh.primitive_plane_add(size=6000, location=(0, 0, 0)); bpy.context.active_object.data.materials.append(ground)
for _ in range(14):
    a = math.radians(rnd.uniform(-70, 70)); r = rnd.uniform(160, 700)
    box(math.sin(a) * r, math.cos(a) * r, rnd.uniform(60, 160), rnd.uniform(40, 120), .3, green)

# buildings: mid-rise blocks everywhere, tall towers in clusters, taller further away so the skyline stays layered
placed = []
for _ in range(430):
    a = math.radians(rnd.uniform(-72, 72)); r = rnd.uniform(230, 2600)
    x, y = math.sin(a) * r, math.cos(a) * r
    w, d = rnd.uniform(18, 46), rnd.uniform(18, 46)
    tall = rnd.random() < (.16 + r / 6000)
    h = rnd.uniform(150, 400) if tall else rnd.uniform(30, 110) * (1 + r / 1500)
    if any(abs(x - px) < (w + pw) / 2 + 4 and abs(y - py) < (d + pd) / 2 + 4 for px, py, pw, pd in placed):
        continue
    placed.append((x, y, w, d))
    m0 = rnd.choice(glass) if (tall or rnd.random() < .45) else rnd.choice(concrete)
    m = hazed(m0, r)
    o = box(x, y, w, d, h, m)
    if tall and rnd.random() < .6:                      # stepped crown
        box(x, y, w * .7, d * .7, h * .12, m, z0=h)

# a few landmark towers on the right of the frame, like the reference's centrepiece skyline
for x, y, w, h in [(-210, 560, 40, 230), (-60, 640, 38, 290), (110, 580, 36, 200), (260, 700, 44, 270), (-390, 760, 48, 310)]:
    gm = hazed(rnd.choice(glass), math.hypot(x, y)); box(x, y, w, w * .9, h, gm); box(x, y, w * .75, w * .65, h * .09, gm, z0=h)

sc.render.filepath = out
sc.render.image_settings.file_format = "PNG"
bpy.ops.render.render(write_still=True)
print("[skyline] wrote", out, W, H, "buildings", len(placed))
