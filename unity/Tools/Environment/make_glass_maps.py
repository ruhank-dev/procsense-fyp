"""Architectural float-glass detail maps: a very low-amplitude large-scale waviness normal (real glass is never optically perfect) and a smoothness map with faint dust / smudge variation.

  python -I make_glass_maps.py <out_dir>  ->  glass_normal.png, glass_mask.png (512 px, tileable). Original project artwork.
"""
import sys
from pathlib import Path
import cv2
import numpy as np

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
N = 512; rng = np.random.default_rng(11)


def tile_noise(sigma):
    g = rng.normal(0, 1, (N, N)).astype(np.float32); pad = int(sigma * 3)
    g = np.pad(g, pad, mode="wrap"); g = cv2.GaussianBlur(g, (0, 0), sigmaX=sigma)[pad:-pad, pad:-pad]
    return g / (np.abs(g).max() + 1e-6)


wave = tile_noise(60) * .8 + tile_noise(25) * .2
gx, gy = cv2.Sobel(wave, cv2.CV_32F, 1, 0, ksize=3), cv2.Sobel(wave, cv2.CV_32F, 0, 1, ksize=3)
n = np.dstack([gx * .25, gy * .25, np.ones_like(gx)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
cv2.imwrite(str(out / "glass_normal.png"), ((n * .5 + .5)[..., ::-1] * 255).astype(np.uint8))
dust = np.clip(tile_noise(18) * .5 + tile_noise(6) * .25 + .15, 0, 1)
smooth = np.clip(.985 - .10 * dust - .04 * np.abs(tile_noise(3)), 0, 1)
mask = np.zeros((N, N, 4), np.uint8); mask[..., 3] = (smooth * 255).astype(np.uint8)
cv2.imwrite(str(out / "glass_mask.png"), mask)
print("glass maps written")
