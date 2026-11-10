#!/usr/bin/env python3
"""Pixel differences of benchmark views against a base configuration (round 4 audit, lane 4).

Usage: uv run --project tools python tools/unity/bench_pixeldiff.py <png_dir> --base B20 --cands B24,B22,B38 [--out report.md] [--sheet-dir dir]
Reads <png_dir>/<config>_<view>.png written by `bench_desktop.ps1 -Png` (DesktopRenderBenchmark, the 12 views V1..V12).
Per view and candidate: percent of pixels whose summed absolute RGB difference exceeds 60 (the threshold used by every earlier visibility check),
percent above 12 (any visible tone change), the largest connected differing area (pixels, after a 3x3 dilation) and its bounding box, and the maximum difference.
Writes an amplified difference image per candidate and view (sheet-dir) for visual inspection. Needs numpy, pillow and scipy (tools venv).
"""
import argparse, os, sys
import numpy as np
from PIL import Image


def load(p):
    return np.asarray(Image.open(p).convert("RGB"), dtype=np.int16)


def largest_region(mask):
    from scipy import ndimage
    if not mask.any():
        return 0, None
    m = ndimage.binary_dilation(mask, iterations=1)
    lab, n = ndimage.label(m)
    sizes = ndimage.sum(mask, lab, index=range(1, n + 1))
    k = int(np.argmax(sizes)) + 1
    ys, xs = np.where(lab == k)
    return int(sizes[k - 1]), (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("png_dir"); ap.add_argument("--base", default="B20"); ap.add_argument("--cands", required=True)
    ap.add_argument("--out"); ap.add_argument("--sheet-dir"); ap.add_argument("--amp", type=float, default=8.0)
    a = ap.parse_args()
    views = sorted({f.split("_", 1)[1][:-4] for f in os.listdir(a.png_dir) if f.startswith(a.base + "_")}, key=lambda v: int(v[1:]))
    lines = ["| Candidate | View | % px > 60 | % px > 12 | Largest area > 60 (px, bbox) | Max diff (sum RGB) |", "|---|---|---|---|---|---|"]
    summary = []
    for c in a.cands.split(","):
        worst60 = worst12 = 0.0; area = 0; wv = ""
        for v in views:
            pb, pc = os.path.join(a.png_dir, f"{a.base}_{v}.png"), os.path.join(a.png_dir, f"{c}_{v}.png")
            if not (os.path.exists(pb) and os.path.exists(pc)):
                lines.append(f"| {c} | {v} | missing | | | |"); continue
            b, x = load(pb), load(pc)
            d = np.abs(b - x).sum(axis=2)
            n = d.size
            p60, p12 = 100.0 * (d > 60).sum() / n, 100.0 * (d > 12).sum() / n
            ar, bb = largest_region(d > 60)
            lines.append(f"| {c} | {v} | {p60:.4f} | {p12:.3f} | {ar} {bb if bb else ''} | {int(d.max())} |")
            if p60 > worst60: worst60, wv = p60, v
            worst12 = max(worst12, p12); area = max(area, ar)
            if a.sheet_dir:
                os.makedirs(a.sheet_dir, exist_ok=True)
                Image.fromarray(np.clip(d * a.amp, 0, 255).astype(np.uint8)).save(os.path.join(a.sheet_dir, f"diff_{c}_{v}.png"))
        summary.append(f"| {c} | {worst60:.4f} ({wv}) | {worst12:.3f} | {area} |")
    out = ["# Pixel difference against " + a.base, "", "| Candidate | Worst % px > 60 (view) | Worst % px > 12 | Largest area > 60 (px) |", "|---|---|---|---|"] + summary + ["", "## Per view", ""] + lines
    txt = "\n".join(out)
    print(txt)
    if a.out:
        open(a.out, "w", encoding="utf-8").write(txt + "\n")


if __name__ == "__main__":
    sys.exit(main())
