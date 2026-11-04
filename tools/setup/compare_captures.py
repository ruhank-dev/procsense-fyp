"""Compares two capture folders taken from the same fixed cameras (reference environment vs clean clone) and writes side-by-side sheets plus a metrics table.

  python -I tools/setup/compare_captures.py <ref_dir> <clone_dir> <out_dir>

For every PNG name present in both folders: mean absolute difference (0-255 grey), the share of pixels that differ by more than 24 levels, and a PSNR. The dust motes and shafts are animated particles, so
tiny isolated differences are expected; large blocks of difference are not. Writes <out_dir>/metrics.json, <out_dir>/<name>_ref_clone_diff.png (three panels: reference, clone, difference x4).
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ref, clone, out = (Path(a) for a in sys.argv[1:4])
out.mkdir(parents=True, exist_ok=True)
rows = []
for p in sorted(ref.glob("*.png")):
    q = clone / p.name
    if not q.exists():
        rows.append({"name": p.name, "error": "missing in clone"}); continue
    a = Image.open(p).convert("RGB"); b = Image.open(q).convert("RGB")
    if a.size != b.size:
        b = b.resize(a.size)
    A = np.asarray(a).astype(np.float32); B = np.asarray(b).astype(np.float32)
    d = np.abs(A - B).mean(2)
    mse = float(((A - B) ** 2).mean()); psnr = 99.0 if mse == 0 else float(10 * np.log10(255 * 255 / mse))
    rows.append({"name": p.name, "mean_abs_diff": round(float(d.mean()), 3), "pct_pixels_over_24": round(float((d > 24).mean() * 100), 3), "psnr_db": round(psnr, 2)})
    w, h = a.size; s = 0.5
    sheet = Image.new("RGB", (int(w * s) * 3, int(h * s)))
    sheet.paste(a.resize((int(w * s), int(h * s))), (0, 0)); sheet.paste(b.resize((int(w * s), int(h * s))), (int(w * s), 0))
    sheet.paste(Image.fromarray(np.clip(d * 4, 0, 255).astype(np.uint8)).convert("RGB").resize((int(w * s), int(h * s))), (int(w * s) * 2, 0))
    sheet.save(out / (p.stem + "_ref_clone_diff.jpg"), quality=88)
(out / "metrics.json").write_text(json.dumps(rows, indent=1), encoding="utf-8")
for r in rows:
    print(r)
bad = [r for r in rows if "error" in r or r["pct_pixels_over_24"] > 2.0]
print(f"{len(rows)} images compared, {len(bad)} above the 2% changed-pixel threshold or missing")
sys.exit(1 if bad else 0)
