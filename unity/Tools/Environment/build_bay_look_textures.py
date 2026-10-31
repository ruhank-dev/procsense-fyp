"""PatientBay look-development textures (procedural, seeded, tileable where needed).

Outputs (default Logs/LookUpgrade, copied into Assets/Textures/Hospital/PatientBayLook by PatientBayLookUpgrade.cs):
  vinyl_floor_albedo.png / vinyl_floor_normal.png   speckled rubberised sheet vinyl, 1.25 m tile
  window_exterior.png                               procedural daylight view (sky, clouds, tree line), nothing above 0.92
  curtain_teal/curtain_privacy _albedo/_normal      woven damask-style print
  ceiling_tile_albedo/_normal                       acoustic tile, 0.6 m
  contact_shadow.png                                soft rounded-box falloff, alpha only (black RGB)
Run: python build_bay_look_textures.py [out_dir]
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2] / "Logs" / "LookUpgrade"
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(20261028)


def to_u8(a):
    return (np.clip(a, 0, 1) * 255 + .5).astype(np.uint8)


def srgb(c):
    return np.array(c, dtype=np.float64) / 255.0


def periodic_noise(n, cells, rng):
    """Smooth tileable value noise, 0..1."""
    g = rng.random((cells, cells))
    t = np.linspace(0, cells, n, endpoint=False)
    i0 = np.floor(t).astype(int)
    f = t - i0
    f = f * f * (3 - 2 * f)
    i1 = (i0 + 1) % cells
    a = g[i0][:, i0] * (1 - f)[:, None] * (1 - f)[None, :] + g[i0][:, i1] * (1 - f)[:, None] * f[None, :]
    b = g[i1][:, i0] * f[:, None] * (1 - f)[None, :] + g[i1][:, i1] * f[:, None] * f[None, :]
    return a + b


def normal_from_height(h, strength):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5
    gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5
    n = np.dstack([-gx * strength, gy * strength, np.ones_like(h)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * .5 + .5


# ---------------------------------------------------------------- vinyl floor (1.25 m tile, 1024 px -> ~1.2 mm/px)
def vinyl():
    n = 1024
    base = srgb((176, 167, 151))
    img = np.ones((n, n, 3)) * base
    mottle = periodic_noise(n, 6, rng) * .6 + periodic_noise(n, 17, rng) * .4
    img *= (1 + (mottle - .5) * .06)[..., None]                       # broad tonal drift, +-3 %
    height = np.zeros((n, n))
    palette = [(srgb((112, 106, 98)), .50), (srgb((205, 198, 184)), .30), (srgb((98, 122, 118)), .10),
               (srgb((70, 98, 128)), .05), (srgb((150, 120, 96)), .05)]
    cols = np.array([p[0] for p in palette])
    probs = np.array([p[1] for p in palette]); probs /= probs.sum()
    for _ in range(26000):                                              # chips: 2-5 px (about 2-6 mm), wrap-around
        cx, cy = rng.integers(0, n, 2)
        r = rng.uniform(1.0, 2.6)
        w = rng.choice(len(palette), p=probs)
        ys = (np.arange(cy - 4, cy + 5) % n)[:, None]
        xs = (np.arange(cx - 4, cx + 5) % n)[None, :]
        d = np.hypot((ys - cy + n / 2) % n - n / 2, (xs - cx + n / 2) % n - n / 2) * np.ones((9, 9))
        m = np.clip(1.6 - d / r, 0, 1) ** 1.0
        blend = m[..., None] * .42
        patch = img[ys, xs]
        img[ys, xs] = patch * (1 - blend) + cols[w] * blend
        height[ys, xs] += m * .25
    img *= (1 + (rng.random((n, n)) - .5) * .035)[..., None]            # fine grain
    height += periodic_noise(n, 64, rng) * .08
    Image.fromarray(to_u8(img)).save(OUT / "vinyl_floor_albedo.png")
    Image.fromarray(to_u8(normal_from_height(height, 1.6))).save(OUT / "vinyl_floor_normal.png")


# ---------------------------------------------------------------- window exterior (procedural daylight view, no photograph)
def blur(a, r):
    from PIL import ImageFilter
    if a.ndim == 3:
        return np.dstack([blur(a[..., i], r) for i in range(a.shape[2])])
    im = Image.fromarray(to_u8(a)).filter(ImageFilter.GaussianBlur(r))
    return np.asarray(im).astype(np.float64) / 255.0


def window():
    w, h = 1024, 576                                                    # 1.94 m x 1.08 m opening
    v = np.linspace(0, 1, h)[:, None] * np.ones((1, w))
    x = np.linspace(0, 1, w)[None, :] * np.ones((h, 1))
    top = np.array([.42, .62, .92]); hor = np.array([.86, .92, .97])
    sky = top * (1 - v[..., None] ** .7) + hor * (v[..., None] ** .7)
    cl = periodic_noise(1024, 5, rng)[:h, :1024] * .6 + periodic_noise(1024, 14, rng)[:h, :1024] * .4
    cloud = np.clip((cl - .5) * 3.2, 0, 1) * (1 - v) ** .5
    sky = sky * (1 - cloud[..., None] * .75) + np.array([.97, .97, .98]) * cloud[..., None] * .75
    img = sky
    # distant buildings and a tree line, then nearer canopy; all soft and low contrast
    far_top = np.full(w, .64)
    x0 = 0
    while x0 < w:                                                       # a few pale distant buildings between tree mounds
        bw = int(rng.integers(40, 120)); bh = rng.uniform(.52, .62)
        if rng.random() < .45: far_top[x0:x0 + bw] = bh
        x0 += bw
    mounds = .62 + .035 * (periodic_noise(1024, 24, rng)[0, :w] - .5) * 2
    far_top = np.minimum(far_top, mounds + .02)
    far = (v > far_top[None, :]).astype(float)
    far_col = np.array([.60, .66, .66]) * (1 + (periodic_noise(1024, 60, rng)[:h, :1024] - .5) * .2)[..., None]
    img = img * (1 - far[..., None]) + far_col * far[..., None]
    canopy = .70 + .06 * (periodic_noise(1024, 12, rng)[0, :w] - .5) * 2
    near = (v > canopy[None, :]).astype(float)
    leaf = (periodic_noise(1024, 200, rng)[:h, :1024] * .5 + periodic_noise(1024, 60, rng)[:h, :1024] * .5)
    near_col = np.array([.24, .38, .20]) * (.55 + leaf[..., None] * 1.0)
    img = img * (1 - near[..., None]) + near_col * near[..., None]
    img = blur(img, 1.6)
    img = np.clip(img, 0, .92)                                          # nothing clips
    Image.fromarray(to_u8(img)).save(OUT / "window_exterior.png")


# ---------------------------------------------------------------- curtains: woven damask-like print, tileable
def damask(name, base, motif, contrast):
    """Woven damask-style print at 1024 px per tile: crisp outlined rosettes, leaf sprigs and a fine twill, tileable."""
    from PIL import ImageDraw
    import math
    n = 1024
    img = Image.new("RGB", (n, n), tuple(base))
    d = ImageDraw.Draw(img)
    dark = tuple(int(c * .78) for c in base)
    step = 256
    for oy in range(-1, 6):
        for ox in range(-1, 6):
            cx = ox * step + (step // 2 if oy % 2 else 0)
            cy = oy * step * 3 // 4
            for ring, (rad, pw, ph, wd) in enumerate(((54, 22, 15, 5), (78, 14, 9, 4))):      # two rings of petals
                for k in range(8):
                    a0 = k * 45 + (22.5 if ring else 0)
                    px = cx + math.cos(math.radians(a0)) * rad
                    py = cy + math.sin(math.radians(a0)) * rad
                    d.ellipse((px - pw, py - ph, px + pw, py + ph), outline=tuple(motif), width=wd)
            d.ellipse((cx - 20, cy - 20, cx + 20, cy + 20), fill=tuple(motif))
            d.ellipse((cx - 9, cy - 9, cx + 9, cy + 9), fill=dark)
            for sx in (-1, 1):                                                                # leaf sprigs between rosettes
                d.arc((cx + sx * 96, cy + 70, cx + sx * 96 + 86, cy + 190), 200, 340, fill=tuple(motif), width=6)
                d.ellipse((cx + sx * 96 + 36, cy + 150, cx + sx * 96 + 56, cy + 176), outline=tuple(motif), width=4)
    a = np.asarray(img).astype(np.float64) / 255.0
    a = blur(a, .55)
    x = np.arange(n)
    twill = (np.sin((x[None, :] + x[:, None]) * 1.9) * .5 + np.sin(x[None, :] * 2.3) * np.sin(x[:, None] * 2.3) * .5) * .022
    a = np.clip(a + (twill * contrast)[..., None], 0, 1)
    Image.fromarray(to_u8(a)).save(OUT / f"{name}_albedo.png")
    hgt = a.mean(axis=2) * .6 + twill
    Image.fromarray(to_u8(normal_from_height(hgt, 1.6))).save(OUT / f"{name}_normal.png")


def cloth_detail():
    """Detail normal for soft goods: broad irregular creases layered over a fine woven twill (tileable)."""
    n = 1024
    ridged = lambda cells: 1 - np.abs(periodic_noise(n, cells, rng) * 2 - 1)
    creases = ridged(5) * .55 + ridged(11) * .3 + ridged(23) * .15
    x = np.arange(n)
    weave = (np.sin((x[None, :] + x[:, None]) * 2.0) * .5 + np.sin(x[None, :] * 3.1) * np.sin(x[:, None] * 3.1) * .5) * .02
    h = blur(creases, 2.5) * .9 + weave
    Image.fromarray(to_u8(normal_from_height(h, 5.5))).save(OUT / "cloth_detail_normal.png")


def curtains():
    damask("curtain_teal", (36, 104, 106), (92, 160, 158), 1.0)
    damask("curtain_privacy", (150, 184, 170), (118, 160, 146), .8)


# ---------------------------------------------------------------- acoustic ceiling tile (0.6 m)
def ceiling():
    n = 512
    base = srgb((238, 237, 230))
    img = np.ones((n, n, 3)) * base
    h = periodic_noise(n, 48, rng) * .6 + periodic_noise(n, 10, rng) * .4
    img *= (1 + (h - .5) * .05)[..., None]
    height = np.zeros((n, n))
    for _ in range(900):                                                # short irregular fissures
        cx, cy = rng.integers(0, n, 2)
        ln = rng.integers(6, 22); ang = rng.uniform(0, np.pi)
        for t in range(ln):
            x = int(cx + np.cos(ang) * t) % n; y = int(cy + np.sin(ang) * t + np.sin(t * .6)) % n
            img[y, x] *= .80; height[y, x] -= .6
    for _ in range(5000):                                               # pinholes
        x, y = rng.integers(0, n, 2)
        img[y, x] *= .70; height[y, x] -= .5
    Image.fromarray(to_u8(img)).save(OUT / "ceiling_tile_albedo.png")
    Image.fromarray(to_u8(normal_from_height(blur(height, .6), 1.4))).save(OUT / "ceiling_tile_normal.png")


# ---------------------------------------------------------------- patient: skin pore relief and printed hospital-gown fabric
def skin():
    n = 512
    h = periodic_noise(n, 96, rng) * .5 + periodic_noise(n, 40, rng) * .3 + periodic_noise(n, 12, rng) * .2
    for _ in range(2600):                                               # pores: tiny round dimples
        cx, cy = rng.integers(0, n, 2); r = rng.uniform(.8, 1.9)
        ys = (np.arange(cy - 3, cy + 4) % n)[:, None]; xs = (np.arange(cx - 3, cx + 4) % n)[None, :]
        d = np.hypot((ys - cy + n / 2) % n - n / 2, (xs - cx + n / 2) % n - n / 2) * np.ones((7, 7))
        h[ys, xs] -= np.clip(1.4 - d / r, 0, 1) * .5
    Image.fromarray(to_u8(normal_from_height(blur(h, .7), 2.0))).save(OUT / "skin_pores_normal.png")


def gown():
    from PIL import ImageDraw
    n = 512
    base = (146, 194, 190)
    img = Image.new("RGB", (n, n), base)
    d = ImageDraw.Draw(img)
    for oy in range(0, n, 64):                                          # small repeating diamond print
        for ox in range(0, n, 64):
            for dx, dy in ((0, 0), (32, 32)):
                cx, cy = ox + dx, oy + dy
                d.polygon([(cx, cy - 9), (cx + 9, cy), (cx, cy + 9), (cx - 9, cy)], fill=(206, 230, 226))
                d.ellipse((cx - 2, cy - 2, cx + 2, cy + 2), fill=(112, 168, 166))
    a = blur(np.asarray(img).astype(float) / 255, .8)
    weave = (np.sin(np.arange(n)[None, :] * 3.1) * np.sin(np.arange(n)[:, None] * 3.1)) * .02
    a = np.clip(a + weave[..., None], 0, 1)
    Image.fromarray(to_u8(a)).save(OUT / "gown_print_albedo.png")


# ---------------------------------------------------------------- contact shadow
def contact():
    n = 256
    u = (np.arange(n) + .5) / n * 2 - 1
    xx, yy = np.meshgrid(u, u)
    d = np.maximum(np.abs(xx), np.abs(yy)) * .5 + np.hypot(xx, yy) * .5 / 1.4142 * 1.0   # rounded-box-ish distance
    a = np.clip(1 - d / .95, 0, 1)
    a = a * a * (3 - 2 * a)
    a = a ** 1.3 * .88                                  # darker, tighter core: still reads on the mid-tone wood floor
    rgba = np.zeros((n, n, 4), dtype=np.uint8)
    rgba[..., 3] = to_u8(a)
    Image.fromarray(rgba, "RGBA").save(OUT / "contact_shadow.png")


if __name__ == "__main__":
    vinyl(); window(); curtains(); cloth_detail(); ceiling(); skin(); gown(); contact()
    print("wrote", sorted(p.name for p in OUT.iterdir()))
