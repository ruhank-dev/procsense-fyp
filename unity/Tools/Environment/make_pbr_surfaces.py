"""Packs CC0 Poly Haven PBR scans into hospital surface maps for URP Lit.

The scans are the wrong colour for a hospital (grey plaster, orange linoleum, black rubber), so only their structure is used:
albedo = chosen hospital colour x the scan's normalised luminance detail (so seams, grain and variation come from a real photograph),
normal = the scan's own normal map, smoothness (metallic map alpha) = 1 - scan roughness remapped, occlusion = the scan's AO.

  python -I make_pbr_surfaces.py <scan_dir> <out_dir>
Sources (all CC0, polyhaven.com): white_plaster_02, rubber_tiles. See docs/assets/EXTERIOR_ASSET_SOURCES.md.
"""
import sys
from pathlib import Path
import cv2
import numpy as np

src, out = Path(sys.argv[1]), Path(sys.argv[2]); out.mkdir(parents=True, exist_ok=True)


def load(name, kind, gray=False):
    im = cv2.imread(str(src / f"{name}_{kind}.jpg"), cv2.IMREAD_GRAYSCALE if gray else cv2.IMREAD_COLOR)
    return im.astype(np.float32) / 255.0


def detail(name, strength):
    """Luminance of the scan divided by its own blurred mean, so only local variation remains (tileable: blur wraps)."""
    g = load(name, "Diffuse", gray=True)
    pad = 192; gp = np.pad(g, pad, mode="wrap")                                        # blur on a wrapped copy so the result stays tileable
    low = cv2.GaussianBlur(gp, (0, 0), 64)[pad:-pad, pad:-pad]
    d = g / np.maximum(low, 1e-3)
    d = np.clip(1 + (d - 1) * strength, .6, 1.4)
    return d


def write_set(prefix, tint, name, strength, smooth_lo, smooth_hi, ao_strength=1.0):
    d = detail(name, strength)
    rgb = np.clip(d[..., None] * np.array(tint, np.float32)[None, None, ::-1], 0, 1)      # BGR for OpenCV
    cv2.imwrite(str(out / f"{prefix}_albedo.png"), (rgb * 255).astype(np.uint8))
    nor = cv2.imread(str(src / f"{name}_nor_gl.jpg"), cv2.IMREAD_COLOR)
    cv2.imwrite(str(out / f"{prefix}_normal.png"), nor)
    rough = load(name, "Rough", gray=True)
    smooth = np.clip(smooth_hi - (rough - rough.min()) / max(1e-3, np.ptp(rough)) * (smooth_hi - smooth_lo), 0, 1)
    mask = np.zeros(rough.shape + (4,), np.float32); mask[..., 3] = smooth                 # RGBA: metallic 0, smoothness in alpha
    cv2.imwrite(str(out / f"{prefix}_mask.png"), (mask * 255).astype(np.uint8))
    ao = 1 - (1 - load(name, "AO", gray=True)) * ao_strength
    cv2.imwrite(str(out / f"{prefix}_ao.png"), (np.clip(ao, 0, 1) * 255).astype(np.uint8))
    print("wrote", prefix, rgb.shape, "smoothness", float(smooth.min()), float(smooth.max()))


# painted plaster: warm off-white, fine grain, matte-satin
write_set("hosp_wall", (0.93, 0.925, 0.90), "white_plaster_02", strength=.28, smooth_lo=.10, smooth_hi=.28, ao_strength=.55)
# resilient floor: blue-grey sheet/tile, visible seams, satin
# the mask holds smoothness/0.45 because the material scales it by _Smoothness 0.45 (GreyVinylFloorContractTests: satin vinyl, 0.2 to 0.45, not a mirror)
write_set("hosp_floor", (0.60, 0.645, 0.70), "rubber_tiles", strength=.9, smooth_lo=.62, smooth_hi=1.0, ao_strength=.8)
