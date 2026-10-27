"""Fabric fold field, seams and the baked maps of the blue mattress cover (numpy; Unity axes, metres).

The field F(x, z) is the cover's height offset along its surface normal on the unfolded domain (top face plus the
wrapped edge developed outward). Large part of it becomes mesh displacement, the residual becomes the normal map.
Rules measured from the patient: no positive fold inside the body footprint (clearance there is only 2.6 mm).
"""
import math

import numpy as np

import bed_common as bc

W, H = 1024, 2048
PX, PZ = (bc.U1 - bc.U0) / W, (bc.V1 - bc.V0) / H
R_EDGE, R_CORNER = .035, .08


def sdf_top(x, z):
    """Signed distance to the seam curve (the outline pulled in by R_EDGE); negative on the flat top, positive on the wrap."""
    hwi, hli, rc = bc.HW - R_EDGE, bc.HL - R_EDGE, R_CORNER - R_EDGE
    qx, qz = np.abs(x) - (hwi - rc), np.abs(z - bc.ZC) - (hli - rc)
    return np.hypot(np.maximum(qx, 0), np.maximum(qz, 0)) + np.minimum(np.maximum(qx, qz), 0) - rc


def add_fold(F, X, Z, p0, ang, length, width, amp, bend=0.0):
    """One tension fold: a ridge with a shallow trough beside it along a gently curved path (derivative-of-Gaussian profile)."""
    d = np.array([math.cos(ang), math.sin(ang)])
    n = np.array([-d[1], d[0]])
    pad = 3.5 * width + abs(bend) * length
    xs = [p0[0], p0[0] + d[0] * length]
    zs = [p0[1], p0[1] + d[1] * length]
    i0, i1 = int((min(xs) - pad - bc.U0) / PX), int((max(xs) + pad - bc.U0) / PX) + 1
    j0, j1 = int((min(zs) - pad - bc.V0) / PZ), int((max(zs) + pad - bc.V0) / PZ) + 1
    i0, j0, i1, j1 = max(i0, 0), max(j0, 0), min(i1, W), min(j1, H)
    if i0 >= i1 or j0 >= j1:
        return
    dx, dz = X[j0:j1, i0:i1] - p0[0], Z[j0:j1, i0:i1] - p0[1]
    t = dx * d[0] + dz * d[1]
    ph = (p0[0] * 37.1 + p0[1] * 11.7) % 6.28
    tn = np.clip(t / length, 0, 1)
    wob = 1 + .32 * np.sin(tn * 7.3 + ph) + .2 * np.sin(tn * 17.0 + 2 * ph)      # amplitude wobble along the fold
    wid = width * (1 + .35 * np.sin(tn * 5.1 + 1.7 * ph))                       # width wobble: the fold swells and pinches
    s = dx * n[0] + dz * n[1] - bend * length * tn ** 2 - .006 * np.sin(tn * 9.0 + ph)
    env = np.where((t > 0) & (t < length), np.sin(np.pi * tn) ** 1.3, 0.0)
    ridge = np.exp(-.5 * (s / wid) ** 2) - .45 * np.exp(-.5 * ((s - 1.9 * wid) / (1.5 * wid)) ** 2)   # ridge + one soft shoulder
    F[j0:j1, i0:i1] += amp * env * wob * ridge


def build_folds(F, X, Z, rng):
    """Irregular folds modelled on the reference: shallow diagonal tension folds from the edges, tucked corners."""
    corners = [(sx, sz) for sx in (1, -1) for sz in (1, -1)]
    for sx, sz in corners:
        cx, cz = sx * bc.HW, bc.ZC + sz * bc.HL
        u_long, u_short = np.array([0, -sz]), np.array([-sx, 0])
        for phi in np.sort(rng.uniform(math.radians(18), math.radians(68), rng.integers(3, 6))):
            v = math.cos(phi) * u_long + math.sin(phi) * u_short
            start = (cx + u_long[0] * rng.uniform(.01, .09) + u_short[0] * rng.uniform(.02, .06),
                     cz + u_long[1] * rng.uniform(.01, .09) + u_short[1] * rng.uniform(.02, .06))
            add_fold(F, X, Z, start, math.atan2(v[1], v[0]), rng.uniform(.18, .46), rng.uniform(.022, .042),
                     rng.choice([-1, 1]) * rng.uniform(.005, .0115), rng.uniform(-.07, .07))
        # hospital-corner tuck: a firmer ridge along the bisector across the wrapped edge, flanked by shallow troughs
        bis = math.atan2(sz, sx)
        for da, amp, w in ((0, .0105, .026), (.5, -.0042, .032), (-.5, -.0042, .032)):
            add_fold(F, X, Z, (cx - sx * .035, cz - sz * .035), bis + da * .6, .24, w, amp, 0)
    for sx in (1, -1):                                  # side tension folds: irregular spacing, mixed tilt
        z = bc.Z0 + rng.uniform(.12, .3)
        while z < bc.Z1 - .15:
            tilt = rng.choice([-1, 1]) * rng.uniform(math.radians(14), math.radians(42))
            base = math.atan2(0, -sx)
            add_fold(F, X, Z, (sx * (bc.HW - .025), z), base + tilt, rng.uniform(.09, .26), rng.uniform(.016, .03),
                     rng.choice([-1, 1]) * rng.uniform(.002, .0052), rng.uniform(-.06, .06))
            z += rng.gamma(2.2, .07) + .05


