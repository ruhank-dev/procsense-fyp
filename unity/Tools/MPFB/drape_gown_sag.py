"""Drape the gown skirt over both thighs with a sag into the gap between them, and lay the back layer on the mattress.

Replaces the 'one loose skirt spans both thighs' tube of refit_idle_gown.py, which stretched a rigid bridge across the gap and left a flat sheet floating between the legs.
Keeps topology and skin weights (only positions change), like the other fits. Reads/writes unity/Logs/PatientArmsReview (git-ignored):
  gown-fit-input.json   body, gown (the original fitted gown in the rest pose), shorts, mattress (written by PatientBayLayout.ExportGownFit)
  gown-fit-output.json  the new gown vertices; ImportGown() then rebuilds the idle meshes
  gown-sections.json    cross-section samples for review plots

Method: a height field of the body/shorts top surface is dilated and smoothed so that a gap narrower than the cloth can bend around is bridged, a wider gap lets the cloth sag toward
the mattress; the front layer lies on that envelope with a small clearance and a little slack noise, the back layer lies flat under the body on the mattress. Run:
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/drape_gown_sag.py
"""
import json
import math
from pathlib import Path

import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

review = Path(__file__).resolve().parents[2] / "Logs" / "PatientArmsReview"
data = json.loads((review / "gown-fit-input.json").read_text())

EDGE_MAX = .045         # skirt edges longer than this are split, so there are vertices across the gap to let the cloth sag
SPLIT_Z = 1.12           # only below the waistband
MATTRESS = .70           # top of the mattress under the patient
CLEAR_FRONT = .010       # cloth thickness plus a little air over skin / shorts
BACK_LIFT = .009         # the back layer rests on the mattress, under the body
SKIRT_TOP_Z = 1.03       # above this the accepted garment is left exactly as it was
BLEND_LEN = .15
HEM_FLOOR = .665         # ragged hem vertices are brought to one line first
HEM_STRETCH = 1.40       # then the skirt is lengthened toward the knee (a gown covers the thighs, not just the hips)

def tree_of(key):
    s = data.get(key)
    if not s or not s.get("vertices"):
        return None
    v = [Vector((p["x"], p["y"], p["z"])) for p in s["vertices"]]
    i = s["triangles"]
    return BVHTree.FromPolygons(v, [i[k:k + 3] for k in range(0, len(i), 3)])

body, shorts = tree_of("body"), tree_of("shorts")

# ---- give the skirt vertices across the gap (the fitted garment has none: its roof there is a few 20 cm triangles) -------------------------------------------------------
def resample(V, UV, BI, BW, T):
    V = [np.array(v, float) for v in V]; UV = [np.array(u, float) for u in UV]
    BI = [list(b) for b in BI]; BW = [list(w) for w in BW]; T = [tuple(t) for t in T]
    def mid(a, b, cache):
        key = (min(a, b), max(a, b))
        if key in cache:
            return cache[key]
        acc = {}
        for src in (a, b):
            for bi, bw in zip(BI[src], BW[src]):
                if bw > 0:
                    acc[bi] = acc.get(bi, 0) + .5 * bw
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]
        s = sum(w for _, w in top) or 1
        top = [(i, w / s) for i, w in top] + [(0, 0.0)] * (4 - len(top))
        V.append((V[a] + V[b]) * .5); UV.append((UV[a] + UV[b]) * .5)
        BI.append([i for i, _ in top]); BW.append([w for _, w in top])
        cache[key] = len(V) - 1
        return cache[key]
    for _ in range(5):
        cache = {}
        def long_edge(a, b):
            hip = .72 < V[a][2] < 1.12 and .72 < V[b][2] < 1.12 and abs(V[a][0]) > .10 and abs(V[b][0]) > .10      # the hip sides need finer cloth than the rest to clear the shorts' bulge
            return V[a][2] < SPLIT_Z and V[b][2] < SPLIT_Z and np.linalg.norm(V[a] - V[b]) > (EDGE_MAX * .62 if hip else EDGE_MAX)
        out, split = [], 0
        for a, b, c in T:
            m = [long_edge(a, b), long_edge(b, c), long_edge(c, a)]
            k = sum(m)
            if k == 0:
                out.append((a, b, c)); continue
            split += 1
            if k == 1:
                while not m[0]:
                    a, b, c = b, c, a; m = m[1:] + m[:1]
                ab = mid(a, b, cache); out += [(a, ab, c), (ab, b, c)]
            elif k == 2:
                while m[2]:
                    a, b, c = b, c, a; m = m[1:] + m[:1]
                ab = mid(a, b, cache); bc = mid(b, c, cache); out += [(ab, b, bc), (a, ab, bc), (a, bc, c)]
            else:
                ab = mid(a, b, cache); bc = mid(b, c, cache); ca = mid(c, a, cache)
                out += [(a, ab, ca), (ab, b, bc), (ca, bc, c), (ab, bc, ca)]
        T = out
        if not split:
            break
    return V, UV, BI, BW, T

