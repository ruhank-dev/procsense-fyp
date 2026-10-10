"""Builds the CT, MRI and MRCP pictures with matching reports into content/xsec/ (ADR-0003, imaging scenarios).

    uv run --project tools --extra images tools/data/xsec.py build

No suitable open set with reports exists for these modalities (docs/research/imaging_scenarios.md), so the library is:
* CLEAN pictures (CT-AP-00, CT-GB-00, MR-AP-00): REAL slices from AMOS 2022 (Zenodo 7155725, CC BY 4.0; fetch_amos.py) cropped, window-levelled and re-encoded. The dataset
  contains patients with many conditions and gives no per-slice diagnosis, so each picture was looked at and only slices without an obvious abnormality in the region shown were
  taken; the clinician confirms.
* PROBLEM pictures (CT-AP-01, CT-AP-02, CT-GB-01, MR-AP-01): the same kind of real slice with the finding PAINTED into it in code (an inflamed appendix, an abscess, a thickened
  gas-containing gallbladder wall) at the size the report states, using the scan's own voxel size, noise and tissue values. They are flagged synthetic, name the real base slice and
  carry the visible mark "SIMULATION: synthetic image, not a real scan". Painting onto a real slice keeps the look of clean and problem pictures the same, so a trainee cannot tell
  them apart by style.
* MRCP (MRCP-00, MRCP-01): drawn entirely in code (bright fluid-filled bile ducts on a dark background, which is what an MRCP shows), flagged synthetic, marked.
Everything is deterministic from the seeds below, starts DRAFT, and is never used to train or test any diagnostic model.
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import nibabel as nib
import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import approvals  # noqa: E402
CACHE = HERE / "cache" / "amos"
OUT = ROOT / "content" / "xsec"
MARK = "SIMULATION: synthetic image, not a real scan"
MARK_HEIGHT = 30
FONTS = ["C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
ATTRIBUTION = ("AMOS: a large-scale abdominal multi-organ benchmark, Ji Y. et al., NeurIPS 2022 Datasets and Benchmarks, doi:10.5281/zenodo.7155725, CC BY 4.0. "
               "Slice cropped, window-levelled and re-encoded without metadata.")
WIDTH = 640


# ------------------------------------------------------------------ helpers
def gblur(a: np.ndarray, s: float) -> np.ndarray:
    h, w = a.shape
    fy, fx = np.fft.fftfreq(h)[:, None], np.fft.rfftfreq(w)[None, :]
    return np.fft.irfft2(np.fft.rfft2(a) * np.exp(-2 * np.pi ** 2 * (s * s) * (fx ** 2 + fy ** 2)), s=(h, w)).astype(np.float32)


def font(size: int):
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            continue
    return ImageFont.load_default()


def ellipse(shape, cx, cy, rx, ry, angle=0.0, edge=1.2) -> np.ndarray:
    """Soft-edged ellipse mask (1 inside), edge in pixels."""
    yy, xx = np.mgrid[:shape[0], :shape[1]].astype(np.float32)
    ca, sa = np.cos(angle), np.sin(angle)
    u, v = (xx - cx) * ca + (yy - cy) * sa, -(xx - cx) * sa + (yy - cy) * ca
    d = np.sqrt((u / rx) ** 2 + (v / ry) ** 2)
    return np.clip((1 - d) * min(rx, ry) / edge + 0.5, 0, 1).astype(np.float32)


def noise(shape, rng, std, corr=0.9) -> np.ndarray:
    n = gblur(rng.normal(0, 1, shape).astype(np.float32), corr)
    return n / (n.std() + 1e-6) * std


def lerp_to(hu, mask, target, rng, std):
    """Move voxels towards a target HU by mask, with noise like the scan's own."""
    return hu * (1 - mask) + (target + noise(hu.shape, rng, std)) * mask


def strand(hu, centre, radius_px, rng, strength=0.7):
    """Inflammatory fat stranding: fat inside the halo becomes denser and streaky."""
    cx, cy = centre
    halo = ellipse(hu.shape, cx, cy, radius_px, radius_px, edge=radius_px * 0.8)
    fat = np.clip((-30 - hu) / 40, 0, 1) * np.clip((hu + 160) / 60, 0, 1)    # 1 in fat, 0 in tissue and air
    streak = np.clip(gblur(rng.normal(0, 1, hu.shape).astype(np.float32), 2.2) * 6 + 0.7, 0, 1)
    return hu + halo * fat * (55 + 35 * streak) * strength


def load_mr(vid: str, z: int):
    """AMOS MRI is stored with the patient's right increasing along the first axis; flip so the right is on the image left (radiological convention)."""
    im = nib.load(CACHE / f"imagesTr_{vid}.nii.gz")
    a = np.asarray(im.dataobj)[::-1, :, z].astype(np.float32).T[::-1, :]
    return a / np.percentile(a, 99.7), float(im.header.get_zooms()[0])


def best_gb_slice(vid: str) -> int:
    lab = np.asarray(nib.load(CACHE / f"labelsTr_{vid}.nii.gz").dataobj)
    return int(np.argmax((lab == 4).sum((0, 1))))


