"""Props for the owner's final reference room: three-seat sofa, palm planter, trailing vine pot, thin wall TV and three framed prints.

Same conventions as prop_kit (metres, Z up, front faces -Y, one mesh per material named EQ_<asset>_M_<Material>, closed geometry with UVs).
Run from the repo root:
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_reference_props.py
Outputs under unity/Logs/ReferenceProps (git-ignored): .blend, .fbx, validation.json. Presentation dimensions are illustrative, not a manufacturer specification.
"""
import json
import math
import random
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import cyl, lathe, rbox, tube  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = HERE.parents[1] / "Logs" / "ReferenceProps"
geo.PALETTE.update({
    "SofaFabric": ((.10, .32, .31, 1), 0, .92), "PlanterGrey": ((.50, .52, .54, 1), .85, .38), "Soil": ((.09, .07, .05, 1), 0, .95),
    "Leaf": ((.12, .36, .13, 1), 0, .55), "LeafLight": ((.22, .46, .16, 1), 0, .55), "Stem": ((.30, .24, .14, 1), 0, .7),
    "Terracotta": ((.62, .30, .17, 1), 0, .85), "TvBlack": ((.012, .013, .015, 1), 0, .3), "TvScreen": ((.004, .005, .007, 1), 0, .08),
    "FrameOak": ((.72, .56, .36, 1), 0, .5), "PrintMat": ((.94, .93, .90, 1), 0, .9), "Print1": ((1, 1, 1, 1), 0, .75), "Print2": ((1, 1, 1, 1), 0, .75), "Print3": ((1, 1, 1, 1), 0, .75),
    "SofaLeg": ((.55, .57, .58, 1), .9, .35), "PalmFrond": ((.2, .5, .2, 1), 0, .5), "PothosLeaf": ((.2, .5, .2, 1), 0, .5),
})