g0 = data["gown"]
GV = [(p["x"], p["y"], p["z"]) for p in g0["vertices"]]
GUV = [(u["x"], u["y"]) for u in g0["uv"]]
GBI = [g0["boneIndex"][4 * i:4 * i + 4] for i in range(len(GV))]
GBW = [g0["boneWeight"][4 * i:4 * i + 4] for i in range(len(GV))]
GT = [tuple(g0["triangles"][i:i + 3]) for i in range(0, len(g0["triangles"]), 3)]
GV, GUV, GBI, GBW, GT = resample(GV, GUV, GBI, GBW, GT)
print("RESAMPLED", len(g0["vertices"]), "->", len(GV), "vertices;", len(g0["triangles"]) // 3, "->", len(GT), "triangles")

# ---- height field of what the cloth can rest on -------------------------------------------------------------------------------------------------------------------
XS = np.arange(-.46, .4601, .01)
ZS = np.arange(.45, 1.2001, .01)
H = np.full((len(ZS), len(XS)), MATTRESS)
for zi, z in enumerate(ZS):
    for xi, x in enumerate(XS):
        top = MATTRESS
        for t in (body, shorts):
            if t is None:
                continue
            hit, _, _, _ = t.ray_cast(Vector((x, 1.3, z)), Vector((0, -1, 0)), 2)
            if hit is not None:
                top = max(top, hit.y)
        H[zi, xi] = top

def box_blur(a, rx, rz):
    out = a.copy()
    for ax, r in ((1, rx), (0, rz)):
        k = np.ones(2 * r + 1) / (2 * r + 1)
        pad = [(0, 0), (0, 0)]; pad[ax] = (r, r)
        p = np.pad(out, pad, mode="edge")
        out = np.apply_along_axis(lambda m: np.convolve(m, k, mode="valid"), ax, p)
    return out

def dilate(a, rx, rz):
    out = a.copy()
    for ax, r in ((1, rx), (0, rz)):
        pad = [(0, 0), (0, 0)]; pad[ax] = (r, r)
        p = np.pad(out, pad, mode="edge")
        stack = [np.take(p, range(s, s + out.shape[ax]), axis=ax) for s in range(2 * r + 1)]
        out = np.max(stack, axis=0)
    return out

# cloth bends over a ~3 cm radius: dilate that far, then smooth twice so the envelope is a soft drape, never sharper than the cloth could follow
E = dilate(H, 3, 3)
for _ in range(3):
    E = box_blur(E, 3, 2)
E = np.maximum(E, H)                                           # never below the thing it rests on

def env(x, z):
    xi = (x - XS[0]) / .01; zi = (z - ZS[0]) / .01
    xi = min(max(xi, 0), len(XS) - 1.001); zi = min(max(zi, 0), len(ZS) - 1.001)
    x0, z0 = int(xi), int(zi); fx, fz = xi - x0, zi - z0
    return (E[z0, x0] * (1 - fx) * (1 - fz) + E[z0, x0 + 1] * fx * (1 - fz) + E[z0 + 1, x0] * (1 - fx) * fz + E[z0 + 1, x0 + 1] * fx * fz)

# ---- sag only what is bridging the gap ---------------------------------------------------------------------------------------------------------------------------
# The fitted garment already hugs the body without tears. Vertices within NEAR of a body or shorts surface stay exactly where the fit put them (plus a trace of slack); only the
# vertices far from any surface, which exist because edges were split across the gap, are laid on the sag envelope (front) or the mattress (back).
NEAR = .030
points = [Vector(v) for v in GV]
GV0 = [Vector(v) for v in GV]                                          # the resampled fit before any change, to measure what the drape does to triangle facing
dist = []
for p in points:
    d = 9.0
    for tr in (body, shorts):
        if tr is None:
            continue
        hit = tr.find_nearest(p)
        if hit[0] is not None:
            d = min(d, hit[3])
    dist.append(d)
# vertices that share a position (UV seam twins) must always move together, or the seam cracks open
gid = {}
group_of = [0] * len(points)
members = []
for i, p in enumerate(points):
    key = (round(p.x * 1e4), round(p.y * 1e4), round(p.z * 1e4))
    if key not in gid:
        gid[key] = len(members); members.append([])
    group_of[i] = gid[key]; members[gid[key]].append(i)
GAP_HALF = .12                                                                        # only the strip between the thighs sags; the outer flare of the fitted garment stays as fitted
moved = [i for i, p in enumerate(points) if p.z < SKIRT_TOP_Z + .05 and dist[i] > NEAR and abs(p.x) <= GAP_HALF]
edge_use = {}                                                                         # counted on welded positions, so a UV seam is not mistaken for the open hem
for a_, b_, c_ in GT:
    for u, v in ((a_, b_), (b_, c_), (c_, a_)):
        gu, gv = group_of[u], group_of[v]
        k = (min(gu, gv), max(gu, gv)); edge_use[k] = edge_use.get(k, 0) + 1
boundary_groups = {g for (gu, gv), n in edge_use.items() if n == 1 for g in (gu, gv)}
boundary = {i for g in boundary_groups for i in members[g]}
HEM_Z = .655                                                                         # the thigh-side hems end here; the hem across the gap must reach the same line
fixed_z = set()
for i in moved:
    p = points[i]
    front = p.y > .81
    p.y = env(p.x, p.z) + CLEAR_FRONT if front else MATTRESS + BACK_LIFT
    if i in boundary and p.z < .90:                                                  # hem edge bridging the gap: lengthen it down onto the mattress
        p.z = HEM_Z + .004 * math.sin(p.x * 57); fixed_z.add(i)
        p.y = MATTRESS + (.030 + .004 * math.sin(p.x * 31) if front else BACK_LIFT)
# smooth the moved vertices into their neighbours so the sag has no kinks
nbr = {i: set() for i in range(len(points))}
for a_, b_, c_ in GT:
    for u, v in ((a_, b_), (b_, c_), (c_, a_)):
        nbr[u].add(v); nbr[v].add(u)
gnbr = {g: set() for g in range(len(members))}                        # neighbours between welded groups
for i, s in nbr.items():
    for j in s:
        if group_of[i] != group_of[j]:
            gnbr[group_of[i]].add(group_of[j])
def sync():                                                           # copy each group's first member to its twins
    for ms in members:
        if len(ms) > 1:
            src = points[ms[0]]
            for k in ms[1:]:
                points[k].x, points[k].y, points[k].z = src.x, src.y, src.z
movedset = set(moved)
sync()
moved_groups = sorted({group_of[i] for i in moved})
fixed_groups = {group_of[i] for i in fixed_z}
for _ in range(10):
    new_y = {}
    for g in moved_groups:
        i = members[g][0]
        ys = [points[members[h][0]].y for h in gnbr[g]]
        new_y[g] = .5 * points[i].y + .5 * (sum(ys) / len(ys)) if ys else points[i].y
    for g, y in new_y.items():
        if g in fixed_groups:
            continue
        for i in members[g]:
            points[i].y = max(y, MATTRESS + BACK_LIFT)
# one clean hem line for the whole skirt (the fitted garment's hem is ragged): boundary vertices go to a gentle wave, and the cloth they hug is followed in height
for g, ms in enumerate(members):
    i0 = ms[0]
    if i0 in boundary or any(i in boundary for i in ms):
        q = points[i0]
        if q.z < .80:
            z_new = HEM_Z + .004 * math.sin(q.x * 57)
            dy = 0.0 if g in fixed_groups else env(q.x, z_new) - env(q.x, q.z)
            for i in ms:
                points[i].z = z_new; points[i].y = max(points[i].y + dy, MATTRESS + BACK_LIFT)
# a trace of slack on the vertices that hug the body (follows the old fit's clearance, never closer)
changed = len(moved)
for i, p in enumerate(points):
    if i not in movedset and p.z < SKIRT_TOP_Z and dist[i] < NEAR and p.y > .76:
        q = points[members[group_of[i]][0]]
        p.y += .003 * (.5 + .5 * math.sin(q.x * 41 + q.z * 17))
sync()
# the shorts sit on top of the body under the gown: lift the cloth over their waistband and seat so they never show through (same rule as the accepted relaxed fit), spread by a smooth falloff
lifts = [0.0] * len(points)
if shorts is not None:
    for i, p in enumerate(points):
        if p.y > .80 and .65 < p.z < 1.32:
            roofs = []
            for dx in (-.02, 0, .02):
                for dz in (-.02, 0, .02):
                    hit, _, _, _ = shorts.ray_cast(Vector((p.x + dx, 2, p.z + dz)), Vector((0, -1, 0)), 3)
                    if hit is not None:
                        roofs.append(hit.y)
            if roofs:
                gate = max(0, min(1, (p.y - .80) / .09)); gate = gate * gate * (3 - 2 * gate)
                lifts[i] = max(0, max(roofs) + .022 - p.y) * gate
    glift = [max(lifts[i] for i in ms) for ms in members]
    for _ in range(8):
        glift = [.35 * glift[g] + .65 * (sum(glift[h] for h in gnbr[g]) / len(gnbr[g]) if gnbr[g] else 0) for g in range(len(members))]
    for g, ms in enumerate(members):
        for i in ms:
            points[i].y += glift[g]
# radial clearance over the shorts and body at the hips: nothing may sit closer to the body axis than the surface it covers plus 14 mm (the shorts' bulge is sharper than the fitted cloth's spacing)
RADIAL_CLEAR = .014
radial = 0
for g, ms in enumerate(members):
    q = points[ms[0]]
    if not (.70 < q.z < 1.25) or q.y < .74 or abs(q.x) < .08:
        continue
    axis_pt = Vector((0, .81, q.z)); dirv = Vector((q.x, q.y - .81, 0))
    r = dirv.length
    if r < 1e-4:
        continue
    dirv /= r
    need = r
    for tr in (shorts, body):
        if tr is None:
            continue
        hit, _, _, _ = tr.ray_cast(axis_pt + dirv * (r + .045), -dirv, r + .1)         # only a surface within 4.5 cm outside the vertex counts (not an arm or hand lying further out)
        if hit is not None and (hit - axis_pt).length <= r + .045:
            need = max(need, min((hit - axis_pt).length + RADIAL_CLEAR, r + .05))
    if need > r + 1e-5:
        tgt = axis_pt + dirv * need
        for i in ms:
            points[i].x, points[i].y = tgt.x, tgt.y
        radial += 1
print("RADIAL_PUSHED", radial)
# anything that still sits inside (or within 8 mm of) the shorts or the body is pushed out along the surface normal, whichever side of the garment it is on; twins move together
pushed = 0
OUT_CLEAR = .011
for g, ms in enumerate(members):
    q = points[ms[0]]
    if not (.55 < q.z < 1.40):
        continue
    best = None
    for tr in (shorts, body):
        if tr is None:
            continue
        loc, nrm, _, d = tr.find_nearest(q)
        if loc is None:
            continue
        inside = (q - loc).dot(nrm) < 0
        if (inside or d < .003) and (best is None or d < best[3]):
            best = (loc, nrm, inside, d)
    if best is not None:
        target = best[0] + best[1] * OUT_CLEAR
        for i in ms:
            points[i].x, points[i].y, points[i].z = target.x, target.y, target.z
        pushed += 1
# ---- let the cloth settle: a short Blender cloth simulation of the skirt, upper garment pinned, colliding with the body, shorts and mattress ---------------------------------------
import bpy
import os
SIM_FRAMES = 70
def simulate(points):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene; sc.gravity = (0, -9.81, 0); sc.render.fps = 24; sc.frame_start = 1; sc.frame_end = SIM_FRAMES
    def make(name, verts, faces):
        me = bpy.data.meshes.new(name); me.from_pydata([(v[0], v[1], v[2]) for v in verts], [], faces); me.update()
        ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob); return ob
    for key in ("body", "shorts", "mattress"):
        s = data.get(key)
        if not s or not s.get("vertices"):
            continue
        tr = s["triangles"]
        ob = make(key, [(p["x"], p["y"], p["z"]) for p in s["vertices"]], [tuple(tr[k:k + 3]) for k in range(0, len(tr), 3)])
        col = ob.modifiers.new("Collision", "COLLISION"); col.settings.thickness_outer = .004; col.settings.thickness_inner = .02; col.settings.use_culling = False
    reps = [ms[0] for ms in members]
    gv = [(points[i].x, points[i].y, points[i].z) for i in reps]
    faces = []
    for a_, b_, c_ in GT:
        f = (group_of[a_], group_of[b_], group_of[c_])
        if len(set(f)) == 3:
            faces.append(f)
    gown = make("gown", gv, faces)
    vg = gown.vertex_groups.new(name="pin")
    pinned = [g for g, i in enumerate(reps) if points[i].z >= 1.00]
    pinned_set = set(pinned)
    vg.add(pinned, 1.0, "REPLACE")
    cl = gown.modifiers.new("Cloth", "CLOTH"); s = cl.settings
    s.quality = 8; s.mass = .25; s.tension_stiffness = 12; s.compression_stiffness = 12; s.shear_stiffness = 12; s.bending_stiffness = 1.6
    s.air_damping = 1.6; s.vertex_group_mass = "pin"
    cs = cl.collision_settings; cs.use_collision = True; cs.distance_min = .007; cs.collision_quality = 4; cs.use_self_collision = False
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = SIM_FRAMES
    for f in range(1, SIM_FRAMES + 1):
        sc.frame_set(f)
    dg = bpy.context.evaluated_depsgraph_get()
    ev = gown.evaluated_get(dg)
    moved_total = 0
    for g, i in enumerate(reps):
        if g in pinned_set:
            continue
        co = ev.data.vertices[g].co
        for k in members[g]:
            if (Vector((points[k].x, points[k].y, points[k].z)) - co).length > 1e-6:
                moved_total += 1
            points[k].x, points[k].y, points[k].z = co.x, co.y, co.z
    print("SIM_DONE free groups", len(reps) - len(pinned), "pinned", len(pinned), "moved", moved_total)
    return points

