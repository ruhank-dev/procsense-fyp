"""Offline report of the clinical-text compression probe (tools/unity/texture_probe.ps1). CPU only, BelowNormal priority.

Reads <dir>/crops/poses.json and <dir>/crops/{U,T,A66,A44}/<target>.png: the same camera pose with the texture imported four ways
  U   = uncompressed RGBA32 at full size (reference)
  T   = today's shipped Android import (no override)
  A66 = ASTC 6x6 at full size, A44 = ASTC 4x4 at full size
and writes <dir>/texture_probe_report.md. The result is declared INVALID, with the reason, when:
  * no surface holds text in the reference state (edge energy below the per-surface minimum),
  * the import states did not change the pixels (A66 identical to the reference on every surface, or A66 and A44 identical on every surface),
  * a state's frame is missing for a surface.
With --doc the valid table replaces section 4 of the lane 3 document. Exit code 0 valid, 3 invalid.
"""
import argparse, csv, json, math, os, sys

try:
    import ctypes
    ctypes.windll.kernel32.SetPriorityClass(ctypes.windll.kernel32.GetCurrentProcess(), 0x4000)  # BELOW_NORMAL_PRIORITY_CLASS
except Exception:
    pass

import numpy as np
from PIL import Image

EDGE_MIN = 4.0
STATES = ["U", "T", "A66", "A44"]
OTHERS = STATES[1:]
LABEL = {"T": "Today (shipped import)", "A66": "ASTC 6x6, full size", "A44": "ASTC 4x4, full size"}


def lum(a):
    return a[..., 0] * 0.299 + a[..., 1] * 0.587 + a[..., 2] * 0.114