def load_ct(vid: str, z: int):
    im = nib.load(CACHE / f"imagesTr_{vid}.nii.gz")
    a = np.rot90(np.asarray(im.dataobj)[:, :, z].astype(np.float32))
    lab = np.rot90(np.asarray(nib.load(CACHE / f"labelsTr_{vid}.nii.gz").dataobj)[:, :, z])
    return a, lab, float(im.header.get_zooms()[0])


def to_image(hu: np.ndarray, crop, lo, hi) -> Image.Image:
    y0, y1, x0, x1 = crop
    g = (np.clip((hu[y0:y1, x0:x1] - lo) / (hi - lo), 0, 1) * 255 + 0.5).astype(np.uint8)
    im = Image.fromarray(g)
    return im.resize((WIDTH, round(WIDTH * g.shape[0] / g.shape[1])), Image.LANCZOS)


def add_mark(im: Image.Image) -> Image.Image:
    out = Image.new("L", (im.width, im.height + MARK_HEIGHT), 0)
    out.paste(im, (0, 0))
    d = ImageDraw.Draw(out)
    f = font(17)
    tw = d.textlength(MARK, font=f)
    d.text(((out.width - tw) / 2, im.height + 4), MARK, fill=235, font=f)
    return out


def strip_hash(im: Image.Image) -> str:
    return hashlib.sha256(im.convert("L").crop((0, im.height - MARK_HEIGHT, im.width, im.height)).tobytes()).hexdigest()


# ------------------------------------------------------------------ bases and findings
# crop = (row0, row1, col0, col1) in the displayed (radiological) slice; window = soft-tissue W400 L40 for CT
CT_WIN = (-160, 240)
BASES = {
    "rif_a": dict(vid="amos_0259", z=37, crop=(110, 400, 30, 490), pix=0.652, ce=False, app=(172, 222), ang=1.2, cae=(145, 225)),
    "rif_b": dict(vid="amos_0088", z=34, crop=(150, 330, 70, 330), pix=0.747, ce=True, app=(176, 248), ang=1.1, cae=(150, 272)),
    "rif_c": dict(vid="amos_0127", z=38, crop=(170, 345, 70, 365), pix=0.782, ce=False, app=(158, 265), ang=1.3, cae=(130, 272)),
    "rif_d": dict(vid="amos_0004", z=33, crop=(150, 345, 70, 385), pix=0.782, ce=True, app=(182, 250), ang=1.0, cae=(192, 268)),
    "mr_a": dict(vid="amos_0555", z=5, crop=(55, 195, 40, 250), pix=1.389, app=(106, 100), ang=0.9),
    "mr_b": dict(vid="amos_0600", z=6, crop=(80, 210, 40, 270), pix=1.1875, app=(108, 113), ang=0.9),
    "mr_c": dict(vid="amos_0584", z=8, crop=(55, 185, 30, 262), pix=1.389, app=(110, 106), ang=0.9),
    "mr_d": dict(vid="amos_0540", z=8, crop=(40, 160, 15, 180), pix=1.953, app=(80, 92), ang=0.9),
    "gb_a": dict(vid="amos_0259", z=None, crop=(110, 400, 30, 490), pix=0.652, ce=False),
    "gb_b": dict(vid="amos_0127", z=None, crop=(130, 360, 70, 450), pix=0.782, ce=False),
    "gb_c": dict(vid="amos_0025", z=None, crop=None, pix=0.782, ce=False),
    "gb_d": dict(vid="amos_0004", z=None, crop=None, pix=0.782, ce=False),
    "mid_a": dict(vid="amos_0259", z=43, crop=None, pix=0.652, ce=False),
    "mid_b": dict(vid="amos_0127", z=42, crop=None, pix=0.782, ce=False),
    "mid_c": dict(vid="amos_0025", z=46, crop=None, pix=0.782, ce=False),
    "mid_d": dict(vid="amos_0004", z=44, crop=None, pix=0.782, ce=False),
}


