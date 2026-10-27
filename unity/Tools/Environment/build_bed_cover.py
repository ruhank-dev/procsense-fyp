"""Blue mattress cover for the PatientBay bed (Blender 4.5.14 Python; numpy). Replaces the old mattress + sheet pair.

Quad-grid top (Coons patch stitched to the outline) + wrapped edge rings + tucked hem, displaced by the fold field in
bed_cover_fields.py, with baked albedo/normal/occlusion maps. Output: unity/Logs/HospitalBed/bed_cover.json (flat
arrays for the Editor), cover_*.png, bed_cover.blend (preview) and bed_cover_validation.json.

  blender-4.5.14 -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_bed_cover.py -- [--hex #RRGGBB]
"""
import json
import math
import sys

import bpy
import numpy as np

sys.path.insert(0, str(__import__("pathlib").Path(__file__).resolve().parent))
import bed_common as bc          # noqa: E402
import bed_cover_fields as cf    # noqa: E402

A_LONG, B_SHORT, N_ARC = 29, 13, 8
R_EDGE, R_CORNER = cf.R_EDGE, cf.R_CORNER
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
HEX = argv[argv.index("--hex") + 1] if "--hex" in argv else "#6180A4"


def outline():
    """Rounded-rectangle samples, counter-clockwise from the +x side: [(x, z, nx, nz, arclen)]; fixed counts so a quad grid fits."""
    cx, cz = bc.HW - R_CORNER, bc.HL - R_CORNER
    pts = []

    def straight(p, q, n):
        pts.extend((p[0] + (q[0] - p[0]) * i / n, p[1] + (q[1] - p[1]) * i / n) for i in range(n))

    def arc(c, a0):
        pts.extend((c[0] + R_CORNER * math.cos(math.radians(a0 + 90 * i / N_ARC)),
                    c[1] + R_CORNER * math.sin(math.radians(a0 + 90 * i / N_ARC))) for i in range(N_ARC))

    straight((bc.HW, -cz), (bc.HW, cz), A_LONG); arc((cx, cz), 0)
    straight((cx, bc.HL), (-cx, bc.HL), B_SHORT); arc((-cx, cz), 90)
    straight((-bc.HW, cz), (-bc.HW, -cz), A_LONG); arc((-cx, -cz), 180)
    straight((-cx, -bc.HL), (cx, -bc.HL), B_SHORT); arc((cx, -cz), 270)
    n = len(pts)
    out, s = [], 0.0
    for i, (x, z) in enumerate(pts):
        px, pz = pts[i - 1]; qx, qz = pts[(i + 1) % n]
        L = math.hypot(qx - px, qz - pz) or 1
        out.append([x, z + bc.ZC, (qz - pz) / L, -(qx - px) / L, s])
        s += math.hypot(qx - x, qz - z)
    return out, s


OUTLINE, PERIM = outline()
N = len(OUTLINE)
NX, NZ = B_SHORT + N_ARC, A_LONG + N_ARC
assert N == 2 * (NX + NZ)
M_HR = A_LONG + N_ARC // 2
M_HL = A_LONG + N_ARC + B_SHORT + N_ARC // 2
M_FL = M_HL + N_ARC // 2 + A_LONG + N_ARC // 2
M_FR = N - N_ARC // 2


def profile():
    """(offset from outline, y, arc length from the seam, role) for each ring, top seam first."""
    pr = []
    for k in range(5):                                   # shoulder fillet
        a = math.radians(90 * k / 4)
        pr.append((-R_EDGE + R_EDGE * math.sin(a), bc.Y_TOP - R_EDGE + R_EDGE * math.cos(a), "fillet"))
    y0, y1 = bc.Y_TOP - R_EDGE, bc.Y_BOT + R_EDGE
    pr += [(0.0, y0 + (y1 - y0) * t, "wall") for t in (.5, 1.0)]
    for k in range(1, 3):                                # lower fillet turning under (mostly hidden: 2 segments)
        a = math.radians(90 * k / 2)
        pr.append((-R_EDGE + R_EDGE * math.cos(a), bc.Y_BOT + R_EDGE - R_EDGE * math.sin(a), "under"))
    pr += [(-R_EDGE - .028, bc.Y_BOT + .004, "hem"), (-R_EDGE - .05, bc.Y_BOT + .0, "hem")]
    out, s = [], 0.0
    for i, (d, y, role) in enumerate(pr):
        if i:
            s += math.hypot(d - pr[i - 1][0], y - pr[i - 1][1])
        out.append((d, y, s, role))
    return out


