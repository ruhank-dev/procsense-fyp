"""Richer Blender 4.5 prop kit for the PatientBay reconstruction (builds on tools/blender/procsense_bpy.py).

Same conventions as the existing props: metres, Blender Z up, front faces -Y, one export mesh per material named
EQ_<asset>_M_<Material>, COL_* / FRONT_MARKER empties kept. Differences: real rounded edges (4+ bevel segments,
auto-smooth by angle), lathe-turned parts, smooth bent steel tubes, rounded-profile extrusions, and a swatch of extra
materials (gas-port colours, chrome, laminate...). Every exported mesh must be closed (manifold) with UVs.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
import procsense_bpy as geo  # noqa: E402

geo.PALETTE.update({
    "Ivory": ((.79, .82, .78, 1), 0, .55),
    "Sage": ((.35, .49, .44, 1), 0, .6),
    "Oak": ((.66, .51, .32, 1), 0, .6),
    "Diffuser": ((.95, .98, .94, 1), 0, .4),
    "GasGreen": ((.05, .45, .12, 1), 0, .4), "GasYellow": ((.80, .62, .05, 1), 0, .4),
    "GasBlue": ((.04, .16, .55, 1), 0, .4), "GasWhite": ((.85, .86, .84, 1), 0, .4),
    "AlarmRed": ((.62, .03, .03, 1), 0, .4), "Brass": ((.62, .46, .18, 1), .9, .35),
    "Teal": ((.12, .38, .40, 1), 0, .85), "Laminate": ((.80, .72, .58, 1), 0, .55),
    "ScreenBlack": ((.01, .012, .014, 1), 0, .12),
    "Curtain": ((.57, .69, .63, 1), 0, .95), "Upholstery": ((.10, .22, .23, 1), 0, .85), "Amber": ((.64, .42, .07, 1), 0, .65),
})


def _finish(obj, part, mat, bevel=0.0, segments=4, angle=32):
    obj.name = f"EQ_{geo.ASSET}_{part}"
    obj.data.materials.clear()
    obj.data.materials.append(geo.material(mat))
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        m = obj.modifiers.new("Rounded edge", "BEVEL")
        m.width = bevel
        m.segments = segments
        m.limit_method = "ANGLE"
        m.angle_limit = math.radians(40)
        m.profile = .7
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(angle))
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=.002)
    bpy.ops.object.mode_set(mode="OBJECT")
    _bm = bmesh.new()
    _bm.from_mesh(obj.data)
    _open = sum(1 for e in _bm.edges if not e.is_manifold)
    _bm.free()
    if _open:
        print(f"OPEN_PART {obj.name} open_edges={_open}")
    return obj


def rbox(part, size, loc, mat="PlasticWhite", r=.004, seg=4):
    """Rounded box: every edge filleted with radius r."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.dimensions = size
    if min(size) < .02:
        seg = 2                                                       # small parts: fewer segments
    return _finish(o, part, mat, min(r, min(size) / 2.3), seg)


def cyl(part, a, b, radius, mat="SteelBrushed", verts=40, r=0.0, seg=3):
    d = Vector(b) - Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=d.length, location=(Vector(a) + Vector(b)) / 2)
    o = bpy.context.object
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    return _finish(o, part, mat, min(r, radius * .8, d.length / 2.4) if r else 0.0, seg)


def lathe(part, profile, loc, mat, axis="Z", verts=32, angle=32):
    """Surface of revolution. profile = [(radius, height), ...] from bottom to top; closed with flat caps on the axis."""
    bm = bmesh.new()
    pts = [(0.0, profile[0][1])] + list(profile) + [(0.0, profile[-1][1])]
    prev = None
    ring = []
    for rr, hh in pts:
        ring.append(bm.verts.new((rr, 0, hh)))
    edges = [bm.edges.new((ring[i], ring[i + 1])) for i in range(len(ring) - 1)]
    geom = ring + edges
    bmesh.ops.spin(bm, geom=geom, cent=(0, 0, 0), axis=(0, 0, 1), angle=math.tau, steps=verts, use_duplicate=False)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(part)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(part, me)
    bpy.context.collection.objects.link(o)
    o.location = loc
    if axis == "Y":
        o.rotation_euler = (math.pi / 2, 0, 0)
    elif axis == "X":
        o.rotation_euler = (0, math.pi / 2, 0)
    elif axis == "-Y":
        o.rotation_euler = (-math.pi / 2, 0, 0)
    bpy.context.view_layer.objects.active = o
    return _finish(o, part, mat, 0.0, angle=angle)


