"""Golden-hour window exterior from the CC0 'Stone Pines' panorama (Poly Haven, Greg Zaal). Crops an equirectangular window (az +-55, el -20..45; the same angular
layout the Unity backdrop mesh uses, mirrored so u grows toward the viewer's left), then re-lights it for late afternoon: warm low-sun colour grade, sun-side sky glow,
a warm rim on foliage, deeper blue-teal upper sky, atmospheric haze toward the horizon. Not a generated image: the scene content is the photograph.
Usage: python golden_exterior.py <lon0_deg> [out.jpg] [strength]"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter

Image.MAX_IMAGE_PIXELS = None
SRC = Path(__file__).resolve().parents[2] / "Logs" / "Exterior" / "stone_pines_src.jpg"
lon0 = float(sys.argv[1]) if len(sys.argv) > 1 else -126
out = Path(sys.argv[2]) if len(sys.argv) > 2 else SRC.parent.parent / "Exterior2" / f"golden_{int(lon0)}.jpg"
S = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
W, H = 3072, 1814

im = Image.open(SRC).convert("RGB"); sw, sh = im.size
u = (np.arange(W) + .5) / W; v = (np.arange(H) + .5) / H
az = (.5 - u) * 110.0                           # mirrored: +az at the image's left
el = 45 - v * 65.0                              # top row = el 45
lon = lon0 + az; lat = el
LIFT = float(sys.argv[4]) if len(sys.argv) > 4 else 3.0                                      # the window only sees world elevations of about -8..+14 deg: show the photograph's crowns and sky there
px = ((lon / 360.0 + .5) % 1.0) * sw; py = (.5 - np.clip(lat + LIFT, -89, 89) / 180.0) * sh
X, Y = np.meshgrid(px, py)
from scipy.ndimage import map_coordinates
src = np.asarray(im).astype(np.float32) / 255
rgb = np.stack([map_coordinates(src[..., k], [Y, X], order=1, mode="wrap") for k in range(3)], -1)


def lin(c): return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)
def srgb(c): return np.where(c <= .0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - .055)


L = lin(rgb)
lum = (L * [.2126, .7152, .0722]).sum(-1)
blue = rgb[..., 2] - np.maximum(rgb[..., 0], rgb[..., 1] * .85)
sky = np.clip((blue - .005) * 9, 0, 1)
sky = np.maximum(sky, np.clip((lum - .45) * 4, 0, 1) * (rgb[..., 2] > rgb[..., 0] * .95))      # bright cloud near blue sky
sky = sky * np.clip((lum - .10) * 6, 0, 1)            # dark needles and bark are never sky
sky = np.clip(gaussian_filter(sky, 1.0) * 1.15 - .08, 0, 1)
el2 = el[:, None] * np.ones((1, W))
# sky: gradient (deep blue aloft -> peach/gold at the horizon); clouds keep their structure and are lit warm from below by the low sun, their shadow sides go lilac
def ramp(e):
    e = np.clip(e, -5, 50)[..., None]
    hor = np.array([1.00, .86, .60]); mid = np.array([.98, .90, .74]); top = np.array([.30, .50, .85])
    t1 = np.clip(e / 20, 0, 1); t2 = np.clip((e - 14) / 30, 0, 1)
    return (hor * (1 - t1) + mid * t1) * (1 - t2) + top * t2
cloud = np.clip((lum - .28) / .55, 0, 1)[..., None] ** 0.8
sky_L = ramp(el2) * (.62 + .95 * cloud) + cloud * np.array([1.0, .66, .30]) * .30 * np.exp(-((el2 - 12) / 26) ** 2)[..., None]
sky_L = sky_L * (1 - .18 * (1 - cloud) * np.array([.4, .1, -.2]))
# land and foliage: gradient map so lit crowns turn gold-yellow and shaded foliage stays deep green, with a share of the photograph's own hue kept for detail
ll = np.clip(lum ** .6 * 1.9, 0, 1.6)[..., None]
shadow_c = np.array([.07, .20, .06]); mid_c = np.array([.66, .64, .15]); lit_c = np.array([1.32, .94, .36])
g = np.where(ll < .55, shadow_c + (mid_c - shadow_c) * (ll / .55), mid_c + (lit_c - mid_c) * np.clip((ll - .55) / .85, 0, 1))
land_L = g * .70 + L * np.array([1.45, 1.10, .52]) * 2.6 * .30
land_L = land_L + (lum[..., None] ** 1.6) * np.array([1.0, .72, .30]) * 1.1 * S             # rim light on the brightest foliage
out_L = land_L * (1 - sky[..., None]) + sky_L * sky[..., None]
haze = np.clip(1 - np.abs(el2 - 2) / 8, 0, 1)[..., None] * .22 * (1 - sky[..., None])
out_L = out_L * (1 - haze) + (np.array([1.0, .66, .34]) * .62) * haze
out_L = np.clip(out_L * 1.28 * np.array([1.07, .97, .90]), 0, None)
# soft shoulder, no flat white
out_L = out_L / (1 + .18 * out_L)
res = np.clip(srgb(out_L), 0, 1)
Image.fromarray((res * 255).astype(np.uint8)).save(out, quality=94)
print("wrote", out, res.mean(0).mean(0).round(3))
