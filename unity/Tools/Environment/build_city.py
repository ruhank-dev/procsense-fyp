"""The city around the hospital: one coherent exterior in world space that PatientBay's window and both corridor glass walls look onto. Blender 4.5.14.

Layers (see docs/hospital-exterior.md): A = detailed near towers (balconies' recesses, slab rings, pilasters, set-back crowns, plant rooms, tanks, antennas), B = simplified mid-distance towers,
the hospital's own podium terrace and tower shell, ground with roads/sidewalks/crosswalks, and static rush-hour traffic with street furniture. Layer C (distant skyline impostors) and the sky are built in Unity.
Everything is authored in Unity world coordinates (x east, y up, z north) and converted to Blender on output, so that the imported FBX lands at the origin with no transform.

  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_city.py
Outputs under unity/Logs/CityBuild (git-ignored): city_near.fbx, city_mid.fbx, city_ground.fbx, city_traffic.fbx, report.json.
"""
import json
import math
import os
import random
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

# CITY_VARIANT=quest (P3-EXT, 8 Nov 2026): same seed, same placement, so every tree and car stands exactly where it stands in the original; only the geometry that is too small to read from the hospital
# is reduced with distance (cars beyond QUEST_CAR_DETAIL_M lose lofted detail, trees beyond the LOD1/LOD2 radii lose twigs and some leaf cards that are replaced by larger ones). Only the traffic and tree meshes are exported,
# as city_traffic_q.fbx and city_trees_q.fbx under CityBuild/quest; the original outputs are never touched. No random number is drawn differently, so the original run is byte-for-byte unchanged.
QUEST = os.environ.get("CITY_VARIANT") == "quest"
QUEST_CAR_DETAIL_M, QUEST_CAR_WHEELS_M = 70.0, 150.0
TREE_LOD1_M, TREE_LOD2_M = 90.0, 180.0
OUT = Path(__file__).resolve().parents[2] / "Logs" / "CityBuild" / ("quest" if QUEST else ""); OUT.mkdir(parents=True, exist_ok=True)
_LOD = 0
_cardn = 0
LOD_STATS = {"cars_detailed": 0, "cars_simple": 0, "cars_far": 0, "trees_l0": 0, "trees_l1": 0, "trees_l2": 0}
rng = random.Random(2026)
HC = (4.0, 2.2)                  # hospital centre (x, z)
GROUND = -48.0                   # street level below the corridor floor (y = 0)
P, STREET = 100.0, 24.0          # block pitch and street width
MIRROR_X = True                  # Blender x = -Unity x (the FBX importer mirrors x); verified by the report's bounds against a Unity import
STYLE = {0: (1.5, 3.9), 1: (1.5, 3.9), 2: (3.0, 3.6), 3: (3.2, 3.1), 4: (2.2, 3.8), 5: (1.8, 4.0)}      # bay width, floor height of each facade tile cell


def B(x, y, z):
    return (-x if MIRROR_X else x, -z, y)


class Sink:
    """Accumulates faces per material key."""
    def __init__(self):
        self.bm = {}; self.tris = 0

    def get(self, m):
        if m not in self.bm:
            b = bmesh.new(); b.loops.layers.uv.new("UVMap"); self.bm[m] = b
        return self.bm[m]

    def face(self, m, pts, outward, uvs):
        bm = self.get(m); uvl = bm.loops.layers.uv.active
        vs = [bm.verts.new(B(*p)) for p in pts]
        ob = Vector(B(0, 0, 0)); o = (Vector(B(*outward)) - ob)
        n = (vs[1].co - vs[0].co).cross(vs[2].co - vs[0].co)
        if n.dot(o) < 0:
            vs = vs[::-1]; uvs = uvs[::-1]
        f = bm.faces.new(vs)
        for l, uv in zip(f.loops, uvs):
            l[uvl].uv = uv
        self.tris += len(vs) - 2


