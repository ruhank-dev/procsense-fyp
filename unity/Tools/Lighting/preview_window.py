"""Offline preview of the window exterior exactly as the engine maps it (unlit spherical patch, radius 22 m around (-1, 1.7, 0), az -55..55, el -20..45), seen through the real
opening (x = -2.45, y 1.30..2.54, z -1.02..1.02) from a camera. Usage: python preview_window.py <texture.jpg> <out.png> [reference|window|bed]"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import map_coordinates

tex_path, out = sys.argv[1], sys.argv[2]
view = sys.argv[3] if len(sys.argv) > 3 else "reference"
CAMS = {"reference": ((2.38, 1.62, -1.84), (-.62, .95, 1.15), 80), "window": ((1.2, 1.55, -1.4), (-2.4, 1.9, 0), 48), "bed": ((.5, 1.4, 1.2), (-2.4, 1.9, 0), 60)}
pos, look, fov = (np.array(v, float) if i < 2 else v for i, v in enumerate(CAMS[view]))
W, H = 1280, 720
im = np.asarray(Image.open(tex_path).convert("RGB")).astype(np.float32) / 255
th_, tw_, _ = im.shape
f = look - pos; f /= np.linalg.norm(f); up = np.array([0, 1, 0.]); r = np.cross(up, f); r /= np.linalg.norm(r); u = np.cross(f, r)
t = math.tan(math.radians(fov) / 2); asp = W / H
X, Y = np.meshgrid((2 * (np.arange(W) + .5) / W - 1) * t * asp, (1 - 2 * (np.arange(H) + .5) / H) * t)
d = f + X[..., None] * r + Y[..., None] * u; d /= np.linalg.norm(d, axis=-1, keepdims=True)
# the opening
tt = (-2.45 - pos[0]) / d[..., 0]
Q = pos + d * tt[..., None]
open_ = (tt > 0) & (Q[..., 1] > 1.30) & (Q[..., 1] < 2.54) & (np.abs(Q[..., 2]) < 1.02)
# sphere intersection from inside
c = np.array([-1.0, 1.7, 0.0]); R = 22.0
oc = pos - c; b = (d * oc).sum(-1); cc = (oc * oc).sum() - R * R
tt2 = -b + np.sqrt(np.maximum(b * b - cc, 0)); P = pos + d * tt2[..., None]
az = np.degrees(np.arctan2(P[..., 2] - c[2], -(P[..., 0] - c[0]))); el = np.degrees(np.arcsin(np.clip((P[..., 1] - c[1]) / R, -1, 1)))
uu = (az + 55) / 110; ww = (el + 20) / 65
col = uu * tw_ - .5; row = (1 - ww) * th_ - .5
img = np.zeros((H, W, 3), np.float32)
for k in range(3): img[..., k] = map_coordinates(im[..., k], [row, col], order=1, mode="nearest")
img[~open_] = (.2, .2, .22)
rows = np.where(open_.any(1))[0]; cols = np.where(open_.any(0))[0]
crop = img[rows.min():rows.max() + 1, cols.min():cols.max() + 1]
Image.fromarray((np.clip(crop, 0, 1) * 255).astype(np.uint8)).save(out)
print("window crop", crop.shape, "mean rgb", (crop.reshape(-1, 3).mean(0) * 255).round())