def tube(part, pts, radius, mat="SteelBrushed", corner=.04, res=10):
    """Smooth tube along a polyline with filleted corners (capped, closed)."""
    cu = bpy.data.curves.new(part, "CURVE")
    cu.dimensions = "3D"
    cu.bevel_depth = radius
    cu.bevel_resolution = 4
    cu.use_fill_caps = True
    sp = cu.splines.new("POLY")
    P = [Vector(p) for p in pts]
    out = [P[0]]
    for i in range(1, len(P) - 1):                                   # fillet each interior corner
        a, b, c = P[i - 1], P[i], P[i + 1]
        da, dc = (a - b), (c - b)
        k = min(corner, da.length / 2.2, dc.length / 2.2)
        p0 = b + da.normalized() * k
        p2 = b + dc.normalized() * k
        for t in range(res + 1):
            s = t / res
            out.append((1 - s) ** 2 * p0 + 2 * (1 - s) * s * b + s * s * p2)
    out.append(P[-1])
    sp.points.add(len(out) - 1)
    for i, p in enumerate(out):
        sp.points[i].co = (p.x, p.y, p.z, 1)
    o = bpy.data.objects.new(part, cu)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o = bpy.context.object
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)           # weld the bevel seams and cap rings
    for _ in range(4):                                                # cap both ends so the tube is a closed solid
        boundary = [e for e in bm.edges if e.is_boundary]
        if not boundary:
            break
        bmesh.ops.holes_fill(bm, edges=boundary, sides=96)
        bm.edges.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data)
    bm.free()
    return _finish(o, part, mat, 0.0, angle=50)


def extrude(part, outline, depth, loc, mat, r=.004, plane="XZ"):
    """Extrude a 2D outline by depth: plane XZ extrudes along +Y (wall panels), plane XY extrudes up along +Z (table tops)."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, 0, z) if plane == "XZ" else (x, z, 0)) for x, z in outline]
    f = bm.faces.new(vs)
    res = bmesh.ops.extrude_face_region(bm, geom=[f])
    verts = [v for v in res["geom"] if isinstance(v, bmesh.types.BMVert)]
    bmesh.ops.translate(bm, vec=(0, depth, 0) if plane == "XZ" else (0, 0, depth), verts=verts)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(part)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(part, me)
    bpy.context.collection.objects.link(o)
    o.location = loc
    bpy.context.view_layer.objects.active = o
    return _finish(o, part, mat, min(r, depth / 2.4), 3)


def rounded_rect(w, h, rad, n=6):
    """Outline points of a rounded rectangle centred on the origin (x,z)."""
    pts = []
    for cx, cz, a0 in ((w / 2 - rad, h / 2 - rad, 0), (-w / 2 + rad, h / 2 - rad, 90), (-w / 2 + rad, -h / 2 + rad, 180), (w / 2 - rad, -h / 2 + rad, 270)):
        for i in range(n + 1):
            a = math.radians(a0 + 90 * i / n)
            pts.append((cx + rad * math.cos(a), cz + rad * math.sin(a)))
    return pts


def tilt(obj, degrees, pivot, axis="X"):
    """Rotate an already-built part about a pivot, baking the rotation into its mesh."""
    from mathutils import Matrix
    m = Matrix.Translation(Vector(pivot)) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-Vector(pivot))
    obj.data.transform(obj.matrix_world.inverted() @ m @ obj.matrix_world)
    obj.data.update()
    return obj


def castor(prefix, x, y, z0=0.0, radius=.04, width=.026, mat_wheel="RubberBlack"):
    """Twin-fork castor on a stem: wheel, hub, fork plates, swivel bearing."""
    cz = z0 + radius
    cyl(f"{prefix}_wheel", (x - width / 2, y, cz), (x + width / 2, y, cz), radius, mat_wheel, 40, r=.004)
    cyl(f"{prefix}_hub", (x - width / 2 - .004, y, cz), (x + width / 2 + .004, y, cz), radius * .38, "SteelBrushed", 24, r=.002)
    for s in (-1, 1):
        rbox(f"{prefix}_fork{s}", (.005, .042, radius * 1.7), (x + s * (width / 2 + .006), y, cz + radius * .55), "PlasticGrey", .002)
    rbox(f"{prefix}_bridge", (width + .020, .042, .008), (x, y, cz + radius * 1.4), "PlasticGrey", .003)
    cyl(f"{prefix}_swivel", (x, y, cz + radius * 1.4), (x, y, cz + radius * 1.4 + .026), .014, "SteelBrushed", 24, r=.002)


def export(name, build, out_dir, tri_budget=60000, allow_open=()):
    """Join parts per material, check closed geometry + UVs, save .blend and .fbx, return a validation record."""
    geo.reset(name)
    build()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for material in sorted({o.data.materials[0].name for o in meshes}):
        group = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data.materials[0].name == material]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = f"EQ_{name}_{material}"
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    triangles = 0
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bad = sum(1 for e in bm.edges if not e.is_manifold)
        assert bad == 0 or any(a in obj.name for a in allow_open), f"{obj.name} has {bad} open edges"
        assert obj.data.uv_layers, obj.name + " lacks UVs"
        triangles += sum(len(f.verts) - 2 for f in bm.faces)
        bm.free()
    assert triangles < tri_budget, (name, triangles)
    coords = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = [round(min(c[i] for c in coords), 4) for i in range(3)]
    high = [round(max(c[i] for c in coords), 4) for i in range(3)]
    out_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(out_dir / (name + ".blend")))
    bpy.ops.export_scene.fbx(filepath=str(out_dir / (name + ".fbx")), object_types={"MESH", "EMPTY"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             use_custom_props=True, add_leaf_bones=False, bake_anim=False)
    return {"name": name, "triangles": triangles, "renderers": len(meshes), "bounds_blender": [low, high],
            "materials": sorted({o.data.materials[0].name for o in meshes})}