def appendix_ct(hu, b, rng, diam_mm, abscess_mm=0.0, stone=False):
    base = hu
    cx, cy = b["app"]
    r = diam_mm / 2 / b["pix"]
    wall, fluid = (95.0, 25.0) if b["ce"] else (42.0, 18.0)
    hu = strand(hu, (cx, cy), r * 3.2 + 10, rng)
    L = r * 2.4
    outer = ellipse(hu.shape, cx, cy, L, r, b["ang"], edge=1.3)
    inner = ellipse(hu.shape, cx, cy, L * 0.85, r * 0.55, b["ang"], edge=1.3)
    hu = lerp_to(hu, outer, wall, rng, 9)
    hu = lerp_to(hu, inner, fluid, rng, 8)
    if stone:
        hu = lerp_to(hu, ellipse(hu.shape, cx + 3, cy, 2.6, 2.0, b["ang"], edge=0.8), 330, rng, 25)
    if abscess_mm:
        ar = abscess_mm / 2 / b["pix"]
        ax_, ay_ = cx - ar * 0.9 - r, cy + ar * 0.5   # collection beside the appendix towards the pelvis
        rim = ellipse(hu.shape, ax_, ay_, ar + 2.0, ar * 0.8 + 2.0, 0.4, edge=1.5)
        hu = strand(hu, (ax_, ay_), ar * 2.2 + 10, rng, 0.8)
        hu = lerp_to(hu, rim, 60.0 if b["ce"] else 38.0, rng, 9)
        hu = lerp_to(hu, ellipse(hu.shape, ax_, ay_, ar, ar * 0.8, 0.4, edge=1.5), 22, rng, 8)
        for _ in range(4):   # small gas bubbles
            gx, gy = ax_ + rng.uniform(-ar * 0.6, ar * 0.6), ay_ + rng.uniform(-ar * 0.4, ar * 0.4)
            hu = lerp_to(hu, ellipse(hu.shape, gx, gy, rng.uniform(1.2, 2.4), rng.uniform(1.0, 2.0), edge=0.8), -700, rng, 40)
    return base + gblur(hu - base, 0.7)   # the scanner's own blur softens anything painted in



