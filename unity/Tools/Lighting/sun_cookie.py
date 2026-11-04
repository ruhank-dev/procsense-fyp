"""Window-pane light cookies for the two baked suns. The cookie is laid out in the light's own plane, so each texel is traced along the sun direction to the window plane (x = -2.4 m)
and tested against a pane layout: two wide mullions and a transom (the owner's reference shows heavy, clearly separated window-frame patches), plus the real opening's edges.
Usage: python sun_cookie.py <pitch> <yaw> <out.png> [size_m=3.2]   Light position must be the window centre (-2.4, 1.9, 0)."""
import math
import sys

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

pitch, yaw, out = float(sys.argv[1]), float(sys.argv[2]), sys.argv[3]
size = float(sys.argv[4]) if len(sys.argv) > 4 else 3.2
N = 1024
p, y = math.radians(pitch), math.radians(yaw)
f = np.array([math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p)])          # travel direction (Unity: x, y, z)
right = np.array([math.cos(y), 0.0, -math.sin(y)]); upv = np.cross(f, right)                  # Unity left-handed: up = forward x right
if upv[1] < 0: upv = -upv
c0 = np.array([-2.4, 1.9, 0.0])
tt = (np.arange(N) + .5) / N - .5
U, V = np.meshgrid(tt * size, tt * size)
pts = c0[None, None, :] + U[..., None] * right + V[..., None] * upv           # points on the light plane
# ray along f from each point to the plane x = -2.4
s = (-2.4 - pts[..., 0]) / f[0]
hit = pts + s[..., None] * f
hy, hz = hit[..., 1], hit[..., 2]
# opening: y 1.34..2.50, z -1.04..1.04 (inside the frames)
inside = (hy > 1.36) & (hy < 2.48) & (hz > -1.02) & (hz < 1.02)
bars = (np.abs(hz) < .085) | (np.abs(hy - 1.92) < .06)
a = (inside & ~bars).astype(np.float32)
a = gaussian_filter(a, 1.2)
a[:8, :] = 0; a[-8:, :] = 0; a[:, :8] = 0; a[:, -8:] = 0                       # clamp border stays dark
Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "L").save(out)
print("cookie", out, "lit fraction %.2f" % a.mean())
