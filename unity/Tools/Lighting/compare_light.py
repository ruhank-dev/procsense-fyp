"""Compares a Unity capture of the PatientBay reference view with the owner's golden-hour reference (and the same reference with orange saturation cut 28%).
Usage: python compare_light.py <unity_reference.png> <out_dir> [label]
Writes: sheet_<label>.png (reference | adjusted target | Unity) and prints region statistics.
Regions are the same fractions of the frame in all images (the camera matches the reference viewpoint)."""
import sys
from pathlib import Path

import numpy as np
from matplotlib.colors import rgb_to_hsv
from PIL import Image, ImageDraw, ImageFont

LIGHT = Path(__file__).resolve().parents[2] / "Logs" / "Lighting"
REGIONS = {
    "floor sunlit": (.40, .70, .75, .90), "floor foreground": (.25, .78, .35, .95), "front wall (sun)": (.02, .30, .12, .55),
    "back wall": (.52, .20, .70, .34), "ceiling": (.30, .05, .60, .14), "curtain": (.28, .28, .33, .42), "mattress": (.50, .58, .60, .64),
}


def load(p, size=(1600, 900)):
    return Image.open(p).convert("RGB").resize(size, Image.LANCZOS)


def stats(img):
    x = np.asarray(img).astype(float) / 255
    h, w, _ = x.shape
    out = {}
    for name, (a, b, c, d) in REGIONS.items():
        r = x[int(b * h):int(d * h), int(a * w):int(c * w)].reshape(-1, 3).mean(0)
        hsv = rgb_to_hsv(r[None, None, :])[0, 0]
        out[name] = ((r * 255).round().astype(int), round(hsv[0] * 360), round(hsv[1], 2), round(hsv[2], 2))
    L = .2126 * x[..., 0] + .7152 * x[..., 1] + .0722 * x[..., 2]
    out["_global"] = (round(float(L.mean()), 3), round(float((L >= .98).mean() * 100), 2), round(float((L <= .03).mean() * 100), 2), (x.reshape(-1, 3).mean(0) * 255).round().astype(int))
    return out


def main():
    unity, out_dir = Path(sys.argv[1]), Path(sys.argv[2])
    label = sys.argv[3] if len(sys.argv) > 3 else "now"
    out_dir.mkdir(parents=True, exist_ok=True)
    ref, adj, now = load(LIGHT / "reference.png"), load(LIGHT / "reference_adjusted.png"), load(unity)
    f = ImageFont.truetype("arialbd.ttf", 28)
    sheet = Image.new("RGB", (1600 * 3 // 2 + 12, 450 + 44))
    for i, (t, im) in enumerate((("REFERENCE", ref), ("TARGET (orange sat -28%)", adj), ("UNITY " + label, now))):
        c = im.resize((800, 450)); sheet.paste(c, (i * 806 if i < 1 else i * 806 - 0, 44))
    sheet = Image.new("RGB", (800 * 3 + 12, 494))
    for i, (t, im) in enumerate((("REFERENCE", ref), ("TARGET (orange sat -28%)", adj), ("UNITY " + label, now))):
        sheet.paste(im.resize((800, 450)), (i * 806, 44)); ImageDraw.Draw(sheet).text((i * 806 + 8, 6), t, font=f, fill="white")
    sheet.save(out_dir / f"sheet_{label}.png")
    sa, sn, sr = stats(adj), stats(now), stats(ref)
    print(f"{'region':18s} | target (reference -28% orange)       | Unity {label}")
    for k in REGIONS:
        print(f"{k:18s} | rgb {sa[k][0]} h{sa[k][1]} s{sa[k][2]} v{sa[k][3]} | rgb {sn[k][0]} h{sn[k][1]} s{sn[k][2]} v{sn[k][3]}")
    print("global (mean lum, clip%, crushed%, mean rgb): reference", sr["_global"], "| target", sa["_global"], "| unity", sn["_global"])


if __name__ == "__main__":
    main()