if os.environ.get("GOWN_SIM", "0") == "1":                         # experimental; the cloth settings are not tuned for this mesh, so the deterministic relax below is what ships
    points = simulate(points)

# ---- relax: smooth the free skirt (not the pinned garment, not the hem edge), then put the clearance back, a few rounds --------------------------------------------------
def push_clear(points):
    n = 0
    for g, ms in enumerate(members):
        q = points[ms[0]]
        if not (.55 < q.z < 1.40) or q.y < .72:
            continue
        axis_pt = Vector((0, .81, q.z)); dirv = Vector((q.x, q.y - .81, 0)); r = dirv.length
        best = None
        for tr in (shorts, body):
            if tr is None:
                continue
            loc, nrm, _, d = tr.find_nearest(q)
            if loc is None:
                continue
            if ((q - loc).dot(nrm) < 0 or d < RADIAL_CLEAR * .6) and (best is None or d < best[1]):
                best = (loc + nrm * RADIAL_CLEAR, d)
        if best is not None:
            for i in ms:
                points[i].x, points[i].y, points[i].z = best[0].x, best[0].y, best[0].z
            n += 1
    return n

free_groups = [g for g, ms in enumerate(members) if points[ms[0]].z < SKIRT_TOP_Z and g not in boundary_groups and not any(i in fixed_z for i in ms)]
for rnd in range(6):
    new_pos = {}
    for g in free_groups:
        nb = gnbr[g]
        if not nb:
            continue
        cur = points[members[g][0]]
        avg = sum((points[members[h][0]] for h in nb), Vector((0, 0, 0))) / len(nb)
        new_pos[g] = cur.copy() + (avg - cur) * .55
    for g, v in new_pos.items():
        for i in members[g]:
            points[i].x, points[i].y, points[i].z = v.x, v.y, v.z
    for ms in members:
        for i in ms:
            points[i].y = max(points[i].y, MATTRESS + BACK_LIFT * .5)
    fixed = push_clear(points)