def edge_pull(F, X, Z, rng):
    """Short irregular ridges running down the turned edge from the seam: the cover gathers where it wraps under."""
    for sx in (1, -1):
        z = bc.Z0 + rng.uniform(.08, .2)
        while z < bc.Z1 - .1:
            ang = math.atan2(0, sx) + rng.uniform(-.7, .7)
            add_fold(F, X, Z, (sx * (bc.HW - R_EDGE + .002), z), ang, rng.uniform(.05, .2), rng.uniform(.01, .02),
                     rng.choice([-1, 1]) * rng.uniform(.0022, .0046), rng.uniform(-.04, .04))
            z += rng.gamma(2.0, .035) + .028


def slack_folds(F, X, Z, rng):
    """A few broad slack folds that travel across the free parts of the top (between the legs and beyond the shoulders)."""
    for sx in (1, -1):
        for zone in ((bc.Z0 + .15, bc.ZC - .25), (bc.ZC + .35, bc.Z1 - .2)):
            for _ in range(2):
                z = rng.uniform(*zone)
                tilt = rng.choice([-1, 1]) * rng.uniform(math.radians(25), math.radians(58))
                add_fold(F, X, Z, (sx * (bc.HW - .04), z), math.atan2(0, -sx) + tilt, rng.uniform(.25, .5), rng.uniform(.04, .07),
                         rng.choice([-1, 1]) * rng.uniform(.0045, .0075), rng.uniform(-.1, .1))


def seam_layer(X, Z):
    """Piped boxed-edge seam, the lower hem seam and stitch dashes as a height layer (normal/albedo only)."""
    d = sdf_top(X, Z)
    h = .0010 * np.exp(-(d / .0028) ** 2) - .0007 * np.exp(-((np.abs(d) - .0062) / .0016) ** 2)
    h += -.0007 * np.exp(-((d - .172) / .0024) ** 2)                       # hem seam
    stitch = np.zeros_like(h)
    for dist in (.0095, .1775):                                             # stitched lines beside both seams
        per = _perimeter(dist)
        for (px_, pz_, tx, tz) in per[::1]:
            i0, i1 = int((px_ - .0022 - bc.U0) / PX), int((px_ + .0022 - bc.U0) / PX) + 1
            j0, j1 = int((pz_ - .0022 - bc.V0) / PZ), int((pz_ + .0022 - bc.V0) / PZ) + 1
            if 0 <= i0 and i1 <= W and 0 <= j0 and j1 <= H:
                stitch[j0:j1, i0:i1] = 1.0
    return h - .00035 * stitch, stitch