def box(sink, mats, x0, x1, y0, y1, z0, z1, uv=None, bottom=False):
    """mats: dict side-> material key for 'side','top','bottom'. uv(face_axis, point)->(u,v) or None for flat (palette) colour."""
    side, top = mats["side"], mats.get("top", mats["side"])
    sides = [("z-", (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (0, 0, -1)), ("z+", (x1, y0, z1), (x0, y0, z1), (x0, y1, z1), (x1, y1, z1), (0, 0, 1)),
             ("x-", (x0, y0, z1), (x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (-1, 0, 0)), ("x+", (x1, y0, z0), (x1, y0, z1), (x1, y1, z1), (x1, y1, z0), (1, 0, 0))]
    for axis, a, b, c, d, nrm in sides:
        pts = [a, b, c, d]
        sink.face(side, pts, nrm, [uv(axis, p) for p in pts] if callable(uv) else [uv] * 4)
    pts = [(x0, y1, z0), (x1, y1, z0), (x1, y1, z1), (x0, y1, z1)]
    sink.face(top, pts, (0, 1, 0), [mats.get("top_uv", lambda p: ((p[0] - x0) / 12.0, (p[2] - z0) / 12.0))(p) if callable(mats.get("top_uv", None)) else (((p[0] - x0) / 12.0), ((p[2] - z0) / 12.0)) for p in pts]
              if top != "Palette" else [mats["top_uv"]] * 4)
    if bottom:
        sink.face(side, [(x0, y0, z1), (x1, y0, z1), (x1, y0, z0), (x0, y0, z0)], (0, -1, 0), [(0, 0)] * 4)


def pal(i):
    """UV centre of palette cell i (8 x 4 cells)."""
    return (((i % 8) + .5) / 8.0, 1.0 - ((i // 8) + .5) / 4.0)


def facade_uv(style, base_y, uo=0.0, vo=0.0):
    bay, floor = STYLE[style]
    def f(axis, p):
        along = p[0] if axis.startswith("z") else p[2]
        return (along / (bay * 4.0) + uo, (p[1] - base_y) / (floor * 4.0) + vo)
    return f


def flat(cell):
    return lambda axis, p: pal(cell)


# ------------------------------------------------------------------------------------------------------------------------------------------------------------------ towers
def tower(sink, cfg, detailed):
    x, z, w, d, h, style = cfg["x"], cfg["z"], cfg["w"], cfg["d"], cfg["h"], cfg["style"]
    x0, x1, z0, z1 = x - w / 2, x + w / 2, z - d / 2, z + d / 2
    base = GROUND + .15
    floor = STYLE[style][1]; lobby = min(h * .15, 2 * floor)
    uo, vo = rng.choice([0, .25, .5, .75]), rng.choice([0, .25, .5, .75])
    fm = f"Facade{style}"
    top = base + h
    crown_h = min(h * .1, 3 * floor) if h > 70 else 0
    shaft_top = top - crown_h
    # lobby podium: a darker, slightly wider concrete base
    if detailed:
        box(sink, {"side": "Concrete", "top": "Concrete"}, x0 - 1.0, x1 + 1.0, base, base + lobby, z0 - 1.0, z1 + 1.0, uv=lambda a, p: ((p[0] + p[2]) / 8.0, p[1] / 8.0))
        box(sink, {"side": fm, "top": "Roof"}, x0, x1, base + lobby, shaft_top, z0, z1, uv=facade_uv(style, base + lobby, uo, vo))
    else:
        box(sink, {"side": fm, "top": "Roof"}, x0, x1, base, shaft_top, z0, z1, uv=facade_uv(style, base, uo, vo))
    if detailed:
        # slab rings every four floors, pilasters at the corners, a thin plinth line
        step = 4 * floor; y = base + lobby + step
        while y < shaft_top - floor:
            box(sink, {"side": "Concrete", "top": "Concrete"}, x0 - .25, x1 + .25, y, y + .55, z0 - .25, z1 + .25, uv=lambda a, p: ((p[0] + p[2]) / 6.0, p[1] / 6.0)); y += step
        if style in (2, 4, 3):
            for cx in (x0, x1):
                for cz in (z0, z1):
                    box(sink, {"side": "Concrete", "top": "Concrete"}, cx - .45, cx + .45, base + lobby, shaft_top, cz - .45, cz + .45, uv=lambda a, p: ((p[0] + p[2]) / 6.0, p[1] / 6.0))
        if style in (0, 1, 5):
            for k in range(1, int(w // 6)):
                fx = x0 + k * w / int(w // 6)
                box(sink, {"side": "Concrete", "top": "Concrete"}, fx - .12, fx + .12, base + lobby, shaft_top, z0 - .22, z0, uv=lambda a, p: (p[0] / 4.0, p[1] / 6.0))
                box(sink, {"side": "Concrete", "top": "Concrete"}, fx - .12, fx + .12, base + lobby, shaft_top, z1, z1 + .22, uv=lambda a, p: (p[0] / 4.0, p[1] / 6.0))
    # crown: set back, plant room, tank, antenna
    if crown_h:
        inset = 2.2
        box(sink, {"side": fm, "top": "Roof"}, x0 + inset, x1 - inset, shaft_top, top, z0 + inset, z1 - inset, uv=facade_uv(style, shaft_top, uo, vo))
        top_y = top
    else:
        top_y = shaft_top
    if detailed or h > 90:
        # parapet and roof equipment
        for k in range(rng.randint(2, 4)):
            ew, ed, eh = rng.uniform(2.5, 7.0), rng.uniform(2.5, 6.0), rng.uniform(1.8, 4.2)
            ex = rng.uniform(x0 + 3 + ew / 2, x1 - 3 - ew / 2) if w > 12 else x; ez = rng.uniform(z0 + 3 + ed / 2, z1 - 3 - ed / 2) if d > 12 else z
            box(sink, {"side": "Concrete", "top": "Palette", "top_uv": pal(2)}, ex - ew / 2, ex + ew / 2, top_y, top_y + eh, ez - ed / 2, ez + ed / 2, uv=lambda a, p: ((p[0] + p[2]) / 6.0, p[1] / 6.0))
        if detailed and rng.random() < .6:
            tx, tz, r, th = rng.uniform(x0 + 4, x1 - 4), rng.uniform(z0 + 4, z1 - 4), rng.uniform(1.4, 2.2), rng.uniform(2.6, 4.0)
            prism(sink, "Palette", tx, tz, r, top_y, top_y + th, 8, 14)
        if detailed and rng.random() < .75:
            mx, mz = x + rng.uniform(-w / 5, w / 5), z + rng.uniform(-d / 5, d / 5); mh = rng.uniform(8, 26)
            box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(2)}, mx - .25, mx + .25, top_y, top_y + mh, mz - .25, mz + .25, uv=flat(2))
            box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(19)}, mx - .45, mx + .45, top_y + mh, top_y + mh + .9, mz - .45, mz + .45, uv=flat(19))     # aviation light
        box(sink, {"side": "Concrete", "top": "Concrete"}, x0 + (inset if crown_h else 0) - .2, x1 - (inset if crown_h else 0) + .2, top_y - .05, top_y + .9, z0 + (inset if crown_h else 0) - .2, z0 + (inset if crown_h else 0), uv=lambda a, p: (p[0] / 6.0, p[1] / 6.0))
        box(sink, {"side": "Concrete", "top": "Concrete"}, x0 + (inset if crown_h else 0) - .2, x1 - (inset if crown_h else 0) + .2, top_y - .05, top_y + .9, z1 - (inset if crown_h else 0), z1 - (inset if crown_h else 0) + .2, uv=lambda a, p: (p[0] / 6.0, p[1] / 6.0))


def prism(sink, mat, cx, cz, r, y0, y1, n, cell):
    for k in range(n):
        a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
        p0, p1 = (cx + r * math.cos(a0), cz + r * math.sin(a0)), (cx + r * math.cos(a1), cz + r * math.sin(a1))
        mid = (cx + r * math.cos((a0 + a1) / 2), 0, cz + r * math.sin((a0 + a1) / 2))
        sink.face(mat, [(p0[0], y0, p0[1]), (p1[0], y0, p1[1]), (p1[0], y1, p1[1]), (p0[0], y1, p0[1])], (mid[0] - cx, 0, mid[2] - cz), [pal(cell)] * 4)
    sink.face(mat, [(cx + r * math.cos(2 * math.pi * k / n), y1, cz + r * math.sin(2 * math.pi * k / n)) for k in range(n)], (0, 1, 0), [pal(cell)] * n)


def plots():
    towers = []
    for i in range(-5, 6):
        for j in range(-5, 6):
            if (i, j) == (0, 0):
                continue
            ring = max(abs(i), abs(j)); cx, cz = HC[0] + i * P, HC[1] + j * P
            for sx in (-1, 1):
                for sz in (-1, 1):
                    if rng.random() > (.92 if ring == 1 else .86 if ring <= 3 else .75):
                        continue
                    w, d = rng.uniform(20, 34), rng.uniform(20, 34)
                    px, pz = cx + sx * 19 + rng.uniform(-3, 3), cz + sz * 19 + rng.uniform(-3, 3)
                    if ring == 1 and i == 0:                                      # the blocks straight ahead of the corridor's glass ends stay low so the skyline shows over them
                        h = rng.uniform(34, 62)
                    elif ring == 1:
                        h = rng.uniform(70, 210)
                    else:
                        h = rng.uniform(40, 120) * (1.0 + .55 * (1 if rng.random() < .22 else 0)) + (30 if ring == 2 else 0)
                        if ring >= 2 and rng.random() < .18:
                            h += rng.uniform(80, 150)
                    style = rng.choice([0, 1, 5, 0, 2]) if h > 100 else rng.choice([2, 3, 4, 3, 0, 4])
                    towers.append(dict(x=px, z=pz, w=w, d=d, h=h, style=style, ring=ring, block=(i, j)))
    return towers


# ------------------------------------------------------------------------------------------------------------------------------------------------------------------ ground, roads
def ground(sink):
    # one huge base plane, blocks raised a little with sidewalk edging, streets as textured strips, intersections with crosswalks
    big = 900.0
    sink.face("Ground", [(HC[0] - big, GROUND - .05, HC[1] - big), (HC[0] + big, GROUND - .05, HC[1] - big), (HC[0] + big, GROUND - .05, HC[1] + big), (HC[0] - big, GROUND - .05, HC[1] + big)], (0, 1, 0),
              [(0, 0), (1, 0), (1, 1), (0, 1)])
    half = P / 2 - STREET / 2
    span = 5
    for i in range(-span, span + 1):
        for j in range(-span, span + 1):
            cx, cz = HC[0] + i * P, HC[1] + j * P
            box(sink, {"side": "Concrete", "top": "Sidewalk"}, cx - half, cx + half, GROUND - .05, GROUND + .15, cz - half, cz + half, uv=lambda a, p: ((p[0] + p[2]) / 6.0, p[1] / 3.0),
                bottom=False)
    car = 15.0                                                                            # carriageway width within the 24 m street
    for k in range(-span, span + 1):
        sx = HC[0] + k * P + P / 2; sz = HC[1] + k * P + P / 2
        # street along z at x = sx (between block columns k and k+1)
        z0, z1 = HC[1] - span * P - P / 2, HC[1] + span * P + P / 2
        ln = z1 - z0
        sink.face("Road", [(sx - car / 2, GROUND, z0), (sx + car / 2, GROUND, z0), (sx + car / 2, GROUND, z1), (sx - car / 2, GROUND, z1)], (0, 1, 0), [(0, 0), (1, 0), (1, ln / 12.0), (0, ln / 12.0)])
        x0, x1 = HC[0] - span * P - P / 2, HC[0] + span * P + P / 2
        lnx = x1 - x0
        sink.face("Road", [(x0, GROUND, sz - car / 2), (x0, GROUND, sz + car / 2), (x1, GROUND, sz + car / 2), (x1, GROUND, sz - car / 2)], (0, 1, 0), [(0, 0), (1, 0), (1, lnx / 12.0), (0, lnx / 12.0)])
    # intersections: plain asphalt over the markings, plus crosswalk stripes
    for a in range(-span, span + 1):
        for b in range(-span, span + 1):
            ix, iz = HC[0] + a * P + P / 2, HC[1] + b * P + P / 2
            box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(16)}, ix - car / 2, ix + car / 2, GROUND - .02, GROUND + .012, iz - car / 2, iz + car / 2, uv=flat(16))
            if abs(a) <= 2 and abs(b) <= 2:
                for s in (-1, 1):
                    for t in range(-6, 7):
                        box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(30)}, ix + t * 1.1 - .3, ix + t * 1.1 + .3, GROUND + .012, GROUND + .02, iz + s * (car / 2 + 1.2) - 1.4, iz + s * (car / 2 + 1.2) + 1.4, uv=flat(30))
                        box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(30)}, ix + s * (car / 2 + 1.2) - 1.4, ix + s * (car / 2 + 1.2) + 1.4, GROUND + .012, GROUND + .02, iz + t * 1.1 - .3, iz + t * 1.1 + .3, uv=flat(30))


