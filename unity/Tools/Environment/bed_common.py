"""Shared helpers for the bed linen builders: bed measurements, patient footprint, fields, PNG writer.

Runs inside Blender's Python (numpy is bundled) or any Python with numpy. All coordinates are Unity world metres
(x right, y up, z toward the bed head) unless a function says otherwise.
"""
import csv
import math
import struct
import zlib
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
OUT = HERE.parents[1] / "Logs" / "HospitalBed"
FOOTPRINT_CSV = HERE / "data" / "patient_bed_footprint.csv"

# Original mattress footprint (0.95 x 2.00 m, underside 0.56, flat top 0.70) - never shortened.
HW, Z0, Z1 = .475, 0.0, 2.0
HL, ZC = (Z1 - Z0) / 2, (Z1 + Z0) / 2
Y_BOT, Y_TOP = .560, .700
# Unfolded texture domain: the cover's wrapped edge is developed outward around the top (exact surface distances).
EXT = .20
U0, U1, V0, V1 = -HW - EXT, HW + EXT, Z0 - EXT, Z1 + EXT


def load_footprint():
    """Cells the patient skin rests on (2 cm grid): {(ix, iz): clearance above the flat top in metres}."""
    cells = {}
    for row in csv.reader(open(FOOTPRINT_CSV)):
        cells[(round(float(row[0]) / .02), round(float(row[1]) / .02))] = float(row[2])
    return cells


def grid_xz(w, h):
    """World x, z of every texel centre, array [row v=0 (z=V0) .. v=1][col]."""
    xs = U0 + (np.arange(w) + .5) / w * (U1 - U0)
    zs = V0 + (np.arange(h) + .5) / h * (V1 - V0)
    return np.meshgrid(xs, zs)


def gauss(arr, sigma_px):
    """Gaussian blur via FFT (wraps at the borders; the fields are zero there)."""
    if sigma_px <= 0:
        return arr
    h, w = arr.shape
    fy = np.fft.fftfreq(h)[:, None]
    fx = np.fft.rfftfreq(w)[None, :]
    k = np.exp(-2 * (math.pi * sigma_px) ** 2 * (fx ** 2 + fy ** 2))
    return np.fft.irfft2(np.fft.rfft2(arr) * k, s=arr.shape)


def footprint_fields(w, h):
    """(occupancy 0/1, distance to the footprint in metres) on the texture grid."""
    x, z = grid_xz(w, h)
    occ = np.zeros((h, w))
    px, pz = (U1 - U0) / w, (V1 - V0) / h
    for (ix, iz), clear in load_footprint().items():
        if clear > .05:
            continue                        # raised hands and the head are handled by the pillow, not the cover
        cx, cz = ix * .02, iz * .02
        i0, i1 = int((cx - .011 - U0) / px), int((cx + .011 - U0) / px) + 1
        j0, j1 = int((cz - .011 - V0) / pz), int((cz + .011 - V0) / pz) + 1
        occ[max(j0, 0):max(j1, 0), max(i0, 0):max(i1, 0)] = 1
    # distance on an 8x downsampled grid (1 cm accuracy is plenty for the masks)
    step = 8
    small = occ[::step, ::step] > 0
    ys, xs_ = np.nonzero(small)
    dist = np.full(small.shape, 1e4)
    if len(ys):
        gy, gx = np.mgrid[0:small.shape[0], 0:small.shape[1]]
        for k in range(len(ys)):
            dist = np.minimum(dist, np.hypot(gy - ys[k], gx - xs_[k]))
    dist = dist * (px + pz) / 2 * step
    dist = np.kron(dist, np.ones((step, step)))[:h, :w]
    if dist.shape != (h, w):
        dist = np.pad(dist, ((0, h - dist.shape[0]), (0, w - dist.shape[1])), mode="edge")
    dist = np.maximum(gauss(dist, 7.0), 0.0)               # smooth the 1 cm quantisation so masks have no stair-steps
    return occ, dist


def smoothstep(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def noise(shape, scale_px, rng):
    """Irregular band-limited noise in about [-1, 1] (blurred white noise)."""
    n = gauss(rng.standard_normal(shape), scale_px)
    return n / (np.abs(n).max() + 1e-9)


def ridged(shape, scale_px, rng):
    return 1 - np.abs(noise(shape, scale_px, rng)) * 1.0


def normal_map(height, px_x, px_y, strength=1.0):
    """Tangent-space (OpenGL, +Y up in v) normal map from a height field in metres, array [v rows][u cols]."""
    dz_dy, dz_dx = np.gradient(height, px_y, px_x)
    n = np.dstack([-dz_dx * strength, -dz_dy * strength, np.ones_like(height)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * .5 + .5


def write_png(path, arr):
    """8-bit PNG from a float array in [0, 1]: [h][w] gray, [h][w][3] RGB or [h][w][4] RGBA; row 0 of `arr` is v=0 (bottom)."""
    a = np.clip(np.asarray(arr) * 255 + .5, 0, 255).astype(np.uint8)[::-1]          # PNG row 0 is the top (v=1)
    h, w = a.shape[:2]
    ctype = 0 if a.ndim == 2 else (2 if a.shape[2] == 3 else 6)
    raw = b"".join(b"\x00" + a[r].tobytes() for r in range(h))

    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, ctype, 0, 0, 0)) \
        + chunk(b"IDAT", zlib.compress(raw, 6)) + chunk(b"IEND", b"")
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_bytes(png)


def srgb_to_lin(c):
    c = np.asarray(c, float)
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def lin_to_srgb(c):
    c = np.clip(np.asarray(c, float), 0, 1)
    return np.where(c <= .0031308, 12.92 * c, 1.055 * c ** (1 / 2.4) - .055)


def hex_rgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def weave_normal(size=256, pitch=16, strength=2.2):
    """Tileable plain-weave normal map (alternating over/under threads); `size` must be a multiple of `pitch`."""
    y, x = np.mgrid[0:size, 0:size].astype(float)
    cx, cy = (x / pitch) % 2, (y / pitch) % 2
    warp = .5 * (np.sin(2 * math.pi * x / pitch) + 1)                  # threads running along y, seen across x
    weft = .5 * (np.sin(2 * math.pi * y / pitch) + 1)
    over = ((np.floor(x / pitch) + np.floor(y / pitch)) % 2) == 0
    h = np.where(over, .6 + .4 * warp, .6 + .4 * weft)
    h = gauss(h, .7)
    gy, gx = np.gradient(h)
    n = np.dstack([-gx * strength, -gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * .5 + .5
