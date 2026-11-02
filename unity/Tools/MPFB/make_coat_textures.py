"""Cotton twill for the nurses' lab coats (albedo + normal, 1024 px, tileable): a diagonal 2/1 twill weave with per-thread tone variation, faint slubs and soft weave shading.

  python unity/Tools/MPFB/make_coat_textures.py  ->  unity/Logs/Staff/labcoat_albedo.png, labcoat_normal.png
"""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage as ndi

OUT = Path(__file__).resolve().parents[2] / "Logs" / "Staff"; OUT.mkdir(parents=True, exist_ok=True)
S = 1024
r = np.random.default_rng(12)
PITCH = 8                                                          # px per thread: 128 threads per tile
yy, xx = np.mgrid[0:S, 0:S]
u, v = (xx // PITCH), (yy // PITCH)
twill = ((u + v) % 3 == 0)                                         # warp floats over three weft threads on a diagonal
fx, fy = (xx % PITCH) / PITCH, (yy % PITCH) / PITCH
thread_x = np.sin(fx * np.pi); thread_y = np.sin(fy * np.pi)
height = np.where(twill, thread_x * .8 + .2, thread_y * .55)       # warp threads stand proud, weft sit back
height = height.astype(np.float32)
tone = r.normal(1.0, .012, (S // PITCH + 1, S // PITCH + 1)).astype(np.float32)
tone = np.kron(tone, np.ones((PITCH, PITCH), np.float32))[:S, :S]
slub = ndi.gaussian_filter(r.random((S, S)).astype(np.float32), (1.2, 30)) - .5                # long fine slubs along the warp
base = 1.0 - (1 - height) * .05 + slub * .025
alb = np.stack([base * 250, base * 251, base * 252], -1) * tone[..., None]
Image.fromarray(np.clip(alb, 0, 255).astype(np.uint8)).save(OUT / "labcoat_albedo.png")
gy, gx = np.gradient(ndi.gaussian_filter(height, .6))
n = np.stack([-gx * 3.0, gy * 3.0, np.ones_like(height)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
Image.fromarray(np.clip((n * .5 + .5) * 255, 0, 255).astype(np.uint8)).save(OUT / "labcoat_normal.png")
print("lab coat twill written to", OUT)