# ------------------------------------------------------------------------------------------------------------------------------------------------------------------ hospital podium
def podium(sink_ground, sink_near):
    # the hospital's podium terrace: a green roof with paving, planters, trees and a parapet, a few floors below the corridor; the hospital tower shell rises from it
    px0, px1, pz0, pz1, py = HC[0] - 24, HC[0] + 24, HC[1] - 36, HC[1] + 38, -15.0
    box(sink_near, {"side": "Facade1", "top": "Terrace"}, px0, px1, GROUND + .15, py, pz0, pz1, uv=facade_uv(1, GROUND + .15), bottom=False)
    box(sink_near, {"side": "Concrete", "top": "Concrete"}, px0 - .2, px1 + .2, py, py + .9, pz0 - .2, pz0 + .1, uv=lambda a, p: (p[0] / 6.0, p[1] / 6.0))
    box(sink_near, {"side": "Concrete", "top": "Concrete"}, px0 - .2, px1 + .2, py, py + .9, pz1 - .1, pz1 + .2, uv=lambda a, p: (p[0] / 6.0, p[1] / 6.0))
    box(sink_near, {"side": "Concrete", "top": "Concrete"}, px0 - .2, px0 + .1, py, py + .9, pz0, pz1, uv=lambda a, p: (p[2] / 6.0, p[1] / 6.0))
    box(sink_near, {"side": "Concrete", "top": "Concrete"}, px1 - .1, px1 + .2, py, py + .9, pz0, pz1, uv=lambda a, p: (p[2] / 6.0, p[1] / 6.0))
    # lawns and planters
    for (lx0, lx1, lz0, lz1) in ((px0 + 3, px0 + 14, pz0 + 4, pz0 + 24), (px1 - 14, px1 - 3, pz0 + 4, pz0 + 24), (px0 + 3, px0 + 14, pz1 - 24, pz1 - 4), (px1 - 14, px1 - 3, pz1 - 24, pz1 - 4)):
        box(sink_near, {"side": "Concrete", "top": "Lawn"}, lx0, lx1, py, py + .5, lz0, lz1, uv=lambda a, p: (p[0] / 6.0, p[1] / 6.0))
        for k in range(5):
            tx, tz = rng.uniform(lx0 + 1.5, lx1 - 1.5), rng.uniform(lz0 + 1.5, lz1 - 1.5)
            tree(sink_near, tx, tz, py + .5, rng.uniform(6.5, 10), 1.5)
    # the hospital tower shell below the corridor floor: what you see looking straight down from the glass
    # The shell used to rise to y = 3.9, above the corridor floor (y = 0). Its east face (x = 11.0) then stood inside the theatre, whose east end reaches x = 13.45, and showed as a blue glass wall from
    # x > 11 (found by the pose sweep, P3-EXT-11). It now stops under the hospital's floor slabs (their undersides are at y = -0.45), so no face of it is inside any room; what is seen looking down from the glass is unchanged.
    sx0, sx1, sz0, sz1 = -2.9, 11.0, -10.0, 14.5
    SHELL_TOP = -0.45
    box(sink_near, {"side": "Facade5", "top": "Roof"}, sx0, sx1, py, SHELL_TOP, sz0, sz1, uv=facade_uv(5, py))


