"""Cycles studio renders of the hero monitor. Run: blender -b build/blender/vital_signs_monitor.blend -P render_monitor.py -- <outdir> [samples] [width]
PBR materials are assigned by material name from the texture set made by monitor_assets.py. Views: front, side, rear, three-quarter, bezel/key close-up,
connector-bay close-up, full stand."""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUT = Path(args[0]); OUT.mkdir(parents=True, exist_ok=True)
SAMPLES = int(args[1]) if len(args) > 1 else 96
WIDTH = int(args[2]) if len(args) > 2 else 1400
BUILD = Path(__file__).resolve().parents[2] / "Logs" / "MonitorBuild"
sc = bpy.context.scene


def img(name, data=True):
    im = bpy.data.images.load(str(BUILD / name))
    if not data:
        im.colorspace_settings.name = "sRGB"
    else:
        im.colorspace_settings.name = "Non-Color"
    return im


def shader(mat, base, metal=0.0, rough=.4, clearcoat=0.0, coat_rough=.05, normal_scale=0.0, emission=None, grain=True, alpha_tex=None, spec=.5):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    bsdf.inputs["Base Color"].default_value = base
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Coat Weight"].default_value = clearcoat
    bsdf.inputs["Coat Roughness"].default_value = coat_rough
    bsdf.inputs["Specular IOR Level"].default_value = spec
    if grain and (normal_scale or rough > .3):
        coord = nt.nodes.new("ShaderNodeTexCoord")
        mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (1, 1, 1)
        mp.inputs["Scale"].default_value = (12.5, 12.5, 12.5)        # object-space box projection: the 8 cm grain tile no longer depends on UV0
        nt.links.new(coord.outputs["Object"], mp.inputs[0])
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img("plastic_orange_peel_n.png"); tn.interpolation = "Linear"; tn.projection = "BOX"; tn.projection_blend = .3
        nm = nt.nodes.new("ShaderNodeNormalMap"); nm.inputs["Strength"].default_value = normal_scale or .35
        nt.links.new(mp.outputs[0], tn.inputs[0]); nt.links.new(tn.outputs[0], nm.inputs["Color"]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
        rn = nt.nodes.new("ShaderNodeTexImage"); rn.image = img("plastic_rough.png"); rn.projection = "BOX"; rn.projection_blend = .3
        nt.links.new(mp.outputs[0], rn.inputs[0])
        mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"; mul.inputs[1].default_value = rough / .45
        nt.links.new(rn.outputs[0], mul.inputs[0]); nt.links.new(mul.outputs[0], bsdf.inputs["Roughness"])
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission[:4]
        bsdf.inputs["Emission Strength"].default_value = emission[4]
    if alpha_tex:
        tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img(alpha_tex, False)
        nt.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
        tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mx = nt.nodes.new("ShaderNodeMixShader")
        nt.links.remove(bsdf.outputs[0].links[0])
        nt.links.new(tn.outputs["Alpha"], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(bsdf.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
    return bsdf, nt


def screen_mat(mat, uibase="monitor_ui.png"):
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = img(uibase, False)
    nt.links.new(bsdf.outputs[0], out.inputs[0]); nt.links.new(tn.outputs[0], bsdf.inputs["Base Color"]); nt.links.new(tn.outputs[0], bsdf.inputs["Emission Color"])
    bsdf.inputs["Emission Strength"].default_value = 1.1; bsdf.inputs["Roughness"].default_value = .35


A = {
    "M_MonShell": lambda m: shader(m, (.58, .595, .59, 1), 0, .44, .0, normal_scale=.08),
    "M_MonBezel": lambda m: shader(m, (.010, .011, .013, 1), 0, .20, 1.0, .03, grain=False),
    "M_MonKey": lambda m: shader(m, (.045, .05, .055, 1), 0, .55, 0, grain=False),
    "M_PlasticGrey": lambda m: shader(m, (.060, .068, .075, 1), 0, .50, 0, normal_scale=.15),
    "M_RubberBlack": lambda m: shader(m, (.016, .018, .02, 1), 0, .72, 0, normal_scale=.25),
    "M_SteelBrushed": lambda m: shader(m, (.55, .56, .58, 1), 1.0, .32, grain=False),
    "M_MonChrome": lambda m: shader(m, (.85, .86, .88, 1), 1.0, .06, grain=False),
    "M_MonPlastic": lambda m: shader(m, (.26, .27, .285, 1), 0, .5, 0, normal_scale=.15),
    "M_MonSteel": lambda m: shader(m, (.55, .56, .58, 1), 1.0, .32, grain=False),
    "M_Chrome": lambda m: shader(m, (.85, .86, .88, 1), 1.0, .06, grain=False),
    "M_MonSocket": lambda m: shader(m, (.004, .004, .005, 1), 0, .6, grain=False),
    "M_GasGreen": lambda m: shader(m, (.03, .42, .12, 1), 0, .38, grain=False),
    "M_GasBlue": lambda m: shader(m, (.03, .12, .52, 1), 0, .38, grain=False),
    "M_GasWhite": lambda m: shader(m, (.80, .81, .80, 1), 0, .38, grain=False),
    "M_GasYellow": lambda m: shader(m, (.78, .60, .04, 1), 0, .38, grain=False),
    "M_LedGreen": lambda m: shader(m, (.05, .8, .2, 1), 0, .1, emission=(.05, .9, .25, 1, 6), grain=False),
    "M_LedAmber": lambda m: shader(m, (.8, .45, .03, 1), 0, .1, emission=(.9, .45, .02, 1, 1.2), grain=False),
    "M_LedRed": lambda m: shader(m, (.4, .02, .02, 1), 0, .1, grain=False),
    "M_MonLabels": lambda m: shader(m, (1, 1, 1, 1), 0, .5, alpha_tex="monitor_labels.png", grain=False),
    "M_MonLCD": screen_mat,
}


def glass(mat):
    mat.use_nodes = True
    nt = mat.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent"); gl = nt.nodes.new("ShaderNodeBsdfGlossy")
    sm = nt.nodes.new("ShaderNodeTexImage"); sm.image = img("glass_smudge.png")
    rough = nt.nodes.new("ShaderNodeMath"); rough.operation = "MULTIPLY_ADD"; rough.inputs[1].default_value = .08; rough.inputs[2].default_value = .015
    nt.links.new(sm.outputs[0], rough.inputs[0]); nt.links.new(rough.outputs[0], gl.inputs["Roughness"])
    lw = nt.nodes.new("ShaderNodeFresnel"); lw.inputs["IOR"].default_value = 1.5
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(lw.outputs[0], mix.inputs[0]); gl.inputs["Color"].default_value = (.55, .55, .56, 1); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(gl.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])


A["M_MonGlass"] = glass
for m in bpy.data.materials:
    if m.name in A:
        A[m.name](m)
    else:
        print("NO SHADER", m.name)

for o in bpy.data.objects:
    if o.type == "MESH" and o.data.materials and o.data.materials[0].name == "M_MonLabels":
        o.visible_shadow = False; o.hide_render = bool(__import__("os").environ.get("NODECAL"))
        bpy.context.view_layer.objects.active = o
        o.select_set(True)
        bpy.ops.object.shade_flat()
# studio ---------------------------------------------------------------------------------------------------------------
w = bpy.data.worlds.new("studio"); sc.world = w; w.use_nodes = True
nt = w.node_tree; nt.nodes.clear()
env = nt.nodes.new("ShaderNodeTexEnvironment"); env.image = bpy.data.images.load(str(BUILD / "studio.hdr"))
bg = nt.nodes.new("ShaderNodeBackground"); bg.inputs["Strength"].default_value = .55
bg2 = nt.nodes.new("ShaderNodeBackground"); bg2.inputs["Color"].default_value = (.60, .61, .62, 1); bg2.inputs["Strength"].default_value = 1.0
lp = nt.nodes.new("ShaderNodeLightPath"); mx = nt.nodes.new("ShaderNodeMixShader")
mp = nt.nodes.new("ShaderNodeMapping"); mp.inputs["Rotation"].default_value = (0, 0, math.radians(200))
co = nt.nodes.new("ShaderNodeTexCoord"); wo = nt.nodes.new("ShaderNodeOutputWorld")
nt.links.new(co.outputs["Generated"], mp.inputs[0]); nt.links.new(mp.outputs[0], env.inputs[0]); nt.links.new(env.outputs[0], bg.inputs[0]); nt.links.new(lp.outputs["Is Camera Ray"], mx.inputs[0]); nt.links.new(bg.outputs[0], mx.inputs[1]); nt.links.new(bg2.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], wo.inputs[0])
# neutral light-grey seamless floor
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, -0.001)); fl = bpy.context.object; fl.name = "StudioFloor"
fm = bpy.data.materials.new("floor"); fm.use_nodes = True; fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.55, .56, .57, 1)
fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .55; fl.data.materials.append(fm); fl.is_shadow_catcher = True
for loc, power, size in (((1.5, -1.3, 2.3), 200, 1.0), ((-1.6, -.6, 1.0), 90, .8), ((.4, 1.8, 1.8), 150, 1.2)):
    bpy.ops.object.light_add(type="AREA", location=loc); li = bpy.context.object
    li.data.energy, li.data.size = power, size
    li.rotation_euler = (Vector((0, 0, 1.25)) - li.location).to_track_quat("-Z", "Y").to_euler()

sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = not __import__("os").environ.get("NODENOISE")
sc.cycles.max_bounces = 10; sc.cycles.glossy_bounces = 8; sc.cycles.transparent_max_bounces = 12
sc.render.resolution_x = WIDTH; sc.render.resolution_y = int(WIDTH * .75); sc.render.film_transparent = False
if __import__("os").environ.get("STD"):
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
else:
    sc.view_settings.view_transform = "AgX"; sc.view_settings.look = "AgX - Base Contrast"
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam


def shoot(name, target, location, lens=85, dof=None):
    cam.data.lens = lens; cam.data.clip_start = .01
    cam.location = location
    cam.rotation_euler = (Vector(target) - Vector(location)).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = str(OUT / f"{name}.png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)


views = {
    "front": ((0, 0, 1.29), (0, -1.6, 1.30), 85),
    "side": ((0, 0, 1.29), (-1.6, -.1, 1.29), 85),
    "rear": ((0, .1, 1.29), (.5, 1.5, 1.45), 85),
    "threequarter": ((0, 0, 1.30), (-.95, -1.25, 1.55), 85),
    "bezel_closeup": ((-.01, -.09, 1.26), (-.12, -.55, 1.34), 85),
    "bay_closeup": ((-.165, -.02, 1.28), (-.55, -.30, 1.40), 85),
    "keys_closeup": ((.0, -.095, 1.18), (.08, -.40, 1.28), 85),
    "stand": ((0, .04, .75), (-1.8, -3.0, 1.35), 60),
    "pole_closeup": ((0, .045, .70), (-.55, -.55, .75), 85),
    "base_closeup": ((0, .045, .09), (-.85, -1.25, .55), 54),
    "glass_closeup": ((-.008, -.10, 1.316), (-.50, -.50, 1.50), 85),
    "housing_closeup": ((-.16, -.05, 1.40), (-.45, -.45, 1.55), 85),
}
PANO = len(args) > 3 and args[3] == "pano"
BALL = len(args) > 3 and args[3] == "ball"
only = [] if (PANO or BALL) else (args[3].split(",") if len(args) > 3 else list(views))
for n in only:
    t, loc, lens = views[n]
    shoot(n, t, loc, lens)


if PANO:
    # the studio itself (HDRI + visible softboxes + floor) as a lat-long map, seen from the monitor's position with the monitor hidden
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name.startswith("EQ_"):
            o.hide_render = True
    fl.is_shadow_catcher = False
    # area lights are not seen by camera rays: put emissive panels where they are (calibrated: radiance = P / (pi * area))
    for li in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        sz, pw = li.data.size, li.data.energy
        bpy.ops.mesh.primitive_plane_add(size=sz, location=li.location); pl = bpy.context.object; pl.rotation_euler = li.rotation_euler
        em = bpy.data.materials.new("lightpanel"); em.use_nodes = True; ent = em.node_tree; ent.nodes.clear()
        eo = ent.nodes.new("ShaderNodeOutputMaterial"); ee = ent.nodes.new("ShaderNodeEmission"); ee.inputs["Strength"].default_value = pw / (math.pi * sz * sz)
        ent.links.new(ee.outputs[0], eo.inputs[0]); pl.data.materials.append(em); pl.visible_shadow = False
    mx.inputs[0].default_value = 0.0
    for l in list(lp.outputs["Is Camera Ray"].links):
        nt.links.remove(l)
    sc.render.resolution_x, sc.render.resolution_y = 2048, 1024
    sc.cycles.samples = 64
    cam.data.type = "PANO"
    try:
        cam.data.panorama_type = "EQUIRECTANGULAR"
    except Exception:
        cam.data.cycles.panorama_type = "EQUIRECTANGULAR"
    cam.location = (0, 0, 1.29); cam.rotation_euler = (math.radians(90), 0, 0)
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    sc.render.image_settings.file_format = "OPEN_EXR"; sc.render.image_settings.color_depth = "32"
    sc.render.filepath = str(OUT / "studio_env.exr")
    bpy.ops.render.render(write_still=True)
    print("RENDERED pano")


if BALL:
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name.startswith("EQ_"):
            o.hide_render = True
    for i, (x, rough) in enumerate(((0.0, .06), (.3, .25), (-.3, .6))):
        bpy.ops.mesh.primitive_uv_sphere_add(radius=.1, location=(x, 0, 1.29), segments=96, ring_count=48)
        b = bpy.context.object; b.data.polygons.foreach_set("use_smooth", [True] * len(b.data.polygons))
        m = bpy.data.materials.new(f"ball{i}"); b.data.materials.append(m)
        shader(m, (.85, .86, .88, 1), 1.0, rough, grain=False)
    shoot("ball", (0, 0, 1.29), (0, -1.6, 1.30), 50)
