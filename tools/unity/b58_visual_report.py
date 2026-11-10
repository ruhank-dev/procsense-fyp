"""Offline analysis of the B58 visual capture (unity/Assets/Tests/PlayMode/B58VisualCapture.cs). CPU only, BelowNormal priority.

Reads <root>/poses.csv and <root>/<preset>/<pose>.png (lossless, 1440 x 1584, same poses for every preset) and compares each candidate with its reference:
  PSNR and SSIM of the whole frame, share of pixels whose RGB sum differs by more than 60, the largest connected area of such pixels (with its bounding box), mean absolute difference.
  For the text categories (monitors, signs) also, inside the middle of the frame where the surface is: edge-energy ratio (candidate / reference gradient energy; 1.000 = as sharp), correlation of the gradient
  images (1.000 = same strokes in the same places) and SSIM restricted to the stroke mask of the reference. No OCR engine is installed on this machine, so legibility is judged by these structure measures plus viewing.
The capture noise floor (preset compared with a second capture of itself, folder name B20_r2) is subtracted from nothing; it is reported next to every pair so that a difference below it is not a rendering difference.
Verdict per category and pair is a HEURISTIC (preserved / subtly different / degraded); the final judgement is made by looking at the sheets written next to the report.

Usage: python -I b58_visual_report.py [--root D:/ProcSenseWork/reports/b58_visual] [--pairs B20:B20_r2,B20:B58,...] [--out report.md]
"""
import argparse, csv, os, sys

try:
    import ctypes
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)  # BELOW_NORMAL_PRIORITY_CLASS
except Exception:
    pass

import numpy as np
from PIL import Image
from scipy import ndimage
from skimage.metrics import structural_similarity

DEFAULT_PAIRS = ["B20:B20_r2", "B20:B58", "B20:B22", "B20:B24", "B61:B62", "B29:B56", "B15:B63", "B15:B64", "B15:B65", "B15:B60"]
TEXT = ("monitors", "signs")
CATS = ["monitors", "signs", "doors", "windows", "people", "lighting", "reflections", "theatre", "bay"]


def load(p):
    return np.asarray(Image.open(p).convert("RGB")).astype(np.float32)


def lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


def grad(l):
    gx = ndimage.sobel(l, axis=1); gy = ndimage.sobel(l, axis=0)
    return np.hypot(gx, gy)


def metrics(ref, cand, text):
    d = np.abs(cand - ref); s = d.sum(axis=2)
    mse = float(((cand - ref) ** 2).mean())
    r = {"psnr": 99.0 if mse == 0 else 10 * np.log10(255 * 255 / mse), "mean": float(d.mean()), "pct60": float((s > 60).mean() * 100)}
    lab, n = ndimage.label(s > 60)
    if n:
        sizes = ndimage.sum(np.ones_like(s), lab, range(1, n + 1)); k = int(np.argmax(sizes)) + 1
        ys, xs = np.where(lab == k)
        r["area"] = int(sizes[k - 1]); r["bbox"] = (int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max()))
    else:
        r["area"] = 0; r["bbox"] = None
    lr, lc = lum(ref), lum(cand)
    r["ssim"] = float(structural_similarity(lr, lc, data_range=255.0))
    if text:
        h, w = lr.shape
        y0, y1, x0, x1 = int(h * 0.25), int(h * 0.75), int(w * 0.2), int(w * 0.8)
        gr, gc = grad(lr[y0:y1, x0:x1]), grad(lc[y0:y1, x0:x1])
        r["edge_ratio"] = float(gc.mean() / max(gr.mean(), 1e-6))
        a, b = gr - gr.mean(), gc - gc.mean()
        r["grad_corr"] = float((a * b).sum() / max(np.sqrt((a * a).sum() * (b * b).sum()), 1e-6))
        mask = ndimage.binary_dilation(gr > np.percentile(gr, 90), iterations=1)
        _, full = structural_similarity(lr[y0:y1, x0:x1], lc[y0:y1, x0:x1], data_range=255.0, full=True)
        r["mask_ssim"] = float(full[mask].mean()) if mask.any() else 1.0
    return r


