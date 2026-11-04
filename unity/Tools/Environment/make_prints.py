"""Three framed wall prints for the final reference room (original artwork, generated here, no third-party imagery).
Text: 'Rest well, get better' / 'Small steps, big recovery' / 'One day at a time'. Usage: python make_prints.py <out_dir>"""
import math
import random
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
W, H = 1024, 1456
PAPER = (246, 243, 236)
INK = (58, 70, 82)
GREENS = [(96, 139, 126), (74, 120, 112), (130, 165, 145), (62, 98, 96)]
SUN = (236, 200, 150)
FONT = "C:/Windows/Fonts/arialbd.ttf"


def leaf(d, cx, cy, length, width, ang, col):
    pts = []
    for i in range(0, 21):
        t = i / 20
        pts.append((t * length, math.sin(math.pi * t) * width))
    for i in range(20, -1, -1):
        t = i / 20
        pts.append((t * length, -math.sin(math.pi * t) * width))
    ca, sa = math.cos(ang), math.sin(ang)
    d.polygon([(cx + x * ca - y * sa, cy + x * sa + y * ca) for x, y in pts], fill=col)
    d.line([(cx, cy), (cx + length * ca, cy + length * sa)], fill=tuple(int(c * .8) for c in col), width=3)


def sprig(d, x, y, angle, n, rnd, scale=1.0):
    ang = angle
    for i in range(n):
        seg = 60 * scale
        nx, ny = x + math.cos(ang) * seg, y + math.sin(ang) * seg
        d.line([(x, y), (nx, ny)], fill=(88, 112, 96), width=5)
        for s in (-1, 1):
            leaf(d, nx, ny, 150 * scale * (1 - i * .06), 34 * scale, ang + s * (.9 + rnd.uniform(-.2, .2)), rnd.choice(GREENS))
        x, y = nx, ny; ang += rnd.uniform(-.12, .12)
    leaf(d, x, y, 120 * scale, 30 * scale, ang, rnd.choice(GREENS))


def make(name, lines, variant):
    rnd = random.Random(hash(name) & 0xFFFF)
    im = Image.new("RGB", (W, H), PAPER); d = ImageDraw.Draw(im)
    if variant == 0:                                              # leaf sprigs rising from the bottom
        for x, a, n in ((330, -1.75, 5), (520, -1.57, 6), (700, -1.40, 5)): sprig(d, x, 1130, a, n, rnd)
    elif variant == 1:                                            # sun disc behind a plant
        d.ellipse((330, 700, 694, 1064), fill=SUN); sprig(d, 512, 1250, -1.57, 6, rnd, 1.15); sprig(d, 430, 1250, -1.9, 4, rnd, .9); sprig(d, 600, 1250, -1.25, 4, rnd, .9)
    else:                                                         # long bough across the lower half
        sprig(d, 170, 1150, -.45, 7, rnd, 1.0); sprig(d, 860, 1200, -2.6, 5, rnd, .9)
    f1 = ImageFont.truetype(FONT, 92)
    y = 150
    for ln in lines:
        w = d.textlength(ln, font=f1)
        d.text(((W - w) / 2, y), ln, font=f1, fill=INK); y += 118
    im.save(out / f"{name}.png")


make("print_1", ["Rest well,", "get better"], 0)
make("print_2", ["Small steps,", "big recovery"], 1)
make("print_3", ["One day", "at a time"], 2)
print("prints written to", out)