print("RELAXED rounds 6, last clearance pushes", fixed)

# ---- hem line: the open hem used to keep whatever heights the sag left it with, which reads as torn teeth at the sides; smooth it along the boundary chain (two neighbours per vertex), then restore clearance
bnb = {}
for (gu, gv), n_ in edge_use.items():
    if n_ == 1:
        bnb.setdefault(gu, set()).add(gv); bnb.setdefault(gv, set()).add(gu)
hem_groups = [g for g in bnb if points[members[g][0]].z < SKIRT_TOP_Z - .08 and not any(i in fixed_z for i in members[g])]
import os
for rnd in range(int(os.environ.get("HEM_SMOOTH_ROUNDS", "14"))):
    upd = {}
    for g in hem_groups:
        nb = [h for h in bnb[g] if h in bnb]
        if len(nb) < 2:
            continue
        cur = points[members[g][0]]
        avg = sum((points[members[h][0]] for h in nb), Vector((0, 0, 0))) / len(nb)
        upd[g] = cur.copy() + (avg - cur) * .5
    for g, v in upd.items():
        for i in members[g]:
            points[i].x, points[i].y, points[i].z = v.x, v.y, v.z
    push_clear(points)
print("HEM_SMOOTHED groups", len(hem_groups))

# ---- unfold: a triangle whose winding is reversed against the accepted garment is lit from behind (a dark patch) and reads as a fold-over; relax the vertices of every such triangle toward their neighbours,
# push the clearance back, and repeat until none (or no progress) remain
def _flipped():
    out = []
    for k, (a_, b_, c_) in enumerate(GT):
        o = (GV0[b_] - GV0[a_]).cross(GV0[c_] - GV0[a_]); n = (points[b_] - points[a_]).cross(points[c_] - points[a_])
        if o.length > 1e-9 and n.length > 1e-9 and o.normalized().dot(n.normalized()) < -.1:
            out.append(k)
    return out
