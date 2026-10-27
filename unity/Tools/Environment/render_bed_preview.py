"""Blender review renders of the hospital bed set: lit views and topology (wireframe) views of what ships.

  blender-4.5.14 -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/render_bed_preview.py -- TAG [view ...] [--nowire]

Opens unity/Logs/HospitalBed/hospital_bed.blend, rebuilds the cover and pillow from their exported meshes with their baked
albedo/normal/occlusion maps, then writes unity/Logs/HospitalBed/preview/TAG-<view>.png. Metres; Cycles for wire views.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "unity" / "Logs" / "HospitalBed"
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
TAG = argv[0] if argv else "frame"

bpy.ops.wm.open_mainfile(filepath=str(OUT / "hospital_bed.blend"))


def image(name, srgb):
    img = bpy.data.images.load(str(OUT / name))
    img.colorspace_settings.name = "sRGB" if srgb else "Non-Color"
    return img


def textured(name, albedo, normal=None, metal=None, detail_scale=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image(albedo, True)
    tex.interpolation = "Linear" if metal is None else "Closest"
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if metal:
        mtex = nt.nodes.new("ShaderNodeTexImage")
        mtex.image = image(metal, False)
        mtex.interpolation = "Closest"
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation, inv.inputs[0].default_value = "SUBTRACT", 1.0
        nt.links.new(mtex.outputs["Color"], sep.inputs["Color"])
        nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
        nt.links.new(mtex.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
        bsdf.inputs["Specular IOR Level"].default_value = .5
    else:
        inv = nt.nodes.new("ShaderNodeMath")
        inv.operation, inv.inputs[0].default_value = "SUBTRACT", 1.0
        nt.links.new(tex.outputs["Alpha"], inv.inputs[1])
        nt.links.new(inv.outputs["Value"], bsdf.inputs["Roughness"])
    if normal:
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = image(normal, False)
        nm = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ntex.outputs["Color"], nm.inputs["Color"])
        nt.links.new(nm.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def json_mesh(name, jfile, mat):
    d = json.load(open(OUT / jfile))
    v, uv, t = d["vertices"], d["uv"], d["tris0"]
    verts = [(v[i], v[i + 2] - 1.0, v[i + 1]) for i in range(0, len(v), 3)]
    faces = [(t[i], t[i + 2], t[i + 1]) for i in range(0, len(t), 3)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = (uv[loop.vertex_index * 2], uv[loop.vertex_index * 2 + 1])
    for p in mesh.polygons:
        p.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return obj


for o in bpy.context.scene.objects:
    if o.type == "MESH":
        o.data.materials.clear()
        o.data.materials.append(textured("M_BedFrame", "frame_albedo.png", metal="frame_metal_smooth.png"))
json_mesh("BED_cover", "bed_cover.json", textured("M_Cover", "cover_albedo.png", "cover_normal.png"))
json_mesh("BED_pillow", "pillow.json", textured("M_Pillow", "pillow_albedo.png", "pillow_normal.png"))

scene = bpy.context.scene
(OUT / "preview").mkdir(exist_ok=True)

# neutral room: floor + soft back wall so the model has grounding and contact shadows
bpy.ops.mesh.primitive_plane_add(size=12, location=(0, 0, 0))
floor = bpy.context.object
floor.name = "REVIEW_floor"
mat = bpy.data.materials.new("REVIEW_floor")
mat.diffuse_color = (.55, .5, .42, 1)
mat.use_nodes = True
mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.46, .38, .27, 1)
mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .55
floor.data.materials.append(mat)

world = scene.world or bpy.data.worlds.new("W")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (.62, .68, .72, 1)
bg.inputs["Strength"].default_value = .9


def light(name, loc, energy, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.size = energy, size
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    track = obj.constraints.new("TRACK_TO")
    track.target = target
    track.track_axis, track.up_axis = "TRACK_NEGATIVE_Z", "UP_Y"
    return obj


target = bpy.data.objects.new("REVIEW_target", None)
bpy.context.collection.objects.link(target)
target.location = (0, 0, .6)
light("Key", (-2.2, -2.4, 3.0), 520, 2.6)
light("Fill", (2.8, -1.2, 1.8), 160, 3.2)
light("Rim", (.6, 3.0, 2.4), 260, 2.0)

cam_data = bpy.data.cameras.new("REVIEW_cam")
cam = bpy.data.objects.new("REVIEW_cam", cam_data)
bpy.context.collection.objects.link(cam)
scene.camera = cam
track = cam.constraints.new("TRACK_TO")
track.target = target
track.track_axis, track.up_axis = "TRACK_NEGATIVE_Z", "UP_Y"

scene.render.resolution_x, scene.render.resolution_y = 1600, 900
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"

VIEWS = {
    "34foot": ((2.6, -3.3, 1.9), (0, .0, .55), 42),
    "34head": ((-2.6, 3.4, 1.9), (0, .0, .6), 42),
    "side":   ((4.0, 0, .95), (0, .0, .62), 36),
    "top":    ((.001, 0, 5.2), (0, 0, .5), 36),
    "footend": ((0, -4.2, 1.2), (0, 0, .62), 34),
    "rail":   ((1.9, 1.0, 1.35), (-.5, .58, .72), 50),
    "board":  ((1.7, .0, 1.5), (0, .95, .95), 50),
    "pillow": ((.85, .2, 1.2), (0, .76, .75), 38),
    "cover":  ((1.0, -1.0, 1.15), (.1, -.55, .68), 42),
    "wheel":  ((1.2, -1.45, .6), (.29, -.66, .2), 45),
}


def render(name, wire):
    scene.render.filepath = str(OUT / "preview" / f"{TAG}-{name}.png")
    bpy.ops.render.render(write_still=True)


def setup_solid():
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 48
    floor.hide_render = False


def setup_wire():
    """Hidden-line wireframe: polygon edges in white over a dark surface, rendered with Cycles (CPU, few samples)."""
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = False
    scene.view_settings.view_transform = "Standard"
    floor.hide_render = True
    bg.inputs["Color"].default_value = (.03, .035, .045, 1)
    bg.inputs["Strength"].default_value = 1.0
    wire = bpy.data.materials.new("REVIEW_wire")
    wire.use_nodes = True
    nt = wire.node_tree
    nt.nodes.clear()
    node = nt.nodes.new("ShaderNodeWireframe")
    node.use_pixel_size = True
    node.inputs["Size"].default_value = 1.1
    dark = nt.nodes.new("ShaderNodeBsdfDiffuse")
    dark.inputs["Color"].default_value = (.045, .05, .06, 1)
    emit = nt.nodes.new("ShaderNodeEmission")
    emit.inputs["Color"].default_value = (.92, .95, 1, 1)
    emit.inputs["Strength"].default_value = 2.2
    mix = nt.nodes.new("ShaderNodeMixShader")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(node.outputs["Fac"], mix.inputs["Fac"])
    nt.links.new(dark.outputs["BSDF"], mix.inputs[1])
    nt.links.new(emit.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], out.inputs["Surface"])
    bpy.context.view_layer.material_override = wire


def point_camera(view):
    loc, tgt, lens_fov = VIEWS[view]
    cam.location = loc
    target.location = tgt
    cam_data.angle = math.radians(lens_fov)
    bpy.context.view_layer.update()


only = [v for v in argv[1:] if v in VIEWS]
for view in (only or VIEWS):
    point_camera(view)
    setup_solid()
    render(view, False)

if "--nowire" not in sys.argv:
    setup_wire()
    for view in ("34foot", "side", "34head"):
        point_camera(view)
        render("wire-" + view, True)
print("PREVIEW DONE", TAG)