def _perimeter(offset, step=.0052):
    """Dash centres every `step` metres along the outline pushed out by `offset` from the seam curve: (x, z, tx, tz)."""
    hw, hl, rc = bc.HW - R_EDGE, bc.HL - R_EDGE, R_CORNER - R_EDGE
    pts, a = [], offset
    cx, cz = hw - rc, hl - rc
    segs = [((hw + a, -cz), (hw + a, cz)), None, ((cx, hl + a), (-cx, hl + a)), None,
            ((-hw - a, cz), (-hw - a, -cz)), None, ((-cx, -hl - a), (cx, -hl - a)), None]
    arcs = [(cx, cz, 0), (-cx, cz, 90), (-cx, -cz, 180), (cx, -cz, 270)]
    for k, seg in enumerate(segs):
        if seg is not None:
            (x0, z0), (x1, z1) = seg
            length = math.hypot(x1 - x0, z1 - z0)
            n = int(length / step)
            for i in range(n):
                t = (i + .5) / n
                pts.append((x0 + (x1 - x0) * t, bc.ZC + z0 + (z1 - z0) * t, (x1 - x0) / length, (z1 - z0) / length))
        else:
            ax, az, a0 = arcs[k // 2]
            r = rc + a
            n = max(2, int(math.radians(90) * r / step))
            for i in range(n):
                ang = math.radians(a0 + 90 * (i + .5) / n)
                pts.append((ax + r * math.cos(ang), bc.ZC + az + r * math.sin(ang), -math.sin(ang), math.cos(ang)))
    return pts


def compute(seed=7, cover_hex="#597898"):
    """All cover fields. Returns dict: F (total, m), F_low (mesh displacement), maps (albedo RGBA, normal RGB, ao L)."""
    rng = np.random.default_rng(seed)
    X, Z = bc.grid_xz(W, H)
    occ, dist = bc.footprint_fields(W, H)
    free = bc.smoothstep(.035, .095, dist)                      # positive folds only well away from the body
    F = np.zeros((H, W))
    build_folds(F, X, Z, rng)
    edge_pull(F, X, Z, rng)
    slack_folds(F, X, Z, rng)
    halo = bc.smoothstep(.035, .06, dist) * (1 - bc.smoothstep(.1, .17, dist))
    patch = .45 + .55 * (bc.noise((H, W), .12 / PX, rng) * .5 + .5)
    F += (bc.noise((H, W), .055 / PX, rng) * .0034 + bc.noise((H, W), .028 / PX, rng) * .0016) * halo * patch   # soft undulations around the contact zone
    d_seam = sdf_top(X, Z)
    taut = bc.smoothstep(.0, .035, np.abs(d_seam + .0))                             # the seam region stays taut
    F = (np.maximum(F, 0) * free + np.minimum(F, 0)) * taut
    F += -.0022 * bc.gauss(occ, .03 / PX) / (bc.gauss(occ, .03 / PX).max() + 1e-9)   # shallow weight depression under the body
    F_low = bc.gauss(F, .008 / PX)
    hf = F - F_low
    hf += .00025 * (bc.ridged((H, W), 3.2, rng) - .5) * free + .0002 * (bc.ridged((H, W), 8.0, rng) - .5) * free
    seam, stitch = seam_layer(X, Z)
    height = hf + seam
    normal = bc.normal_map(height, PX, PZ, 3.0)
    cav = bc.gauss(F, 11) - F
    ao = 1 - np.clip(cav * 55, 0, .24)
    ao *= 1 - .3 * bc.smoothstep(.09, .17, d_seam)                                  # the tucked underside falls into shade
    base = bc.srgb_to_lin(bc.hex_rgb(cover_hex))
    fold = F_low + hf
    value = 1 + .5 * np.clip(fold / .004, -1, 1) * .12 + .035 * bc.noise((H, W), .09 / PX, rng) + .018 * bc.noise((H, W), 5, rng)
    value *= 1 + .12 * stitch
    lin = base[None, None, :] * value[:, :, None]
    smooth = np.clip(.13 + .05 * np.clip(fold / .004, -1, 1) + .018 * bc.noise((H, W), 9, rng) + .07 * stitch
                     + .05 * (np.abs(d_seam) < .004), .08, .26)
    albedo = np.dstack([bc.lin_to_srgb(lin), smooth])
    return {"F": F, "F_low": F_low, "albedo": albedo, "normal": normal, "ao": ao, "dist": dist, "free": free,
            "occ": occ, "d_seam": d_seam, "stitch": stitch}


def sample(field, x, z):
    """Bilinear sample of a texture-grid field at world (x, z)."""
    fx = (np.asarray(x) - bc.U0) / PX - .5
    fz = (np.asarray(z) - bc.V0) / PZ - .5
    fx, fz = np.clip(fx, 0, W - 1.001), np.clip(fz, 0, H - 1.001)
    i, j = fx.astype(int), fz.astype(int)
    tx, tz = fx - i, fz - j
    return (field[j, i] * (1 - tx) * (1 - tz) + field[j, i + 1] * tx * (1 - tz)
            + field[j + 1, i] * (1 - tx) * tz + field[j + 1, i + 1] * tx * tz)
