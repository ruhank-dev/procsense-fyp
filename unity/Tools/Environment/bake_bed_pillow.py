"""Pillow mesh, UVs and baked maps from the cloth result (numpy only; run with any Python after build_bed_pillow.py).

Writes unity/Logs/HospitalBed: pillow_albedo.png (smoothness in alpha), pillow_normal.png, pillow_occlusion.png,
pillow.json (flat mesh arrays), patient_pillow.obj (mirrored for the Unity OBJ importer) and pillow_validation.json.
  python unity/Tools/Environment/bake_bed_pillow.py [--hex #E7E9E5]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bed_common as bc  # noqa: E402

argv = sys.argv[1:]
HEX = argv[argv.index("--hex") + 1] if "--hex" in argv else "#E7E9E5"
CX, CZ = 0.0, 1.75
PW, PD = .56 * 1.12, .30 * 1.2          # texture window around the case
TW, TH = 1024, 640
ORIGIN = (0.0, .785, 1.76)              # the existing scene object; the OBJ is written relative to it


def rasterize(verts, tris, w, h):
    """Top-surface height (and coverage) per texel by splatting the triangles that face up."""
    height = np.full((h, w), np.nan)
    uv = np.column_stack([(verts[:, 0] - CX) / PW + .5, (verts[:, 2] - CZ) / PD + .5])
    t = np.array(tris).reshape(-1, 3)
    for a, b, c in t:
        n = np.cross(verts[b] - verts[a], verts[c] - verts[a])
        if n[1] <= 0:
            continue
        tri = uv[[a, b, c]] * [w, h]
        x0, x1 = int(max(tri[:, 0].min() - 1, 0)), int(min(tri[:, 0].max() + 2, w))
        y0, y1 = int(max(tri[:, 1].min() - 1, 0)), int(min(tri[:, 1].max() + 2, h))
        if x1 <= x0 or y1 <= y0:
            continue
        gx, gy = np.meshgrid(np.arange(x0, x1) + .5, np.arange(y0, y1) + .5)
        d = (tri[1, 1] - tri[2, 1]) * (tri[0, 0] - tri[2, 0]) + (tri[2, 0] - tri[1, 0]) * (tri[0, 1] - tri[2, 1])
        if abs(d) < 1e-9:
            continue
        l1 = ((tri[1, 1] - tri[2, 1]) * (gx - tri[2, 0]) + (tri[2, 0] - tri[1, 0]) * (gy - tri[2, 1])) / d
        l2 = ((tri[2, 1] - tri[0, 1]) * (gx - tri[2, 0]) + (tri[0, 0] - tri[2, 0]) * (gy - tri[2, 1])) / d
        l3 = 1 - l1 - l2
        inside = (l1 >= -.01) & (l2 >= -.01) & (l3 >= -.01)
        z = l1 * verts[a, 1] + l2 * verts[b, 1] + l3 * verts[c, 1]
        sl = height[y0:y1, x0:x1]
        sl[inside] = z[inside]
    return height


def main():
    verts = np.load(bc.OUT / "pillow_verts.npy")
    tris = json.loads((bc.OUT / "pillow_tris.json").read_text())
    rng = np.random.default_rng(5)
    hgt = rasterize(verts, tris, TW, TH)
    cover = ~np.isnan(hgt)
    base_y = bc.Y_TOP
    hz = np.where(cover, hgt, base_y)
    px, pz = PW / TW, PD / TH
    # case outline distance: seam and stitches follow the silhouette
    inside = cover.astype(float)
    dist_edge = np.zeros_like(inside)
    soft = bc.gauss(inside, 5)
    dist_edge = np.clip(soft, 0, 1)
    # fine fabric creases: short irregular ridges, stronger near the seam and in the compressed zone
    seam_w = bc.smoothstep(.25, .75, bc.gauss(inside, 12))                    # 0 at the seam -> 1 inside
    crease = (bc.ridged((TH, TW), 9.0, rng) - .5) * .0007 + (bc.ridged((TH, TW), 26, rng) - .5) * .0013 * (1 - seam_w * .5)
    seam_h = .0024 * np.exp(-((1 - seam_w - .06) / .05) ** 2) * cover - .0009 * np.exp(-((1 - seam_w - .13) / .03) ** 2) * cover
    # top-stitch dashes ~1 cm inside the seam
    ring = (np.abs(bc.gauss(inside, 7) - .5) < .02)
    ys, xs = np.nonzero(ring)
    stitch = np.zeros((TH, TW))
    keep = ((xs // 4 + ys // 4) % 3 == 0)
    stitch[ys[keep], xs[keep]] = 1.0
    stitch = bc.gauss(stitch, 1.0)
    stitch = np.clip(stitch * 3, 0, 1) * cover
    height_hf = crease * cover + seam_h - .0009 * stitch
    normal = bc.normal_map(height_hf, px, pz, 1.7)
    smooth_h = bc.gauss(np.where(cover, hz, np.nanmean(hgt)), 10)
    cav = np.clip((smooth_h - hz) * 40 + (bc.gauss(np.where(cover, hz, base_y), 3) - bc.gauss(np.where(cover, hz, base_y), 12)) * -80, 0, .5)
    ao = np.clip(1 - cav * .7 - .35 * (1 - seam_w) * cover, .35, 1.0)
    base = bc.srgb_to_lin(bc.hex_rgb(HEX))
    val = 1 + .03 * bc.noise((TH, TW), 40, rng) + .015 * bc.noise((TH, TW), 4, rng) + .06 * (1 - seam_w) * cover - .08 * np.clip(cav, 0, .5)
    val = val * (1 - .08 * stitch) * (1 - .06 * np.exp(-((1 - seam_w - .06) / .05) ** 2))
    lin = base[None, None, :] * val[:, :, None]
    smooth = np.clip(.12 + .02 * bc.noise((TH, TW), 8, rng) + .05 * stitch, .08, .2)
    bc.write_png(bc.OUT / "pillow_albedo.png", np.dstack([bc.lin_to_srgb(lin), smooth]))
    bc.write_png(bc.OUT / "pillow_normal.png", normal)
    bc.write_png(bc.OUT / "pillow_occlusion.png", ao)

    # mesh arrays with planar UVs and smooth normals
    t = np.array(tris).reshape(-1, 3)
    acc = np.zeros_like(verts)
    fn = np.cross(verts[t[:, 1]] - verts[t[:, 0]], verts[t[:, 2]] - verts[t[:, 0]])
    for k in range(3):
        np.add.at(acc, t[:, k], fn)
    nrm = acc / (np.linalg.norm(acc, axis=1, keepdims=True) + 1e-12)
    uv = np.column_stack([(verts[:, 0] - CX) / PW + .5, (verts[:, 2] - CZ) / PD + .5])
    flat = lambda a: [round(float(c), 6) for c in np.asarray(a).ravel()]
    (bc.OUT / "pillow.json").write_text(json.dumps({"vertices": flat(verts), "normals": flat(nrm), "uv": flat(uv),
                                                    "tris0": [int(i) for i in tris], "tris1": []}))
    lines = ["# hospital pillow, cloth simulation result", "o PatientPillow"]
    ox, oy, oz = ORIGIN
    lines += [f"v {-(x - ox):.6f} {y - oy:.6f} {z - oz:.6f}" for x, y, z in verts]
    lines += [f"vt {u:.6f} {v:.6f}" for u, v in uv]
    lines += [f"vn {-n[0]:.6f} {n[1]:.6f} {n[2]:.6f}" for n in nrm]
    lines.append("s 1")
    for a, b, c in t:
        lines.append(f"f {a + 1}/{a + 1}/{a + 1} {c + 1}/{c + 1}/{c + 1} {b + 1}/{b + 1}/{b + 1}")
    (bc.OUT / "patient_pillow.obj").write_text("\n".join(lines) + "\n")
    wrong = int(np.sum(fn[:, 1][(verts[t].mean(axis=1)[:, 1] > base_y + .03)] < 0))
    report = {"triangles": len(t), "vertices": len(verts), "top_faces_facing_down": wrong,
              "crown_y": round(float(verts[:, 1].max()), 4), "z_range": [round(float(verts[:, 2].min()), 3), round(float(verts[:, 2].max()), 3)],
              "hex": HEX}
    (bc.OUT / "pillow_validation.json").write_text(json.dumps(report, indent=2) + "\n")
    print("PILLOW BAKE", json.dumps(report))


main()
