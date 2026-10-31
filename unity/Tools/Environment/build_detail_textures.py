"""Seamless detail textures for the final-look PatientBay (all procedural, original). Writes albedo/normal PNGs to <out_dir>.
Usage: python build_detail_textures.py <out_dir>
  vinyl_floor_albedo/normal   2048  grey medical vinyl, fine speckle + soft marbling, 1 m repeat
  ceiling_tile_albedo/normal  2048  2x2 acoustic tiles (600 mm), pinhole perforation, fissures, recessed grid line
  wall_paint_normal           1024  orange-peel roller texture
  sofa_weave_albedo/normal    1024  teal woven upholstery (heavy slub weave), 0.30 m repeat
  curtain_albedo/normal       1024  blue hospital curtain fabric, fine weave with a faint stripe, 0.20 m repeat
  gown_albedo/normal          1024  pale-blue gown cotton with a small repeating dot print, 0.15 m repeat
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(20261031)


def noise(n, sigma, amp=1.0):
    a = gaussian_filter(rng.standard_normal((n, n)), sigma, mode="wrap")
    return amp * a / (a.std() + 1e-9)


def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5 * strength
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5 * strength
    nz = np.ones_like(h)
    ln = np.sqrt(gx * gx + gy * gy + nz * nz)
    n = np.stack([-gx / ln, gy / ln, nz / ln], -1)                  # OpenGL (Y up) convention, as the project's other normal maps
    return ((n * .5 + .5) * 255).astype(np.uint8)


def save(name, arr):
    Image.fromarray(arr).save(out / name)
    print("wrote", name, arr.shape)


def rgb(base, var):
    return np.clip(np.stack([base[i] + var for i in range(3)], -1), 0, 1)


# ---- vinyl floor ----------------------------------------------------------------------------------------------------------------------------------------------
N = 2048
sp = noise(N, .9) * .5 + noise(N, 2.5) * .5
flecks = (rng.random((N, N)) > .9965) * rng.uniform(.5, 1.0, (N, N))
marble = noise(N, 38) * .6 + noise(N, 14) * .4
var = .008 * sp + .008 * marble + .04 * gaussian_filter(flecks, .8, mode="wrap")
save("vinyl_floor_albedo.png", (rgb((.62, .65, .70), var) * 255).astype(np.uint8))
save("vinyl_floor_normal.png", normal_from_height(.5 * sp + .8 * gaussian_filter(flecks, .8, mode="wrap") * 4, 1.4))

# ---- ceiling tile -----------------------------------------------------------------------------------------------------------------------------------------------
h = np.zeros((N, N)); alb = np.ones((N, N)) * .86
T = N // 2
yy, xx = np.mgrid[0:N, 0:N]
fis = noise(N, 2.2) * .6 + noise(N, 7) * .4
alb -= .045 * (fis > 1.0) + .02 * noise(N, 20)
step = 24
for py in range(step // 2, N, step):                                  # pinhole perforation on a regular staggered grid
    for px in range(step // 2 + (py // step % 2) * step // 2, N, step):
        r = 3 + int(rng.integers(0, 2))
        y0, y1, x0, x1 = max(py - r, 0), min(py + r + 1, N), max(px - r, 0), min(px + r + 1, N)
        m = ((yy[y0:y1, x0:x1] - py) ** 2 + (xx[y0:y1, x0:x1] - px) ** 2) <= r * r
        alb[y0:y1, x0:x1][m] -= .13; h[y0:y1, x0:x1][m] -= .6
for k in (0, T):                                                      # recessed tile joints
    alb[k:k + 6, :] -= .12; alb[:, k:k + 6] -= .12; h[k:k + 6, :] -= 1.5; h[:, k:k + 6] -= 1.5
h += .25 * fis
g = np.clip(alb, 0, 1)
save("ceiling_tile_albedo.png", (np.stack([g * 1.0, g * .995, g * .975], -1) * 255).astype(np.uint8))
save("ceiling_tile_normal.png", normal_from_height(gaussian_filter(h, .8, mode="wrap"), 1.0))

# ---- wall paint (orange peel) -------------------------------------------------------------------------------------------------------------------------
M = 1024
peel = noise(M, 1.4) * .6 + noise(M, 3.5) * .4
save("wall_paint_normal.png", normal_from_height(peel, .9))


# ---- woven fabrics -------------------------------------------------------------------------------------------------------------------------------------------
def weave(n, pitch, slub, over=.5):
    """Plain weave height field: alternating over/under threads with per-thread slub variation."""
    y, x = np.mgrid[0:n, 0:n] / pitch
    wx = np.sin(np.pi * x) ** 2; wy = np.sin(np.pi * y) ** 2
    parity = ((np.floor(x) + np.floor(y)) % 2)
    hgt = np.where(parity == 0, wx * (1 - over) + over * .3, wy * (1 - over) + over * .3)
    ty = rng.standard_normal(int(n / pitch) + 2); tx = rng.standard_normal(int(n / pitch) + 2)
    slubv = tx[np.floor(x).astype(int) % len(tx)] * .5 + ty[np.floor(y).astype(int) % len(ty)] * .5
    return hgt + slub * slubv


hs = weave(M, 8, .35) + .25 * noise(M, 5)
base = (.20, .54, .50)
v = .10 * (hs - hs.mean())
save("sofa_weave_albedo.png", (rgb(base, v) * 255).astype(np.uint8))
save("sofa_weave_normal.png", normal_from_height(hs, 2.2))

hc = weave(M, 5, .15) + .1 * noise(M, 4)
stripe = np.sin(np.arange(M) / M * 2 * np.pi * 16)[None, :] * .02
v = .07 * (hc - hc.mean()) + stripe
save("curtain_albedo.png", (rgb((.30, .43, .62), v) * 255).astype(np.uint8))
save("curtain_normal.png", normal_from_height(hc, 1.6))

hg = weave(M, 4, .08)
dots = np.zeros((M, M)); gy, gx = np.mgrid[0:M, 0:M]
for cy in range(32, M, 128):
    for cx in range(32 + (cy // 128 % 2) * 64, M, 128):
        dots[((gy - cy) ** 2 + (gx - cx) ** 2) < 49] = 1
v = .05 * (hg - hg.mean())
img = rgb((.34, .66, .66), v)
save("gown_albedo.png", (img * 255).astype(np.uint8))
save("gown_normal.png", normal_from_height(hg, 1.2))

# ---- ceiling light diffuser (albedo doubles as the emission map; LDR-safe, Quest has no HDR) ------------------------------------------------------
D = 1024
yy, xx = np.mgrid[0:D, 0:D] / (D - 1)
edge = np.minimum(np.minimum(xx, 1 - xx), np.minimum(yy, 1 - yy))
fall = np.clip(edge / .16, 0, 1) ** .6                                # soft darkening toward the frame
prism = .035 * (np.sin(xx * 2 * np.pi * 40) * np.sin(yy * 2 * np.pi * 40) > 0)   # fine prismatic pattern
lens = .90 * (.72 + .28 * fall) - prism
lens[edge < .012] = .38                                               # visible frame lip
lens += .012 * noise(D, 3)
g = np.clip(lens, 0, 1)
save("ceiling_diffuser_albedo.png", (np.stack([g, g * .995, g * .97], -1) * 255).astype(np.uint8))

# ---- skin micro detail (URP detail maps: tiled over the patient's UVs for pores and fine lines; the albedo detail is mid-grey mottling, x2 multiply) ------------------------
S = 1024
hp = np.zeros((S, S))
pts = rng.integers(0, S, (14000, 2))
hp[pts[:, 0], pts[:, 1]] = rng.uniform(.4, 1.0, 14000)
pores = gaussian_filter(hp, 1.3, mode="wrap"); pores /= pores.max()
fine = gaussian_filter(rng.standard_normal((S, S)), (.7, 5.5), mode="wrap") + gaussian_filter(rng.standard_normal((S, S)), (5.5, .7), mode="wrap")
fine /= np.abs(fine).max()
skin_h = -2.2 * pores + .35 * fine + .15 * noise(S, 2)
save("skin_detail_normal.png", normal_from_height(skin_h, 5.0))
mott = noise(S, 18) * .5 + noise(S, 6) * .35 + noise(S, 2) * .15
red = noise(S, 28)
alb = np.stack([.5 + .020 * mott + .016 * red, .5 + .018 * mott - .006 * red, .5 + .020 * mott - .012 * red], -1)
save("skin_detail_albedo.png", (np.clip(alb, 0, 1) * 255).astype(np.uint8))
