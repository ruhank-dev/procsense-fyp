"""Perforated stainless-steel sheet maps (waiting benches): staggered 5 mm round holes on a 10 mm pitch, one 0.1 m tile per texture (bench UVs are cube-projected at 0.1 m).

  python -I make_perforated_steel.py <out_dir>   ->  perf_steel_{albedo,normal,mask,ao}.png (1024 px). Alpha of the albedo is the hole cut-out (alpha-clipped material).
Original project artwork; steel tone and brush grain follow ambientCG Metal011 (CC0) only loosely, nothing is copied.
"""
import sys
from pathlib import Path
import cv2
import numpy as np

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
N, holes = 1024, 10
yy, xx = np.mgrid[0:N, 0:N].astype(np.float32) / N * holes                         # tile coordinates in pitch units
row = np.floor(yy + .5)
cx = (np.floor(xx + .5 * (row % 2)) ) + .5 * (row % 2)                               # staggered rows
d = np.sqrt((xx - (np.round(xx - .5 * (row % 2)) + .5 * (row % 2))) ** 2 + (yy - row) ** 2)
hole = d < .27                                                                      # 5.4 mm diameter on a 10 mm pitch
rim = (d >= .27) & (d < .34)
rng = np.random.default_rng(7)
grain = cv2.GaussianBlur(rng.normal(0, 1, (N, N)).astype(np.float32), (0, 0), sigmaX=40, sigmaY=0.8)     # long brushed streaks along x
grain = grain / (np.abs(grain).max() + 1e-6)
base = 0.62 + 0.05 * grain
alb = np.dstack([base * 1.00, base * 0.99, base * 0.97])                              # BGR near-neutral steel
alpha = np.where(hole, 0, 255).astype(np.uint8)
alb8 = (np.clip(alb, 0, 1) * 255).astype(np.uint8)
cv2.imwrite(str(out / "perf_steel_albedo.png"), np.dstack([alb8, alpha]))
# normal: bevelled hole rims
hgt = cv2.GaussianBlur(np.where(rim, .5, np.where(hole, 0, 1)).astype(np.float32), (0, 0), 2.2)
gx, gy = cv2.Sobel(hgt, cv2.CV_32F, 1, 0, ksize=3), cv2.Sobel(hgt, cv2.CV_32F, 0, 1, ksize=3)
nz = np.ones_like(gx) * 0.9
n = np.dstack([gx * 3, gy * 3, nz]); n /= np.linalg.norm(n, axis=2, keepdims=True)
cv2.imwrite(str(out / "perf_steel_normal.png"), ((n * .5 + .5)[..., ::-1] * 255).astype(np.uint8))   # BGR file: R=x, G=y(GL), B=z
mask = np.zeros((N, N, 4), np.uint8); mask[..., 2] = 255; mask[..., 3] = (np.clip(.80 + .06 * grain - .25 * rim, 0, 1) * 255).astype(np.uint8)
cv2.imwrite(str(out / "perf_steel_mask.png"), mask)
cv2.imwrite(str(out / "perf_steel_ao.png"), (np.clip(1 - .55 * rim, 0, 1) * 255).astype(np.uint8))
print("perforated steel maps written")