TREES = None                      # the tree sink (Bark + Leaf), exported as city_trees.fbx; set in main()


def _ring(cx, cy, cz, r, n=7, tilt=(0, 0, 0)):
    return [(cx + r * math.cos(2 * math.pi * k / n), cy, cz + r * math.sin(2 * math.pi * k / n)) for k in range(n)]


def _limb(sink, p0, p1, r0, r1, n=6, v0=0.0):
    """A tapered n-sided limb between two points (Bark uv: u around, v along)."""
    d = Vector(p1) - Vector(p0); L = d.length
    if L < 1e-4:
        return
    if (_LOD >= 1 and n == 4) or (_LOD >= 2 and n == 5):         # twigs (LOD1) and secondary branches (LOD2) are thinner than a pixel from the hospital; the leaf cards that hide them stay
        return
    d /= L
    up = Vector((0, 1, 0)) if abs(d.y) < .95 else Vector((1, 0, 0))
    a = d.cross(up).normalized(); b = d.cross(a).normalized()
    ring0 = [Vector(p0) + (a * math.cos(2 * math.pi * k / n) + b * math.sin(2 * math.pi * k / n)) * r0 for k in range(n)]
    ring1 = [Vector(p1) + (a * math.cos(2 * math.pi * k / n) + b * math.sin(2 * math.pi * k / n)) * r1 for k in range(n)]
    for k in range(n):
        k2 = (k + 1) % n
        out = (ring0[k] + ring0[k2]) / 2 - Vector(p0)
        sink.face("Bark", [tuple(ring0[k]), tuple(ring0[k2]), tuple(ring1[k2]), tuple(ring1[k])], tuple(out), [(k / n * 2, v0), ((k + 1) / n * 2, v0), ((k + 1) / n * 2, v0 + L * .6), (k / n * 2, v0 + L * .6)])


