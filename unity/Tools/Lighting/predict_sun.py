"""Predicts where ONE baked sun lands, from the real PatientBay geometry (no Unity needed): the wall opening, the window frame members and the two gathered curtains that stand in front
of the window. Each pixel of the reference camera is traced to its first room surface, then traced back along the sun to see whether it passes the open part of the window.
Usage: python predict_sun.py <out.png> yaw:pitch [yaw:pitch ...] [--mullion W] [--transom H] [--nocurtain]
  yaw  = Unity light yaw (degrees from +Z toward +X) of the direction the light TRAVELS; pitch = degrees below the horizon.
Prints, per candidate: floor lit %, wall lit % (front z=-2 / back z=+2 / door x=+2.5), the sunlit area visible from the reference camera."""
import math
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

W, H = 480, 270
CAM = np.array([2.38, 1.62, -1.84]); LOOK = np.array([-.62, .95, 1.15]); FOV = 80
ROOM = dict(x=(-2.5, 2.5), y=(0.0, 2.8), z=(-2.0, 2.0))

args = [a for a in sys.argv[2:]]
mull = .06; transom = 0.0; curtains = True
if "--mullion" in args: mull = float(args[args.index("--mullion") + 1])
if "--transom" in args: transom = float(args[args.index("--transom") + 1])
if "--nocurtain" in args: curtains = False
cands = [tuple(map(float, a.split(":"))) for a in args if ":" in a and not a.startswith("--")]
out = sys.argv[1]


def cam_rays():
    f = LOOK - CAM; f /= np.linalg.norm(f); up = np.array([0, 1, 0.]); r = np.cross(up, f); r /= np.linalg.norm(r); u = np.cross(f, r)
    th = math.tan(math.radians(FOV) / 2); asp = W / H
    xs = (2 * (np.arange(W) + .5) / W - 1) * th * asp; ys = (1 - 2 * (np.arange(H) + .5) / H) * th
    X, Y = np.meshgrid(xs, ys)
    d = f[None, None, :] + X[..., None] * r + Y[..., None] * u
    return d / np.linalg.norm(d, axis=-1, keepdims=True)


def first_hit(d):
    best = np.full(d.shape[:2], 1e9); P = np.zeros(d.shape); surf = np.full(d.shape[:2], -1)
    for sid, (ax, val) in enumerate(((0, -2.5), (0, 2.5), (2, -2.0), (2, 2.0), (1, 0.0), (1, 2.8))):
        with np.errstate(divide="ignore", invalid="ignore"):
            t = (val - CAM[ax]) / d[..., ax]
        pts = CAM[None, None, :] + d * t[..., None]
        ok = (t > 0) & (t < best)
        for a2, lim in ((0, ROOM["x"]), (1, ROOM["y"]), (2, ROOM["z"])):
            if a2 != ax: ok &= (pts[..., a2] >= lim[0] - 1e-6) & (pts[..., a2] <= lim[1] + 1e-6)
        best = np.where(ok, t, best); P = np.where(ok[..., None], pts, P); surf = np.where(ok, sid, surf)
    return P, surf


def lit(P, travel):
    """True where the reverse ray (toward the sun) leaves through the open window."""
    d = -travel
    with np.errstate(divide="ignore", invalid="ignore"):
        t = (-2.45 - P[..., 0]) / d[0]                       # reach the outer face of the window wall
    Q = P + d[None, None, :] * t[..., None]
    ok = (t > 0)
    inside = ok & (Q[..., 1] > 1.30) & (Q[..., 1] < 2.54) & (np.abs(Q[..., 2]) < 1.02)
    # frame plane x = -2.34 : members
    t2 = (-2.34 - P[..., 0]) / d[0]; F = P + d[None, None, :] * t2[..., None]
    fy, fz = F[..., 1], F[..., 2]
    blocked = (np.abs(fz) < mull / 2) | (np.abs(fz + 1.03) < .03) | (np.abs(fz - 1.03) < .03) | (fy < 1.34) | (fy > 2.50)
    if transom > 0: blocked |= np.abs(fy - 1.92) < transom / 2
    # curtains (x ~ -2.17): z in [.45,1.07] and [-1.13,-.51], y 1.03..2.53
    if curtains:
        t3 = (-2.17 - P[..., 0]) / d[0]; C = P + d[None, None, :] * t3[..., None]
        cy, cz = C[..., 1], C[..., 2]
        blocked |= (cy > 1.02) & (cy < 2.53) & (((cz > .45) & (cz < 1.07)) | ((cz > -1.13) & (cz < -.51)))
    # the ray must stay inside the room volume on its way to the window (it cannot pass through another wall or the ceiling)
    inside &= (Q[..., 1] > 0)
    return inside & ~blocked


d = cam_rays(); P, surf = first_hit(d)
font = ImageFont.truetype("arialbd.ttf", 18)
tiles = []
print("yaw pitch | lit%: floor  front(z-2)  back(z+2)  door(x+2.5)  winwall  | what the camera sees lit")
for yaw, pitch in cands:
    y, p = math.radians(yaw), math.radians(pitch)
    travel = np.array([math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p)])
    L = lit(P, travel)
    names = {4: "floor", 2: "front", 3: "back", 1: "door", 0: "winwall"}
    pct = {n: (100 * L[surf == s].mean() if (surf == s).any() else 0) for s, n in names.items()}
    print(f"{yaw:5.0f} {pitch:5.0f} | {pct['floor']:5.1f} {pct['front']:9.1f} {pct['back']:9.1f} {pct['door']:10.1f} {pct['winwall']:8.1f}  | {L.mean() * 100:.1f}% of the frame")
    img = np.zeros((H, W, 3), np.float32)
    base = {0: (.55, .55, .5), 1: (.5, .5, .55), 2: (.6, .55, .5), 3: (.6, .55, .5), 4: (.45, .32, .2), 5: (.7, .7, .7), -1: (0, 0, 0)}
    for s, c in base.items(): img[surf == s] = c
    img[L] = np.array([1.0, .78, .35]) * (.6 + .4 * (surf[L] == 4)[:, None])
    t = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8)); ImageDraw.Draw(t).text((6, 4), f"yaw {yaw:.0f} pitch {pitch:.0f}", font=font, fill=(255, 255, 255)); tiles.append(t)
cols = min(3, len(tiles)); rows = (len(tiles) + cols - 1) // cols
S = Image.new("RGB", (cols * (W + 4), rows * (H + 4)))
for i, t in enumerate(tiles): S.paste(t, ((i % cols) * (W + 4), (i // cols) * (H + 4)))
S.save(out)
