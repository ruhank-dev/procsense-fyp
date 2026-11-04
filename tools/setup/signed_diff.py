"""Signed brightness difference (clone minus working project) per image and per horizontal third: python -I tools/setup/signed_diff.py <dir_main> <dir_clone>"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

a_dir, b_dir = Path(sys.argv[1]), Path(sys.argv[2])
for p in sorted(a_dir.glob("*.png")):
    q = b_dir / p.name
    if not q.exists():
        continue
    A = np.asarray(Image.open(p).convert("RGB")).astype(np.float32)
    B = np.asarray(Image.open(q).convert("RGB")).astype(np.float32)
    d = (B - A).mean(2)
    h = d.shape[0]
    parts = [round(float(d[i * h // 3:(i + 1) * h // 3].mean()), 2) for i in range(3)]
    rgb = [round(float((B - A)[..., c].mean()), 2) for c in range(3)]
    print(f"{p.name:28s} signed mean {d.mean():+.2f}  top/mid/bottom {parts}  R,G,B {rgb}")