def _card(sink, c, size, yaw, tilt, flip):
    """A leaf-cluster card centred on c: a square of side `size`, turned about the vertical by yaw and leaned by tilt (0 = upright, 90 = flat)."""
    global _cardn
    if _LOD:
        _cardn += 1
        keep, grow = (3, 1.18) if _LOD == 1 else (2, 1.38)       # keep 3 of every 5 (LOD1) or 2 of every 5 (LOD2) cards and enlarge them so the canopy covers the same area
        if _cardn % 5 >= keep: return
        size *= grow
    cy, sy = math.cos(yaw), math.sin(yaw); ct, st = math.cos(tilt), math.sin(tilt)
    h = size / 2
    pts = []
    for (u, v) in ((-h, -h), (h, -h), (h, h), (-h, h)):
        x0, y0 = u, v * ct; z0 = v * st
        pts.append((c[0] + x0 * cy - z0 * sy, c[1] + y0, c[2] + x0 * sy + z0 * cy))
    uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
    if flip: uvs = [(1 - u, v) for u, v in uvs]
    sink.face("Leaf", pts, (0, 1, 0), uvs)


def tree(sink, x, z, y, h, detail=1.0):
    """A real tree: a curved tapering trunk, a spreading scaffold of branches, and crossed foliage-cluster cards (the leaf atlas) around every branch end; canopy shape and density vary with the seed.
    Written into the tree sink whatever sink the caller passes, so every tree shares two materials (Bark, Leaf) and one draw call each."""
    global _LOD
    s = TREES
    r = rng
    dist = math.hypot(x - HC[0], z - HC[1])
    _LOD = (0 if dist < TREE_LOD1_M else 1 if dist < TREE_LOD2_M else 2) if QUEST else 0
    LOD_STATS["trees_l%d" % _LOD] += 1
    trunk_h = h * r.uniform(.30, .38); lean = (r.uniform(-.25, .25), r.uniform(-.25, .25))
    r0 = h * .028 + .05
    pts = [(x, y, z)]
    for i in range(1, 4):
        t = i / 3
        pts.append((x + lean[0] * t * t, y + trunk_h * t, z + lean[1] * t * t))
    for i in range(3):
        _limb(s, pts[i], pts[i + 1], r0 * (1 - i * .16), r0 * (1 - (i + 1) * .16), 7, v0=i)
    top = Vector(pts[-1]); canopy_r = h * r.uniform(.30, .40); canopy_h = h - trunk_h
    nb = 5 + int(r.random() * 3)
    tips = []
    for b in range(nb):
        ang = 2 * math.pi * b / nb + r.uniform(-.35, .35); rise = r.uniform(.35, .85)
        start = Vector(pts[2]) + (top - Vector(pts[2])) * r.uniform(.2, 1.0)
        reach = canopy_r * r.uniform(.55, 1.0)
        mid = start + Vector((math.cos(ang) * reach * .5, canopy_h * rise * .45, math.sin(ang) * reach * .5))
        tip = start + Vector((math.cos(ang) * reach, canopy_h * rise * .85, math.sin(ang) * reach))
        _limb(s, tuple(start), tuple(mid), r0 * .42, r0 * .30, 5); _limb(s, tuple(mid), tuple(tip), r0 * .30, r0 * .12, 4)
        tips.append((mid, tip))
        for sub in range(2):                                                                         # a twig fork near the tip
            sa = ang + r.uniform(-.8, .8); st = tip + Vector((math.cos(sa) * reach * .28, canopy_h * .10 * r.random(), math.sin(sa) * reach * .28))
            _limb(s, tuple(tip), tuple(st), r0 * .12, r0 * .06, 4); tips.append((tip, st))
    count = int((26 + h * 7) * detail)
    for i in range(count):
        a, b = tips[r.randrange(len(tips))]
        c = b + (a - b) * r.uniform(-.1, .5) + Vector((r.uniform(-1, 1), r.uniform(-.4, .9), r.uniform(-1, 1))) * (h * .075)
        size = h * r.uniform(.30, .44)
        yaw = r.uniform(0, math.pi)
        for k in range(2):                                                                            # crossed pair
            _card(s, (c.x, c.y, c.z), size, yaw + k * math.pi / 2 + r.uniform(-.2, .2), r.uniform(0, .5), r.random() < .5)
        if r.random() < .5:                                                                           # a flatter card catches the sky
            _card(s, (c.x, c.y + size * .1, c.z), size * .9, yaw, r.uniform(1.0, 1.4), r.random() < .5)


def _slab(sink, lx0, lx1, z0, z1, yb, yt0, yt1, wb0, wb1, wt0, wt1, side_cell, top_cell, tx, nrm, end0=False, end1=False):
    """A tapered slice of a vehicle: bottom half-widths wb, top half-widths wt, top heights yt0 (at z0) and yt1 (at z1)."""
    def P(x, y, z): return tx(x, y, z)
    for sgn in (-1, 1):
        sink.face("Palette", [P(sgn * wb0, yb, z0), P(sgn * wb1, yb, z1), P(sgn * wt1, yt1, z1), P(sgn * wt0, yt0, z0)], nrm(sgn, 0, 0), [pal(side_cell)] * 4)
    sink.face("Palette", [P(-wt0, yt0, z0), P(wt0, yt0, z0), P(wt1, yt1, z1), P(-wt1, yt1, z1)], nrm(0, 1, 0), [pal(top_cell)] * 4)
    if end0: sink.face("Palette", [P(-wb0, yb, z0), P(wb0, yb, z0), P(wt0, yt0, z0), P(-wt0, yt0, z0)], nrm(0, 0, -1), [pal(side_cell)] * 4)
    if end1: sink.face("Palette", [P(wb1, yb, z1), P(-wb1, yb, z1), P(-wt1, yt1, z1), P(wt1, yt1, z1)], nrm(0, 0, 1), [pal(side_cell)] * 4)


