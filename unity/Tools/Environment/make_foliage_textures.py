"""Alpha-cutout foliage textures (original, procedural): a pinnate palm frond and a heart-shaped pothos leaf. Usage: python make_foliage_textures.py <out_dir>"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
SS = 2


def shade(c, k):
    return tuple(max(0, min(255, int(v * k))) for v in c)


def frond(W=1024, H=512, seed=3):
    rnd = random.Random(seed)
    w, h = W * SS, H * SS
    rgb = Image.new("RGB", (w, h), (58, 104, 40)); al = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(rgb); a = ImageDraw.Draw(al)
    cy = h // 2
    base = (52, 112, 44)
    n = 32
    for i in range(n):
        t = i / (n - 1)
        x = w * (.07 + .88 * t)
        ln = h * (.49 - .24 * t ** 1.2) * rnd.uniform(.93, 1.05)
        for side in (-1, 1):
            ang = math.radians(66 - 28 * t + rnd.uniform(-3, 3))          # leaflets sweep toward the tip
            ex, ey = x + math.cos(ang) * ln * .85, cy + side * math.sin(ang) * ln
            wd = ln * .042
            nx, ny = -math.sin(ang) * side, math.cos(ang)                  # normal for the leaflet width
            pts = []
            for k in range(0, 11):
                u = k / 10
                px, py = x + (ex - x) * u, cy + (ey - cy) * u + .06 * ln * u * u * side * 0
                ww = wd * math.sin(math.pi * (u * .88 + .06)) ** .7
                pts.append((px + ny * 0 + (-(ey - cy)) / ln * ww * 0, py))
            # build polygon from centre line +/- width perpendicular to the leaflet direction
            dx, dy = ex - x, ey - cy; L = math.hypot(dx, dy); pxn, pyn = -dy / L, dx / L
            left, right = [], []
            for k in range(0, 13):
                u = k / 12
                cxk, cyk = x + dx * u, cy + dy * u
                ww = wd * (math.sin(math.pi * min(1, u * .9 + .05)) ** .65)
                left.append((cxk + pxn * ww, cyk + pyn * ww)); right.append((cxk - pxn * ww, cyk - pyn * ww))
            poly = left + right[::-1]
            col = shade(base, rnd.uniform(.82, 1.18))
            d.polygon(poly, fill=col); a.polygon(poly, fill=255)
            d.line([(x, cy), (ex, ey)], fill=shade(col, 1.35), width=max(2, int(ln * .012)))
            half = [(cxk, cyk) for cxk, cyk in left[len(left) // 2:]]
            d.line(left[2:], fill=shade(col, .78), width=2)               # darker leaflet edge for depth
    d.line([(w * .02, cy), (w * .98, cy)], fill=(108, 96, 58), width=int(h * .016)); a.line([(w * .02, cy), (w * .98, cy)], fill=255, width=int(h * .016))
    rgb = rgb.resize((W, H), Image.LANCZOS); al = al.resize((W, H), Image.LANCZOS)
    al = al.point(lambda v: 255 if v > 90 else int(v * 2.0))
    im = rgb.copy(); im.putalpha(al); im.save(out / "palm_frond.png"); print("palm_frond.png")


def leaf(W=512, H=512):
    w, h = W * SS, H * SS
    rgb = Image.new("RGB", (w, h), (48, 104, 44)); al = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(rgb); a = ImageDraw.Draw(al)
    pts = []
    for k in range(0, 61):                                              # heart-shaped leaf, tip up
        t = k / 60 * 2 * math.pi
        x = 16 * math.sin(t) ** 3; y = 13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t)
        pts.append((w / 2 + x * w * .028, h * .52 - y * h * .030))
    d.polygon(pts, fill=(52, 118, 46)); a.polygon(pts, fill=255)
    d.line([(w / 2, h * .9), (w / 2, h * .12)], fill=(120, 170, 90), width=int(h * .012))
    for s in (-1, 1):
        for q in range(1, 5):
            y0 = h * (.82 - q * .14)
            d.line([(w / 2, y0), (w / 2 + s * w * .22, y0 - h * .10)], fill=(88, 148, 70), width=int(h * .006))
    rgb = rgb.resize((W, H), Image.LANCZOS); al = al.resize((W, H), Image.LANCZOS).point(lambda v: 255 if v > 90 else int(v * 2.0))
    im = rgb.copy(); im.putalpha(al); im.save(out / "pothos_leaf.png"); print("pothos_leaf.png")


frond(); leaf()
