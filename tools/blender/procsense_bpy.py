"""Small, metre-based pilot geometry library. Blender Z up, front faces -Y."""
import math
import bpy
from mathutils import Vector

ASSET = ""
PALETTE = {
    "PlasticWhite": ((0.72, 0.76, 0.78, 1), 0, .34),
    "PlasticGrey": ((.055, .075, .085, 1), 0, .38),
    "SteelBrushed": ((.38, .43, .47, 1), .8, .3),
    "Chrome": ((.62, .67, .7, 1), .95, .18),
    "RubberBlack": ((.018, .025, .028, 1), 0, .75),
    "ScreenOff": ((.009, .023, .029, 1), .05, .24),
    "AccentBlue": ((.025, .25, .35, 1), .15, .35),
    "FabricNavy": ((.014, .04, .085, 1), 0, .9),
    "PlasticClear": ((.86, .9, .92, .35), 0, .12),    # specimen pot (p1-theatre-extension-s04); Unity needs a transparent material for it
}


def reset(asset):
    global ASSET
    ASSET = asset
    bpy.context.collection.name = "EQ_" + asset
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in list(bpy.data.materials):
        bpy.data.materials.remove(block)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1


def material(name):
    key = "M_" + name
    if key in bpy.data.materials:
        return bpy.data.materials[key]
    color, metal, rough = PALETTE[name]
    mat = bpy.data.materials.new(key)
    mat.diffuse_color = color
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = color
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = rough
    if color[3] < 1:                                   # a see-through plastic (the specimen pot)
        shader.inputs["Alpha"].default_value = color[3]
        mat.blend_method = "BLEND"
    return mat


def finish(obj, part, mat, bevel=0):
    obj.name = f"EQ_{ASSET}_{part}"
    obj.data.materials.append(material(mat))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Manufactured edge", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
        normal = obj.modifiers.new("Weighted normals", "WEIGHTED_NORMAL")
        bpy.ops.object.modifier_apply(modifier=normal.name)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15)
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def box(part, size, loc, mat="PlasticWhite", bevel=.002):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = bpy.context.object
    obj.dimensions = size
    return finish(obj, part, mat, min(bevel, min(size) / 4))


def rod(part, a, b, radius, mat="SteelBrushed", vertices=16):
    delta = Vector(b) - Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=delta.length,
                                       location=(Vector(a) + Vector(b)) / 2)
    obj = bpy.context.object
    obj.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(obj, part, mat)


def tube(part, points, radius=.006, mat="SteelBrushed"):
    # Connected sections retain closed end caps; each mesh is manifold.
    for index, (a, b) in enumerate(zip(points, points[1:])):
        rod(f"{part}_{index}", a, b, radius, mat, 12)


def point(name, loc, size=None):
    obj = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.empty_display_size = .025
    if size:
        obj["size_x"], obj["size_y"], obj["size_z"] = size
    return obj


def base(height):
    rod("Upright", (0, .045, .12), (0, .045, height), .019, "Chrome")
    rod("Hub", (0, .045, .09), (0, .045, .18), .055, "PlasticGrey")
    for i in range(5):
        angle = i * math.tau / 5
        x, y = .26 * math.cos(angle), .045 + .26 * math.sin(angle)
        tube(f"Leg{i}", [(0, .045, .13), (x, y, .085)], .019, "PlasticGrey")
        rod(f"CasterFork{i}", (x, y, .04), (x, y, .09), .012)
        rod(f"Caster{i}", (x-.018, y, .038), (x+.018, y, .038), .038, "RubberBlack")
    point("COL_Base", (0, .045, .07), (.57, .57, .14))
    point("COL_Pole", (0, .045, height / 2), (.05, .05, height))
