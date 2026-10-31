"""Textures for the hero patient monitor (PIL + numpy; seeded, tileable where it matters).

Writes to unity/Logs/MonitorBuild:
  monitor_labels.png      RGBA decal atlas 2048x1024: key legends, status icons, wordmark, connector captions, rear compliance label
  monitor_ui.png          1044x652 clinical screen backdrop (status bar, three idle traces, parameter panels); live values are drawn by ProcSense on top
  plastic_orange_peel_n.png   tileable fine-grain normal for moulded plastic (256)
  plastic_rough.png           tileable roughness variation (256), mid-grey around 0.45
  glass_smudge.png            tileable faint smudge/dust mask for the front glass (512)
The legend atlas rectangles are exported as monitor_labels.json (u0, v0, u1, v1 in 0..1, v up) for the Blender decal UVs.
"""
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

OUT = Path(__file__).resolve().parents[2] / "Logs" / "MonitorBuild"
OUT.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(20261029)


def font(size, bold=False):
    for name in (("arialbd.ttf" if bold else "arial.ttf"), "segoeuib.ttf" if bold else "segoeui.ttf", "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def periodic(n, cells):
    g = rng.random((cells, cells))
    t = np.linspace(0, cells, n, endpoint=False); i0 = np.floor(t).astype(int); f = t - i0; f = f * f * (3 - 2 * f); i1 = (i0 + 1) % cells
    a = g[i0][:, i0] * (1 - f)[:, None] * (1 - f)[None, :] + g[i0][:, i1] * (1 - f)[:, None] * f[None, :]
    b = g[i1][:, i0] * f[:, None] * (1 - f)[None, :] + g[i1][:, i1] * f[:, None] * f[None, :]
    return a + b


def normal_from(h, k):
    gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * .5; gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * .5
    n = np.dstack([-gx * k, gy * k, np.ones_like(h)]); n /= np.linalg.norm(n, axis=2, keepdims=True)
    return n * .5 + .5


def u8(a):
    return (np.clip(a, 0, 1) * 255 + .5).astype(np.uint8)


# ------------------------------------------------------------------ moulded-plastic micro surface
def plastic():
    n = 256
    h = periodic(n, 64) * .5 + periodic(n, 32) * .3 + rng.random((n, n)) * .2             # fine orange-peel grain
    Image.fromarray(u8(normal_from(h, 2.4))).save(OUT / "plastic_orange_peel_n.png")
    r = .45 + (periodic(n, 6) - .5) * .12 + (periodic(n, 48) - .5) * .06
    Image.fromarray(u8(np.dstack([r, r, r]))).save(OUT / "plastic_rough.png")
    s = np.clip((periodic(512, 5) * .6 + periodic(512, 21) * .4 - .55) * 3.0, 0, 1)        # sparse fingerprint-ish smudges
    Image.fromarray(u8(np.dstack([s, s, s]))).save(OUT / "glass_smudge.png")
    # engine glass: metallic-gloss map (R = 0 metallic, A = smoothness .985 clean .. .90 smudged) and a very low-amplitude waviness normal
    sm = .985 - s * .085 - (periodic(512, 3) - .5) * .02
    ar = np.full_like(s, 5 / 255.0)                                               # URP uses the map's RGB directly as specular reflectance: 5/255 = 2% (anti-reflective coated glass; bare glass is 4%)
    g = np.dstack([ar, ar, ar, sm])
    img = Image.fromarray(u8(g), "RGBA"); img.save(OUT / "glass_gloss.png")
    Image.fromarray(u8(normal_from((periodic(512, 4) * .7 + periodic(512, 11) * .3), .35))).save(OUT / "glass_normal.png")


# ------------------------------------------------------------------ decal atlas
ICON = (232, 234, 236, 255)
RED = (214, 56, 48, 255)
AMBER = (240, 168, 30, 255)
GREEN = (60, 190, 100, 255)


def icon_power(d, cx, cy, r, col=ICON):
    d.arc((cx - r, cy - r, cx + r, cy + r), -60, 240, fill=col, width=max(2, r // 5)); d.line((cx, cy - r - 2, cx, cy - r * .1), fill=col, width=max(2, r // 5))


def icon_bell(d, cx, cy, r, col=ICON, slash=False):
    d.pieslice((cx - r, cy - r, cx + r, cy + r * .6), 180, 360, fill=col); d.rectangle((cx - r, cy - 1, cx + r, cy + r * .6), fill=col)
    d.ellipse((cx - r * .22, cy + r * .6, cx + r * .22, cy + r * .95), fill=col)
    if slash: d.line((cx - r * 1.1, cy + r, cx + r * 1.1, cy - r), fill=RED, width=max(3, r // 4))


def icon_menu(d, cx, cy, r, col=ICON):
    for k in (-1, 0, 1): d.rounded_rectangle((cx - r, cy + k * r * .6 - 3, cx + r, cy + k * r * .6 + 3), radius=3, fill=col)


def icon_home(d, cx, cy, r, col=ICON):
    d.polygon([(cx - r, cy), (cx, cy - r), (cx + r, cy)], fill=col); d.rectangle((cx - r * .7, cy, cx + r * .7, cy + r * .8), fill=col)


def icon_cuff(d, cx, cy, r, col=ICON):
    d.rounded_rectangle((cx - r, cy - r * .55, cx + r, cy + r * .55), radius=6, outline=col, width=max(3, r // 5)); d.line((cx - r * .3, cy, cx + r * .3, cy), fill=col, width=4)


def atlas():
    W, H = 2048, 1024
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(img); rects = {}

    def put(name, x, y, w, h, draw):
        sub = Image.new("RGBA", (w, h), (0, 0, 0, 0)); sd = ImageDraw.Draw(sub); draw(sd, w, h); img.alpha_composite(sub, (x, y))
        rects[name] = [x / W, 1 - (y + h) / H, (x + w) / W, 1 - y / H]
    BG = (46, 52, 58, 255)
    def keyed(icon, circle=False, **kw):
        def f(s, w, h):
            if circle: s.ellipse((0, 0, w - 1, h - 1), fill=BG)
            else: s.rounded_rectangle((0, 0, w - 1, h - 1), radius=44, fill=BG)
            icon(s, w // 2, h // 2 + (4 if circle else 0), **kw)
        return f
    put("power", 20, 20, 168, 168, keyed(icon_power, True, r=46))
    put("silence", 200, 20, 252, 168, keyed(icon_bell, r=40, slash=True))
    put("menu", 470, 20, 252, 168, keyed(icon_menu, r=44))
    put("home", 740, 20, 252, 168, keyed(icon_home, r=40))
    # connector captions (side bay)
    def cap(text, col=ICON):
        def f(s, w, h):
            s.text((w // 2, h // 2), text, font=font(54, True), fill=col, anchor="mm")
        return f
    for i, (nm, tx) in enumerate((("cap_ecg", "ECG"), ("cap_spo2", "SpO2"), ("cap_nibp", "NIBP"), ("cap_temp", "TEMP"))):
        put(nm, 20 + i * 330, 220, 310, 110, cap(tx))
    # wordmark
    def word(s, w, h):
        s.text((w // 2, h // 2), "PROCSENSE", font=font(78, True), fill=(176, 182, 188, 255), anchor="mm")
    put("wordmark", 20, 360, 640, 130, word)
    def status(s, w, h):
        s.ellipse((8, 8, w - 8, h - 8), fill=GREEN)
    put("led_green", 700, 360, 90, 90, status)
    put("led_amber", 800, 360, 90, 90, lambda s, w, h: s.ellipse((8, 8, w - 8, h - 8), fill=AMBER))
    # rear compliance label (generic, no real approvals)
    def label(s, w, h):
        s.rounded_rectangle((0, 0, w - 1, h - 1), radius=10, fill=(226, 228, 228, 255), outline=(90, 94, 98, 255), width=3)
        f1, f2 = font(44, True), font(30)
        s.text((24, 18), "PROCSENSE PS-M12", font=f1, fill=(20, 22, 24, 255))
        s.text((24, 72), "Bedside patient monitor (training simulator)", font=f2, fill=(30, 32, 34, 255))
        s.text((24, 116), "100-240 V~  50/60 Hz  60 VA", font=f2, fill=(30, 32, 34, 255))
        s.text((24, 160), "SN: SIM-000123   MFD: 2026-10", font=f2, fill=(30, 32, 34, 255))
        for k in range(46):                                                                           # a dummy bar pattern, not a real code
            x = 24 + k * 11; s.rectangle((x, 214, x + (3 if k % 3 else 6), 262), fill=(20, 22, 24, 255))
        s.text((w - 28, h - 26), "TYPE BF", font=f2, fill=(30, 32, 34, 255), anchor="rb")
    put("rear_label", 20, 540, 760, 300, label)
    # soft-touch key tops (printed colour blocks the legends sit on)
    put("key_blue", 820, 540, 200, 120, lambda s, w, h: s.rounded_rectangle((0, 0, w - 1, h - 1), radius=18, fill=(34, 86, 150, 255)))
    put("key_start", 1040, 540, 600, 200, lambda s, w, h: (s.rounded_rectangle((0, 0, w - 1, h - 1), radius=28, fill=(30, 36, 44, 255)), icon_cuff(s, 110, h // 2, 48), s.text((190, h // 2), "NIBP START", font=font(60, True), fill=ICON, anchor="lm")))
    img.save(OUT / "monitor_labels.png"); (OUT / "monitor_labels.json").write_text(json.dumps(rects, indent=1))


# ------------------------------------------------------------------ clinical screen backdrop
def screen():
    W, H = 1044, 652
    img = Image.new("RGB", (W, H), (4, 8, 10)); d = ImageDraw.Draw(img)
    d.rectangle((0, 0, W, 54), fill=(16, 26, 32)); d.text((18, 27), "BED 01", font=font(30, True), fill=(220, 230, 235), anchor="lm")
    d.text((W // 2, 27), "ADULT  |  SIMULATED PATIENT", font=font(24), fill=(150, 170, 178), anchor="mm"); d.text((W - 18, 27), "12:00", font=font(30, True), fill=(220, 230, 235), anchor="rm")
    d.ellipse((W - 150, 18, W - 130, 38), outline=(120, 140, 148), width=3)
    # waveform lanes (idle baselines until sensors are connected)
    lanes = [("II", (60, 235, 120), 70, 215), ("PLETH", (70, 205, 245), 235, 350), ("RESP", (245, 215, 70), 370, 470)]
    for name, col, y0, y1 in lanes:
        d.line((14, (y0 + y1) // 2, 700, (y0 + y1) // 2), fill=tuple(int(c * .45) for c in col), width=3)
        d.text((18, y0 + 4), name, font=font(24, True), fill=col)
        for gx in range(60, 700, 80): d.line((gx, y0 + 30, gx, y1 - 8), fill=(18, 30, 34), width=1)
        d.line((700, y0 - 6, 700, y1 + 6), fill=(26, 40, 46), width=2)
    # parameter panels (live ProcSense text is placed over these)
    panels = [("SpO2", "%", (70, 205, 245)), ("PULSE", "/min", (60, 235, 120)), ("NIBP", "mmHg", (235, 235, 240)), ("TEMP", "C", (245, 215, 70))]
    for i, (nm, unit, col) in enumerate(panels):
        y = 64 + i * 128
        d.rounded_rectangle((716, y, W - 12, y + 118), radius=10, fill=(10, 18, 22), outline=tuple(int(c * .5) for c in col), width=3)
        d.text((W - 26, y + 10), unit, font=font(22), fill=tuple(int(c * .7) for c in col), anchor="ra")
    # soft-key strip
    d.rectangle((0, H - 96, 700, H), fill=(12, 20, 24))
    for k, t in enumerate(("Silence", "Alarms", "NIBP", "Review", "Setup", "Main", "Menu")):
        x = 8 + k * 98; d.rounded_rectangle((x, H - 86, x + 90, H - 12), radius=8, fill=(24, 36, 42), outline=(52, 70, 78), width=2); d.text((x + 45, H - 49), t, font=font(18), fill=(190, 205, 210), anchor="mm")
    img = img.filter(ImageFilter.GaussianBlur(.5)); img.save(OUT / "monitor_ui.png")


if __name__ == "__main__":
    plastic(); atlas(); screen()
    print("wrote", sorted(p.name for p in OUT.iterdir()))