def car_detailed(sink, x, z, yaw, kind, colour):
    """A passenger car from lofted slices: a sloped bonnet and boot, a cabin with raked glass and pillars, bumpers, lamps, wheels with hubs. About 250 triangles."""
    c, s = math.cos(yaw), math.sin(yaw)
    def tx(lx, ly, lz): return (x + lx * c + lz * s, GROUND + ly, z - lx * s + lz * c)
    def nrm(dx, dy, dz): return (dx * c + dz * s, dy, -dx * s + dz * c)
    L, W = kind["len"], kind["wid"]; hw = W / 2; belt = kind["body_h"] + .1; roof = belt + kind["cabin"][2] + .12
    zf, zr = L / 2, -L / 2
    GLASS, TYRE, HUB, LAMP, TAIL, DARK = 16, 17, 20, 18, 19, 4
    # lower body: rear bumper -> boot -> belt -> bonnet -> front bumper (slices along z with their top heights)
    st = [(zr, .62), (zr + .22, .86), (zr + .75, belt - .02), (-.30 * L / 2, belt), (.30 * L / 2, belt - .03), (zf - .85, belt - .1), (zf - .25, .84), (zf, .64)]
    wd = [.86, .94, 1.0, 1.0, 1.0, .98, .93, .84]
    for i in range(len(st) - 1):
        (za, ya), (zb, yb2) = st[i], st[i + 1]
        _slab(sink, 0, 0, za, zb, .30, ya, yb2, hw * wd[i], hw * wd[i + 1], hw * wd[i] * .97, hw * wd[i + 1] * .97, colour, colour, tx, nrm, end0=(i == 0), end1=(i == len(st) - 2))
    # cabin: rear glass -> roof -> windscreen
    cw, cl, ch, off = kind["cabin"]
    zc0, zc1 = off - cl / 2, off + cl / 2
    rake = cl * .27
    cab = [(zc0, belt - .02, .90), (zc0 + rake, roof, .78), (zc1 - rake * 1.1, roof, .78), (zc1, belt - .05, .90)]
    for i in range(3):
        (za, ya, wa), (zb, yb2, wb) = cab[i], cab[i + 1]
        top_cell = GLASS if i != 1 else colour
        side_cell = GLASS
        _slab(sink, 0, 0, za, zb, belt - .02, ya, yb2, hw * wa * .90, hw * wb * .90, hw * (wa - .12) * .90, hw * (wb - .12) * .90, side_cell, top_cell, tx, nrm)
    for zp in (zc0 + rake * .55, (zc0 + zc1) / 2 + .15, zc1 - rake * .55):                           # pillars over the side glass
        for sgn in (-1, 1):
            sink.face("Palette", [tx(sgn * hw * .905, belt - .02, zp - .045), tx(sgn * hw * .905, belt - .02, zp + .045), tx(sgn * hw * .78, roof - .01, zp + .03), tx(sgn * hw * .78, roof - .01, zp - .03)], nrm(sgn, 0, 0), [pal(colour)] * 4)
    # bumpers, lamps, plate
    for zz, lc in ((zf + .005, LAMP), (zr - .005, TAIL)):
        for sgn in (-1, 1):
            sink.face("Palette", [tx(sgn * hw * .84, .62, zz), tx(sgn * hw * .55, .62, zz), tx(sgn * hw * .55, .74, zz), tx(sgn * hw * .84, .74, zz)], nrm(0, 0, 1 if zz > 0 else -1), [pal(lc)] * 4)
        sink.face("Palette", [tx(-hw * .3, .46, zz), tx(hw * .3, .46, zz), tx(hw * .3, .60, zz), tx(-hw * .3, .60, zz)], nrm(0, 0, 1 if zz > 0 else -1), [pal(DARK)] * 4)
    # wheels: octagonal tyres with a lighter hub on the outer face
    for sgn in (-1, 1):
        for zf_ in kind["axles"]:
            r_ = .34; n = 8
            cx_ = sgn * (hw - .06)
            for k in range(n):
                a0, a1 = 2 * math.pi * k / n, 2 * math.pi * (k + 1) / n
                p = [tx(cx_ - .11, .34 + r_ * math.sin(a0), zf_ + r_ * math.cos(a0)), tx(cx_ - .11, .34 + r_ * math.sin(a1), zf_ + r_ * math.cos(a1)), tx(cx_ + .11, .34 + r_ * math.sin(a1), zf_ + r_ * math.cos(a1)), tx(cx_ + .11, .34 + r_ * math.sin(a0), zf_ + r_ * math.cos(a0))]
                sink.face("Palette", p, nrm(0, math.sin((a0 + a1) / 2), math.cos((a0 + a1) / 2)), [pal(TYRE)] * 4)
            hubx = cx_ + sgn * .115
            sink.face("Palette", [tx(hubx, .34 + .21 * math.sin(2 * math.pi * k / n), zf_ + .21 * math.cos(2 * math.pi * k / n)) for k in range(n)], nrm(sgn, 0, 0), [pal(HUB)] * n)