def cap(v, limit=.005):
    """Soft cap on raised folds: geometry never rises more than ~5 mm above the flat top (tests and hands use the bounds top)."""
    return limit * math.tanh(v / limit) if v > 0 else v


def coons(top):
    """Grid vertices (NX+1) x (NZ+1) from the four boundary curves of the seam ring `top` (N x 2)."""
    fb = np.array([top[(M_FL + i) % N] for i in range(NX + 1)]); tt = np.array([top[(M_HL - i) % N] for i in range(NX + 1)])
    lf = np.array([top[(M_FL - j) % N] for j in range(NZ + 1)]); rg = np.array([top[(M_FR + j) % N] for j in range(NZ + 1)])
    g = np.zeros((NX + 1, NZ + 1, 2))
    for i in range(NX + 1):
        for j in range(NZ + 1):
            u, v = i / NX, j / NZ
            g[i, j] = ((1 - v) * fb[i] + v * tt[i] + (1 - u) * lf[j] + u * rg[j]
                       - ((1 - u) * (1 - v) * fb[0] + u * (1 - v) * fb[-1] + (1 - u) * v * tt[0] + u * v * tt[-1]))
    return g


def build(fields):
    P = np.array([[o[0], o[1]] for o in OUTLINE]); Nn = np.array([[o[2], o[3]] for o in OUTLINE])
    prof = profile()
    verts, uvs, tris, kinds, tri_role = [], [], [], [], []

    def uv_of(ux, uz):
        return ((ux - bc.U0) / (bc.U1 - bc.U0), (uz - bc.V0) / (bc.V1 - bc.V0))

    rng = np.random.default_rng(11)
    scallop = [(rng.uniform(.07, .16), rng.uniform(0, 6.28), rng.uniform(.0025, .0055)) for _ in range(4)]
    ring_start = []
    for k, (d, y, s, role) in enumerate(prof):
        ring_start.append(len(verts))
        prev = prof[k - 1] if k else None
        nxt = prof[k + 1] if k + 1 < len(prof) else None
        a, b = (prev or (d, y)), (nxt or (d, y))
        tang = np.array([b[0] - a[0], b[1] - a[1]]); tang /= np.linalg.norm(tang) or 1
        pn = np.array([tang[1], -tang[0]])                       # profile normal (offset, y), pointing outward/up
        for i in range(N):
            x, z = P[i] + Nn[i] * d
            ux, uz = (P[i] + Nn[i] * (-R_EDGE + s))
            f = cap(float(cf.sample(fields["F_low"], ux, uz))) if role in ("fillet", "wall") or k == 0 else 0.0
            drape = sum(am * math.sin(OUTLINE[i][4] / p * math.tau + ph) for p, ph, am in scallop)
            if role in ("under", "hem"):                         # elastic hem gather: irregular scallop, tucked under
                f += drape * (1 if role == "hem" else .5)
            elif role == "wall":
                f += drape * .45                                     # the turned edge drapes unevenly, not a straight wall
            nx3, ny3, nz3 = Nn[i][0] * pn[0], pn[1], Nn[i][1] * pn[0]
            verts.append((x + nx3 * f, y + ny3 * f, z + nz3 * f))
            uvs.append(uv_of(ux, uz))
            kinds.append(role)
    # top grid (interior new vertices; boundary reuses ring 0)
    g = coons(P + Nn * (-R_EDGE))
    top_id = {}
    for i in range(NX + 1):
        for j in range(NZ + 1):
            if j == 0: top_id[i, j] = (M_FL + i) % N
            elif j == NZ: top_id[i, j] = (M_HL - i) % N
            elif i == 0: top_id[i, j] = (M_FL - j) % N
            elif i == NX: top_id[i, j] = (M_FR + j) % N
            else:
                x, z = g[i, j]
                top_id[i, j] = len(verts)
                verts.append((x, bc.Y_TOP + cap(float(cf.sample(fields["F_low"], x, z))), z))
                uvs.append(uv_of(x, z)); kinds.append("top")
    for i in range(NX):
        for j in range(NZ):
            a, b, c, d = top_id[i, j], top_id[i, j + 1], top_id[i + 1, j + 1], top_id[i + 1, j]
            tris += [a, b, c, a, c, d]                            # clockwise seen from above = Unity front face
            tri_role += ["top", "top"]
    for k in range(len(prof) - 1):
        r0, r1 = ring_start[k], ring_start[k + 1]
        for i in range(N):
            j = (i + 1) % N
            tris += [r0 + i, r0 + j, r1 + j, r0 + i, r1 + j, r1 + i]
            tri_role += [prof[k + 1][3]] * 2
    return np.array(verts), np.array(uvs), tris, tri_role