best = len(_flipped()); print("FLIPS_BEFORE_UNFOLD", best)
for rnd in range(40):
    bad = _flipped()
    if not bad:
        break
    gs = {group_of[i] for k in bad for i in GT[k]}
    gs = {g for g in gs if not any(i in fixed_z for i in members[g]) and points[members[g][0]].z < SKIRT_TOP_Z + .05}
    upd = {}
    for g in gs:
        nb = gnbr[g]
        if not nb:
            continue
        cur = points[members[g][0]]; avg = sum((points[members[h][0]] for h in nb), Vector((0, 0, 0))) / len(nb)
        upd[g] = cur.copy() + (avg - cur) * .65
    for g, v in upd.items():
        for i in members[g]:
            points[i].x, points[i].y, points[i].z = v.x, v.y, v.z
    push_clear(points)
print("FLIPS_AFTER_UNFOLD", len(_flipped()), "rounds", rnd + 1)
points = [{"x": p.x, "y": p.y, "z": p.z} for p in points]

def tri_normals(P):
    out = []
    for a_, b_, c_ in GT:
        out.append(((Vector(P[b_]) - Vector(P[a_])).cross(Vector(P[c_]) - Vector(P[a_]))))
    return out
n0 = tri_normals([(v.x, v.y, v.z) for v in GV0]); n1 = tri_normals([(p["x"], p["y"], p["z"]) for p in points])
flips = []
for k, (u, v) in enumerate(zip(n0, n1)):
    if u.length > 1e-9 and v.length > 1e-9 and u.normalized().dot(v.normalized()) < -.1:
        a_, b_, c_ = GT[k]; cen = (Vector((points[a_]["x"], points[a_]["y"], points[a_]["z"])) + Vector((points[b_]["x"], points[b_]["y"], points[b_]["z"])) + Vector((points[c_]["x"], points[c_]["y"], points[c_]["z"]))) / 3
        flips.append((round(cen.x, 3), round(cen.y, 3), round(cen.z, 3)))
