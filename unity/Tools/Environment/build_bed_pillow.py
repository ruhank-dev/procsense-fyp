"""Hospital pillow by offline cloth simulation (Blender 4.5.14). Only the frozen result ships; nothing simulates at runtime.

A pre-shaped, pressure-inflated pillowcase (two quad layers welded at the seam) is dropped on the mattress while the real
patient head/neck/hair proxy (exported from Unity by HospitalBedReview) presses down on it. The final frame gives the head
compression and the irregular folds. Output: unity/Logs/HospitalBed/pillow.json, patient_pillow.obj (mirrored for the Unity
OBJ importer), pillow_*.png, pillow_validation.json, pillow.blend.

  blender-4.5.14 -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_bed_pillow.py -- [--hex #E7E9E5]
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bed_common as bc  # noqa: E402

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HEX = argv[argv.index("--hex") + 1] if "--hex" in argv else "#E7E9E5"
PRESSURE = float(argv[argv.index("--pressure") + 1]) if "--pressure" in argv else 9.0
VOLUME = float(argv[argv.index("--volume") + 1]) if "--volume" in argv else 1.05
PROXY = bc.OUT.parent / "BedReview" / "patient_head_proxy.obj"
CX, CZ = 0.0, 1.75                      # pillow centre (Unity x, z); the board face is at z = 1.896
PW, PD = .56, .30                       # case size across and along the bed
NX, NZ = 22, 14
Z_TOP = bc.Y_TOP                        # the mattress cover top (Unity y), Blender z
P_SQ = 5.0                              # corner squareness of the outline
GAP = .0035


def to_b(x, y, z):                      # Unity world -> Blender bed-local
    return (x, z - 1.0, y)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 96


def head_proxy():
    verts, faces = [], []
    for line in open(PROXY):
        p = line.split()
        if p and p[0] == "v":
            verts.append(to_b(*map(float, p[1:4])))
        elif p and p[0] == "f":
            a, b, c = (int(i) - 1 for i in p[1:4])
            faces.append((a, c, b))      # mirrored axes flip the winding
    mesh = bpy.data.meshes.new("HEAD")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new("HEAD", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_add(type="COLLISION")
    obj.collision.thickness_outer = .0015
    obj.collision.use_culling = False
    obj.location.z = .12                 # starts clear of the pillow, settles to its true resting position
    obj.keyframe_insert("location", frame=1)
    obj.location.z = 0.0
    obj.keyframe_insert("location", frame=44)
    return obj


def mattress_collider():
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, Z_TOP - .05))
    m = bpy.context.object
    m.dimensions = (1.4, 2.4, .1)
    bpy.ops.object.modifier_add(type="COLLISION")
    m.collision.thickness_outer = .002
    return m


def shape():
    """Pre-inflated two-layer pillowcase on a superellipse outline; boundary vertices are shared (the seam)."""
    bm = bmesh.new()
    top, bot = {}, {}

    def rr(u, v):
        n = (abs(u) ** P_SQ + abs(v) ** P_SQ) ** (1 / P_SQ)
        s = max(abs(u), abs(v)) / n if n > 1e-9 else 1.0
        return u * s * PW / 2, v * s * PD / 2, n * s                      # last: radial 0..1 (1 at the seam)

    rng = np.random.default_rng(3)
    ph = rng.uniform(0, 6.28, 6)
    SEAM = .042                                   # the seam sits at mid-height: both layers bulge away from it, edge pinches flat
    topE, botE = {}, {}
    LIP = .002                                      # half-thickness of the seam flange, which also stands 1.2 cm proud
    for i in range(NX + 1):
        for j in range(NZ + 1):
            u, v = i / NX * 2 - 1, j / NZ * 2 - 1
            x, y, r = rr(u, v)
            edge = (i in (0, NX)) or (j in (0, NZ))
            z0 = Z_TOP + GAP
            bump = (.006 * math.sin(5.1 * u + ph[0]) * math.sin(4.3 * v + ph[1]) + .004 * math.sin(8.7 * u + ph[2] + 2 * v)
                    + .003 * math.sin(11 * v + ph[3] - 3 * u))
            lobes = 1 + .55 * u * u                  # much fuller outer lobes around a shallower cradle
            ht = 0.0 if edge else (.068 * lobes * (1 - min(r, 1) ** 2.3) ** .6 * (1 + .12 * v + .06 * math.sin(3 * u + ph[4]))
                                   + bump * (1 - r ** 2))
            if edge:
                ox, oy = x * 1.045, y * 1.045        # the flange stands proud of the stitched edge
                topE[i, j] = bm.verts.new((CX + ox, CZ - 1.0 + oy, z0 + SEAM + LIP))
                botE[i, j] = bm.verts.new((CX + ox, CZ - 1.0 + oy, z0 + SEAM - LIP))
                top[i, j], bot[i, j] = topE[i, j], botE[i, j]
            else:
                top[i, j] = bm.verts.new((CX + x, CZ - 1.0 + y, z0 + SEAM + ht))
                bot[i, j] = bm.verts.new((CX + x, CZ - 1.0 + y, z0 + .006 + (SEAM - .006) * r ** 2.6))
    for i in range(NX):
        for j in range(NZ):
            bm.faces.new((top[i, j], top[i + 1, j], top[i + 1, j + 1], top[i, j + 1]))
            bm.faces.new((bot[i, j], bot[i, j + 1], bot[i + 1, j + 1], bot[i + 1, j]))
    loop = ([(i, 0) for i in range(NX)] + [(NX, j) for j in range(NZ)] + [(i, NZ) for i in range(NX, 0, -1)]
            + [(0, j) for j in range(NZ, 0, -1)])
    for k, a_ in enumerate(loop):
        b_ = loop[(k + 1) % len(loop)]
        bm.faces.new((topE[a_], topE[b_], botE[b_], botE[a_]))                # seam strip closes the case
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("PILLOW")
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("PILLOW", mesh)
    bpy.context.collection.objects.link(obj)
    return obj


def volume(mesh):
    bm = bmesh.new()
    bm.from_mesh(mesh)
    v = bm.calc_volume(signed=True)
    bm.free()
    return abs(v)


def simulate(p):
    bpy.context.view_layer.objects.active = p
    bpy.ops.object.modifier_add(type="CLOTH")
    s = p.modifiers["Cloth"].settings
    s.quality = 12
    s.mass = .22
    s.air_damping = 1.2
    s.tension_stiffness, s.compression_stiffness, s.shear_stiffness = 18, 18, 6
    s.bending_stiffness, s.bending_damping = 1.2, .6
    s.use_pressure = True
    s.use_pressure_volume = True
    s.target_volume = volume(p.data) * VOLUME
    s.pressure_factor = 1.0
    s.uniform_pressure_force = PRESSURE
    c = p.modifiers["Cloth"].collision_settings
    c.use_collision = True
    c.distance_min = .002
    c.collision_quality = 4
    c.use_self_collision = False
    sc = bpy.context.scene
    for f in range(1, sc.frame_end + 1):
        sc.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = p.evaluated_get(dg)
    mesh = ev.to_mesh()
    verts = [Vector(v.co) for v in mesh.vertices]
    polys = [tuple(pl.vertices) for pl in mesh.polygons]
    ev.to_mesh_clear()
    return verts, polys


def main():
    reset()
    head, mat_col = head_proxy(), mattress_collider()
    p = shape()
    v0 = volume(p.data)
    verts, polys = simulate(p)
    # safety projection: nothing may sit inside the head or below the cover (collision margins can leak)
    sc = bpy.context.scene
    sc.frame_set(sc.frame_end)
    dg = bpy.context.evaluated_depsgraph_get()
    hv = [head.matrix_world @ Vector(v.co) for v in head.data.vertices]
    bvh = BVHTree.FromPolygons(hv, [tuple(pl.vertices) for pl in head.data.polygons])
    fixed, min_gap = 0, 9.0
    for k, co in enumerate(verts):
        hit = bvh.find_nearest(co)
        if hit and hit[0] is not None:
            loc, nrm, _, dist = hit
            side = (co - loc).dot(nrm)
            if side < .0012:                      # inside (or touching) the head: push out along its normal
                verts[k] = loc + nrm * .0015
                fixed += 1
            else:
                min_gap = min(min_gap, side)
        if verts[k].z < Z_TOP + .0012:
            verts[k].z = Z_TOP + .0012
    pts = np.array([(v.x, v.z, v.y + 1.0) for v in verts])          # Unity axes (x, y up, z along)
    tris = []
    for a, b, c, d in polys:                                            # mirror flips winding
        tris += [a, c, b, a, d, c]
    bc.OUT.mkdir(parents=True, exist_ok=True)
    np.save(bc.OUT / "pillow_verts.npy", pts)
    (bc.OUT / "pillow_tris.json").write_text(json.dumps(tris))
    report = {"triangles": len(tris) // 3, "volume_before_l": round(v0 * 1000, 2),
              "crown_y": round(float(pts[:, 1].max()), 4), "min_y": round(float(pts[:, 1].min()), 4),
              "vertices_pushed_out_of_head": fixed, "min_gap_to_head_m": round(min_gap, 4),
              "z_range": [round(float(pts[:, 2].min()), 3), round(float(pts[:, 2].max()), 3)],
              "top_pct_y": [round(float(q), 3) for q in np.percentile(pts[:, 1], [50, 90, 99])],
              "pressure": PRESSURE, "volume_factor": VOLUME}
    (bc.OUT / "pillow_sim_report.json").write_text(json.dumps(report, indent=2) + "\n")
    bpy.ops.wm.save_as_mainfile(filepath=str(bc.OUT / "pillow.blend"))
    print("PILLOW SIM", json.dumps(report))


main()