def verdict(rows, text):
    """Heuristic over the worst pose of a category."""
    worst_psnr = min(r["psnr"] for r in rows); worst_pct = max(r["pct60"] for r in rows)
    bad_text = text and (min(r["grad_corr"] for r in rows) < 0.97 or min(r["mask_ssim"] for r in rows) < 0.9 or not (0.95 <= min(r["edge_ratio"] for r in rows) and max(r["edge_ratio"] for r in rows) <= 1.05))
    if worst_psnr >= 42 and worst_pct <= 0.05 and not bad_text:
        return "preserved"
    if worst_psnr >= 30 and worst_pct <= 1.5 and not (text and (min(r["grad_corr"] for r in rows) < 0.9 or min(r["mask_ssim"] for r in rows) < 0.7)):
        return "subtly different"
    return "degraded (heuristic)"


def sheet(ref, cand, name, path):
    d = np.clip(np.abs(cand - ref).sum(axis=2) * 3.0, 0, 255).astype(np.uint8)
    d3 = np.stack([d] * 3, axis=2)
    tiles = [np.clip(ref, 0, 255).astype(np.uint8), np.clip(cand, 0, 255).astype(np.uint8), d3]
    im = Image.fromarray(np.concatenate(tiles, axis=1)); im.thumbnail((2400, 900)); im.save(path, quality=88)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--root", default="D:/ProcSenseWork/reports/b58_visual")
    ap.add_argument("--pairs", default=",".join(DEFAULT_PAIRS))
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    cat = {}
    for row in csv.DictReader(open(os.path.join(a.root, "poses.csv"), encoding="utf-8")):
        cat[row["id"]] = row["category"]
    out = ["# B58 visual comparison (generated)\n"]
    os.makedirs(os.path.join(a.root, "sheets"), exist_ok=True)
    for pair in a.pairs.split(","):
        rname, cname = pair.split(":")
        rd, cd = os.path.join(a.root, rname), os.path.join(a.root, cname)
        if not os.path.isdir(rd) or not os.path.isdir(cd):
            out.append("## %s against %s\n\nnot captured (folder missing)\n" % (cname, rname)); continue
        res = {}
        for pid in sorted(cat):
            f1, f2 = os.path.join(rd, pid + ".png"), os.path.join(cd, pid + ".png")
            if not (os.path.exists(f1) and os.path.exists(f2)): continue
            ref, cand = load(f1), load(f2)
            m = metrics(ref, cand, cat[pid] in TEXT)
            res[pid] = (m, ref, cand)
        out.append("## %s against %s\n" % (cname, rname))
        out.append("| Category | Verdict (heuristic) | Worst PSNR dB | Worst SSIM | Worst % px > 60 | Largest area px (pose) | Text: edge ratio / gradient corr / mask SSIM (worst) |\n|---|---|---|---|---|---|---|")
        for c in CATS:
            rows = [(pid, m) for pid, (m, _, _) in res.items() if cat[pid] == c]
            if not rows: continue
            ms = [m for _, m in rows]; text = c in TEXT
            wp = min(rows, key=lambda x: x[1]["psnr"]); la = max(rows, key=lambda x: x[1]["area"])
            t = "n/a"
            if text:
                t = "%.3f / %.3f / %.3f" % (min(m["edge_ratio"] for m in ms), min(m["grad_corr"] for m in ms), min(m["mask_ssim"] for m in ms))
            out.append("| %s | %s | %.1f (%s) | %.4f | %.3f | %d (%s) | %s |" % (c, verdict(ms, text), wp[1]["psnr"], wp[0], min(m["ssim"] for m in ms), max(m["pct60"] for m in ms), la[1]["area"], la[0], t))
            ref, cand = res[wp[0]][1], res[wp[0]][2]
            sheet(ref, cand, wp[0], os.path.join(a.root, "sheets", "%s_vs_%s_%s_%s.jpg" % (cname, rname, c, wp[0])))
        out.append("\nPer pose:\n\n| Pose | Category | PSNR | SSIM | % px > 60 | Largest area (bbox) | Max connected area |\n|---|---|---|---|---|---|---|")
        for pid, (m, _, _) in res.items():
            out.append("| %s | %s | %.1f | %.4f | %.3f | %d %s | %s |" % (pid, cat[pid], m["psnr"], m["ssim"], m["pct60"], m["area"], m["bbox"] if m["bbox"] else "", ""))
        out.append("")
    md = "\n".join(out) + "\n"
    open(a.out or os.path.join(a.root, "b58_visual_report.md"), "w", encoding="utf-8").write(md)
    print(md[:4000])
    return 0


if __name__ == "__main__":
    sys.exit(main())
