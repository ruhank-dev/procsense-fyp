"""Textures for the staff props: the clipboard chart sheets and the name badges. Pure Pillow, readable text, fictional names and a fictional hospital (ProcSense simulation).

  python unity/Tools/MPFB/make_staff_textures.py          -> build/mpfb/output/staff/props/*.png
"""
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[3] / "build/mpfb/output/staff/props"
OUT.mkdir(parents=True, exist_ok=True)


def font(size, bold=False):
    for name in (("arialbd.ttf" if bold else "arial.ttf"), "segoeuib.ttf" if bold else "segoeui.ttf", "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def chart(name, title, seed):
    """A clipped observation/handover sheet, A4 proportions, 1024 x 1448 (about 0.2 mm per pixel at A4)."""
    rnd = random.Random(seed)
    W, H = 1024, 1448
    im = Image.new("RGB", (W, H), (250, 249, 245)); d = ImageDraw.Draw(im)
    d.rectangle([40, 40, W - 40, 150], fill=(31, 82, 96)); d.text((60, 62), title, fill=(255, 255, 255), font=font(40, True)); d.text((60, 108), "ProcSense University Hospital  |  Simulation use only", fill=(210, 230, 235), font=font(22))
    y = 180
    for lab, val in (("Patient", "—  (see wristband)"), ("Ward / Bay", "Patient Bay 1"), ("Date", "—"), ("Nurse", name)):
        d.text((60, y), lab + ":", fill=(60, 60, 60), font=font(26, True)); d.line([260, y + 34, W - 60, y + 34], fill=(150, 150, 150), width=2); d.text((270, y), val, fill=(30, 40, 120), font=font(26)); y += 52
    y += 20
    cols = ["Time", "Temp", "Pulse", "BP", "SpO2", "RR", "Init."]
    xs = [60 + i * (W - 120) // len(cols) for i in range(len(cols) + 1)]
    d.rectangle([60, y, W - 60, y + 52], fill=(225, 235, 238), outline=(90, 90, 90), width=2)
    for i, c in enumerate(cols):
        d.text((xs[i] + 10, y + 12), c, fill=(20, 20, 20), font=font(26, True)); d.line([xs[i], y, xs[i], y + 52 + 12 * 62], fill=(90, 90, 90), width=2)
    d.line([xs[-1], y, xs[-1], y + 52 + 12 * 62], fill=(90, 90, 90), width=2)
    for r in range(12):
        yy = y + 52 + r * 62; d.line([60, yy, W - 60, yy], fill=(120, 120, 120), width=2)
        if r < 7:
            vals = [f"{8 + r // 2:02d}:{'00' if r % 2 == 0 else '30'}", f"{36.4 + rnd.random() * 1.2:.1f}", f"{68 + rnd.randint(0, 24)}", f"{110 + rnd.randint(0, 24)}/{66 + rnd.randint(0, 16)}", f"{95 + rnd.randint(0, 4)}%", f"{14 + rnd.randint(0, 6)}", "DO" if name.startswith("Dan") else ("PN" if name.startswith("Pri") else "EM")]
            for i, v in enumerate(vals):
                d.text((xs[i] + 10, yy + 16), v, fill=(30, 40, 120), font=font(24))
    yy = y + 52 + 12 * 62 + 24
    d.text((60, yy), "Notes:", fill=(60, 60, 60), font=font(26, True))
    for k in range(5):
        d.line([60, yy + 70 + k * 50, W - 60, yy + 70 + k * 50], fill=(170, 170, 170), width=2)
        if k < 3:
            d.line([70, yy + 52 + k * 50, 70 + rnd.randint(300, 880), yy + 56 + k * 50 + rnd.randint(-6, 6)], fill=(40, 50, 130), width=3)
    im.save(OUT / f"chart_{seed}.png")


def badge(person, role, dept, color, fname):
    W, H = 512, 320
    im = Image.new("RGB", (W, H), (248, 248, 246)); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 78], fill=color); d.text((22, 14), "ProcSense Hospital", fill=(255, 255, 255), font=font(36, True)); d.text((22, 52), dept, fill=(235, 245, 248), font=font(20))
    d.rectangle([22, 104, 142, 252], fill=(210, 214, 218), outline=(120, 120, 120)); d.ellipse([54, 116, 110, 172], fill=(160, 165, 170)); d.ellipse([34, 170, 130, 300], fill=(160, 165, 170))
    d.text((166, 112), person, fill=(20, 20, 20), font=font(42, True)); d.text((166, 170), role, fill=(30, 80, 100), font=font(32, True)); d.text((166, 222), "Staff ID  SIM-" + str(abs(hash(person)) % 9000 + 1000), fill=(90, 90, 90), font=font(22))
    d.rectangle([0, H - 26, W, H], fill=color)
    im.save(OUT / fname)


def main():
    chart("Priya Nair, RN", "OBSERVATION CHART", 1)
    chart("Daniel Okoye, RN", "HANDOVER SHEET", 2)
    badge("Priya Nair", "Registered Nurse", "Medical-Surgical", (31, 110, 120), "badge_nurse_f.png")
    badge("Daniel Okoye", "Registered Nurse", "Medical-Surgical", (31, 110, 120), "badge_nurse_m.png")
    badge("Elena Marsh", "Reception", "Patient Services", (35, 70, 120), "badge_receptionist.png")
    print("textures ok", sorted(p.name for p in OUT.glob("*.png")))


main()
