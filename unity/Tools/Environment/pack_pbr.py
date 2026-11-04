"""Packs a CC0 PBR scan (ambientCG or Poly Haven) into the four maps URP Lit needs.

  python -I pack_pbr.py <ambientcg|polyhaven> <scan_dir> <asset_id> <out_dir> <prefix> [smooth_scale] [size]

albedo = the scan colour (sRGB), normal = OpenGL normal map (Unity convention), mask = RGBA (R metallic, A smoothness = 1 - roughness * smooth_scale), ao = ambient occlusion (white when the scan has none).
Scans are resized to `size` (default 2048). Sources and licences: docs/research/asset_sources_and_licenses.md.
"""
import sys
from pathlib import Path
import cv2
import numpy as np

kind, src, aid, out, prefix = sys.argv[1], Path(sys.argv[2]), sys.argv[3], Path(sys.argv[4]), sys.argv[5]
scale = float(sys.argv[6]) if len(sys.argv) > 6 else 1.0
size = int(sys.argv[7]) if len(sys.argv) > 7 else 2048
out.mkdir(parents=True, exist_ok=True)

if kind == "ambientcg":
    f = lambda k: src / f"{aid}_2K-JPG_{k}.jpg"
    names = dict(color="Color", normal="NormalGL", rough="Roughness", ao="AmbientOcclusion", metal="Metalness")
else:
    f = lambda k: src / f"{aid}_{k}.jpg"
    names = dict(color="Diffuse", normal="nor_gl", rough="Rough", ao="AO", metal="Metal")


def load(key, gray=False):
    p = f(names[key])
    if not p.exists():
        return None
    im = cv2.imread(str(p), cv2.IMREAD_GRAYSCALE if gray else cv2.IMREAD_COLOR)
    return cv2.resize(im, (size, size), interpolation=cv2.INTER_AREA)


color = load("color")
cv2.imwrite(str(out / f"{prefix}_albedo.png"), color)
cv2.imwrite(str(out / f"{prefix}_normal.png"), load("normal"))
rough = load("rough", True).astype(np.float32) / 255
metal = load("metal", True)
mask = np.zeros((size, size, 4), np.uint8)
if metal is not None:
    mask[..., 2] = metal                                                   # BGRA: red channel = metallic
mask[..., 3] = (np.clip(1 - rough * scale, 0, 1) * 255).astype(np.uint8)
cv2.imwrite(str(out / f"{prefix}_mask.png"), mask)
ao = load("ao", True)
cv2.imwrite(str(out / f"{prefix}_ao.png"), ao if ao is not None else np.full((size, size), 255, np.uint8))
print("packed", prefix, "metal" if metal is not None else "dielectric", "ao" if ao is not None else "no-ao")