def vertex_normals(v, tris):
    acc = np.zeros_like(v)
    t = np.array(tris).reshape(-1, 3)
    n = np.cross(v[t[:, 1]] - v[t[:, 0]], v[t[:, 2]] - v[t[:, 0]])
    for k in range(3):
        np.add.at(acc, t[:, k], n)
    return acc / (np.linalg.norm(acc, axis=1, keepdims=True) + 1e-12), n


def main():
    fields = cf.compute(seed=7, cover_hex=HEX)
    verts, uvs, tris, roles = build(fields)
    normals, face_n = vertex_normals(verts, tris)
    t = np.array(tris).reshape(-1, 3)
    # exact winding test per surface role: top faces up, wall faces outward, tucked underside faces down
    ctr = verts[t].mean(axis=1)
    radial = ctr - np.array([0, 0, bc.ZC]); radial[:, 1] = 0
    wrong = 0
    for n_, role, rad in zip(face_n, roles, radial):
        if role == "top": wrong += n_[1] <= 0
        elif role in ("under", "hem"): wrong += n_[1] >= 0
        else: wrong += float(n_[0] * rad[0] + n_[2] * rad[2]) <= 0
    disagree = wrong / len(roles)
    bc.OUT.mkdir(parents=True, exist_ok=True)
    bc.write_png(bc.OUT / "cover_albedo.png", fields["albedo"])
    bc.write_png(bc.OUT / "cover_normal.png", fields["normal"])
    bc.write_png(bc.OUT / "cover_occlusion.png", fields["ao"])
    bc.write_png(bc.OUT / "fabric_weave_normal.png", bc.weave_normal(256, 16, 2.2))
    flat = lambda a: [round(float(c), 6) for c in np.asarray(a).ravel()]
    (bc.OUT / "bed_cover.json").write_text(json.dumps({"vertices": flat(verts), "normals": flat(normals), "uv": flat(uvs),
                                                      "tris0": [int(i) for i in tris], "tris1": []}))
    # clearance rule check on the displaced top: every footprint cell must stay within its measured clearance
    worst = 1.0
    for (ix, iz), clear in bc.load_footprint().items():
        x, z = ix * .02, iz * .02
        h = float(cf.sample(fields["F_low"], x, z))
        worst = min(worst, clear - 0.0015 - h)
    bpy.ops.wm.read_factory_settings(use_empty=True)                  # no default cube/light/camera in the source file
    mesh = bpy.data.meshes.new("BED_cover")
    mesh.from_pydata([(x, z - 1.0, y) for x, y, z in verts], [], [(t_[0], t_[2], t_[1]) for t_ in t])
    mesh.update()
    for p in mesh.polygons:
        p.use_smooth = True
    layer = mesh.uv_layers.new(name="UVMap")
    for loop in mesh.loops:
        layer.data[loop.index].uv = tuple(uvs[loop.vertex_index])
    obj = bpy.data.objects.new("BED_cover", mesh)
    bpy.context.collection.objects.link(obj)
    mat = bpy.data.materials.new("M_Cover")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*bc.srgb_to_lin(bc.hex_rgb(HEX)), 1)
    mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .84
    mesh.materials.append(mat)
    bpy.ops.wm.save_as_mainfile(filepath=str(bc.OUT / "bed_cover.blend"))
    report = {"triangles": len(tris) // 3, "vertices": len(verts), "winding_disagree": round(disagree, 4),
              "min_clearance_margin_m": round(worst, 4), "max_fold_up_m": round(float(fields["F_low"].max()), 4),
              "min_fold_down_m": round(float(fields["F_low"].min()), 4), "cover_hex": HEX}
    assert disagree < .01 and worst > 0, report
    (bc.OUT / "bed_cover_validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("BED COVER", json.dumps(report))


main()
