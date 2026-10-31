"""Sun-to-shade contrast of a Unity capture, using the ray-traced sunlit mask of the same sun direction (real geometry). Furniture is not in the mask, so only the floor/wall pixels that the
mask calls lit or shaded AND whose colour is wood/paint-like (not bed, curtains, monitor) are compared by medians, which are robust to the few occluded pixels.
Usage: python contrast_metrics.py <capture.png> <yaw> <pitch> [mullion] [transom] [--nocurtain]"""
import importlib.util
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image

cap, yaw, pitch = sys.argv[1], float(sys.argv[2]), float(sys.argv[3])
mullion = float(sys.argv[4]) if len(sys.argv) > 4 and not sys.argv[4].startswith("--") else .06
transom = float(sys.argv[5]) if len(sys.argv) > 5 and not sys.argv[5].startswith("--") else 0.0
nocurtain = "--nocurtain" in sys.argv
sys.argv = ["predict_sun.py", "_unused.png", f"{yaw}:{pitch}", "--mullion", str(mullion), "--transom", str(transom)] + (["--nocurtain"] if nocurtain else [])
spec = importlib.util.spec_from_file_location("ps", Path(__file__).with_name("predict_sun.py")); ps = importlib.util.module_from_spec(spec)
import io, contextlib
with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(ps)
y, p = math.radians(yaw), math.radians(pitch)
travel = np.array([math.sin(y) * math.cos(p), -math.sin(p), math.cos(y) * math.cos(p)])
L = ps.lit(ps.P, travel)
im = np.asarray(Image.open(cap).convert("RGB").resize((ps.W, ps.H), Image.LANCZOS)).astype(float) / 255
lum = (im * [.2126, .7152, .0722]).sum(-1)
print("surface | lit px | shaded px | median lum lit | median lum shaded | contrast (lit/shade)")
for sid, name in ((4, "floor"), (2, "front wall"), (3, "back wall"), (1, "door wall")):
    s = ps.surf == sid
    a, b = L & s, (~L) & s
    if a.sum() > 30 and b.sum() > 30:
        # exclude the darkest 5 percent of shaded pixels (furniture shadows / objects) and use medians
        lo = np.median(lum[a]); hi = np.median(lum[b])
        print(f"{name:10s} | {a.sum():6d} | {b.sum():9d} | {lo:14.3f} | {hi:17.3f} | {lo / max(hi, 1e-4):.2f}x")
    else:
        print(f"{name:10s} | {a.sum():6d} | {b.sum():9d} | (too little lit area in the frame)")
