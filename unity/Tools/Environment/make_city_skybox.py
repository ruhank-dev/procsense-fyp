"""Builds the photographic city skybox from a Poly Haven HDRI (CC0).

The source is a rooftop panorama, so below the horizon it shows a lawn and fence that would read as standing on that roof.
From the hospital's high floor the view below the horizon must be far-off city haze, so the lower hemisphere is replaced
with a haze gradient sampled from the skyline band and blended in over a few degrees; the sky and skyline above are untouched.

  python -I make_city_skybox.py <in.hdr> <out.hdr> [blend_start_deg=0.3] [blend_end_deg=5] [shift_down_deg=0] [haze_band_weight=.65] [nosun]
Prints the sun's azimuth (column fraction * 360) so SKY_ROTATION can be derived the same way as for the previous sky.
"""
import sys
import cv2
import numpy as np

src, dst = sys.argv[1], sys.argv[2]
b0 = float(sys.argv[3]) if len(sys.argv) > 3 else 0.3
b1 = float(sys.argv[4]) if len(sys.argv) > 4 else 5.0
img = cv2.imread(src, cv2.IMREAD_UNCHANGED | cv2.IMREAD_ANYDEPTH)[:, :, ::-1].astype(np.float32)   # RGB
h, w, _ = img.shape
lum = img @ np.array([.2126, .7152, .0722], np.float32)
y, x = np.unravel_index(np.argmax(cv2.GaussianBlur(lum, (0, 0), 6)), lum.shape); lum0 = lum
if len(sys.argv) > 7 and sys.argv[7] == "nosun":                                  # the baked PatientBay sun stays the only sun: paint the disc out of the sky
    ys, xs = np.unravel_index(np.argmax(cv2.GaussianBlur(lum, (0, 0), 6)), lum.shape)
    yy, xx = np.mgrid[0:h, 0:w]; dx = np.minimum(abs(xx - xs), w - abs(xx - xs)) * (360.0 / w); dy = (yy - ys) * (180.0 / h)
    r = np.sqrt((dx * np.cos(np.radians(90 - ys / h * 180))) ** 2 + dy ** 2); m = np.clip((14 - r) / 6, 0, 1)[:, :, None]
    ring = img[(r > 16) & (r < 22)].reshape(-1, 3); fill = np.median(ring, axis=0)
    img = img * (1 - m) + fill[None, None, :] * m
    lum = img @ np.array([.2126, .7152, .0722], np.float32)
print("sun pixel", x, y, "azimuth_by_column", round(x / w * 360, 1), "elevation", round(90 - y / h * 180, 1), "peak", float(lum0[y, x]))

elev = (0.5 - (np.arange(h) + .5) / h) * 180.0                                   # +90 zenith .. -90 nadir
band = (elev > 0.5) & (elev < 4.0)                                               # skyline band just above the horizon
haze_row = np.median(img[band].reshape(-1, 3), axis=0)                           # tone of the far city at the horizon
sky_horizon = np.percentile(img[(elev > 6) & (elev < 12)].reshape(-1, 3), 50, axis=0)
w_band = float(sys.argv[6]) if len(sys.argv) > 6 else .65
haze = w_band * haze_row + (1 - w_band) * sky_horizon
print("haze colour", haze)
t = np.clip((b0 - elev) / (b1 - b0), 0, 1); t = (t * t * (3 - 2 * t))[:, None, None]             # 0 above the horizon, 1 below -b1
depth = np.clip((-elev - b1) / 60.0, 0, 1)[:, None, None]                        # slow darkening towards the nadir (never seen from a window)
ground = haze[None, None, :] * (1 - .45 * depth)
out = img * (1 - t) + ground * t
shift = float(sys.argv[5]) if len(sys.argv) > 5 else 0.0                    # degrees the picture is moved DOWN: the viewer is high, so the horizon crosses the towers above their bases
if shift:
    n = int(round(shift / 180.0 * h)); out = np.concatenate([out[:n][::-1], out[:-n]], axis=0)
cv2.imwrite(dst, out[:, :, ::-1].astype(np.float32))
print("wrote", dst, out.shape)