# ---- sofa ------------------------------------------------------------------------------------------------------------------------------------------------------
def fabric_uv(obj, metres):
    """World-scale cube projection so the weave texture repeats every `metres` regardless of part size."""
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=metres, correct_aspect=True, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def pillow(part, size, loc, mat="SofaFabric", bulge=.20, r=.045, seg=5, tilt_deg=0.0, pivot=None, uv_m=.30):
    """Padded cushion: a heavily rounded block whose faces are puffed outward, so it reads as stuffed fabric rather than a box."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object; o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    m = o.modifiers.new("round", "BEVEL"); m.width = min(r, min(size) / 2.4); m.segments = seg; m.limit_method = "NONE"; m.profile = .75
    bpy.ops.object.modifier_apply(modifier=m.name)
    sub = o.modifiers.new("sub", "SUBSURF"); sub.levels = 1; sub.render_levels = 1
    bpy.ops.object.modifier_apply(modifier=sub.name)
    cx, cy, cz = loc
    hx, hy, hz = size[0] / 2, size[1] / 2, size[2] / 2
    for v in o.data.vertices:
        nx, ny, nz = v.co.x / hx, v.co.y / hy, v.co.z / hz
        ax = max(range(3), key=lambda k: abs((nx, ny, nz)[k]))             # which face this vertex sits on
        e = [max(0.0, 1 - c * c) for c in (nx, ny, nz)]                      # 1 at the face centre, 0 at the rim
        if ax == 0: v.co.x += math.copysign(hx * bulge * e[1] * e[2], nx)
        elif ax == 1: v.co.y += math.copysign(hy * bulge * e[0] * e[2], ny)
        else: v.co.z += math.copysign(hz * bulge * e[0] * e[1], nz)
    obj = pk._finish(o, part, mat, 0.0, angle=70)
    fabric_uv(obj, uv_m)
    if tilt_deg:
        pk.tilt(obj, tilt_deg, pivot or loc, "X")
    return obj


def piping(prefix, size, loc, z_top, thick=.0035, mat="SofaFabric"):
    """Welted seam around the top edge of a cushion: four thin rounded bars."""
    x, y, z = loc
    hx, hy = size[0] / 2 - .004, size[1] / 2 - .004
    for k, (a, b, sx, sy) in enumerate([((x - hx, y - hy, z_top), (x + hx, y - hy, z_top), 1, 0), ((x - hx, y + hy, z_top), (x + hx, y + hy, z_top), 1, 0),
                                       ((x - hx, y - hy, z_top), (x - hx, y + hy, z_top), 0, 1), ((x + hx, y - hy, z_top), (x + hx, y + hy, z_top), 0, 1)]):
        cyl(f"{prefix}pipe{k}", a, b, thick, mat, 12, r=0.0)


def sofa():
    W, D = 2.05, .88
    for s in (-1, 1):                                                    # slim brushed-steel legs
        for y in (-D / 2 + .09, D / 2 - .09):
            cyl(f"leg{s}{y:.2f}", (s * (W / 2 - .09), y, 0), (s * (W / 2 - .09), y, .12), .016, "SofaLeg", 20, r=.002)
    pillow("plinth", (W, D, .17), (0, 0, .12 + .085), bulge=.03, r=.035, seg=4)
    for i, x in enumerate((-.585, 0, .585)):                             # three individually padded seat cushions with welted seams
        sz = (.572, .62, .14); lc = (x, -.10, .29 + .07 + .01)
        pillow(f"seat{i}", sz, lc, bulge=.14 + .03 * (i == 1), r=.04, seg=5)
        piping(f"s{i}", sz, lc, lc[2] + sz[2] / 2 - .004)
    for i, x in enumerate((-.585, 0, .585)):                             # three back cushions, leaned back
        sz = (.572, .15, .46); lc = (x, .305, .625)
        pillow(f"back{i}", sz, lc, bulge=.10, r=.032, seg=5, tilt_deg=-11, pivot=(x, .305, .39))
    for s in (-1, 1):                                                    # rolled arms: a padded body with a round top roll
        pillow(f"arm{s}", (.17, D, .46), (s * (W / 2 - .085), 0, .21 + .23), bulge=.10, r=.05, seg=5)
        cyl(f"roll{s}", (s * (W / 2 - .085), -D / 2 + .05, .685), (s * (W / 2 - .085), D / 2 - .05, .685), .075, "SofaFabric", 24, r=.0)
    pillow("rear", (W - .34, .13, .62), (0, D / 2 - .065, .21 + .31), bulge=.06, r=.04, seg=4)


# ---- plants --------------------------------------------------------------------------------------------------------------------------------------------------
def blade(part, base, yaw_deg, up_deg, length, width, droop, mat, thick=.0018, seg=3):
    """One leaflet: a tapered, drooping strip with real thickness (closed solid)."""
    yaw, up = math.radians(yaw_deg), math.radians(up_deg)
    d = Vector((math.cos(yaw) * math.cos(up), math.sin(yaw) * math.cos(up), math.sin(up)))
    side = Vector((-math.sin(yaw), math.cos(yaw), 0))
    bm = bmesh.new()
    left, right = [], []
    for i in range(seg + 1):
        u = i / seg
        p = Vector(base) + d * length * u - Vector((0, 0, droop * u * u))
        w = width * (math.sin(math.pi * min(1, (u * .85 + .08))) ** .8) * (1 - .15 * u)
        left.append(bm.verts.new(p + side * w / 2)); right.append(bm.verts.new(p - side * w / 2))
    for i in range(seg):
        bm.faces.new((left[i], left[i + 1], right[i + 1], right[i]))
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(part, me); bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    sm = o.modifiers.new("thick", "SOLIDIFY"); sm.thickness = thick; sm.offset = 0
    bpy.ops.object.modifier_apply(modifier=sm.name)
    return pk._finish(o, part, mat, 0.0, angle=60)


def palm_frond(tag, base, yaw, up, length, mat_a, mat_b, rnd):
    """Pinnate palm frond: a curved rachis with paired leaflets that shorten toward the tip."""
    pts = []
    yawr, upr = math.radians(yaw), math.radians(up)
    d = Vector((math.cos(yawr), math.sin(yawr), 0))
    for i in range(7):
        u = i / 6
        pts.append(Vector(base) + d * length * u * math.cos(upr) + Vector((0, 0, length * (math.sin(upr) * u - .38 * u * u))))
    tube(f"{tag}_rachis", [tuple(p) for p in pts[::2] + [pts[-1]]], .0045, "Stem", .03, 3)
    for i in range(2, 7, 1 + (i0 := 0)):
        p0 = pts[i]; u = i / 6
        ll = length * (.46 - .16 * u)
        for s in (-1, 1):
            tilt = 38 + rnd.uniform(-6, 6)
            blade(f"{tag}_lf{i}{s}", p0, yaw + s * (62 - 20 * u), tilt - 20 * u, ll, .062, ll * .30, mat_a if (i + (s > 0)) % 2 else mat_b)


def card(part, base, yaw_deg, up_deg, length, width, droop, mat, roll_deg=0.0, fold=.05, segs=8):
    """Curved, folded foliage card with its own 0..1 UVs (alpha-cutout leaf texture). Open surface by design; the material is double-sided in Unity."""
    yaw, up, roll = math.radians(yaw_deg), math.radians(up_deg), math.radians(roll_deg)
    d = Vector((math.cos(yaw), math.sin(yaw), 0))
    side0 = Vector((-math.sin(yaw), math.cos(yaw), 0))
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new("UVMap")
    rows = []
    for i in range(segs + 1):
        u = i / segs
        c = Vector(base) + d * (length * u * math.cos(up)) + Vector((0, 0, length * (math.sin(up) * u - droop * u * u)))
        tang = (Vector(base) + d * (length * (u + .01) * math.cos(up)) + Vector((0, 0, length * (math.sin(up) * (u + .01) - droop * (u + .01) ** 2))) - c).normalized()
        side = (side0 * math.cos(roll) + side0.cross(tang) * math.sin(roll)).normalized()
        nrm = side.cross(tang).normalized()
        w = width * .5 * (1.0 if u > .05 else .4 + 12 * u)
        rows.append([bm.verts.new(c + side * w * v + nrm * fold * width * (1 - abs(v)) ) for v in (-1, 0, 1)])
    for i in range(segs):
        for k in range(2):
            f = bm.faces.new((rows[i][k], rows[i][k + 1], rows[i + 1][k + 1], rows[i + 1][k]))
            for l in f.loops:
                vi = rows[i].index(l.vert) if l.vert in rows[i] else rows[i + 1].index(l.vert)
                ui = i if l.vert in rows[i] else i + 1
                l[uvl].uv = (ui / segs, .5 + (vi - 1) * .5)
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(f"EQ_{geo.ASSET}_{part}", me); bpy.context.collection.objects.link(o)
    o.data.materials.append(geo.material(mat))
    bpy.context.view_layer.objects.active = o
    for pgon in o.data.polygons: pgon.use_smooth = True
    return o


def palm():
    rnd = random.Random(11)
    lathe("planter", [(.165, 0.0), (.172, .02), (.185, .50), (.19, .54), (.172, .54), (.168, .52), (.0, .52)], (0, 0, 0), "PlanterGrey", "Z", 48)
    lathe("soil", [(.0, .525), (.17, .525), (.17, .535), (.0, .535)], (0, 0, 0), "Soil", "Z", 32)
    for k, (tx, ty, ht, lean) in enumerate([(0, 0, 1.05, 4), (.07, .05, .84, 14), (-.07, -.04, .72, -12), (.02, -.08, .6, 8)]):
        pts = [(0, 0, .53), (tx * .4, ty * .4, .53 + ht * .35), (tx, ty, .53 + ht * .78)]
        tube(f"trunk{k}", pts, .011, "Stem", .06, 8)
        top = Vector(pts[-1]); n = 9 if k == 0 else 7
        for j in range(n):
            yaw = j * 360 / n + rnd.uniform(-14, 14) + k * 23
            L = rnd.uniform(.85, 1.15) * (1.0 if k == 0 else .88)
            card(f"frond{k}_{j}", tuple(top), yaw, rnd.uniform(48, 86), L, L * .66, rnd.uniform(.30, .55), "PalmFrond", rnd.uniform(-18, 18))


def vine():
    rnd = random.Random(5)
    lathe("pot", [(.055, 0), (.075, .01), (.09, .13), (.095, .14), (.082, .14), (.0, .125)], (0, 0, 0), "Terracotta", "Z", 32)
    lathe("rim", [(.0, .122), (.080, .122), (.080, .126), (.0, .126)], (0, 0, 0), "Soil", "Z", 24)
    for k in range(8):
        ang = math.tau * k / 8 + rnd.uniform(-.2, .2)
        pts = [(math.cos(ang) * .035, math.sin(ang) * .035, .13)]
        for s in range(1, 6):
            rr = .035 + .055 * s; zz = .13 + .05 * min(s, 2) - .085 * max(0, s - 2) ** 1.25
            pts.append((math.cos(ang) * rr, math.sin(ang) * rr, zz))
        tube(f"vine{k}", pts, .0026, "Stem", .02, 5)
        for q, p in enumerate(pts[1:], 1):
            for sgn in (-1, 1):
                card(f"vl{k}_{q}_{sgn}", p, math.degrees(ang) + sgn * rnd.uniform(40, 80), rnd.uniform(-60, -20), .085, .085, .2, "PothosLeaf", rnd.uniform(-20, 20), .06, 3)


# ---- wall TV -------------------------------------------------------------------------------------------------------------------------------------------------
def wall_tv():
    W, H, T = 1.05, .62, .024
    rbox("panel", (W, T, H), (0, 0, 0), "TvBlack", .004, 4)
    rbox("screen", (W - .016, .002, H - .016), (0, -T / 2 - .0005, 0), "TvScreen", .001, 2)
    rbox("housing", (W * .55, .022, H * .62), (0, T / 2 + .008, -.01), "TvBlack", .006, 3)
    rbox("bracket", (.18, .030, .12), (0, T / 2 + .028, 0), "TvBlack", .004, 3)
    rbox("wallplate", (.20, .004, .20), (0, T / 2 + .045, 0), "TvBlack", .002, 2)


# ---- framed prints -------------------------------------------------------------------------------------------------------------------------------------------
def art(mat, w, h, y):
    """Thin closed slab whose front face (-Y) carries the full 0..1 UV range; every other face is collapsed to a point so the print texture is never smeared."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = (v.co.x * w, y + v.co.y * .003, v.co.z * h)
    uvl = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            c = l.vert.co
            l[uvl].uv = ((c.x / w) + .5, (c.z / h) + .5) if f.normal.y < -.5 else (0.0, 0.0)
    me = bpy.data.meshes.new("art"); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(f"EQ_{geo.ASSET}_art", me); bpy.context.collection.objects.link(o)
    o.data.materials.append(geo.material(mat))
    return o


def print_frame(n):
    def build():
        W, H, T = .52, .72, .022
        rbox("frame", (W, T, H), (0, 0, 0), "FrameOak", .004, 4)
        rbox("mat", (W - .05, .004, H - .05), (0, -T / 2 - .0005, 0), "PrintMat", .001, 2)
        art(f"Print{n}", W - .12, H - .12, -T / 2 - .0035)
    return build


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [("ref_sofa", sofa), ("ref_palm", palm), ("ref_vine", vine), ("ref_wall_tv", wall_tv), ("ref_print1", print_frame(1)), ("ref_print2", print_frame(2)), ("ref_print3", print_frame(3))]
    results = [pk.export(n, f, OUT, tri_budget=60000, allow_open=("PalmFrond", "PothosLeaf")) for n, f in jobs]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("REFERENCE PROPS", json.dumps([(r["name"], r["triangles"]) for r in results]))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
