"""Stylised synthetic ultrasound-style images for the finding codes that have no suitable real image (ADR-0003).

    uv run --project tools --extra images tools/data/ultrasound.py synthetic

These are TEACHING ILLUSTRATIONS drawn in code, not scans and not the output of a trained generative model: a fan-shaped field with speckle texture, a simple
anatomy sketch and the finding drawn at the size the report states (the measurement shown by calipers in the picture is the number in the report). Every image is
deterministic from its seed and carries the visible mark "SIMULATION: synthetic image, not a real scan". They are never used to train or test any diagnostic model.
Realism limits: this is much simpler than a real scan; a photo-realistic generator would be a separate, licence-checked decision (ADR-0003).
"""
from __future__ import annotations

import hashlib
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

W, H = 640, 480
PX_PER_MM = 3.4            # 14 cm of depth over 480 px
APEX = (W / 2, -60.0)
MARK = "SIMULATION: synthetic image, not a real scan"
MARK_HEIGHT = 30
FONTS = ["C:/Windows/Fonts/arialbd.ttf", "C:/Windows/Fonts/arial.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]


def gblur(a: np.ndarray, sx: float, sy: float) -> np.ndarray:
    fy = np.fft.fftfreq(H)[:, None]
    fx = np.fft.rfftfreq(W)[None, :]
    return np.fft.irfft2(np.fft.rfft2(a) * np.exp(-2 * np.pi ** 2 * ((sx * fx) ** 2 + (sy * fy) ** 2)), s=(H, W)).astype(np.float32)


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for f in FONTS:
        try:
            return ImageFont.truetype(f, size)
        except OSError:
            continue
    return ImageFont.load_default()


def seed_for(code: str, variant: int) -> int:
    return int(hashlib.sha256(f"{code}/{variant}".encode()).hexdigest()[:8], 16)


class Field:
    """A canvas of float intensities (0..1) with polar helpers around the probe apex."""

    def __init__(self, rng: np.random.RandomState):
        self.rng = rng
        y, x = np.mgrid[0:H, 0:W].astype(np.float32)
        self.x, self.y = x, y
        self.r = np.hypot(x - APEX[0], y - APEX[1])
        self.theta = np.arctan2(x - APEX[0], y - APEX[1])
        self.img = np.zeros((H, W), np.float32)
        self.gain = np.ones((H, W), np.float32)

    def smooth_noise(self, sx: float, sy: float) -> np.ndarray:
        """Unit-variance Gaussian noise smoothed with a Gaussian of (sx, sy) pixels (done in the frequency domain; wider in x like beam-formed speckle)."""
        n = self.rng.randn(H, W).astype(np.float32)
        fy = np.fft.fftfreq(H)[:, None]
        fx = np.fft.rfftfreq(W)[None, :]
        spectrum = np.fft.rfft2(n) * np.exp(-2 * np.pi ** 2 * ((sx * fx) ** 2 + (sy * fy) ** 2))
        out = np.fft.irfft2(spectrum, s=(H, W)).astype(np.float32)
        return out / out.std()

    def speckle(self, mean: float, contrast: float = 0.5) -> np.ndarray:
        a, b = self.smooth_noise(2.2, 1), self.smooth_noise(2.2, 1)
        s = np.sqrt(a * a + b * b)   # Rayleigh-like envelope
        return mean * (1 - contrast + contrast * s / s.mean())

    def region(self, mask: np.ndarray, mean: float, contrast: float = 0.5) -> None:
        self.img = np.where(mask, self.speckle(mean, contrast), self.img)

    def ellipse_mask(self, cx: float, cy: float, a: float, b: float, angle: float = 0.0) -> np.ndarray:
        ca, sa = math.cos(angle), math.sin(angle)
        dx, dy = self.x - cx, self.y - cy
        u, v = dx * ca + dy * sa, -dx * sa + dy * ca
        return (u / a) ** 2 + (v / b) ** 2


def soft(mask_value: np.ndarray, edge: float = 0.08) -> np.ndarray:
    """1 inside the unit ellipse, 0 outside, with a soft edge (mask_value is the squared normalised radius)."""
    return np.clip((1 + edge - mask_value) / (2 * edge), 0, 1)


def shadow_below(f: Field, cx: float, cy: float, half_width: float, strength: float = 0.85) -> None:
    """Acoustic shadow: everything deeper than (cx, cy) along the beam, within half_width pixels, is darkened."""
    ang = math.atan2(cx - APEX[0], cy - APEX[1])
    r0 = math.hypot(cx - APEX[0], cy - APEX[1])
    within = np.abs((f.theta - ang) * r0) < half_width
    deeper = f.r > r0
    f.gain = np.where(within & deeper, f.gain * (1 - strength), f.gain)


def enhance_below(f: Field, cx: float, cy: float, half_width: float, amount: float = 0.35) -> None:
    ang = math.atan2(cx - APEX[0], cy - APEX[1])
    r0 = math.hypot(cx - APEX[0], cy - APEX[1])
    within = np.abs((f.theta - ang) * r0) < half_width
    f.gain = np.where(within & (f.r > r0), f.gain * (1 + amount), f.gain)


def base_tissue(f: Field, liver_side: bool = True) -> None:
    """Skin and fat layers, muscle fascia lines and a liver-like parenchyma below them."""
    f.img = f.speckle(0.18, 0.55)
    for depth, strength, width in ((95, 0.9, 2.5), (150, 0.55, 2.0), (175, 0.6, 2.0), (205, 0.5, 2.0)):
        line = np.exp(-((f.r - depth - 10 * np.sin(f.theta * 4)) ** 2) / (2 * width ** 2))
        f.img = np.maximum(f.img, strength * line * (0.7 + 0.3 * f.smooth_noise(6, 1)))
    f.img = np.where(f.r < 85, f.img * 0.35, f.img)           # dark fat directly under the probe
    weight = np.clip((W * 0.80 - f.x - 0.45 * (f.y - 250)) / 120.0, 0, 1) if liver_side else np.ones((H, W), np.float32)
    weight = weight * np.clip((f.r - 215) / 30.0, 0, 1)                       # parenchyma starts below the muscle layers, with a soft edge
    f.img = f.img * (1 - weight) + f.speckle(0.34, 0.5) * weight


def finish(f: Field) -> np.ndarray:
    depth_gain = np.exp(-f.r / 420.0) * 1.35
    out = np.clip(f.img * gblur(f.gain, 5, 2) * depth_gain, 0, 1)             # shadows and enhancement have soft edges
    inside = (np.abs(f.theta) < 0.62) & (f.r > 55) & (f.r < 585)
    out = np.where(inside, out, 0.0)
    out = np.asarray(Image.fromarray((out * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius=0.8))).astype(np.float32) / 255
    out = np.clip((out - 0.02) * 1.25, 0, 1) ** 0.85
    return (out * 255).astype(np.uint8)


def caliper(draw: ImageDraw.ImageDraw, p1: tuple[float, float], p2: tuple[float, float], label: str, dy: int = -16) -> None:
    for p in (p1, p2):
        draw.line([(p[0] - 5, p[1]), (p[0] + 5, p[1])], fill=255, width=1)
        draw.line([(p[0], p[1] - 5), (p[0], p[1] + 5)], fill=255, width=1)
    n = 14
    for i in range(0, n, 2):
        a = (p1[0] + (p2[0] - p1[0]) * i / n, p1[1] + (p2[1] - p1[1]) * i / n)
        b = (p1[0] + (p2[0] - p1[0]) * (i + 1) / n, p1[1] + (p2[1] - p1[1]) * (i + 1) / n)
        draw.line([a, b], fill=255, width=1)
    mx, my = (p1[0] + p2[0]) / 2, (p1[1] + p2[1]) / 2
    draw.text((mx + 8, my + dy), label, fill=255, font=font(15))


def with_mark(gray: np.ndarray, extra: list[tuple] | None = None) -> Image.Image:
    """Adds the visible mark in a strip along the bottom (and a small tag at the top left), after the drawing is done."""
    img = Image.fromarray(gray).convert("L")
    draw = ImageDraw.Draw(img)
    draw.rectangle([0, H - MARK_HEIGHT, W, H], fill=0)
    f = font(17)
    box = draw.textbbox((0, 0), MARK, font=f)
    draw.text(((W - (box[2] - box[0])) / 2, H - MARK_HEIGHT + 5), MARK, fill=255, font=f)
    draw.text((10, 8), "SYNTHETIC", fill=235, font=font(14))
    return img


def mark_strip(img: Image.Image) -> bytes:
    """The pixels of the mark strip, used to prove later that the mark is still in the file."""
    return img.convert("L").crop((0, H - MARK_HEIGHT, W, H)).tobytes()


# ---------------------------------------------------------------------------- the findings

def gallbladder(f: Field, rng, cx=330.0, cy=330.0, a=125.0, b=62.0, angle=-0.2, wall_mm=2.0, stones=0, sludge=False, polyp_mm=0.0, thick_wall=False, wall_gas=False):
    base_tissue(f)
    d = f.ellipse_mask(cx, cy, a, b, angle)
    inner = f.ellipse_mask(cx, cy, a - wall_mm * PX_PER_MM, b - wall_mm * PX_PER_MM, angle)
    wall = soft(d) * (1 - soft(inner))
    lumen = soft(inner)
    f.img = f.img * (1 - lumen) + lumen * (0.015 + 0.01 * f.smooth_noise(3, 1))
    f.img = np.maximum(f.img, wall * (0.95 if not thick_wall else 0.7) * (0.8 + 0.2 * f.smooth_noise(4, 1)))
    enhance_below(f, cx, cy + b * 0.6, a * 0.8, 0.3)
    info = {"wall_mm": wall_mm}
    if sludge:   # layering: dependent echoes with a flat fluid level
        level = cy + b * 0.15
        layer = lumen * (f.y > level) * (0.28 + 0.1 * f.smooth_noise(3, 1))
        f.img = np.maximum(f.img, layer)
    for i in range(stones):
        sx = cx - a * 0.35 + i * 38 + rng.randint(-8, 8)
        sy = cy + b * 0.55 - rng.randint(0, 8)
        sm = f.ellipse_mask(sx, sy, 17 + rng.randint(0, 6), 9 + rng.randint(0, 3), 0)
        f.img = np.maximum(f.img, soft(sm, 0.15) * 0.95 * (1 - soft(f.ellipse_mask(sx, sy + 3, 14, 6, 0), 0.2) * 0.4))
        shadow_below(f, sx, sy + 8, 15, 0.92)
    if polyp_mm:
        pr = polyp_mm / 2 * PX_PER_MM
        px_, py_ = cx - a * 0.1, cy - b * 0.55
        pm = f.ellipse_mask(px_, py_, pr, pr * 0.9, 0)
        f.img = np.maximum(f.img, soft(pm, 0.15) * 0.55 * (0.8 + 0.2 * f.smooth_noise(2, 1)))
        info["polyp_px"] = (px_, py_, pr)
    if wall_gas:   # bright dots with dirty shadowing in the wall
        for k in range(5):
            gx, gy = cx - a * 0.6 + k * 40, cy - b * 0.9 + rng.randint(-3, 4)
            f.img = np.maximum(f.img, soft(f.ellipse_mask(gx, gy, 5, 4, 0), 0.2) * 1.0)
            shadow_below(f, gx, gy + 5, 8, 0.6)
    info.update(center=(cx, cy), axes=(a, b), angle=angle)
    return info


def duct(f: Field, rng, duct_mm: float, y=215.0, x0=170.0, x1=470.0, stone: bool = False):
    """A bile duct drawn as a gently curved dark tube with thin walls, tapering at both ends, near the portal vein (a circle below it)."""
    half = duct_mm * PX_PER_MM / 2
    span = x1 - x0
    inside = ((f.x > x0) & (f.x < x1)).astype(np.float32)
    taper = (0.55 + 0.45 * np.clip(np.sin(np.pi * np.clip((f.x - x0) / span, 0, 1)), 0, None) ** 0.5) * inside
    cl = y - 0.08 * (f.x - x0) + 9 * np.sin((f.x - x0) / 55.0)
    dist = np.abs(f.y - cl)
    lumen = np.clip((half * taper - dist) / 2.5, 0, 1) * inside
    wall_edge = np.exp(-((dist - half * taper) ** 2) / 4.0) * inside
    f.img = f.img * (1 - 0.96 * lumen) + 0.8 * wall_edge * (1 - lumen)
    pv = f.ellipse_mask(300, y + 48, 20, 17, 0)
    f.img = f.img * (1 - 0.95 * soft(pv))
    yc = float(y - 0.08 * (330 - x0) + 9 * math.sin((330 - x0) / 55.0))
    out = {"duct_mm": duct_mm, "p1": (330.0, yc - half), "p2": (330.0, yc + half)}
    if stone:
        sx = 400.0
        sy = float(y - 0.08 * (sx - x0) + 9 * math.sin((sx - x0) / 55.0))
        f.img = np.maximum(f.img, soft(f.ellipse_mask(sx, sy + half * 0.3, 11, 9, 0), 0.15) * 0.98)
        shadow_below(f, sx, sy + half, 11, 0.9)
    return out


def appendix(f: Field, rng, diameter_mm: float, inflamed: bool, appendicolith: bool = False, noncompressible: bool = True):
    base_tissue(f, liver_side=False)
    cx, cy = 340.0, 300.0
    R = diameter_mm / 2 * PX_PER_MM
    if inflamed:
        halo = f.ellipse_mask(cx, cy, R * 2.4, R * 2.1, 0)
        f.img = np.where(halo < 1, f.speckle(0.62, 0.4), f.img)       # inflamed (echogenic) fat around it
    outer = f.ellipse_mask(cx, cy, R, R, 0)
    inner = f.ellipse_mask(cx, cy, R * 0.55, R * 0.55, 0)
    ring = soft(outer) * (1 - soft(inner))
    f.img = f.img * (1 - soft(outer)) + ring * 0.12 + soft(inner) * 0.85    # dark swollen wall, bright mucosa inside (target sign)
    if appendicolith:
        f.img = np.maximum(f.img, soft(f.ellipse_mask(cx, cy, R * 0.25, R * 0.25, 0), 0.2) * 1.0)
        shadow_below(f, cx, cy + R * 0.3, R * 0.3, 0.9)
    return {"appendix_mm": diameter_mm, "p1": (cx - R, cy), "p2": (cx + R, cy), "compressible": not noncompressible}


def abscess(f: Field, rng, mm=26.0):
    cx, cy = 480.0, 335.0
    a, b = mm / 2 * PX_PER_MM, mm / 2 * PX_PER_MM * 0.75
    m = f.ellipse_mask(cx, cy, a, b, 0.3)
    f.img = f.img * (1 - soft(m)) + soft(m) * (0.05 + 0.18 * np.clip(f.smooth_noise(5, 3), 0, 2) / 2)
    f.img = np.maximum(f.img, 0.8 * np.exp(-((m - 1) ** 2) / 0.02))
    for k in range(4):   # debris and gas
        gx, gy = cx - a * 0.4 + k * 24, cy - b * 0.4 + rng.randint(-4, 5)
        f.img = np.maximum(f.img, soft(f.ellipse_mask(gx, gy, 4, 3, 0), 0.2) * 0.95)
    enhance_below(f, cx, cy + b, a * 0.8, 0.25)
    return {"abscess_mm": mm}


def ovarian_cyst(f: Field, rng, mm=32.0):
    cx, cy = 190.0, 345.0
    a = mm / 2 * PX_PER_MM
    m = f.ellipse_mask(cx, cy, a, a * 0.85, 0.1)
    f.img = f.img * (1 - soft(m)) + soft(m) * 0.012
    f.img = np.maximum(f.img, 0.45 * np.exp(-((m - 1) ** 2) / 0.015))
    enhance_below(f, cx, cy + a * 0.8, a, 0.4)
    return {"cyst_mm": mm}


def bowel_gas(f: Field, rng):
    for k in range(5):
        gx = 120 + k * 95 + rng.randint(-20, 20)
        gy = 250 + rng.randint(-25, 25)
        f.img = np.maximum(f.img, soft(f.ellipse_mask(gx, gy, 45, 6, rng.uniform(-0.3, 0.3)), 0.2) * 0.95)
        shadow_below(f, gx, gy + 6, 40, 0.55)



def bladder(f: Field, cx=320.0, cy=230.0, a=100.0, b=48.0):
    """Urine-filled bladder: a dark rounded space with a thin bright wall and bright enhancement behind it."""
    m = f.ellipse_mask(cx, cy, a, b, 0)
    f.img = f.img * (1 - soft(m)) + soft(m) * 0.012
    f.img = np.maximum(f.img, 0.55 * np.exp(-((m - 1) ** 2) / 0.02))
    enhance_below(f, cx, cy + b, a * 0.9, 0.35)


def uterus(f: Field, rng, cx=320.0, cy=345.0, a=88.0, b=58.0, sac: bool = False):
    m = f.ellipse_mask(cx, cy, a, b, 0)
    f.img = f.img * (1 - soft(m)) + soft(m) * f.speckle(0.42, 0.45)
    line = np.exp(-((f.y - cy - 4 * np.sin((f.x - cx) / 30.0)) ** 2) / 3.0) * (np.abs(f.x - cx) < a * 0.62)    # endometrial echo
    f.img = np.maximum(f.img, 0.85 * line * soft(m))
    if sac:
        s = f.ellipse_mask(cx + 4, cy, 17, 12, 0)
        f.img = f.img * (1 - 0.95 * soft(s))
        f.img = np.maximum(f.img, 0.6 * np.exp(-((s - 1) ** 2) / 0.03))
    return {"uterus_mm": round(2 * a / PX_PER_MM, 0)}


def ovary(f: Field, rng, cx=150.0, cy=385.0, a=38.0, b=24.0, follicles=3, enlarged=False):
    m = f.ellipse_mask(cx, cy, a, b, 0.15)
    f.img = f.img * (1 - soft(m)) + soft(m) * f.speckle(0.5 if enlarged else 0.36, 0.5)
    for k in range(follicles):
        ang = 6.28 * k / max(1, follicles) + rng.uniform(0, 0.6)
        fx, fy = cx + np.cos(ang) * a * (0.62 if enlarged else 0.35), cy + np.sin(ang) * b * (0.62 if enlarged else 0.4)
        fm = f.ellipse_mask(fx, fy, 6 + rng.randint(0, 3), 5 + rng.randint(0, 3), 0)
        f.img = f.img * (1 - 0.95 * soft(fm))
    return {"ovary_mm": round(2 * a / PX_PER_MM, 0)}


def free_fluid(f: Field, rng, cx=320.0, cy=455.0, a=170.0, b=22.0):
    m = f.ellipse_mask(cx, cy, a, b, 0)
    f.img = f.img * (1 - 0.97 * soft(m))


def tubal_ring(f: Field, rng, cx=495.0, cy=385.0, a=34.0):
    """An adnexal mass with a bright, thick ring and a dark centre (the 'tubal ring' of an ectopic pregnancy)."""
    m = f.ellipse_mask(cx, cy, a, a * 0.8, 0.2)
    inner = f.ellipse_mask(cx, cy, a * 0.5, a * 0.4, 0.2)
    f.img = f.img * (1 - soft(m)) + soft(m) * f.speckle(0.55, 0.4)
    f.img = f.img * (1 - 0.9 * soft(inner))
    f.img = np.maximum(f.img, 0.7 * np.exp(-((m - 0.55) ** 2) / 0.05) * soft(m))
    return {"mass_mm": round(2 * a / PX_PER_MM, 0)}


def kidney(f: Field, rng, hydronephrosis: bool = False, stone: bool = False, pelvis_mm: float = 4.0):
    """Long-axis kidney: bean-shaped cortex, darker medullary pyramids, a bright central sinus; with hydronephrosis the sinus holds dark dilated calyces and pelvis."""
    base_tissue(f, liver_side=False)
    cx, cy, a, b = 320.0, 340.0, 150.0, 70.0
    m = f.ellipse_mask(cx, cy, a, b, -0.12)
    f.img = f.img * (1 - soft(m)) + soft(m) * f.speckle(0.33, 0.45)
    f.img = np.maximum(f.img, 0.8 * np.exp(-((m - 1) ** 2) / 0.012))     # bright capsule
    for k in range(7):                                                  # medullary pyramids
        ang = -0.12 + 3.14 * (k + 0.5) / 7 + 3.14
        px_, py_ = cx + np.cos(ang) * a * 0.55, cy + np.sin(ang) * b * 0.55
        pm = f.ellipse_mask(px_, py_, 14, 9, ang)
        f.img = f.img * (1 - 0.55 * soft(pm))
    sinus = f.ellipse_mask(cx, cy, a * 0.55, b * 0.32, -0.12)
    if hydronephrosis:
        dil = pelvis_mm * PX_PER_MM / 2
        f.img = f.img * (1 - soft(sinus)) + soft(sinus) * 0.3
        pel = f.ellipse_mask(cx + 5, cy, a * 0.45, max(dil, 8.0), -0.12)
        f.img = f.img * (1 - 0.97 * soft(pel))
        for k in range(4):
            cmx = cx - 60 + k * 38
            f.img = f.img * (1 - 0.95 * soft(f.ellipse_mask(cmx, cy - 18 + 9 * (k % 2) * 2 - 9, 12, 9 + dil * 0.3, 0)))
        enhance_below(f, cx, cy + b, a * 0.8, 0.3)
    else:
        f.img = f.img * (1 - soft(sinus)) + soft(sinus) * (0.78 * (0.8 + 0.2 * f.smooth_noise(2, 1)))
    if stone:
        sx, sy = cx + a * 0.55, cy + 2
        f.img = np.maximum(f.img, soft(f.ellipse_mask(sx, sy, 8, 6, 0), 0.15) * 1.0)
        shadow_below(f, sx, sy + 6, 9, 0.9)
    return {"kidney_mm": round(2 * a / PX_PER_MM, 0), "pelvis_mm": pelvis_mm}


# --------------------------------------------------------------------------- one image per (code, variant)

def render(code: str, variant: int, fixed: dict | None = None) -> tuple[Image.Image, dict]:
    global PX_PER_MM
    PX_PER_MM = 6.5 if code.startswith("US-AP") else (3.0 if code.startswith("US-PV") else 3.4)      # appendix scans are done at a shallower depth, so structures look larger
    fixed = fixed or {}
    seed = seed_for(code, variant)
    rng = np.random.RandomState(seed)
    f = Field(rng)
    r = np.random.RandomState(seed + 1)
    meas: dict = {}
    marks: list[tuple] = []
    if code == "US-GB-08":      # equivocal: a little wall thickening, no clear stone, no fluid: 'possible early cholecystitis'
        wall = round(float(r.uniform(3.3, 3.9)), 1)
        info = gallbladder(f, rng, wall_mm=wall, stones=0, sludge=bool(variant % 2))
        d = duct(f, rng, round(float(r.uniform(3.5, 5.5)), 1))
        meas = {"wall_mm": wall, "stones": "no", "fluid": "no", "duct_mm": d["duct_mm"], "equivocal": "yes"}
        marks.append((d["p1"], d["p2"], f"{d['duct_mm']:.1f} mm"))
    elif code == "US-PV-00":    # clean pelvic scan
        base_tissue(f, liver_side=False)
        bladder(f)
        u = uterus(f, rng)
        o = ovary(f, rng, follicles=3 + variant % 3)
        meas = {"uterus": "normal", "uterus_mm": u["uterus_mm"], "fluid": "no", "adnexal_mass": "no"}
    elif code == "US-PV-01":    # ectopic pregnancy: empty uterus, adnexal ring, free fluid
        base_tissue(f, liver_side=False)
        bladder(f)
        u = uterus(f, rng, sac=False)
        ovary(f, rng, follicles=2)
        free_fluid(f, rng)
        t = tubal_ring(f, rng)
        meas = {"uterus": "empty", "sac": "no", "adnexal_mass": "yes", "mass_mm": t["mass_mm"], "fluid": "yes"}
        marks.append(((495.0 - 34 * 0.9, 385.0), (495.0 + 34 * 0.9, 385.0), f"{t['mass_mm']:.0f} mm"))
    elif code == "US-PV-02":    # ovarian torsion: one large oedematous ovary with peripheral follicles
        base_tissue(f, liver_side=False)
        bladder(f)
        uterus(f, rng)
        a = 95 + 6 * (variant % 3)
        o = ovary(f, rng, cx=500.0, cy=380.0, a=a, b=48.0, follicles=7, enlarged=True)
        meas = {"ovary_mm": o["ovary_mm"], "fluid": "no", "doppler_flow": "reduced"}
        marks.append(((500.0 - a, 380.0), (500.0 + a, 380.0), f"{o['ovary_mm']:.0f} mm"))
    elif code == "US-RN-00":    # clean kidney
        k = kidney(f, rng)
        meas = {"hydronephrosis": "no", "stone": "no", "kidney_mm": k["kidney_mm"]}
    elif code == "US-RN-01":    # hydronephrosis with a stone
        pel = round(float(r.uniform(11.0, 18.0)), 0)
        k = kidney(f, rng, hydronephrosis=True, stone=True, pelvis_mm=pel)
        meas = {"hydronephrosis": "yes", "stone": "yes", "pelvis_mm": pel, "kidney_mm": k["kidney_mm"]}
        marks.append(((330.0, 340.0 - pel * PX_PER_MM / 2), (330.0, 340.0 + pel * PX_PER_MM / 2), f"{pel:.0f} mm"))
    elif code == "US-GB-00":      # clean: normal gallbladder, thin wall, no stones, normal duct
        wall = round(float(r.uniform(1.5, 2.8)), 1)
        info = gallbladder(f, rng, wall_mm=wall, stones=0)
        d = duct(f, rng, round(float(r.uniform(3.0, 5.0)), 1))
        meas = {"stones": "no", "wall_mm": wall, "duct_mm": d["duct_mm"], "fluid": "no", "sludge": "no"}
        marks.append((d["p1"], d["p2"], f"{d['duct_mm']:.1f} mm"))
    elif code == "US-GB-03":      # bile duct wider than 6 mm
        duct_mm = fixed.get("duct_mm", round(float(r.uniform(7.0, 10.0)), 1))
        info = gallbladder(f, rng, wall_mm=round(float(r.uniform(1.5, 2.8)), 1), stones=1)
        d = duct(f, rng, duct_mm)
        meas = {"duct_mm": duct_mm, "wall_mm": info["wall_mm"], "stones": "yes"}
        marks.append((d["p1"], d["p2"], f"{duct_mm:.1f} mm"))
    elif code == "US-GB-04":    # stone seen in the bile duct
        duct_mm = round(float(r.uniform(7.0, 9.5)), 1)
        info = gallbladder(f, rng, wall_mm=2.0, stones=1)
        d = duct(f, rng, duct_mm, stone=True)
        meas = {"duct_stone": "yes", "duct_mm": duct_mm, "stones": "yes"}
        marks.append((d["p1"], d["p2"], f"{duct_mm:.1f} mm"))
    elif code == "US-GB-07":    # no stones, only sludge
        info = gallbladder(f, rng, wall_mm=2.0, stones=0, sludge=True)
        meas = {"stones": "no", "sludge": "yes", "wall_mm": 2.0}
    elif code == "US-GB-01":    # simple gallstones, thin wall, normal duct
        wall = round(float(r.uniform(1.5, 3.0)), 1)
        info = gallbladder(f, rng, wall_mm=wall, stones=2 + variant % 2)
        d = duct(f, rng, round(float(r.uniform(3.0, 5.5)), 1))
        meas = {"stones": "yes", "wall_mm": wall, "duct_mm": d["duct_mm"], "fluid": "no"}
        marks.append((d["p1"], d["p2"], f"{d['duct_mm']:.1f} mm"))
    elif code == "US-AP-01":    # positive, simple
        mm = fixed.get("appendix_mm", round(float(r.uniform(7.0, 10.0)), 1))
        info = appendix(f, rng, mm, inflamed=True, appendicolith=fixed.get("appendicolith", bool(variant % 2)))
        meas = {"appendix_mm": mm, "compressible": "no", "perforation": "no"}
        meas["appendicolith"] = "yes" if fixed.get("appendicolith", bool(variant % 2)) else "no"
        marks.append((info["p1"], info["p2"], f"{mm:.1f} mm"))
    elif code == "US-AP-02":    # positive, complicated
        mm = round(float(r.uniform(9.0, 12.0)), 1)
        info = appendix(f, rng, mm, inflamed=True, appendicolith=True)
        ab = abscess(f, rng, mm=round(float(r.uniform(18, 24)), 0))
        meas = {"appendix_mm": mm, "abscess_mm": ab["abscess_mm"], "complication": "abscess"}
        marks.append((info["p1"], info["p2"], f"{mm:.1f} mm"))
    elif code == "US-AP-03":    # normal appendix, something else found
        mm = round(float(r.uniform(4.0, 5.5)), 1)
        info = appendix(f, rng, mm, inflamed=False, noncompressible=False)
        cyst = ovarian_cyst(f, rng, mm=round(float(r.uniform(22, 30)), 0))
        meas = {"appendix": "normal", "appendix_mm": mm, "other_finding": "yes", "cyst_mm": cyst["cyst_mm"]}
    elif code == "US-AP-04":    # appendix not seen
        base_tissue(f, liver_side=False)
        bowel_gas(f, rng)
        meas = {"appendix": "not_seen"}
    elif code == "US-AP-05":    # normal appendix, nothing else
        mm = round(float(r.uniform(4.0, 5.5)), 1)
        info = appendix(f, rng, mm, inflamed=False, noncompressible=False)
        meas = {"appendix": "normal", "appendix_mm": mm, "other_finding": "no"}
        marks.append((info["p1"], info["p2"], f"{mm:.1f} mm"))
    else:
        raise ValueError(f"no synthetic generator for {code}")
    gray = finish(f)
    img = Image.fromarray(gray).convert("L")
    draw = ImageDraw.Draw(img)
    for p1, p2, label in marks:
        caliper(draw, p1, p2, label)
    out = with_mark(np.asarray(img))
    return out, {"seed": seed, "measurements": meas, "variant": variant}
