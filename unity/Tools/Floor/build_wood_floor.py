"""Unique, non-repeating plank floor atlas for PatientBay (5 m x 4 m), built from photographic CC0 scans.

Source: ambientCG WoodFloor040 (CC0 1.0, https://ambientcg.com/view?id=WoodFloor040), the same pine boards the original floor used. The 4K scan is a
seamless 1.9 m tile; this script finds its individual boards, then lays a NEW floor: 0.12 m boards, random staggered end joints (>= 0.25 m between
neighbours), each board cut from a random seam-free stretch of a source board (mirrored/flipped at random, gently warped along the grain, tinted per board),
V-groove seams, a restrained seam-only occlusion, and a satin-varnish smoothness map. Output (unity/Logs/FloorBuild/out):
  floor_albedo_smooth.png  sRGB RGB + linear smoothness in alpha        4096 x 3280  (819 texels/m)
  floor_normal.png         tangent-space normal, OpenGL (Unity) layout   4096 x 3280
  floor_preview_*.png      layout / source-usage diagnostics
Atlas axes: image x = U (room X), image row 0 = top = V 1; arrays are kept in PNG orientation so the scan's OpenGL normals need no re-flipping.
Usage:  python build_wood_floor.py [seed]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter, gaussian_filter1d, map_coordinates

Image.MAX_IMAGE_PIXELS = None
HERE = Path(__file__).resolve()
BUILD = HERE.parents[2] / "Logs" / "FloorBuild"
SRC = BUILD / "src040" / "WoodFloor040_4K-JPG_"
OUT = BUILD / "out"
OUT.mkdir(parents=True, exist_ok=True)
SEED = int(sys.argv[1]) if len(sys.argv) > 1 else 20261029
rng = np.random.default_rng(SEED)

PPM = 819.2                                    # texels per metre
ROOM_X, ROOM_Z = 5.0, 4.0
W, H = 4096, 3280
BOARD = .12
GROOVE_PX = 2.0
TARGET_SRGB = np.array([160, 112, 60]) / 255   # richer honey-brown for the golden-hour room (the original floor read hue ~36, the reference hue ~28 in sun)

# ---------------------------------------------------------------------------------------------------------------- source
col = np.asarray(Image.open(str(SRC) + "Color.jpg").convert("RGB")).astype(np.float32) / 255
rough = np.asarray(Image.open(str(SRC) + "Roughness.jpg").convert("L")).astype(np.float32) / 255
nrm = np.asarray(Image.open(str(SRC) + "NormalGL.jpg").convert("RGB")).astype(np.float32) / 255 * 2 - 1
planks = json.loads((BUILD / "planks040.json").read_text())
planks = [p for p in planks if p["len"] >= 520]
print(len(planks), "source boards,", sum(p["len"] for p in planks) / 2156, "m")
SRC_PX_PER_M = 2156.0
WIDTH_SRC = 228
S = (BOARD * PPM - GROOVE_PX) / WIDTH_SRC            # source -> atlas scale
usage = np.zeros(len(planks))
used_rects = []


def lin(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def srgb(c):
    return np.where(c <= .0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - .055)


def pick_source(length_px, wide_px):
    """A seam-free stretch of a source board at least length_px (atlas) long; prefers lightly used, low-knot boards."""
    need = int(length_px / S) + 24
    cand = [(i, p) for i, p in enumerate(planks) if p["len"] >= need]
    w = np.array([math.exp(-usage[i] * .9) * (1.0 if p["knot"] < .15 else .45) * (p["len"] - need + 80) ** .35 for i, p in cand])
    i, p = cand[rng.choice(len(cand), p=w / w.sum())]
    usage[i] += 1
    y0 = p["y0"] + int(rng.integers(0, p["len"] - need + 1))
    return i, p, y0, need


def warp_grid(h, w):
    """Smooth sideways drift of the grain along the board (a few source texels), so repeats of one stretch do not coincide."""
    amp = rng.uniform(1.5, 4.5)
    drift = gaussian_filter1d(rng.normal(0, 1, h), sigma=rng.uniform(90, 220))
    drift = drift / (np.abs(drift).max() + 1e-6) * amp
    xs = np.arange(w)[None, :] + drift[:, None]
    ys = np.arange(h)[:, None] + np.zeros((1, w))
    return ys, xs


def resample(arr, h_out, w_out, nearest=False):
    if arr.ndim == 2:
        return np.asarray(Image.fromarray(arr).resize((w_out, h_out), Image.LANCZOS if not nearest else Image.NEAREST))
    return np.stack([resample(arr[..., k], h_out, w_out) for k in range(arr.shape[2])], axis=-1)


# ---------------------------------------------------------------------------------------------------------------- layout
board_cols = [(0.0, .10)] + [(.10 + .12 * k, .10 + .12 * (k + 1)) for k in range(40)] + [(4.90, 5.0)]
layout = []                                     # (col index, z0, z1)
prev = None
for ci, (x0, x1) in enumerate(board_cols):
    for attempt in range(200):
        joints, z = [], rng.uniform(.30, 1.0)
        while z < ROOM_Z - .3:
            joints.append(z)
            z += rng.uniform(.50, 1.00)
        if joints and ROOM_Z - joints[-1] > 1.0:                  # cannot happen with <= 1.0 steps, kept as a guard
            joints.append(joints[-1] + 1.0)
        if prev is None or all(min([abs(j - q) for q in prev] + [9]) >= .22 for j in joints):
            break
    prev = joints
    zs = [0.0] + joints + [ROOM_Z]
    for a, b in zip(zs[:-1], zs[1:]):
        layout.append((ci, a, b))
print(len(layout), "boards laid")

albedo = np.zeros((H, W, 3), np.float32)
smooth = np.zeros((H, W), np.float32)
normal = np.zeros((H, W, 3), np.float32); normal[..., 2] = 1
height = np.zeros((H, W), np.float32)           # groove depth field (1 = full groove)
board_id = np.zeros((H, W), np.int32)
tone_map = np.zeros((H, W), np.float32)

for bi, (ci, za, zb) in enumerate(layout):
    x0 = int(round(board_cols[ci][0] * PPM)); x1 = int(round(board_cols[ci][1] * PPM))
    r0 = int(round(za * PPM)); r1 = int(round(zb * PPM))
    w, h = x1 - x0, r1 - r0
    inner_w = w - int(GROOVE_PX)
    s_here = S                                       # one scale for every board; narrow edge boards simply crop less of the scan board
    src_w = min(WIDTH_SRC, int(round(inner_w / S)))
    i, p, y0, need = pick_source(h, inner_w)
    need = int(h / s_here) + 24
    if y0 + need > p["y1"]:
        y0 = p["y1"] - need
    sx0 = p["x0"] + int(rng.integers(0, WIDTH_SRC - src_w + 1))
    crop_c = col[y0:y0 + need, sx0:sx0 + src_w].copy()
    crop_r = rough[y0:y0 + need, sx0:sx0 + src_w].copy()
    crop_n = nrm[y0:y0 + need, sx0:sx0 + src_w].copy()
    fx, fy = rng.random() < .5, rng.random() < .5
    if fx:
        crop_c, crop_r, crop_n = crop_c[:, ::-1], crop_r[:, ::-1], crop_n[:, ::-1].copy(); crop_n[..., 0] *= -1
    if fy:
        crop_c, crop_r, crop_n = crop_c[::-1], crop_r[::-1], crop_n[::-1].copy(); crop_n[..., 1] *= -1
    ys, xs = warp_grid(need, src_w)
    mc = lambda a: np.stack([map_coordinates(a[..., k], [ys, xs], order=1, mode="nearest") for k in range(3)], -1)
    crop_c, crop_n = mc(np.ascontiguousarray(crop_c)), mc(np.ascontiguousarray(crop_n))
    crop_r = map_coordinates(np.ascontiguousarray(crop_r), [ys, xs], order=1, mode="nearest")
    cut = int((need - h / s_here) / 2)
    crop_c, crop_r, crop_n = crop_c[cut:need - cut], crop_r[cut:need - cut], crop_n[cut:need - cut]
    c_out = resample(crop_c, h, inner_w); r_out = resample(crop_r, h, inner_w); n_out = resample(crop_n, h, inner_w)
    n_out = n_out / np.maximum(np.linalg.norm(n_out, axis=-1, keepdims=True), 1e-6)

    # per-board tone: warm/cool and lightness shifts in linear light, a slow gradient along the board, and a faint flat-sawn banding
    L = lin(c_out)
    base = rng.normal(1.0, .055)
    warm = rng.normal(0, .028)
    mult = np.array([base * (1 + warm), base, base * (1 - warm * 1.4)])
    along = (1 + .045 * np.sin(np.linspace(0, rng.uniform(1, 3) * math.pi, h) + rng.uniform(0, 6)))[:, None, None]
    L = np.clip(L * mult[None, None, :] * along, 0, 1)
    c_out = srgb(L)
    gx0 = x0 + int(GROOVE_PX // 2)
    albedo[r0:r1, gx0:gx0 + inner_w] = c_out
    smooth[r0:r1, gx0:gx0 + inner_w] = r_out
    normal[r0:r1, gx0:gx0 + inner_w] = n_out
    board_id[r0:r1, x0:x1] = bi + 1
    tone_map[r0:r1, x0:x1] = rng.normal(0, 1)
    # the groove columns take the neighbour-most pixel so there is no hole
    albedo[r0:r1, x0:gx0] = c_out[:, :1]; albedo[r0:r1, gx0 + inner_w:x1] = c_out[:, -1:]
    smooth[r0:r1, x0:gx0] = r_out[:, :1]; smooth[r0:r1, gx0 + inner_w:x1] = r_out[:, -1:]

# ---------------------------------------------------------------------------------------------------------------- seams
# distance (in texels) to the nearest board boundary: side grooves and end joints
edge = np.zeros((H, W), bool)
edge[:, 1:] |= board_id[:, 1:] != board_id[:, :-1]
edge[1:, :] |= board_id[1:, :] != board_id[:-1, :]
from scipy.ndimage import distance_transform_edt
dist = distance_transform_edt(~edge)
groove = np.clip(1 - dist / 1.7, 0, 1) ** 1.2                         # ~3.4 texel wide V-groove (about 4 mm)
shoulder = np.clip(1 - dist / 5.5, 0, 1)                               # soft bevel / occlusion shoulder
albedo_lin = lin(albedo)
albedo_lin *= (1 - .78 * groove)[..., None] * (1 - .10 * shoulder ** 1.5)[..., None]
# very slight wood-dust tone inside the groove
albedo_lin[..., 2] *= 1 + .12 * groove
albedo = srgb(albedo_lin)

# ---------------------------------------------------------------------------------------------------------------- tone: muted original grade
mean_lin = lin(albedo).reshape(-1, 3).mean(0)
gain = lin(TARGET_SRGB) / mean_lin
albedo = np.clip(srgb(np.clip(lin(albedo) * gain[None, None, :], 0, 1)), 0, 1)
print("grade gain (linear)", gain.round(3))

# ---------------------------------------------------------------------------------------------------------------- smoothness (satin polyurethane): from the scan's roughness, remapped, plus wear
r = smooth
rn = (r - np.percentile(r, 2)) / (np.percentile(r, 98) - np.percentile(r, 2) + 1e-6)
sm = .625 - (np.clip(rn, 0, 1) - .5) * .12                              # .64 - .76 from the grain itself (satin polyurethane: soft, readable reflections, not a mirror)
big = gaussian_filter(rng.normal(0, 1, (H // 8, W // 8)).astype(np.float32), 18)
big = np.array(Image.fromarray(big).resize((W, H), Image.BICUBIC))
big /= (big.std() + 1e-6)
sm += big * .022                                                        # broad wear / traffic patches
streak = gaussian_filter(rng.normal(0, 1, (H // 2, W // 16)).astype(np.float32), (14, 1.2))
streak = np.array(Image.fromarray(streak).resize((W, H), Image.BICUBIC)); streak /= (streak.std() + 1e-6)
sm += streak * .014                                                      # long scuff streaks along the boards
sm += (tone_map * .008)                                                  # board-to-board finish difference
sm = np.clip(sm, .52, .74)
sm = sm * (1 - groove) + .30 * groove                                    # dust and open pores in the grooves are rougher

# ---------------------------------------------------------------------------------------------------------------- normal: scan relief + groove bevel
# groove height profile -> slope
hf = -(groove * 1.0) - .25 * shoulder ** 2
hf = gaussian_filter(hf, .7)
gy, gx = np.gradient(hf)
BEVEL = 2.4
nx = nrm_x = normal[..., 0] * .85 - gx * BEVEL
ny = normal[..., 1] * .85 + gy * BEVEL                                 # image rows run from far (top) to near, so +row = -Z: flip sign below when stored
nz = np.sqrt(np.clip(1 - nx ** 2 - ny ** 2, .05, 1))
nn = np.stack([nx, ny, nz], -1); nn /= np.linalg.norm(nn, axis=-1, keepdims=True)

# ---------------------------------------------------------------------------------------------------------------- save (PNG orientation)
A = np.zeros((H, W, 4), np.uint8)
A[..., :3] = (np.clip(albedo, 0, 1) * 255 + .5).astype(np.uint8)
A[..., 3] = (np.clip(sm, 0, 1) * 255 + .5).astype(np.uint8)
N = ((nn * .5 + .5) * 255 + .5).astype(np.uint8)
np.save(OUT / "_layout.npy", board_id)
for arr, name, mode in ((A, "floor_albedo_smooth.png", "RGBA"), (N, "floor_normal.png", "RGB")):
    Image.fromarray(arr, mode).save(OUT / name)
Image.fromarray(A[..., :3]).resize((1280, 1025), Image.LANCZOS).save(OUT / "floor_preview_albedo.png")
print("saved", OUT, "| mean sRGB", (A[..., :3].reshape(-1, 3).mean(0)).round(1), "smooth", sm.mean().round(3), "usage max", usage.max())