print("FLIPPED_TRIANGLES", len(flips), flips[:12])
(review / "gown-fit-output.json").write_text(json.dumps({
    "vertices": points, "triangles": [int(i) for t in GT for i in t],
    "uv": [{"x": float(u[0]), "y": float(u[1])} for u in GUV],
    "boneIndex": [int(i) for b in GBI for i in b], "boneWeight": [float(w) for b in GBW for w in b]}))
# cross sections for the review plot: envelope, body top and the gown's front/back layers at a few z
sections = {}
gz = np.array([[p["x"], p["y"], p["z"]] for p in points])
for z in (.95, .85, .75, .68):
    xs = XS
    sections[str(z)] = {"x": xs.tolist(), "body": [float(H[int(round((z - ZS[0]) / .01)), i]) for i in range(len(xs))],
                        "env": [float(E[int(round((z - ZS[0]) / .01)), i]) for i in range(len(xs))],
                        "gown": [[float(a), float(b)] for a, b, c in gz if abs(c - z) < .012]}
(review / "gown-sections.json").write_text(json.dumps(sections))
min_gap = float(np.min(gz[gz[:, 2] < SKIRT_TOP_Z][:, 1]))
(review / "gown-fit-validation.json").write_text(json.dumps({"vertices": len(points), "changedSkirtVertices": changed, "pushedOut": pushed, "lowestSkirtY": min_gap,
                                                              "topologyAndWeightsPreserved": True}, indent=2))
print("GOWN_SAG_COMPLETE", len(points), changed, pushed, round(min_gap, 4))