# ------------------------------------------------------------------------------------------------------------------------------------------------------------------ traffic
def car(sink, x, z, yaw, kind, color, wheels=True):
    c, s = math.cos(yaw), math.sin(yaw)
    def tx(lx, ly, lz):
        return (x + lx * c + lz * s, GROUND + ly, z - lx * s + lz * c)
    def part(lx0, lx1, ly0, ly1, lz0, lz1, cell, top_cell=None):
        pts = [(lx0, ly0, lz0), (lx1, ly0, lz0), (lx1, ly0, lz1), (lx0, ly0, lz1)]
        # transform the 8 corners and emit 5 quads
        def P3(a, b, cc): return tx(a, b, cc)
        cs = {(0, 0, 0): P3(lx0, ly0, lz0), (1, 0, 0): P3(lx1, ly0, lz0), (1, 0, 1): P3(lx1, ly0, lz1), (0, 0, 1): P3(lx0, ly0, lz1), (0, 1, 0): P3(lx0, ly1, lz0), (1, 1, 0): P3(lx1, ly1, lz0), (1, 1, 1): P3(lx1, ly1, lz1), (0, 1, 1): P3(lx0, ly1, lz1)}
        def nrm(dx, dy, dz): return (dx * c + dz * s, dy, -dx * s + dz * c)
        faces = [([(0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0)], nrm(0, 0, -1)), ([(1, 0, 1), (0, 0, 1), (0, 1, 1), (1, 1, 1)], nrm(0, 0, 1)), ([(0, 0, 1), (0, 0, 0), (0, 1, 0), (0, 1, 1)], nrm(-1, 0, 0)),
                 ([(1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0)], nrm(1, 0, 0)), ([(0, 1, 0), (1, 1, 0), (1, 1, 1), (0, 1, 1)], nrm(0, 1, 0))]
        for k, (idx, n) in enumerate(faces):
            sink.face("Palette", [cs[i] for i in idx], n, [pal(top_cell if (top_cell is not None and k == 4) else cell)] * 4)
    L, W = kind["len"], kind["wid"]
    # wheels (the far-traffic variant replaces the separate wheel blocks by one dark under-body block: from over 150 m a wheel is under a pixel wide)
    if wheels:
        for sx in (-1, 1):
            for zf in kind["axles"]:
                part(sx * (W / 2 - .12) - .12, sx * (W / 2 - .12) + .12, 0, .62, zf - .32, zf + .32, 17)
    else:
        part(-W / 2 * .92, W / 2 * .92, 0, .34, -L / 2 * .9, L / 2 * .9, 17)
    part(-W / 2, W / 2, .30, kind["body_h"], -L / 2, L / 2, color, color)
    if kind.get("cabin"):
        cw, cl, ch, off = kind["cabin"]
        part(-cw / 2, cw / 2, kind["body_h"], kind["body_h"] + ch, off - cl / 2, off + cl / 2, 16, color)
    if kind.get("band"):                                                                   # window band (bus / van)
        part(-W / 2 - .005, W / 2 + .005, kind["body_h"] * .55, kind["body_h"] * .82, -L / 2 + .6, L / 2 - .6, 16)


KINDS = {
    "sedan": dict(len=4.6, wid=1.85, body_h=.82, axles=(-1.4, 1.4), cabin=(1.55, 2.5, .6, -.1)),
    "hatch": dict(len=4.0, wid=1.8, body_h=.85, axles=(-1.2, 1.2), cabin=(1.55, 2.3, .62, -.2)),
    "suv": dict(len=4.8, wid=1.95, body_h=1.05, axles=(-1.5, 1.5), cabin=(1.7, 3.0, .62, -.2)),
    "van": dict(len=5.4, wid=2.0, body_h=1.95, axles=(-1.7, 1.7), band=True),
    "bus": dict(len=12.0, wid=2.55, body_h=3.2, axles=(-3.8, 3.8), band=True),
    "truck": dict(len=8.5, wid=2.45, body_h=3.0, axles=(-2.8, 1.5, 2.8)),
}
BODY = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14]


def traffic(sink, count):
    car_len = 0
    span = 150.0
    def lane_positions(lo, hi):
        z = lo
        while z < hi:
            kindname = rng.choices(["sedan", "hatch", "suv", "van", "bus", "truck"], [38, 20, 20, 8, 5, 9])[0]
            L = KINDS[kindname]["len"]; gap = rng.uniform(.9, 3.2)
            yield z + L / 2, kindname
            z += L + gap
    cars = 0
    for k in range(-1, 2):                                                               # streets near the hospital
        for kind_axis in ("z", "x"):
            for lane, direction in ((-5.25, 1), (-1.75, 1), (1.75, -1), (5.25, -1)):
                for s, kindname in lane_positions(-span, span):
                    if rng.random() < .1:                                                  # a gap now and then
                        continue
                    kd = KINDS[kindname]; color = rng.choice([0, 1, 2, 3, 4, 5, 6, 7, 8, 12, 13, 0, 1, 3, 4]) if kindname not in ("bus",) else rng.choice([9, 22, 6])
                    if kind_axis == "z":
                        sx = HC[0] + k * P + P / 2; x = sx + lane; z = HC[1] + s
                        # skip the intersections' boxes for cross traffic: stop before them
                        rel = (s + P / 2) % P - P / 2
                        if abs(abs(rel) - P / 2) < 9.0 and abs(lane) > 0 and (k % 2 == 0):
                            continue
                        yaw = 0 if direction > 0 else math.pi
                    else:
                        sz = HC[1] + k * P + P / 2; z = sz + lane; x = HC[0] + s
                        rel = (s + P / 2) % P - P / 2
                        if abs(abs(rel) - P / 2) < 9.0 and (k % 2 != 0):
                            continue
                        yaw = math.pi / 2 if direction > 0 else -math.pi / 2
                    det = kindname in ('sedan', 'hatch', 'suv') and math.hypot(x - HC[0], z - HC[1]) < (QUEST_CAR_DETAIL_M if QUEST else 135)
                    far = QUEST and not det and math.hypot(x - HC[0], z - HC[1]) >= QUEST_CAR_WHEELS_M
                    LOD_STATS["cars_detailed" if det else "cars_far" if far else "cars_simple"] += 1
                    car_detailed(sink, x, z, yaw, kd, color) if det else car(sink, x, z, yaw, kd, color, wheels=not far); cars += 1
    return cars