def body_bbox(hu):
    """Bounding box (top, bottom, left, right) of the body: soft tissue eroded a few pixels so the thin CT table lines drop out."""
    m = (hu > -300).astype(np.float32)
    for _ in range(5):
        m = np.minimum.reduce([np.roll(np.roll(m, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1)])
    ys, xs = np.nonzero(m)
    return int(ys.min()), int(ys.max()), int(xs.min()), int(xs.max())


def bowel_obstruction(hu, bbox, rng, loop_mm, pix):
    """Several fluid-filled, dilated small-bowel loops with thin enhancing walls in the anterior and central abdomen."""
    base = hu
    t, b, l, r = bbox
    cx, height, width = (l + r) / 2, b - t, r - l
    placed = []
    attempts = 0
    while len(placed) < 6 and attempts < 200:
        attempts += 1
        x = cx + rng.uniform(-0.3, 0.3) * width
        y = t + (0.19 + rng.uniform(0, 0.17)) * height
        if all(np.hypot(x - px, y - py) > 0.95 * (loop_mm / pix) for px, py in placed):
            placed.append((x, y))
    for x, y in placed:
        rad = loop_mm / 2 / pix * rng.uniform(0.85, 1.1)
        ang = rng.uniform(-0.5, 0.5) + (np.pi / 2 if rng.random() < 0.3 else 0.0)
        rx, ry = rad * rng.uniform(1.5, 2.1), rad * rng.uniform(0.7, 0.9)
        hu = lerp_to(hu, ellipse(hu.shape, x, y, rx + 1.6, ry + 1.6, ang, edge=1.2), 50.0, rng, 8)
        hu = lerp_to(hu, ellipse(hu.shape, x, y, rx, ry, ang, edge=1.2), 14.0, rng, 7)
        for fold in range(2):       # faint valvulae lines across the lumen
            fx = rng.uniform(-0.5, 0.5) * rx
            hu = lerp_to(hu, ellipse(hu.shape, x + fx * np.cos(ang), y + fx * np.sin(ang), 1.0, ry * 0.9, ang + np.pi / 2, edge=0.8) * 0.5, 40.0, rng, 6)
    return base + gblur(hu - base, 0.7)


def free_air_ct(hu, bbox, rng, pix):
    """Free intraperitoneal gas just under the anterior abdominal wall (the highest point of a patient lying on their back), with some mesenteric stranding."""
    base = hu
    t, b, l, r = bbox
    cx, height, width = (l + r) / 2, b - t, r - l
    y0 = t + 0.17 * height
    for k in range(3):
        x = cx + (k - 1) * 0.12 * width + rng.uniform(-6, 6)
        hu = lerp_to(hu, ellipse(hu.shape, x, y0 + rng.uniform(-2, 3), rng.uniform(18, 32), rng.uniform(3.5, 6.5), rng.uniform(-0.1, 0.1), edge=1.0), -960.0, rng, 20)
    for k in range(4):
        hu = lerp_to(hu, ellipse(hu.shape, cx + rng.uniform(-0.2, 0.2) * width, y0 + rng.uniform(6, 18), rng.uniform(1.5, 3.0), rng.uniform(1.2, 2.4), edge=0.8), -900.0, rng, 30)
    hu = strand(hu, (cx, t + 0.34 * height), 0.2 * width, rng, 0.8)
    return base + gblur(hu - base, 0.7)



def colon_lesion(hu, b, rng, kind, size_mm):
    """A caecal mass (irregular soft tissue, little stranding) or right-sided diverticulitis (thickened wall, heavy stranding, an inflamed outpouching with gas) at the caecum."""
    base = hu
    cx, cy = b["cae"]
    px = b["pix"]
    r = size_mm / 2 / px
    soft_hu = 85.0 if b["ce"] else 46.0
    if kind == "mass":
        for k in range(4):
            dx, dy = rng.uniform(-0.45, 0.45) * r, rng.uniform(-0.45, 0.45) * r
            hu = lerp_to(hu, ellipse(hu.shape, cx + dx, cy + dy, r * rng.uniform(0.55, 0.9), r * rng.uniform(0.5, 0.85), rng.uniform(0, 3.14), edge=1.3), soft_hu, rng, 11)
        hu = strand(hu, (cx, cy), r * 1.7, rng, 0.35)
    else:
        outer = ellipse(hu.shape, cx, cy, r, r * 0.9, 0.3, edge=1.3)
        inner = ellipse(hu.shape, cx, cy, r * 0.5, r * 0.42, 0.3, edge=1.3)
        hu = strand(hu, (cx, cy), r * 2.6, rng, 1.0)
        hu = lerp_to(hu, np.clip(outer - inner, 0, 1), soft_hu * 0.95, rng, 10)
        ang = rng.uniform(0, 6.28)
        ox, oy = cx + np.cos(ang) * r * 1.0, cy + np.sin(ang) * r * 0.9
        hu = lerp_to(hu, ellipse(hu.shape, ox, oy, 5.0, 4.0, edge=1.0), soft_hu * 0.8, rng, 10)
        hu = lerp_to(hu, ellipse(hu.shape, ox, oy, 2.2, 1.8, edge=0.8), -700.0, rng, 30)
    return base + gblur(hu - base, 0.7)


def appendix_mr(a, b, rng, diam_mm):
    """Post-contrast fat-suppressed T1: the inflamed appendix has a bright enhancing wall and a darker lumen; the fat around it, normally dark, becomes hazy."""
    base = a
    cx, cy = b["app"]
    r = diam_mm / 2 / b["pix"]
    fat = np.clip((0.16 - a) / 0.12, 0, 1)
    halo = ellipse(a.shape, cx, cy, r * 3.0 + 4, r * 3.0 + 4, edge=r * 2.0)
    streak = np.clip(gblur(rng.normal(0, 1, a.shape).astype(np.float32), 1.3) * 4 + 0.6, 0, 1)
    a = a + halo * fat * (0.17 + 0.14 * streak)
    outer = ellipse(a.shape, cx, cy, r * 2.2, r, b["ang"], edge=0.9)
    inner = ellipse(a.shape, cx, cy, r * 1.8, r * 0.5, b["ang"], edge=0.9)
    a = a * (1 - outer) + (0.62 + noise(a.shape, rng, 0.03, 0.8)) * outer
    a = a * (1 - inner) + (0.30 + noise(a.shape, rng, 0.03, 0.8)) * inner
    return base + gblur(a - base, 0.6)


def gallbladder_ct(hu, lab, b, rng, wall_mm):
    base = hu
    gb = (lab == 4).astype(np.float32)
    if gb.sum() < 30:
        raise SystemExit("no gallbladder in this slice")
    ys, xs = np.nonzero(gb)
    cx, cy = xs.mean(), ys.mean()
    px = b["pix"]
    grown = gb.copy()
    for _ in range(max(2, round(wall_mm / px))):      # dilate by the wall thickness (3 x 3 maximum, repeated)
        grown = np.maximum.reduce([np.roll(np.roll(grown, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1)])
    wall = np.clip(gblur(grown, 0.8), 0, 1) * (1 - gb)
    hu = strand(hu, (cx, cy), max(np.ptp(xs), np.ptp(ys)) * 1.1 + 14, rng, 0.9)
    hu = lerp_to(hu, np.clip(wall, 0, 1), 55.0, rng, 9)
    ring = np.argwhere((wall > 0.6))
    for _ in range(5):   # gas locules inside the thickened wall: pick voxels of the wall ring
        gy, gx = ring[rng.integers(len(ring))]
        hu = lerp_to(hu, ellipse(hu.shape, gx, gy, rng.uniform(1.0, 1.8), rng.uniform(0.8, 1.4), edge=0.7), -650, rng, 40)
    return base + gblur(hu - base, 0.7)


# ------------------------------------------------------------------ MRCP drawn in code
def mrcp(variant: int, duct_mm: float, stone_mm: float):
    """A coronal thick-slab MRCP: fluid is bright, everything else dark. Drawn at twice the size, then blurred like a real acquisition (partial volume), with Rician noise,
    faint solid-organ signal, bowel fluid loops and coil falloff."""
    rng = np.random.default_rng(7000 + variant)
    h, w, ss = 480, 640, 2
    H, W = h * ss, w * ss
    layer = np.zeros((H, W), np.float32)
    mm = 2.0 * ss            # supersampled pixels per mm (0.5 mm per output pixel)
    cx = W * 0.5 + rng.uniform(-8, 8)

    def stamp(x, y, r, val):
        d = ImageDraw.Draw(img)
        d.ellipse([x - r, y - r, x + r, y + r], fill=val)

    def tube(points, w0_mm, w1_mm, val):
        n = len(points)
        for i, ((x0, y0), (x1, y1)) in enumerate(zip(points[:-1], points[1:])):
            wm = w0_mm + (w1_mm - w0_mm) * i / max(1, n - 2)
            ImageDraw.Draw(img).line([(x0, y0), (x1, y1)], fill=int(val * (0.88 + 0.12 * np.sin(i * 0.7))), width=max(1, round(wm * mm)))
            stamp(x1, y1, wm * mm / 2, int(val * 0.95))

    def curve(p0, p1, bend, n=30, wobble=2.0):
        (x0, y0), (x1, y1) = p0, p1
        ph = rng.uniform(0, 6)
        return [(x0 + (x1 - x0) * t + bend * np.sin(np.pi * t) + wobble * mm * np.sin(5 * t + ph) * np.sin(np.pi * t), y0 + (y1 - y0) * t) for t in np.linspace(0, 1, n)]

    def blob(x, y, rx, ry, val, ang=0.0):
        layer_img = Image.new("L", (W, H), 0)
        ImageDraw.Draw(layer_img).ellipse([x - rx, y - ry, x + rx, y + ry], fill=val)
        return np.asarray(layer_img.rotate(np.degrees(ang), center=(x, y), resample=Image.BICUBIC)).astype(np.float32)

    # faint solid organs (liver on the image left, spleen right) and kidney-height structures
    soft = blob(cx - 110 * mm, H * 0.40, 120 * mm, 95 * mm, 22) + blob(cx + 150 * mm, H * 0.36, 36 * mm, 66 * mm, 20)
    soft = gblur(soft, 14.0)
    img = Image.new("L", (W, H), 0)
    hilum = (cx - 14 * mm, H * 0.30)
    papilla = (cx + 6 * mm, H * 0.70)
    # common hepatic duct then common bile duct: gentle S curve, tapering slightly towards the papilla
    tube(curve(hilum, papilla, 9 * mm, 40, 1.2), duct_mm, duct_mm * 0.82, 232)
    # right and left hepatic ducts and their branches (tree), thinner than the main duct; a little wider when the main duct is dilated
    scale = 1.0 + 0.3 * max(0.0, duct_mm - 6) / 6
    for sgn, ang0 in ((-1, 2.6), (1, 0.5)):
        x, y, ang = hilum[0], hilum[1], ang0
        pts = [hilum]
        for seg in range(16):
            ang += rng.normal(0, 0.1)
            x, y = x + 9 * mm * np.cos(ang), y - 6.5 * mm * np.sin(ang)
            pts.append((x, y))
            if seg in (2, 4, 6, 8, 10, 12):
                bx, by, bang = x, y, ang + sgn * rng.uniform(0.45, 0.95)
                bp = [(bx, by)]
                for _ in range(rng.integers(4, 8)):
                    bang += rng.normal(0, 0.18)
                    bx, by = bx + 6 * mm * np.cos(bang), by - 5 * mm * np.sin(bang)
                    bp.append((bx, by))
                tube(bp, 2.2 * scale, 0.9, 205)
        tube(pts, 4.0 * scale, 1.6, 222)
    # gallbladder: pear shape with a neck, cystic duct joining the common duct
    gbx, gby = cx - 58 * mm + rng.uniform(-6, 6) * mm, H * 0.50
    img_d = ImageDraw.Draw(img)
    img_d.ellipse([gbx - 15 * mm, gby - 6 * mm, gbx + 15 * mm, gby + 42 * mm], fill=240)
    img_d.ellipse([gbx - 8 * mm, gby - 36 * mm, gbx + 8 * mm, gby + 2 * mm], fill=235)
    tube(curve((gbx + 5 * mm, gby - 34 * mm), (cx - 7 * mm, H * 0.40), -9 * mm, 24, 1.5), 2.6, 2.4, 215)
    # main pancreatic duct: thin, joining at the papilla
    tube(curve((papilla[0] + 70 * mm, H * 0.64), papilla, 4 * mm, 24, 0.8), 1.8, 2.4, 195)
    # fluid in the stomach (a pouch with folds), the duodenal loop and several small bowel loops: irregular outlines, not ovals
    def irregular(x, y, rx, ry, val, jitter=0.12):
        k = 48
        phase = rng.uniform(0, 6.28, 3)
        pts = []
        for i in range(k):
            t = 2 * np.pi * i / k
            rr = 1 + jitter * (np.sin(3 * t + phase[0]) + 0.6 * np.sin(5 * t + phase[1]) + 0.4 * np.sin(8 * t + phase[2]))
            pts.append((x + rx * rr * np.cos(t), y + ry * rr * np.sin(t)))
        img_d.polygon(pts, fill=val)
    irregular(cx + 82 * mm, H * 0.30, 30 * mm, 48 * mm, 168, 0.14)
    irregular(cx + 30 * mm, H * 0.75, 24 * mm, 14 * mm, 200, 0.15)
    tube(curve((cx + 52 * mm, H * 0.74), (cx + 125 * mm, H * 0.62), -14 * mm, 26, 4.0), 12.0, 14.0, 160)     # duodenojejunal loop
    for (bx, by, rx, ry, v) in ((cx + 128 * mm, H * 0.80, 20 * mm, 11 * mm, 145), (cx + 70 * mm, H * 0.90, 24 * mm, 9 * mm, 150), (cx - 22 * mm, H * 0.89, 22 * mm, 9 * mm, 138), (cx + 160 * mm, H * 0.66, 14 * mm, 22 * mm, 140)):
        irregular(bx, by, rx, ry, v, 0.22)
    # stone: a signal void in the distal duct with a bright crescent of bile above it
    if stone_mm:
        t = 0.82
        sy = hilum[1] + (papilla[1] - hilum[1]) * t
        sx = hilum[0] + (papilla[0] - hilum[0]) * t + 9 * mm * np.sin(np.pi * t)
        r = stone_mm / 2 * mm
        img_d.ellipse([sx - r, sy - r, sx + r, sy + r], fill=22)
    a = np.asarray(img.resize((w, h), Image.LANCZOS)).astype(np.float32)
    a = gblur(a, 1.5)   # partial-volume softening of every edge
    soft_small = np.asarray(Image.fromarray(np.clip(soft, 0, 255).astype(np.uint8)).resize((w, h), Image.LANCZOS)).astype(np.float32)
    a = np.maximum(a, soft_small) + 0.12 * soft_small
    a *= 0.9 + 0.2 * np.clip(gblur(rng.normal(0, 1, (h, w)).astype(np.float32), 25) * 3 + 0.5, 0, 1)   # slow intensity drift
    a = np.sqrt((a + rng.normal(0, 6, a.shape)) ** 2 + rng.normal(0, 6, a.shape) ** 2)             # Rician noise
    yy, xx = np.mgrid[:h, :w]
    a *= 1.0 - 0.3 * (((xx - w / 2) / (w / 2)) ** 2 + ((yy - h / 2) / (h / 2)) ** 2)               # coil falloff
    return Image.fromarray(np.clip(a * 1.08, 0, 255).astype(np.uint8))


# ------------------------------------------------------------------ the library
def report(code, text, impression, measurements, synthetic):
    return {"text": f"{text} Impression: {impression}", "measurements": measurements, "synthetic": synthetic}


def build() -> int:
    for f in OUT.glob("*/*"):
        f.unlink()
    entries, reports = [], []

    def save(iid, code, modality, im, synthetic, base, rep, seed=None, kind=None):
        folder = OUT / code
        folder.mkdir(parents=True, exist_ok=True)
        path = folder / f"{iid}.png"
        if synthetic:
            im = add_mark(im.convert("L"))
        im.save(path, "PNG", optimize=True)
        raw = path.read_bytes()
        e = {"id": iid, "file": f"{code}/{iid}.png", "sha256": hashlib.sha256(raw).hexdigest(), "bytes": len(raw), "width": im.width, "height": im.height, "finding_code": code,
             "modality": modality, "synthetic": synthetic, "status": "DRAFT", "cases": [], "review_required": True}
        if synthetic:
            e.update(source=None, licence="ProcSense generated (CC0 for our own use)", attribution="" if not base else base["attribution"],
                     generator={"name": "xsec.py", "seed": seed, "kind": kind, "base": base["ref"] if base else None},
                     mark={"text": MARK, "strip_sha256": strip_hash(im), "strip_height": MARK_HEIGHT})
        else:
            e.update(source={"dataset": "AMOS 2022", "class": base["ref"], "archive": "amos22.zip", "member": base["member"], "doi": "10.5281/zenodo.7155725",
                             "url": "https://zenodo.org/records/7155725"}, licence="CC BY 4.0", attribution=ATTRIBUTION, generator=None, mark=None)
        entries.append(e)
        reports.append({"id": f"rep-{iid}", "finding_code": code, "image_id": iid, **rep, "status": "DRAFT",
                        "wording": "Category wording of the finding; numbers are those painted or drawn in the picture; real clean pictures state only the absence of an obvious abnormality"})

    def ref(b):
        return {"ref": f"{b['vid']} slice {b['z']}", "member": f"amos22/imagesTr/{b['vid']}.nii.gz", "attribution": ATTRIBUTION}

    def auto_crop(hu, margin=14):
        t, bt, l, r = body_bbox(hu)
        return (max(0, t - margin), min(hu.shape[0], bt + margin), max(0, l - margin), min(hu.shape[1], r + margin))

    # ---- CT, appendix level: four real slices as the clean result, the finding painted into each for the problems
    for v, name in enumerate(("rif_a", "rif_b", "rif_c", "rif_d")):
        b = BASES[name]
        hu, lab, _ = load_ct(b["vid"], b["z"])
        save(f"ct-ap-00-r{v}", "CT-AP-00", "CT", to_image(hu, b["crop"], *CT_WIN), False, ref(b),
             report("CT-AP-00", "CT abdomen and pelvis, axial, soft-tissue window, right iliac fossa level. No acute abnormality is identified at this level: no periappendiceal fat stranding and no collection.",
                    "No acute abnormality at the level of the right iliac fossa.", {"appendix": "normal", "collection": "no"}, False))
        diam = [11.0, 12.0, 10.0, 13.0][v]
        stone = v % 2 == 0
        rng = np.random.default_rng(100 + v)
        hp = appendix_ct(hu.copy(), b, rng, diam, 0.0, stone)
        lith = " containing a calcified appendicitis stone (appendicolith)" if stone else ""
        save(f"ct-ap-01-s{v}", "CT-AP-01", "CT", to_image(hp, b["crop"], *CT_WIN), True, ref(b),
             report("CT-AP-01", f"CT abdomen and pelvis, axial. The appendix is dilated to {diam:g} mm with wall thickening{lith} and surrounding fat stranding. No collection or free gas.",
                    "Acute appendicitis, uncomplicated.", {"appendix_mm": diam, "collection": "no"}, True), seed=100 + v, kind="finding painted into a real CT slice")
        rng = np.random.default_rng(200 + v)
        diam, ab = [13.0, 14.0, 12.0, 15.0][v], [28.0, 34.0, 26.0, 38.0][v]
        hp = appendix_ct(hu.copy(), b, rng, diam, ab, False)
        save(f"ct-ap-02-s{v}", "CT-AP-02", "CT", to_image(hp, b["crop"], *CT_WIN), True, ref(b),
             report("CT-AP-02", f"CT abdomen and pelvis, axial. The appendix is dilated to {diam:g} mm with surrounding fat stranding, and an adjacent rim-enhancing collection of {ab:g} mm containing small gas bubbles.",
                    "Complicated appendicitis with an abscess.", {"appendix_mm": diam, "collection": "yes", "abscess_mm": ab}, True), seed=200 + v, kind="finding painted into a real CT slice")

    # ---- CT, right iliac fossa: caecal mass (older patient) and right-sided diverticulitis (mimics of appendicitis)
    for v, name in enumerate(("rif_a", "rif_b", "rif_c", "rif_d")):
        b = BASES[name]
        hu, lab, _ = load_ct(b["vid"], b["z"])
        rng = np.random.default_rng(700 + v)
        size = [38.0, 45.0, 40.0, 50.0][v]
        hp = colon_lesion(hu.copy(), b, rng, "mass", size)
        save(f"ct-ap-03-s{v}", "CT-AP-03", "CT", to_image(hp, b["crop"], *CT_WIN), True, ref(b),
             report("CT-AP-03", f"CT abdomen and pelvis, axial. An irregular soft-tissue mass of {size:g} mm arises from the caecal wall. The appendix is not inflamed and there is only mild surrounding stranding.",
                    "Caecal mass, suspicious for a tumour.", {"mass_mm": size, "appendix": "normal"}, True), seed=700 + v, kind="mass painted into a real CT slice")
        rng = np.random.default_rng(800 + v)
        wall = [8.0, 9.0, 8.0, 10.0][v]
        hp = colon_lesion(hu.copy(), b, rng, "diverticulitis", wall * 2.8)
        save(f"ct-ap-04-s{v}", "CT-AP-04", "CT", to_image(hp, b["crop"], *CT_WIN), True, ref(b),
             report("CT-AP-04", f"CT abdomen and pelvis, axial. Segmental thickening of the caecal and ascending colon wall (wall {wall:g} mm) with marked pericolic fat stranding and an inflamed diverticulum containing a gas bubble. The appendix is normal.",
                    "Right-sided colonic diverticulitis.", {"wall_mm": wall, "diverticulum": "yes", "appendix": "normal"}, True), seed=800 + v, kind="diverticulitis painted into a real CT slice")

    # ---- MRI, appendix level (post-contrast T1 fat-suppressed appearance)
    for v, name in enumerate(("mr_a", "mr_b", "mr_c", "mr_d")):
        b = BASES[name]
        a_, _ = load_mr(b["vid"], b["z"])
        save(f"mr-ap-00-r{v}", "MR-AP-00", "MRI", to_image(a_, b["crop"], 0.0, 1.0), False, ref(b),
             report("MR-AP-00", "MRI abdomen and pelvis, axial T1-weighted fat-suppressed contrast-enhanced appearance, right iliac fossa level. The appendix is not enlarged and there is no periappendiceal inflammation or collection.",
                    "No evidence of appendicitis at this level.", {"appendix": "normal", "collection": "no"}, False))
        rng = np.random.default_rng(400 + v)
        diam = [10.0, 11.0, 12.0, 11.0][v]
        hp = appendix_mr(a_.copy(), b, rng, diam)
        save(f"mr-ap-01-s{v}", "MR-AP-01", "MRI", to_image(hp, b["crop"], 0.0, 1.0), True, ref(b),
             report("MR-AP-01", f"MRI abdomen and pelvis, axial T1-weighted fat-suppressed contrast-enhanced appearance. The appendix is dilated to {diam:g} mm with an enhancing wall and surrounding inflammatory change. No collection.",
                    "Acute appendicitis.", {"appendix_mm": diam, "collection": "no"}, True), seed=400 + v, kind="finding painted into a real MRI slice")

    # ---- CT, gallbladder level
    for v, name in enumerate(("gb_a", "gb_b", "gb_c", "gb_d")):
        b = BASES[name]
        b["z"] = best_gb_slice(b["vid"])
        hu, lab, _ = load_ct(b["vid"], b["z"])
        crop = b["crop"] or auto_crop(hu)
        save(f"ct-gb-00-r{v}", "CT-GB-00", "CT", to_image(hu, crop, *CT_WIN), False, ref(b),
             report("CT-GB-00", "CT abdomen, axial, soft-tissue window, upper abdomen. The gallbladder is seen without wall thickening, pericholecystic stranding or gas.",
                    "No acute gallbladder abnormality.", {}, False))
        rng = np.random.default_rng(300 + v)
        wall = [6.0, 7.0, 6.0, 8.0][v]
        hp = gallbladder_ct(hu.copy(), lab, b, rng, wall)
        save(f"ct-gb-01-s{v}", "CT-GB-01", "CT", to_image(hp, crop, *CT_WIN), True, ref(b),
             report("CT-GB-01", f"CT abdomen, axial. The gallbladder wall is thickened to {wall:g} mm with gas locules within the wall and surrounding fat stranding.",
                    "Complicated (emphysematous) cholecystitis.", {"wall_mm": wall, "complication": "gas"}, True), seed=300 + v, kind="finding painted into a real CT slice")

    # ---- CT, mid abdomen: clean, small bowel obstruction, free air (perforation)
    for v, name in enumerate(("mid_a", "mid_b", "mid_c", "mid_d")):
        b = BASES[name]
        hu, lab, _ = load_ct(b["vid"], b["z"])
        crop = auto_crop(hu)
        bbox = body_bbox(hu)
        save(f"ct-ab-00-r{v}", "CT-AB-00", "CT", to_image(hu, crop, *CT_WIN), False, ref(b),
             report("CT-AB-00", "CT abdomen, axial, soft-tissue window, mid abdomen. No bowel dilatation, no free gas and no free fluid at this level.", "No acute abdominal abnormality at this level.",
                    {}, False))
        rng = np.random.default_rng(500 + v)
        loop = [32.0, 34.0, 30.0, 36.0][v]
        hp = bowel_obstruction(hu.copy(), bbox, rng, loop, b["pix"])
        save(f"ct-ab-01-s{v}", "CT-AB-01", "CT", to_image(hp, crop, *CT_WIN), True, ref(b),
             report("CT-AB-01", f"CT abdomen, axial. Multiple fluid-filled dilated small bowel loops, the widest {loop:g} mm. Appearances are of small bowel obstruction.",
                    "Small bowel obstruction.", {"bowel_mm": loop, "obstruction": "yes"}, True), seed=500 + v, kind="dilated loops painted into a real CT slice")
        rng = np.random.default_rng(600 + v)
        hp = free_air_ct(hu.copy(), bbox, rng, b["pix"])
        save(f"ct-ab-02-s{v}", "CT-AB-02", "CT", to_image(hp, crop, *CT_WIN), True, ref(b),
             report("CT-AB-02", "CT abdomen, axial. Free gas lies under the anterior abdominal wall with mesenteric fat stranding. Appearances are of a perforated hollow organ.",
                    "Free intraperitoneal air: perforation.", {"free_air": "yes"}, True), seed=600 + v, kind="free gas painted into a real CT slice")

    # ---- MRCP (drawn in code)
    for v in range(4):
        duct = round(3.8 + 0.4 * v, 1)
        save(f"mrcp-00-s{v}", "MRCP-00", "MRCP", mrcp(v, duct, 0.0), True, None,
             report("MRCP-00", f"MRCP, coronal thick-slab. The common bile duct measures {duct:g} mm. No filling defect. Intrahepatic ducts are not dilated.", "Normal biliary tree.",
                    {"duct_mm": duct, "stone": "no"}, True), seed=7000 + v, kind="drawn entirely in code")
        duct, stone = round(9.5 + 0.8 * v, 1), round(5.0 + 0.7 * v, 1)
        save(f"mrcp-01-s{v}", "MRCP-01", "MRCP", mrcp(10 + v, duct, stone), True, None,
             report("MRCP-01", f"MRCP, coronal thick-slab. The common bile duct is dilated to {duct:g} mm. A {stone:g} mm filling defect lies in the distal duct, with dilated duct above it.",
                    "Stone in the common bile duct.", {"duct_mm": duct, "stone_mm": stone, "stone": "yes"}, True), seed=7010 + v, kind="drawn entirely in code")

    approvals.apply(entries, reports)
    entries.sort(key=lambda e: e["id"])
    reports.sort(key=lambda r: r["id"])
    (OUT / "index.json").write_text(json.dumps({"schema": 1, "images": entries}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    (ROOT / "content" / "xsec_reports.json").write_text(json.dumps({"schema": 1, "reports": reports}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"{len(entries)} pictures ({sum(1 for e in entries if not e['synthetic'])} real, {sum(1 for e in entries if e['synthetic'])} synthetic), {len(reports)} reports")
    return 0


if __name__ == "__main__":
    sys.exit(build() if (sys.argv[1:] or [""])[0] == "build" else print(__doc__) or 1)
