"""Second-generation city art (owner review 1 Nov 2026: the first facades were flat colour blocks): six facade tiles at 2048 px with bevelled aluminium frames, sky and skyline reflections in the glass, blinds and
lit interiors, frit spandrels, slab edges, brick courses, balconies with railings and rain streaks; road, sidewalk and plaza paving at 1024 px with cracks, patches and wear; a leaf-cluster atlas and bark for the trees.

  python unity/Tools/Environment/make_city_art_v2.py      -> unity/Logs/CityArt/*.png   (same names as v1, so the Unity import and materials are unchanged)

A facade tile still covers 4 bays x 4 floors, now 2048 px square (512 px per bay), so the building generator's UVs do not change. Smoothness is packed in the metallic map's alpha.
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as ndi

import make_city_textures as v1

OUT = v1.OUT
S = 2048
BAYS = FLOORS = 4
BW = S // BAYS
LIGHT = np.array([-.62, -.78], np.float32)                      # sun from the upper left (x, y in image space)


def clamp(a):
    return np.clip(a, 0, 255).astype(np.uint8)


def noise(h, w, cells, r, octaves=3):
    out = np.zeros((h, w), np.float32); amp = 1.0; tot = 0
    for o in range(octaves):
        n = max(2, cells * 2 ** o)
        small = r.random((n, max(2, int(n * w / h)))).astype(np.float32)
        out += amp * ndi.zoom(small, (h / small.shape[0], w / small.shape[1]), order=3)[:h, :w]; tot += amp; amp *= .5
    return out / tot


def edge_light(mask, blur):
    """Directional bevel shading from a mask: +1 on edges facing the sun, -1 on the opposite edges."""
    m = ndi.gaussian_filter(mask.astype(np.float32), blur)
    gy, gx = np.gradient(m)
    return np.clip(-(gx * LIGHT[0] + gy * LIGHT[1]) * blur * 2.2, -1, 1)


def normal_from_height(hm, strength):
    gy, gx = np.gradient(hm.astype(np.float32))
    n = np.stack([-gx * strength, gy * strength, np.ones_like(hm, dtype=np.float32)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return clamp((n * .5 + .5) * 255)


class Cell:
    def __init__(self, h, w):
        self.a = np.zeros((h, w, 3), np.float32); self.h = np.zeros((h, w), np.float32); self.m = np.zeros((h, w), np.float32); self.s = np.zeros((h, w), np.float32)
        self.H, self.W = h, w
        self.yy, self.xx = np.mgrid[0:h, 0:w]

    def fill(self, mask, colour, height=None, metal=None, smooth=None, jitter=0.0, r=None):
        col = np.array(colour, np.float32)
        for c in range(3):
            self.a[..., c][mask] = col[c]
        if jitter and r is not None:
            self.a[mask] += r.normal(0, jitter, (int(mask.sum()), 1))
        if height is not None: self.h[mask] = height
        if metal is not None: self.m[mask] = metal
        if smooth is not None: self.s[mask] = smooth

    def shade(self, mask, amount, blur=3.0, height=3.0):
        el = edge_light(mask, blur)
        self.a *= (1 + amount * el)[..., None]
        self.h += el * height


def sky_reflection(cell, r, top, bottom, sky_noise_crop, tint, strength=1.0):
    """What a glass pane shows: a bright sky above, a darker street below, soft cloud texture continuing across neighbouring panes."""
    yy = cell.yy / cell.H
    base = np.array(top, np.float32)[None, None, :] * (1 - yy[..., None]) + np.array(bottom, np.float32)[None, None, :] * yy[..., None]
    cloud = (sky_noise_crop - .5) * 70 * strength
    return np.clip(base + cloud[..., None] * np.array([1, 1, 1.05]) + np.array(tint, np.float32), 0, 255)


def pane_contents(cell, mask, r, sky, kind_tone):
    """Fills a vision pane: reflection by default, sometimes blinds, sometimes a lit interior."""
    roll = r.random()
    a = sky.copy()
    ys, xs = np.nonzero(mask)
    if ys.size == 0:
        return
    y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
    ph = y1 - y0
    if roll < .20:                                                         # lit interior seen through the glass
        interior = np.array((46, 52, 58), np.float32) * (.8 + .4 * r.random())
        sub = np.zeros_like(a); sub[:] = interior
        yy = (cell.yy - y0) / max(ph, 1)
        sub += np.where((yy > .03) & (yy < .09), 1, 0)[..., None] * np.array((190, 180, 150), np.float32) * (.5 + .4 * r.random())      # ceiling light strip
        desk = (yy > .62) & (yy < .70) & (cell.xx > x0 + (x1 - x0) * .15) & (cell.xx < x0 + (x1 - x0) * (.55 + .3 * r.random()))
        sub[desk] = (30, 30, 32)
        a = a * .35 + sub * .65
    elif roll < .46:                                                        # blinds drawn to a random depth
        depth = .35 + .6 * r.random(); slat = 9
        bl = (cell.yy - y0 < ph * depth) & mask
        pattern = (np.sin((cell.yy - y0) / slat * math.pi * 2) * .5 + .5)
        blind = np.array((205, 203, 196), np.float32)[None, None, :] * (.82 + .18 * pattern[..., None])
        a[bl] = a[bl] * .25 + blind[bl] * .75
    elif roll < .53:                                                        # curtains
        cw = (cell.xx - x0) / max(x1 - x0, 1)
        side = (cw < .28) | (cw > .72)
        cur = np.array((196, 188, 170), np.float32) * (.78 + .22 * np.sin((cell.xx) / 6.0))[..., None]
        a[side & mask] = a[side & mask] * .3 + cur[side & mask] * .7
    cell.a[mask] = a[mask]
    cell.m[mask] = .6; cell.s[mask] = .95; cell.h[mask] = -2.0
    # reveal: a thin shadow under the head of the opening
    reveal = mask & (cell.yy < y0 + 10)
    cell.a[reveal] *= .62


def curtain_cell(r, tone, glass_tint, tile_sky, cx, cy):
    c = Cell(BW, BW); H = W = BW
    frame_col = (150 + 6 * tone, 154 + 6 * tone, 158 + 6 * tone)
    c.fill(np.ones((H, W), bool), frame_col, 0, .85, .45)
    mull = 16
    glass_mask = (c.yy > 14) & (c.yy < H * .74) & (c.xx > mull) & (c.xx < W - mull)
    sky = sky_reflection(c, r, (176, 204, 232), (60, 74, 90), tile_sky[cy * BW:(cy + 1) * BW, cx * BW:(cx + 1) * BW], glass_tint, 1.0)
    pane_contents(c, glass_mask, r, sky, tone)
    c.shade(glass_mask, -.55, 4, -1.5)                                            # glass sits behind the frame: frame edge catches light, glass edge falls into shadow
    frame_mask = ~glass_mask
    c.shade(frame_mask & (c.yy < H * .74 + 6), .5, 3, 3.0)
    spand = (c.yy >= H * .74 + 12) & (c.yy < H - 46) & (c.xx > mull) & (c.xx < W - mull)
    dots = ((c.xx // 7 + c.yy // 7) % 2 == 0)
    sp_base = np.array(glass_tint, np.float32) * .0 + np.array((46, 60, 72), np.float32) * (.85 + .3 * r.random())
    c.fill(spand, sp_base, -1.0, .45, .78)
    c.a[spand & dots] += 14
    c.shade(spand, -.4, 3, -1.2)
    slab = c.yy >= H - 40
    c.fill(slab, (176 + 6 * tone, 178 + 6 * tone, 178 + 6 * tone), 4.0, 0.0, .3)
    c.a[slab] += r.normal(0, 5, (int(slab.sum()), 1))
    under = (c.yy >= H - 40) & (c.yy < H - 34)
    c.a[under] *= .55
    c.shade(slab, .25, 3, 2.0)
    # transom mid-height of the vision glass
    c.fill((c.yy > H * .37) & (c.yy < H * .37 + 8) & glass_mask, frame_col, 2.0, .85, .45)
    return c


def mirror_cell(r, tone, tile_sky, cx, cy):
    c = Cell(BW, BW); H = W = BW
    sky = sky_reflection(c, r, (190, 214, 238), (84, 104, 124), tile_sky[cy * BW:(cy + 1) * BW, cx * BW:(cx + 1) * BW], (-6, 0, 6), 1.4)
    c.a[:] = sky * (.92 + .08 * tone)
    c.m[:] = .9; c.s[:] = .97
    seam = (c.xx < 5) | (c.xx >= W - 5) | (c.yy >= H - 7)
    c.fill(seam, (126, 132, 140), 2.0, .9, .6)
    return c


def ribbon_cell(r, tone, tile_sky, cx, cy):
    c = Cell(BW, BW); H = W = BW
    c.fill(np.ones((H, W), bool), (178 + 5 * tone, 178 + 5 * tone, 174 + 5 * tone), 0, 0, .25, 4, r)
    band = (c.yy > H * .24) & (c.yy < H * .76)
    sky = sky_reflection(c, r, (170, 198, 226), (54, 68, 84), tile_sky[cy * BW:(cy + 1) * BW, cx * BW:(cx + 1) * BW], (-8, -4, 0))
    pane_contents(c, band, r, sky, tone)
    c.shade(band, -.5, 4, -2.0)
    for xm in (0, W // 2):
        c.fill((c.xx >= xm) & (c.xx < xm + 10) & band, (62, 66, 72), 3.0, .85, .5)
    fin = (c.yy > H * .24 - 20) & (c.yy <= H * .24)                                  # projecting sunshade
    c.fill(fin, (206, 208, 206), 5.0, 0, .3)
    c.shade(fin, .35, 3, 2)
    shadow = (c.yy > H * .24) & (c.yy < H * .24 + 26)
    c.a[shadow] *= .72
    j = (c.xx < 3) | (c.xx >= W - 3)
    c.a[j] *= .6; c.h[j] -= 1.5
    return c


def apartment_cell(r, tone, tile_sky, cx, cy):
    c = Cell(BW, BW); H = W = BW
    brick = r.random() < .5
    if brick:
        wall = np.array((152 + 8 * tone, 92 + 6 * tone, 74), np.float32)
        c.fill(np.ones((H, W), bool), wall, 0, 0, .15)
        rows = c.yy // 14; off = (rows % 2) * 18
        bx = (c.xx + off) // 36
        tone_b = ndi.zoom(r.normal(1, .08, (H // 14 + 2, W // 36 + 3)), (14, 36), order=0)[:H, :W]
        c.a *= tone_b[..., None]
        mortar = ((c.yy % 14) < 2) | (((c.xx + off) % 36) < 2)
        c.a[mortar] = (190, 184, 170); c.h[mortar] = -1.2
    else:
        c.fill(np.ones((H, W), bool), (218 + 4 * tone, 205 + 4 * tone, 184 + 4 * tone), 0, 0, .12, 3, r)
    win = (c.yy > H * .12) & (c.yy < H * .60) & (c.xx > W * .28) & (c.xx < W * .72)
    sky = sky_reflection(c, r, (168, 196, 222), (50, 62, 76), tile_sky[cy * BW:(cy + 1) * BW, cx * BW:(cx + 1) * BW], (-10, -4, 0))
    pane_contents(c, win, r, sky, tone)
    frame = ((c.yy > H * .12 - 12) & (c.yy < H * .60 + 12) & (c.xx > W * .28 - 12) & (c.xx < W * .72 + 12)) & ~win
    c.fill(frame, (236, 236, 232), 3.0, 0, .35)
    c.fill(win & (np.abs(c.xx - W * .50) < 5), (236, 236, 232), 3.0, 0, .35)
    c.shade(win, -.5, 3, -1.5); c.shade(frame, .4, 3, 2)
    sill = (c.yy >= H * .60 + 12) & (c.yy < H * .60 + 28) & (c.xx > W * .25) & (c.xx < W * .75)
    c.fill(sill, (222, 222, 218), 5.0, 0, .3); c.shade(sill, .35, 3, 2)
    c.a[(c.yy >= H * .60 + 28) & (c.yy < H * .60 + 56) & (c.xx > W * .27) & (c.xx < W * .73)] *= .78
    rec = (c.yy >= H * .70) & (c.yy < H * .97) & (c.xx > W * .08) & (c.xx < W * .92)                # balcony recess
    c.fill(rec, (70 + 6 * tone, 66, 62), -4.0, 0, .2)
    c.a[rec & (c.yy < H * .70 + 40)] *= .6                                                          # shadow under the slab above
    rail_top = (c.yy >= H * .74) & (c.yy < H * .765) & (c.xx > W * .08) & (c.xx < W * .92)
    c.fill(rail_top, (58, 62, 66), 3.0, .7, .45)
    bal = (c.yy >= H * .765) & (c.yy < H * .96) & (c.xx > W * .08) & (c.xx < W * .92) & ((c.xx % 16) < 4)
    c.fill(bal, (58, 62, 66), 2.0, .7, .45)
    if r.random() < .25:                                                                             # air-conditioning unit on the balcony
        ac = (c.yy >= H * .82) & (c.yy < H * .94) & (c.xx > W * .6) & (c.xx < W * .82)
        c.fill(ac, (206, 208, 206), 4.0, .2, .3); c.shade(ac, .3, 2, 1.5)
    c.fill((c.yy >= H - 14) & (c.xx > W * .04) & (c.xx < W * .96), (214, 214, 210), 5.0, 0, .3)
    return c


def stone_cell(r, tone, tile_sky, cx, cy):
    c = Cell(BW, BW); H = W = BW
    c.fill(np.ones((H, W), bool), (208 + 6 * tone, 192 + 6 * tone, 162 + 4 * tone), 0, 0, .12, 4, r)
    for yb in range(0, H, 40):
        c.a[yb:yb + 2] *= .86; c.h[yb:yb + 2] -= 1.0
    sky = sky_reflection(c, r, (166, 192, 218), (48, 56, 66), tile_sky[cy * BW:(cy + 1) * BW, cx * BW:(cx + 1) * BW], (-10, -4, 0))
    for cxw in (W * .27, W * .73):
        win = (c.yy > H * .18) & (c.yy < H * .70) & (c.xx > cxw - W * .10) & (c.xx < cxw + W * .10)
        pane_contents(c, win, r, sky, tone)
        c.shade(win, -.5, 3, -1.5)
        arch = (c.yy > H * .18 - 14) & (c.yy <= H * .18) & (c.xx > cxw - W * .13) & (c.xx < cxw + W * .13)
        c.fill(arch, (226, 216, 194), 4.0, 0, .25); c.shade(arch, .4, 3, 2)
        sill = (c.yy >= H * .70) & (c.yy < H * .74) & (c.xx > cxw - W * .13) & (c.xx < cxw + W * .13)
        c.fill(sill, (232, 224, 204), 5.0, 0, .25); c.shade(sill, .4, 3, 2)
        c.fill(win & (np.abs(c.xx - cxw) < 4), (230, 226, 214), 3.0, 0, .3)
        c.fill(win & (np.abs(c.yy - H * .38) < 4), (230, 226, 214), 3.0, 0, .3)
        c.a[(c.yy >= H * .74) & (c.yy < H * .74 + 36) & (c.xx > cxw - W * .11) & (c.xx < cxw + W * .11)] *= .8
    c.fill(c.yy >= H - 10, (190, 176, 150), 3.0, 0, .2)
    return c


KINDS = {"glass_blue": ("curtain", (-6, 0, 8)), "glass_green": ("curtain", (-10, 6, -2)), "ribbon": ("ribbon", None), "apartment": ("apartment", None), "stone": ("stone", None), "mirror": ("mirror", None)}


def facade(name, kind, seed):
    r = np.random.default_rng(seed)
    ctype, tint = KINDS[kind]
    tile_sky = noise(S, S, 6, r, 4)                               # one cloud field across the whole tile so reflections continue between panes
    alb = np.zeros((S, S, 3), np.float32); hm = np.zeros((S, S), np.float32); mt = np.zeros((S, S), np.float32); sm = np.zeros((S, S), np.float32)
    for fy in range(FLOORS):
        for bx in range(BAYS):
            tone = float(r.normal(0, 1))
            if ctype == "curtain": c = curtain_cell(r, tone, tint, tile_sky, bx, fy)
            elif ctype == "mirror": c = mirror_cell(r, tone, tile_sky, bx, fy)
            elif ctype == "ribbon": c = ribbon_cell(r, tone, tile_sky, bx, fy)
            elif ctype == "apartment": c = apartment_cell(r, tone, tile_sky, bx, fy)
            else: c = stone_cell(r, tone, tile_sky, bx, fy)
            s = np.s_[fy * BW:(fy + 1) * BW, bx * BW:(bx + 1) * BW]
            alb[s], hm[s], mt[s], sm[s] = c.a, c.h, c.m, c.s
    # weathering: rain streaks running down from the horizontal ledges, a slow vertical stain pattern, ambient occlusion toward each cell's lower edge
    streak = ndi.gaussian_filter(r.random((1, S)).astype(np.float32), (0, 9)); streak = (streak - streak.min()) / (streak.max() - streak.min() + 1e-6)
    ydrop = (np.arange(S) % BW) / BW
    stain = 1 - .06 * streak * (.3 + ydrop[:, None])
    alb *= stain[..., None]
    alb *= (1 - .08 * (np.arange(S) % BW > BW - 20)[:, None].astype(np.float32))[..., None]
    Image.fromarray(clamp(alb)).save(OUT / f"{name}_albedo.png")
    Image.fromarray(normal_from_height(hm, 3.2)).save(OUT / f"{name}_normal.png")
    out = np.zeros((S, S, 4), np.uint8); out[..., 0] = clamp(mt * 255); out[..., 3] = clamp(sm * 255)
    Image.fromarray(out, "RGBA").save(OUT / f"{name}_metal.png")
    print("facade", name, kind)


# ---------------------------------------------------------------------------------------------------------------------------------------------------------- ground
def road():
    """15 m carriageway: four lanes, double yellow, dashed white, worn asphalt with tyre tracks, patches, cracks, a manhole. 1024 x 1280 px for 15 x 12 m (68 px/m)."""
    W, H = 1024, 1280; r = np.random.default_rng(7)
    base = 54 + noise(H, W, 40, r, 4) * 14 + r.normal(0, 3.2, (H, W))
    img = np.stack([base, base, base + 2], -1)
    ppm = W / 15.0
    for lane in (7.5 - 5.25, 7.5 - 1.75, 7.5 + 1.75, 7.5 + 5.25):                    # tyre tracks
        for off in (-.8, .8):
            x = int((lane + off) * ppm); img[:, x - 22:x + 22] *= .88 + .04 * noise(H, 44, 30, r)[..., None]
    pil = Image.fromarray(clamp(img)); d = ImageDraw.Draw(pil)
    for x in (7.5 - .12, 7.5 + .12): d.rectangle([x * ppm - 3, 0, x * ppm + 3, H], fill=(214, 176, 44))
    for lx in (7.5 - 3.5, 7.5 + 3.5):
        for y in range(0, H, 200): d.rectangle([lx * ppm - 5, y, lx * ppm + 5, y + 110], fill=(222, 222, 218))
    for ex in (.25, 14.75): d.rectangle([ex * ppm - 5, 0, ex * ppm + 5, H], fill=(214, 214, 208))
    a = np.array(pil, np.float32)
    wear = noise(H, W, 30, r, 3); a[(a[..., 0] > 190)] *= (.78 + .22 * wear[(a[..., 0] > 190)])[:, None]                     # worn paint
    patch = ndi.gaussian_filter((noise(H, W, 9, r, 2) > .62).astype(np.float32), 5)                                      # soft-edged asphalt patches
    a *= (1 - .10 * patch)[..., None]
    for _ in range(26):                                                              # cracks
        x, y = r.integers(0, W), r.integers(0, H); pts = [(x, y)]
        for _ in range(12): x += int(r.normal(0, 14)); y += int(r.integers(8, 26)); pts.append((x, y))
        ImageDraw.Draw(pil := Image.fromarray(clamp(a))).line(pts, fill=(22, 22, 24), width=1); a = np.array(pil, np.float32)
    pil = Image.fromarray(clamp(a)); d = ImageDraw.Draw(pil)
    d.ellipse([int(8.8 * ppm), 600, int(8.8 * ppm) + 60, 660], fill=(40, 40, 42), outline=(90, 90, 92))
    pil.save(OUT / "road.png")


def paving():
    """Sidewalk slabs (128 px per 0.9 m), plaza concrete, roof membrane and lawn, 1024 px."""
    r = np.random.default_rng(11)
    S2 = 1024
    n = noise(S2, S2, 24, r, 4)
    sw = np.full((S2, S2, 3), 170, np.float32) + (n[..., None] - .5) * 26 + r.normal(0, 4, (S2, S2, 3))
    for k in range(0, S2, 128):
        sw[k:k + 4] *= .62; sw[:, k:k + 4] *= .62
    for _ in range(40):
        x, y = r.integers(0, S2 - 60), r.integers(0, S2 - 60); sw[y:y + r.integers(10, 50), x:x + r.integers(10, 50)] *= .93
    Image.fromarray(clamp(sw)).save(OUT / "sidewalk.png")
    cc = np.full((S2, S2, 3), (128, 130, 128), np.float32) + (n[..., None] - .5) * 30 + r.normal(0, 4, (S2, S2, 3))
    for k in range(0, S2, 256):
        cc[k:k + 5] *= .66; cc[:, k:k + 5] *= .66
    Image.fromarray(clamp(cc)).save(OUT / "concrete.png")
    rf = np.full((S2, S2, 3), (90, 92, 94), np.float32) + (n[..., None] - .5) * 40 + r.normal(0, 8, (S2, S2, 3))
    Image.fromarray(clamp(rf)).save(OUT / "roof.png")
    g = noise(S2, S2, 60, r, 4)
    lawn = np.zeros((S2, S2, 3), np.float32); lawn[..., 0] = 58 + g * 40; lawn[..., 1] = 92 + g * 54; lawn[..., 2] = 40 + g * 22
    lawn += r.normal(0, 9, lawn.shape)
    Image.fromarray(clamp(lawn)).save(OUT / "lawn.png")


# ---------------------------------------------------------------------------------------------------------------------------------------------------------- trees
def leaf_cluster():
    """1024 px RGBA atlas of one foliage cluster: ~70 ovate leaves with midrib, veins and per-leaf colour, dense and darker toward the centre; alpha-cut edges."""
    r = np.random.default_rng(3); N = 2048
    img = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    leaves = []
    for _ in range(190):
        ang = r.random() * math.tau; rad = (r.random() ** .6) * N * .40
        leaves.append((N / 2 + math.cos(ang) * rad, N / 2 + math.sin(ang) * rad, r.random() * math.tau, r.uniform(120, 190), rad / (N * .4)))
    leaves.sort(key=lambda t: -t[4])                                                              # outer leaves first, inner on top
    greens = [(64, 112, 44), (78, 128, 52), (52, 96, 40), (92, 138, 58), (70, 118, 48), (104, 146, 62)]
    for cx, cy, rot, size, depth in leaves:
        L, Wd = size, size * .52
        pts = []
        for t in np.linspace(0, 1, 18):
            w = math.sin(t * math.pi) ** .85 * Wd / 2 * (1 - .25 * t)
            pts.append((t * L, w))
        poly = [(x, y) for x, y in pts] + [(x, -y) for x, y in pts[::-1]]
        c, s = math.cos(rot), math.sin(rot)
        P = [(cx + x * c - y * s - L / 2 * c, cy + x * s + y * c - L / 2 * s) for x, y in poly]
        col = np.array(greens[r.integers(len(greens))], np.float32) * (.62 + .5 * (1 - depth) + .1 * r.random())
        d.polygon(P, fill=tuple(int(min(255, v)) for v in col) + (255,))
        mid0 = (cx - L / 2 * c, cy - L / 2 * s); mid1 = (cx + L / 2 * c, cy + L / 2 * s)
        d.line([mid0, mid1], fill=tuple(int(min(255, v * 1.25)) for v in col) + (255,), width=3)
        for t in (.25, .45, .65):
            bx, by = mid0[0] + (mid1[0] - mid0[0]) * t, mid0[1] + (mid1[1] - mid0[1]) * t
            for sgn in (-1, 1):
                ex = bx + (c * .5 - s * sgn * .7) * L * .22; ey = by + (s * .5 + c * sgn * .7) * L * .22
                d.line([(bx, by), (ex, ey)], fill=tuple(int(min(255, v * 1.12)) for v in col) + (255,), width=2)
    img = img.filter(ImageFilter.GaussianBlur(.8)).resize((1024, 1024), Image.LANCZOS)
    a = np.array(img); a[..., 3] = np.where(a[..., 3] > 110, 255, 0)                               # alpha cut with a hard edge (the material clips at 0.5)
    # fill colour under transparent pixels so mips do not bleed black
    rgb = a[..., :3].astype(np.float32); m = a[..., 3] > 0
    idx = ndi.distance_transform_edt(~m, return_distances=False, return_indices=True)
    rgb = rgb[idx[0], idx[1]]
    Image.fromarray(np.dstack([rgb.astype(np.uint8), a[..., 3]]), "RGBA").save(OUT / "leaf_cluster.png")


def bark():
    r = np.random.default_rng(5); H = W = 512
    n = noise(H, W, 4, r, 5)
    ridge = np.abs(np.sin((np.arange(W)[None, :] / W * 38 + n * 3.0) * math.pi))
    base = 66 + ridge * 48 + n * 20
    img = np.stack([base * 1.0, base * .86, base * .72], -1) + r.normal(0, 3, (H, W, 3))
    Image.fromarray(clamp(img)).save(OUT / "bark.png")
    hm = ridge * 6; Image.fromarray(normal_from_height(hm, 2.4)).save(OUT / "bark_normal.png")


def main():
    for i, (name, kind) in enumerate((("facade0", "glass_blue"), ("facade1", "glass_green"), ("facade2", "ribbon"), ("facade3", "apartment"), ("facade4", "stone"), ("facade5", "mirror"))):
        facade(name, kind, 100 + i)
    road(); paving(); leaf_cluster(); bark()
    v1.palette()
    print("city art v2:", sorted(p.name for p in OUT.glob("*.png")))


if __name__ == "__main__":
    main()
