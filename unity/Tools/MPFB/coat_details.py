"""Lab-coat construction details for the staff, from the owner's reference photos (3 Nov 2026): notched lapel collar, centre snap placket, two patch pockets and a stethoscope around the neck.

Called from staff_outfit.outfit() after the coat is fitted. Everything is placed by ray casting onto the real coat surface (rest pose, character faces -Y, left side +X), skinned with the weights of the nearest
body vertex so it follows every animation, and exported with the rig like the other garment parts.
"""
import math

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def _mat(name, rgb, rough=.5, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF"); b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m


def _weights_from(obj, body):
    from mathutils.kdtree import KDTree
    kd = KDTree(len(body.data.vertices))
    for v in body.data.vertices:
        kd.insert(body.matrix_world @ v.co, v.index)
    kd.balance(); names = {g.index: g.name for g in body.vertex_groups}
    for g in body.vertex_groups:
        if g.name not in obj.vertex_groups:
            obj.vertex_groups.new(name=g.name)
    for v in obj.data.vertices:
        _, bi, _ = kd.find(obj.matrix_world @ v.co)
        for g in body.data.vertices[bi].groups:
            obj.vertex_groups[names[g.group]].add([v.index], g.weight, "REPLACE")


def _object(name, bm, mat, rig, body, smooth=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); o.data.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = smooth
    o.parent = rig; o.parent_type = "OBJECT"
    _weights_from(o, body)
    mod = o.modifiers.new("Armature", "ARMATURE"); mod.object = rig
    return o


def add_details(rig, human, coat, key):
    tree = BVHTree.FromPolygons([coat.matrix_world @ v.co for v in coat.data.vertices], [p.vertices[:] for p in coat.data.polygons])

    def front_y(x, z, default=-.12):
        hit = tree.ray_cast(Vector((x, -1.0, z)), Vector((0, 1, 0)))
        return hit[0].y if hit[0] is not None else default

    bones = rig.data.bones
    neck = (rig.matrix_world @ bones["neck_01"].head_local); chest = (rig.matrix_world @ bones["spine_03"].head_local); pelvis = (rig.matrix_world @ bones["pelvis"].head_local)
    white = _mat(key + "_coat_trim", (.93, .93, .91), .8); pearl = _mat(key + "_snap", (.82, .83, .85), .35, .6); black = _mat(key + "_stethoscope_tube", (.02, .02, .025), .45)
    steel = _mat(key + "_stethoscope_head", (.62, .65, .68), .3, 1.0)
    zt = neck.z - .015

    # --- patch pockets: rounded slabs proud of the coat at hip height, tilted slightly with the cloth
    for sx in (-1, 1):
        x = sx * .12; z = pelvis.z - .005; bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co = (v.co.x * .14, v.co.y * .008, v.co.z * .17)
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=.004, segments=2, affect="EDGES")
        y = front_y(x, z) - .003
        for v in bm.verts:
            v.co += Vector((x, y, z))
        _object("pocket_" + ("l" if sx > 0 else "r"), bm, white, rig, human)

    # --- notched lapels: two flat collar flaps folding down from the neck to the chest, V shaped
    for sx in (-1, 1):
        bm = bmesh.new()
        pts = [(sx * .030, zt + .010), (sx * .092, zt + .004), (sx * .122, zt - .045), (sx * .040, zt - .190), (sx * .006, zt - .150)]
        verts = [bm.verts.new((px, front_y(px, pz) - .004, pz)) for px, pz in pts]
        f = bm.faces.new(verts)
        ret = bmesh.ops.extrude_face_region(bm, geom=[f]); moved = [v for v in ret["geom"] if isinstance(v, bmesh.types.BMVert)]
        bmesh.ops.translate(bm, vec=(0, -.007, 0), verts=moved)
        _object("lapel_" + ("l" if sx > 0 else "r"), bm, white, rig, human)

    # --- collar stand behind and around the neck
    bm = bmesh.new(); ring = []
    for k in range(18):
        a = math.pi * (k / 17.0) * 1.0 + math.pi * .0
        ring.append(a)
    top, bot = [], []
    for a in ring:
        x = math.cos(a) * .072; y = neck.y + (-math.sin(a) * .05) + .012
        top.append(bm.verts.new((x, y, zt + .028))); bot.append(bm.verts.new((x * 1.05, y * 1.0, zt - .004)))
    for i in range(len(top) - 1):
        bm.faces.new((bot[i], bot[i + 1], top[i + 1], top[i]))
    _object("collar_stand", bm, white, rig, human)

    # --- centre snap placket: five pearl snaps down the front
    for i in range(5):
        z = chest.z + .035 - i * (chest.z + .035 - (pelvis.z - .08)) / 4.0
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=14, radius1=.0105, radius2=.0105, depth=.005)
        bmesh.ops.rotate(bm, verts=bm.verts[:], cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(math.pi / 2, 3, "X"))
        for v in bm.verts:
            v.co += Vector((0, front_y(0, z) - .004, z))
        _object(f"snap_{i}", bm, pearl, rig, human)

    # --- stethoscope: tube around the neck hanging in a U on the chest, silver chest piece
    pts = [(0, neck.y + .055, neck.z + .005), (.062, neck.y + .035, neck.z + .000), (.085, front_y(.085, zt - .05) - .018, zt - .060), (.062, front_y(.062, zt - .14) - .020, zt - .150),
           (.035, front_y(.035, zt - .23) - .022, zt - .235), (0, front_y(0, zt - .25) - .024, zt - .252), (-.035, front_y(-.035, zt - .23) - .022, zt - .235), (-.062, front_y(-.062, zt - .14) - .020, zt - .150),
           (-.085, front_y(-.085, zt - .05) - .018, zt - .060), (-.062, neck.y + .035, neck.z + .000)]
    cu = bpy.data.curves.new("stethoscope", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = .0065; cu.bevel_resolution = 3; cu.use_fill_caps = True
    sp = cu.splines.new("NURBS"); sp.points.add(len(pts) - 1)
    for p, c in zip(sp.points, pts):
        p.co = (c[0], c[1], c[2], 1)
    sp.use_endpoint_u = True; sp.order_u = 4; sp.use_cyclic_u = True
    tube = bpy.data.objects.new("stethoscope_tube", cu); bpy.context.collection.objects.link(tube)
    bpy.context.view_layer.objects.active = tube; tube.select_set(True); bpy.ops.object.convert(target="MESH"); tube.data.materials.append(black)
    for p in tube.data.polygons:
        p.use_smooth = True
    tube.parent = rig; _weights_from(tube, human); mod = tube.modifiers.new("Armature", "ARMATURE"); mod.object = rig
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=.024, radius2=.021, depth=.012)
    bmesh.ops.rotate(bm, verts=bm.verts[:], cent=(0, 0, 0), matrix=__import__("mathutils").Matrix.Rotation(math.pi / 2, 3, "X"))
    for v in bm.verts:
        v.co += Vector((0.0, front_y(0, zt - .27) - .028, zt - .270))
    _object("stethoscope_head", bm, steel, rig, human)
    print("COAT_DETAILS", key)