def edge_energy(a):
    l = lum(a)
    if l.shape[0] < 4 or l.shape[1] < 4:
        return 0.0
    return float(np.abs(np.diff(l, axis=1))[:-1, :].mean() + np.abs(np.diff(l, axis=0))[:, :-1].mean())


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", required=True)
    ap.add_argument("--doc", default="")
    a = ap.parse_args()
    crops = os.path.join(a.dir, "crops")
    poses = json.load(open(os.path.join(crops, "poses.json"), encoding="utf-8"))["poses"]
    rows, dropped, problems = [], [], []
    for p in poses:
        if not p.get("valid"):
            dropped.append((p["target"], p.get("note", "")))
            continue
        imgs = {}
        for s in STATES:
            f = os.path.join(crops, s, p["target"] + ".png")
            if not os.path.exists(f):
                problems.append("missing frame %s/%s.png" % (s, p["target"]))
                break
            imgs[s] = np.asarray(Image.open(f).convert("RGB")).astype(np.float32)
        if len(imgs) != len(STATES):
            continue
        x0, y0, x1, y1 = [int(round(p[k])) for k in ("x0", "y0", "x1", "y1")]
        h, w = imgs["U"].shape[:2]
        x0, x1, y0, y1 = max(0, x0), min(w, x1), max(0, y0), min(h, y1)
        cu = imgs["U"][y0:y1, x0:x1]
        if cu.shape[0] < 16 or cu.shape[1] < 16:
            dropped.append((p["target"], "crop too small"))
            continue
        e = edge_energy(cu)
        need = 0.7 * float(p.get("edge_min", EDGE_MIN))  # the in-test measure samples every second pixel; allow for the difference
        if e < need:
            dropped.append((p["target"], "plain crop in the reference state (edge energy %.1f, needs %.1f)" % (e, need)))
            continue
        r = {"target": p["target"], "size": "%dx%d" % (cu.shape[1], cu.shape[0]), "edge": e, "crop": {"U": cu}}
        for s in OTHERS:
            c = imgs[s][y0:y1, x0:x1]
            r["crop"][s] = c
            d = c - cu
            mse = float((d * d).mean())
            r[s] = {"identical": mse == 0.0, "psnr": 99.0 if mse == 0.0 else 10 * math.log10(255 * 255 / mse),
                    "bad": float((np.abs(d).max(axis=2) > 24).mean() * 100), "sharp": edge_energy(c) / max(e, 1e-6)}
        r["a66_vs_a44_identical"] = bool(np.array_equal(r["crop"]["A66"], r["crop"]["A44"]))
        rows.append(r)

    # formats actually loaded per state (informational: proof the import state was applied)
    fmt = {}
    for s in STATES:
        f = os.path.join(crops, s, "formats.csv")
        if os.path.exists(f):
            for row in csv.DictReader(open(f, encoding="utf-8")):
                fmt.setdefault(row["target"], {})[s] = row["format"].replace("ASTC_", "") + " " + row["width"] + "x" + row["height"]
    valid = True
    reasons = []
    if not rows:
        valid = False
        reasons.append("no surface holds text in the reference state")
    else:
        if all(r["A66"]["identical"] for r in rows):
            valid = False
            reasons.append("ASTC 6x6 left every pixel identical to the reference: the import state was not applied (or the surfaces do not use these textures)")
        if all(r["a66_vs_a44_identical"] for r in rows):
            valid = False
            reasons.append("ASTC 4x4 and 6x6 are identical on every surface: the import states did not change the pixels")
    reasons += problems

    out = []
    out.append("# Clinical-text compression probe (generated)\n")
    out.append("Result: **%s**" % ("VALID" if valid else "INVALID: " + "; ".join(reasons)))
    out.append("\nSurfaces compared: %d; dropped: %d. Every figure is the surface rectangle at a Quest eye (1832 x 1920, 4x MSAA, 0.8 to 1.2 m), against the uncompressed full-size reference. Cell: PSNR dB / share of pixels more than 24 levels off / edge sharpness relative to the reference (1.000 = as sharp).\n" % (len(rows), len(dropped)))
    if rows:
        out.append("| Texture | Crop (px) | " + " | ".join(LABEL[s] for s in OTHERS) + " | Loaded format U / T / 6x6 / 4x4 |")
        out.append("|---|---|" + "---|" * len(OTHERS) + "---|")
        f = lambda v: "identical" if v["identical"] else "%.1f / %.2f / %.3f" % (v["psnr"], v["bad"], v["sharp"])
        for r in rows:
            ff = fmt.get(r["target"], {})
            out.append("| %s | %s | %s | %s |" % (r["target"], r["size"], " | ".join(f(r[s]) for s in OTHERS), " / ".join(ff.get(s, "?") for s in STATES)))
        out.append("")
        for s in OTHERS:
            out.append("Mean PSNR, %s: %.1f dB over %d surfaces; worst %.1f dB (%s)." % (LABEL[s], np.mean([r[s]["psnr"] for r in rows]), len(rows), min(r[s]["psnr"] for r in rows), min(rows, key=lambda r: r[s]["psnr"])["target"]))
        out.append("\nAs a rule of thumb, differences below about 30 to 35 dB can show on sharp text edges.")
        worst = sorted(rows, key=lambda r: r["T"]["psnr"])[:3]
        tiles = []
        for r in worst:
            ch = min(c.shape[0] for c in r["crop"].values()); cw = min(c.shape[1] for c in r["crop"].values())
            ch, cw = min(ch, 300), min(cw, 300)
            tiles.append(np.concatenate([r["crop"][s][:ch, :cw] for s in STATES], axis=1).astype(np.uint8))
        if tiles:
            W = max(t.shape[1] for t in tiles)
            tiles = [np.pad(t, ((0, 0), (0, W - t.shape[1]), (0, 0))) for t in tiles]
            sheet = os.path.join(a.dir, "text_sheet.png")
            Image.fromarray(np.concatenate(tiles, axis=0)).save(sheet)
            out.append("\nSheet of the three surfaces most affected by today's import (left to right: uncompressed full size, today, ASTC 6x6 full size, ASTC 4x4 full size): " + sheet.replace(chr(92), "/"))
    if dropped:
        out.append("\nDropped surfaces: " + "; ".join("%s (%s)" % d for d in dropped))
    md = "\n".join(out) + "\n"
    open(os.path.join(a.dir, "texture_probe_report.md"), "w", encoding="utf-8").write(md)
    print(md)
    if valid and a.doc and os.path.exists(a.doc):
        d = open(a.doc, encoding="utf-8").read()
        i, j = d.find("## 4. Clinical text readability"), d.find("## 5. A/B configurations")
        if i >= 0 and j > i:
            body = "## 4. Clinical text readability (pixel check, valid run)\n\n" + md.split("\n", 2)[2] + "\n"
            open(a.doc, "w", encoding="utf-8").write(d[:i] + body + d[j:])
    return 0 if valid else 3


if __name__ == "__main__":
    sys.exit(main())