def furniture(sink):
    n = 0
    for k in range(-1, 2):
        sx = HC[0] + k * P + P / 2
        for z in range(-150, 151, 36):
            for side in (-1, 1):
                x = sx + side * 8.2
                box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(2)}, x - .1, x + .1, GROUND + .15, GROUND + 8.4, HC[1] + z - .1, HC[1] + z + .1, uv=flat(2))
                box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(18)}, x - side * 1.7 - .35, x + .0, GROUND + 8.3, GROUND + 8.45, HC[1] + z - .2, HC[1] + z + .2, uv=flat(18)); n += 1
                if abs(z) <= 126:
                    tree(sink, x + side * 2.6, HC[1] + z + 18, GROUND + .15, rng.uniform(6.5, 9.5), .8)
        sz = HC[1] + k * P + P / 2
        for x in range(-150, 151, 36):
            for side in (-1, 1):
                zz = sz + side * 8.2
                box(sink, {"side": "Palette", "top": "Palette", "top_uv": pal(2)}, HC[0] + x - .1, HC[0] + x + .1, GROUND + .15, GROUND + 8.4, zz - .1, zz + .1, uv=flat(2)); n += 1
                if abs(x) <= 126:
                    tree(sink, HC[0] + x + 18, zz + side * 2.6, GROUND + .15, rng.uniform(6.5, 9.5), .8)
    return n


# ------------------------------------------------------------------------------------------------------------------------------------------------------------------ output
def export(name, sink):
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for key, bm in sink.bm.items():
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces) if False else None
        me = bpy.data.meshes.new(key); bm.to_mesh(me); bm.free()
        mat = bpy.data.materials.new(f"M_{key}")
        ob = bpy.data.objects.new(f"EQ_{name}_M_{key}", me); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat)
        for p in me.polygons:
            p.use_smooth = False
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=str(OUT / f"{name}.fbx"), use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


CHUNK_M = float(os.environ.get("CITY_CHUNK_M", "80"))     # cell size of the Quest variant's spatial chunks (metres); round 3 measured 80, 160, 320 and 2000 (one renderer per material)


def export_chunks(name, sink, keys=None, cell=CHUNK_M):
    """Quest variant: the same faces as export(), split into one object per (cell, material) by face centroid, so the runtime can draw only the cells that lie inside the view cone through a window.
    Faces keep their world positions (the FBX still lands at the origin); every object is named EQ_<name>_c<ix>_<iz>_M_<material key>."""
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    n = 0
    for key, bm in sink.bm.items():
        if keys is not None and key not in keys:
            continue
        uvl = bm.loops.layers.uv.active; cells = {}
        for f in bm.faces:
            c = sum((v.co for v in f.verts), Vector()) / len(f.verts)                  # Blender coordinates: x = -Unity x, y = -Unity z
            cells.setdefault((int(math.floor(-c.x / cell)), int(math.floor(-c.y / cell))), []).append(f)
        mat = bpy.data.materials.new(f"M_{key}")
        for (ix, iz), faces in cells.items():
            nb = bmesh.new(); nuv = nb.loops.layers.uv.new("UVMap")
            for f in faces:
                nf = nb.faces.new([nb.verts.new(v.co) for v in f.verts])
                for l, ol in zip(nf.loops, f.loops):
                    l[nuv].uv = ol[uvl].uv
            me = bpy.data.meshes.new(f"{key}_{ix}_{iz}"); nb.to_mesh(me); nb.free()
            ob = bpy.data.objects.new(f"EQ_{name}_c{ix}_{iz}_M_{key}", me); bpy.context.collection.objects.link(ob); ob.data.materials.append(mat)
            for p in me.polygons:
                p.use_smooth = False
            n += 1
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=str(OUT / f"{name}.fbx"), use_selection=True, object_types={"MESH"}, axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    return n


def main():
    global TREES
    TREES = Sink()
    report = {}
    ts = plots()
    near, mid = Sink(), Sink()
    for t in ts:
        (near if t["ring"] == 1 else mid).__class__
        tower(near if t["ring"] == 1 else mid, t, detailed=(t["ring"] == 1))
    gsink = Sink(); ground(gsink)
    podium(gsink, near)
    report["towers_near"] = sum(1 for t in ts if t["ring"] == 1); report["towers_mid"] = sum(1 for t in ts if t["ring"] > 1)
    xs = [t["x"] - t["w"] / 2 for t in ts if t["ring"] == 1] + [t["x"] + t["w"] / 2 for t in ts if t["ring"] == 1]
    report["near_x_min_max_unity"] = [min(xs), max(xs)]
    report["near_tris"] = near.tris; report["mid_tris"] = mid.tris; report["ground_tris"] = gsink.tris
    tsink = Sink(); report["vehicles"] = traffic(tsink, 0); report["street_poles"] = furniture(tsink); report["traffic_tris"] = tsink.tris
    report["tree_tris"] = TREES.tris; report["lod"] = LOD_STATS
    if QUEST:
        report["chunks"] = {"traffic": export_chunks("city_traffic_q", tsink), "trees": export_chunks("city_trees_q", TREES), "ground_palette": export_chunks("city_ground_q", gsink, {"Palette"}),
                            "mid_concrete": export_chunks("city_mid_q", mid, {"Concrete"})}
    else:
        export("city_trees", TREES); export("city_near", near); export("city_mid", mid); export("city_ground", gsink); export("city_traffic", tsink)
    (OUT / "report.json").write_text(json.dumps(report, indent=2))
    print("CITY", json.dumps(report))


main()
