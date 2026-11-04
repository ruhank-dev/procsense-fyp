"""Procedural gathered-curtain mesh (numpy): irregular pleats, looser folds toward the hem, scalloped lower edge, real cloth thickness.

Canonical frame: width along +X (0..W), height along +Y (0..H, hem at 0), the room-facing side toward +Z.
Returns vertices, normals, uv (arc-length based, so a printed fabric does not stretch in the folds), uv2 (lightmap: clean charts,
no overlaps) and triangles. Closed thin shell: front face, back face, side and top/hem strips.
"""
import math

import numpy as np


def _noise1(n, cells, rng):
    g = rng.random(cells + 1) * 2 - 1
    t = np.linspace(0, cells, n)
    i = np.minimum(t.astype(int), cells - 1)
    f = t - i
    f = f * f * (3 - 2 * f)
    return g[i] * (1 - f) + g[i + 1] * f


def make(width, height, pleats, amp, thickness=.006, nu=120, nv=44, seed=1, heading=.55, scallop=.012, drift=.012, single_sided=False):
    rng = np.random.default_rng(seed)
    u = np.linspace(0, 1, nu)
    v = np.linspace(0, 1, nv)                                           # 0 = hem, 1 = top
    # irregular pleat spacing: phase = integral of a slowly varying frequency
    freq = pleats * (1 + .22 * _noise1(nu, 7, rng))
    phase = np.concatenate([[0], np.cumsum((freq[1:] + freq[:-1]) / 2 * np.diff(u))]) * math.tau
    ph2 = phase + .7
    amp_u = 1 + .18 * _noise1(nu, 9, rng)                                # pleat depth varies along the width
    X = np.zeros((nv, nu)); Y = np.zeros((nv, nu)); Z = np.zeros((nv, nu))
    for j, vj in enumerate(v):
        loose = 1 - vj                                                   # folds open up toward the hem
        a = amp * (heading + (1 - heading) * loose ** .8) * amp_u
        shift = drift * math.sin(vj * 5.3 + 1.1) * (1 + loose)           # folds wander slightly down the drop
        wave = np.sin(phase + shift * math.tau * 3) + .22 * np.sin(2 * ph2 + vj * 3.1)
        X[j] = u * width + drift * .6 * loose * np.sin(phase * .5 + vj * 4.0)
        Z[j] = a * wave
        # scalloped hem: the cloth lifts slightly at the crest of each fold
        Y[j] = vj * height - scallop * loose ** 3 * (1 - (np.sin(phase) * .5 + .5)) * (1 + .4 * _noise1(nu, 11, rng))
    # outward-facing normals of the front sheet from the surface gradient
    gx = np.gradient(np.stack([X, Y, Z], -1), axis=1)
    gy = np.gradient(np.stack([X, Y, Z], -1), axis=0)
    n = np.cross(gx, gy)
    n /= np.linalg.norm(n, axis=-1, keepdims=True) + 1e-12
    n *= np.sign(n[..., 2:3] + 1e-9)                                     # face the room (+Z)
    front = np.stack([X, Y, Z], -1)
    back = front - n * thickness
    # arc-length u for the print, so folds do not stretch the pattern
    seg = np.linalg.norm(np.diff(front, axis=1), axis=-1)
    arc = np.concatenate([np.zeros((nv, 1)), np.cumsum(seg, axis=1)], axis=1)
    U = arc / arc[:, -1:].mean()
    V = np.repeat(v[:, None], nu, axis=1)

    verts, norms, uv, uv2, tris = [], [], [], [], []

    def add_sheet(P, N, flip, chart):
        base = len(verts)
        for j in range(nv):
            for i in range(nu):
                verts.append(P[j, i]); norms.append(N[j, i] * (-1 if flip else 1))
                uv.append((U[j, i], V[j, i]))
                uv2.append((chart[0] + (i / (nu - 1)) * chart[2], chart[1] + (j / (nv - 1)) * chart[3]))
        for j in range(nv - 1):
            for i in range(nu - 1):
                a = base + j * nu + i; b = a + 1; c = a + nu + 1; d = a + nu
                tris.extend([a, c, d, a, b, c] if not flip else [a, d, c, a, c, b])   # Unity front face = clockwise

    add_sheet(front, n, False, (0.0, 0.0, 1.0, 1.0) if single_sided else (0.0, 0.0, .48, 1.0))
    if single_sided:                                                     # one sheet, drawn double-sided by the material (Quest triangle budget)
        return (np.array(verts, dtype=np.float32), np.array(norms, dtype=np.float32), np.array(uv, dtype=np.float32),
                np.array(uv2, dtype=np.float32), np.array(tris, dtype=np.int32))
    add_sheet(back, n, True, (.52, 0.0, .48, 1.0))

    def strip(Pa, Pb, Na):
        """Edge strip joining the front and back sheets along a polyline of indices (sides, hem, top)."""
        base = len(verts)
        m = len(Pa)
        for k in range(m):
            verts.append(Pa[k]); verts.append(Pb[k])
            nn = Na[k]
            norms.append(nn); norms.append(nn)
            uv.append((0, k / (m - 1))); uv.append((.02, k / (m - 1)))
            uv2.append((.995, .995)); uv2.append((.995, .995))
        for k in range(m - 1):
            a = base + 2 * k; b = a + 1; c = a + 3; d = a + 2
            tris.extend([a, c, b, a, d, c])

    def edge_normal(P_front, P_back, k_dir):
        return None

    # side strips (left, right), hem and top
    for sel in (0, nu - 1):
        Pa = front[:, sel]; Pb = back[:, sel]
        side = -1.0 if sel == 0 else 1.0
        Na = np.tile(np.array([side, 0, 0]), (nv, 1))
        if sel == 0:
            strip(Pb, Pa, Na)
        else:
            strip(Pa, Pb, Na)
    for jj, nrm in ((0, (0, -1, 0)), (nv - 1, (0, 1, 0))):
        Pa = front[jj]; Pb = back[jj]
        Na = np.tile(np.array(nrm, dtype=float), (nu, 1))
        if jj == 0:
            strip(Pa, Pb, Na)
        else:
            strip(Pb, Pa, Na)
    return (np.array(verts, dtype=np.float32), np.array(norms, dtype=np.float32), np.array(uv, dtype=np.float32),
            np.array(uv2, dtype=np.float32), np.array(tris, dtype=np.int32))


def write_json(path, width, height, pleats, amp, seed):
    import json
    v, n, uv, uv2, t = make(width, height, pleats, amp, seed=seed)
    json.dump({"width": width, "height": height, "vertices": v.ravel().round(5).tolist(), "normals": n.ravel().round(4).tolist(),
               "uv": uv.ravel().round(5).tolist(), "uv2": uv2.ravel().round(5).tolist(), "tris": t.tolist()}, open(path, "w"))
    return len(v), len(t) // 3


if __name__ == "__main__":
    from pathlib import Path
    out = Path(__file__).resolve().parents[2] / "Logs" / "BayRebuild"
    out.mkdir(parents=True, exist_ok=True)
    for name, seed in (("window_curtain_a", 11), ("window_curtain_b", 23)):
        print(name, write_json(out / (name + ".json"), .61, 1.48, 7.5, .042, seed))
