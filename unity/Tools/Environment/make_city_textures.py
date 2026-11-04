"""Textures for the city outside the hospital windows, generated procedurally (no third-party imagery): six building facade styles (albedo, normal, metallic/smoothness), road, sidewalk,
terrace paving, a palette atlas for vehicles and street furniture, and three hazy skyline impostor strips.

  python unity/Tools/Environment/make_city_textures.py     -> unity/Logs/CityArt/*.png

A facade tile covers 4 bays x 4 floors (1024 px square); the building generator sets each face's UVs so the tile repeats once per 4 bays / 4 floors.
Smoothness is packed in the alpha of the metallic map (Unity's convention): R = metallic, A = smoothness.
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = Path(__file__).resolve().parents[3] / "unity" / "Logs" / "CityArt"; OUT.mkdir(parents=True, exist_ok=True)
S = 1024                      # tile size
BAYS = FLOORS = 4
BW = S // BAYS                # 256 px per bay
rng = np.random.default_rng(2026)


def clamp(a):
    return np.clip(a, 0, 255).astype(np.uint8)


def normal_from_height(h, strength=6.0):
    gy, gx = np.gradient(h.astype(np.float32))
    n = np.stack([-gx * strength, gy * strength, np.ones_like(h, dtype=np.float32)], -1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    return clamp((n * .5 + .5) * 255)


def facade(name, kind):
    alb = np.zeros((S, S, 3), np.float32); height = np.zeros((S, S), np.float32); metal = np.zeros((S, S), np.float32); smooth = np.zeros((S, S), np.float32)
    yy, xx = np.mgrid[0:S, 0:S]
    for f in range(FLOORS):
        for b in range(BAYS):
            x0, y0 = b * BW, f * BW
            tone = rng.normal(0, 1)
            cell = np.s_[y0:y0 + BW, x0:x0 + BW]
            alb[cell], height[cell], metal[cell], smooth[cell] = cell_paint(kind, tone, rng)
    # slab lines and a gentle weathering streak pattern
    streak = np.clip(rng.normal(1.0, .035, (1, S)), .9, 1.1); alb *= streak[..., None]
    alb *= (.94 + .06 * np.sin(yy / 41.0 + xx / 233.0))[..., None]
    Image.fromarray(clamp(alb)).save(OUT / f"{name}_albedo.png")
    Image.fromarray(normal_from_height(height)).save(OUT / f"{name}_normal.png")
    mt = np.zeros((S, S, 4), np.uint8); mt[..., 0] = clamp(metal * 255); mt[..., 3] = clamp(smooth * 255); mt[..., 1] = mt[..., 2] = 0
    Image.fromarray(mt, "RGBA").save(OUT / f"{name}_metal.png")


def cell_paint(kind, tone, r):
    H = W = BW
    a = np.zeros((H, W, 3), np.float32); h = np.zeros((H, W), np.float32); m = np.zeros((H, W), np.float32); s = np.zeros((H, W), np.float32)
    yy, xx = np.mgrid[0:H, 0:W]

    def fill(mask, colour, height=0.0, metallic=0.0, smooth=.4):
        for c in range(3):
            a[..., c][mask] = colour[c]
        h[mask] = height; m[mask] = metallic; s[mask] = smooth

    def glass(mask, base, gradient=26, lit=.05):
        col = np.array(base, np.float32) + tone * 6
        g = (1 - yy[mask] / H)[:, None] * gradient                                     # lighter near the top: sky reflection
        reflect = np.array([34, 44, 56], np.float32) * 0
        for c in range(3):
            a[..., c][mask] = np.clip(col[c] + g[:, 0] * (.8 + .4 * (c == 2)) + r.normal(0, 4, g.shape[0]), 0, 255)
        if r.random() < lit:                                                          # a few lit offices even by day
            for c, v in enumerate((236, 214, 150)):
                a[..., c][mask] = v * (.55 + .3 * r.random())
        h[mask] = -2.0; m[mask] = .55; s[mask] = .94

    if kind in ("glass_blue", "glass_green"):
        base = (30, 54, 78) if kind == "glass_blue" else (26, 70, 66)
        fill(np.ones((H, W), bool), (92, 98, 106), 0, .85, .55)                        # aluminium frame
        glass((yy > 10) & (yy < H * .78) & (xx > 8) & (xx < W - 8), base)
        fill((yy >= H * .78 + 6) & (yy < H - 8) & (xx > 8) & (xx < W - 8), tuple(np.array(base) * .8 + np.array([34, 36, 40]) * .5), -.8, .3, .55)    # spandrel panel
        fill(yy >= H - 8, (178, 182, 186), 2.5, .2, .5)                                # slab edge
        fill((xx < 5) | (xx >= W - 5), (70, 74, 80), 3.0, .85, .6)                      # mullion
    elif kind == "ribbon":
        fill(np.ones((H, W), bool), (176, 178, 176), 0, .0, .22)                       # precast concrete
        glass((yy > H * .22) & (yy < H * .78) & (xx > 0) & (xx < W), (36, 58, 76), 20, .04)
        fill((xx < 6) | (xx >= W - 6), (190, 192, 190), 3.0, 0, .3)
        fill((yy > H * .22 - 4) & (yy <= H * .22), (150, 152, 150), 2.0, 0, .3); fill((yy >= H * .78) & (yy < H * .78 + 5), (210, 212, 210), 2.5, 0, .3)
        a += r.normal(0, 3, a.shape)
    elif kind == "apartment":
        brick = r.random() < .5
        wall = (150 + 10 * tone, 96 + 6 * tone, 78) if brick else (216 + 4 * tone, 203 + 4 * tone, 182)
        fill(np.ones((H, W), bool), wall, 0, 0, .15)
        if brick:
            for yb in range(0, H, 10):
                a[yb:yb + 1, :, :] *= .84
            for yb in range(0, H, 20):
                for xb in range(((yb // 20) % 2) * 12, W, 24):
                    a[yb:yb + 10, xb:xb + 1, :] *= .84
        glass((yy > H * .14) & (yy < H * .62) & (xx > W * .30) & (xx < W * .70), (46, 66, 84), 16, .08)
        fill((yy >= H * .62) & (yy < H * .66) & (xx > W * .27) & (xx < W * .73), (226, 226, 222), 2.5, 0, .3)
        rec = (yy >= H * .68) & (yy < H * .98) & (xx > W * .1) & (xx < W * .9)        # balcony: recessed, dark, with a rail
        fill(rec, (74, 70, 66), -3.5, 0, .2)
        fill((yy >= H * .70) & (yy < H * .735) & (xx > W * .1) & (xx < W * .9), (60, 64, 68), 1.5, .7, .45)
        fill((yy >= H * .98) & (xx > W * .06) & (xx < W * .94), (222, 222, 218), 3, 0, .3)
    elif kind == "stone":
        fill(np.ones((H, W), bool), (206 + 6 * tone, 190 + 6 * tone, 160 + 4 * tone), 0, 0, .12)
        for yb in range(0, H, 32):
            a[yb:yb + 1, :, :] *= .88
        for k, cx in enumerate((W * .27, W * .73)):
            glass((yy > H * .16) & (yy < H * .70) & (xx > cx - W * .09) & (xx < cx + W * .09), (44, 52, 62), 14, .05)
            fill((yy >= H * .70) & (yy < H * .74) & (xx > cx - W * .12) & (xx < cx + W * .12), (232, 224, 204), 2.5, 0, .3)
            fill((yy >= H * .12) & (yy < H * .16) & (xx > cx - W * .11) & (xx < cx + W * .11), (222, 212, 190), 2.0, 0, .3)
        fill(yy >= H - 6, (190, 176, 150), 2, 0, .2)
        a += r.normal(0, 4, a.shape)
    else:                                                                              # mirror
        base = np.array((126, 156, 182), np.float32) + tone * 5
        for c in range(3):
            a[..., c] = base[c] * (.82 + .3 * (1 - yy / H)) + r.normal(0, 3, (H, W))
        h[:] = 0; m[:] = .9; s[:] = .97
        fill((xx < 3) | (xx >= W - 3) | (yy >= H - 4), (150, 156, 164), 2.0, .9, .6)
    return a, h, m, s


def road():
    """15 m carriageway x 12 m: four lanes (3.5 m), double yellow centre, dashed lane lines, worn asphalt. 512 x 640 px."""
    W, H = 512, 640
    n = rng.normal(0, 1, (H, W)).astype(np.float32); n = np.array(Image.fromarray(clamp(128 + n * 30)).filter(ImageFilter.GaussianBlur(1.2)), np.float32)
    base = 58 + (n - 128) * .5
    im = np.stack([base, base, base + 2], -1)
    ppm = W / 15.0
    d = ImageDraw.Draw(Image.fromarray(clamp(im)))
    img = Image.fromarray(clamp(im)); d = ImageDraw.Draw(img)
    for x in (7.5 - .12, 7.5 + .12):                                                  # double yellow
        d.rectangle([x * ppm - 2, 0, x * ppm + 2, H], fill=(206, 170, 40))
    for lx in (7.5 - 3.5, 7.5 + 3.5):                                                 # dashed white
        for y in range(0, H, 100):
            d.rectangle([lx * ppm - 3, y, lx * ppm + 3, y + 55], fill=(214, 214, 210))
    for ex in (.25, 14.75):
        d.rectangle([ex * ppm - 3, 0, ex * ppm + 3, H], fill=(206, 206, 200))
    arr = np.array(img, np.float32)
    # tyre tracks: darker, slightly smoother bands in each lane, oil drips
    for lane in (7.5 - 5.25, 7.5 - 1.75, 7.5 + 1.75, 7.5 + 5.25):
        for off in (-.8, .8):
            x = int((lane + off) * ppm); arr[:, x - 14:x + 14, :] *= .9
    arr *= (.96 + .08 * rng.random((1, 1, 1)))
    Image.fromarray(clamp(arr)).save(OUT / "road.png")


def sidewalk():
    S2 = 512
    im = np.full((S2, S2, 3), 168, np.float32); im += rng.normal(0, 5, im.shape)
    for k in range(0, S2, 128):
        im[k:k + 3, :, :] *= .68; im[:, k:k + 3, :] *= .68
    Image.fromarray(clamp(im)).save(OUT / "sidewalk.png")
    im2 = np.full((S2, S2, 3), (124, 126, 124), np.float32); im2 += rng.normal(0, 4, im2.shape)                    # concrete / terrace paving
    for k in range(0, S2, 256):
        im2[k:k + 3, :, :] *= .7; im2[:, k:k + 3, :] *= .7
    Image.fromarray(clamp(im2)).save(OUT / "concrete.png")
    im3 = np.zeros((S2, S2, 3), np.float32)                                                                          # roof gravel / membrane
    im3[:] = (92, 94, 96); im3 += rng.normal(0, 9, im3.shape)
    Image.fromarray(clamp(im3)).save(OUT / "roof.png")
    g = np.zeros((S2, S2, 3), np.float32); g[:] = (64, 94, 54); g += rng.normal(0, 12, g.shape)                      # green roof / lawn
    Image.fromarray(clamp(g)).save(OUT / "lawn.png")


def palette():
    """8 x 4 cells of 32 px: 24 vehicle / furniture colours + glass + tyre + light + metal."""
    cols = [(236, 236, 234), (206, 208, 210), (150, 154, 160), (62, 66, 72), (26, 28, 32), (170, 30, 32), (30, 62, 150), (28, 100, 70),
            (210, 140, 30), (240, 200, 40), (104, 68, 44), (132, 28, 74), (60, 100, 130), (200, 200, 190), (180, 70, 30), (90, 100, 110),
            (22, 30, 38), (14, 14, 16), (250, 244, 214), (212, 40, 40), (120, 124, 128), (230, 120, 20), (250, 250, 250), (40, 150, 70),
            (30, 90, 200), (190, 190, 200), (96, 80, 60), (64, 72, 60), (200, 160, 120), (150, 170, 190), (255, 255, 255), (0, 0, 0)]
    im = Image.new("RGB", (256, 128))
    d = ImageDraw.Draw(im)
    for i, c in enumerate(cols):
        x, y = (i % 8) * 32, (i // 8) * 32; d.rectangle([x, y, x + 31, y + 31], fill=c)
    im.save(OUT / "palette.png")


def skyline(name, k, seed, density, base_haze, tall):
    """A 4096 x 512 horizon strip, buildings in silhouette tinted toward the haze colour; `k` is how far the layer is toward haze."""
    r = np.random.default_rng(seed)
    W, H = 4096 * 2, 1024
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img)
    haze = np.array(base_haze, np.float32)
    x = 0
    skyline_profile = np.clip(np.convolve(r.normal(0, 1, W // 60 + 20), np.ones(7) / 7, mode="same"), -1, 1)
    while x < W:
        w = int(r.integers(30, 150)); bh = int(H * (.12 + .5 * tall * (.5 + .5 * skyline_profile[min(len(skyline_profile) - 1, x // 60)]) * r.random() ** .7))
        shade = np.array((70, 84, 100), np.float32) * (.75 + .5 * r.random())
        col = shade * (1 - k) + haze * k
        top = H - bh
        d.rectangle([x, top, x + w, H], fill=tuple(int(c) for c in col) + (255,))
        # window texture: faint lighter and darker dots
        for _ in range(int(w * bh / 700 * density)):
            wx, wy = x + int(r.integers(2, max(3, w - 2))), top + int(r.integers(4, max(5, bh)))
            c2 = col * (1.12 if r.random() < .5 else .88)
            d.rectangle([wx, wy, wx + 2, wy + 3], fill=tuple(int(max(0, min(255, c))) for c in c2) + (255,))
        if r.random() < .25:                                                         # crown / spire
            d.rectangle([x + w // 2 - 2, top - int(r.integers(10, 60)), x + w // 2 + 2, top], fill=tuple(int(c) for c in col) + (255,))
        if r.random() < .3:                                                          # setback
            d.rectangle([x + w // 5, top - int(r.integers(14, 50)), x + w - w // 5, top], fill=tuple(int(c * .97) for c in col) + (255,))
        x += w + int(r.integers(-14, 16))
    arr = np.array(img, np.float32)
    # vertical haze: lower part fades toward the haze colour, a slight sunlit left edge brightening
    yy = np.linspace(0, 1, H)[:, None]
    mix = np.clip(((yy - .45) / .55), 0, 1) ** 1.5 * .6
    for c in range(3):
        arr[..., c] = arr[..., c] * (1 - mix) + haze[c] * mix
    out = Image.fromarray(clamp(arr), "RGBA").resize((W // 2, H // 2), Image.LANCZOS)
    out.save(OUT / f"{name}.png")


def main():
    for name, kind in (("facade0", "glass_blue"), ("facade1", "glass_green"), ("facade2", "ribbon"), ("facade3", "apartment"), ("facade4", "stone"), ("facade5", "mirror")):
        facade(name, kind)
    road(); sidewalk(); palette()
    skyline("skyline_a", .46, 1, 1.0, (188, 206, 224), .55)
    skyline("skyline_b", .64, 2, .7, (192, 210, 228), .48)
    skyline("skyline_c", .80, 3, .4, (196, 214, 230), .40)
    print("city textures:", sorted(p.name for p in OUT.glob("*.png")))



if __name__ == "__main__":
    main()
