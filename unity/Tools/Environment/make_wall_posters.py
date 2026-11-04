"""Hospital wall posters (original project artwork, project-owned): hand hygiene, safety, wellbeing and anatomy infographics in the corridor's teal / blue-grey brand palette.

  python -I make_wall_posters.py <out_dir>          ->  poster_01..poster_12.png (1024 x 1448, A-series ratio)
Fonts: Windows Segoe UI / Arial (system fonts, not redistributed). Text is generic safety information, no clinical values.
"""
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
W, H = 1024, 1448
TEAL, TEAL2, NAVY, SAGE, PAPER, AMBER, RED = (22, 94, 100), (60, 150, 150), (30, 52, 80), (170, 200, 190), (246, 246, 240), (208, 150, 60), (178, 48, 44)
WHITE = (255, 255, 255)


def font(size, bold=False):
    for f in (("segoeuib.ttf" if bold else "segoeui.ttf"), ("arialbd.ttf" if bold else "arial.ttf")):
        try:
            return ImageFont.truetype("C:/Windows/Fonts/" + f, size)
        except OSError:
            pass
    return ImageFont.load_default()


def centred(d, y, text, f, fill):
    d.text(((W - d.textlength(text, font=f)) / 2, y), text, font=f, fill=fill)


def wrap(d, text, f, width):
    line, lines = "", []
    for wd in text.split():
        t = (line + " " + wd).strip()
        if d.textlength(t, font=f) <= width:
            line = t
        else:
            lines.append(line); line = wd
    lines.append(line)
    return lines


def base(title, sub, accent=TEAL):
    im = Image.new("RGB", (W, H), PAPER); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 330], fill=accent)
    for i in range(6):
        d.ellipse([W - 330 + i * 38, -200 + i * 22, W + 330 - i * 38, 420 - i * 22], outline=WHITE, width=2)
    centred(d, 90, title, font(96, True), WHITE); centred(d, 220, sub, font(46), (225, 240, 238))
    d.rectangle([0, H - 90, W, H], fill=NAVY); centred(d, H - 70, "ProcSense University Hospital", font(38), (225, 232, 240))
    return im, d


def disc(d, cx, cy, n, accent=TEAL, ink=WHITE):
    d.ellipse([cx - 44, cy - 44, cx + 44, cy + 44], fill=accent)
    f = font(60, True); d.text((cx - d.textlength(str(n), font=f) / 2, cy - 42), str(n), font=f, fill=ink)


def list_poster(title, sub, items, accent=TEAL, outline=SAGE, step=190):
    im, d = base(title, sub, accent)
    for i, s in enumerate(items):
        y = 400 + i * step
        d.rounded_rectangle([80, y, W - 80, y + step - 30], 30, fill=WHITE, outline=outline, width=5)
        disc(d, 170, y + (step - 30) // 2, i + 1, accent)
        for j, ln in enumerate(wrap(d, s, font(44, True), 640)[:2]):
            d.text((250, y + 28 + j * 54), ln, font=font(44, True), fill=NAVY)
    return im


def hand_hygiene():
    im, d = base("Clean your hands", "Soap and water: 40 to 60 seconds")
    steps = ["Wet hands with water", "Apply soap to cover all surfaces", "Rub palms together", "Rub the back of each hand", "Interlace fingers, rub between", "Backs of fingers against palms",
             "Rotate thumbs in the palm", "Rub fingertips in the palm", "Rinse and dry with a single-use towel"]
    for i, s in enumerate(steps):
        x, y = 70 + (i % 3) * 300, 380 + (i // 3) * 320
        d.rounded_rectangle([x, y, x + 280, y + 290], 28, fill=WHITE, outline=SAGE, width=4)
        disc(d, x + 60, y + 60, i + 1)
        cx, cy = x + 150, y + 150
        d.ellipse([cx - 50, cy - 20, cx + 50, cy + 70], fill=(236, 200, 170))
        for k in range(4):
            d.rounded_rectangle([cx - 52 + k * 25, cy - 74, cx - 32 + k * 25, cy - 8], 10, fill=(236, 200, 170))
        for j, ln in enumerate(wrap(d, s, font(25), 250)[:3]):
            d.text((x + 16, y + 214 + j * 28), ln, font=font(25), fill=NAVY)
    return im


def wristband():
    im, d = base("Check the wristband", "Right patient, right care", NAVY)
    d.rounded_rectangle([110, 410, W - 110, 650], 60, fill=WHITE, outline=NAVY, width=6)
    d.rounded_rectangle([140, 440, 300, 620], 40, fill=RED); d.text((158, 510), "ALLERGY", font=font(32, True), fill=WHITE)
    for j, ln in enumerate(["Full name", "Date of birth", "Hospital number"]):
        d.text((340, 450 + j * 60), ln, font=font(44), fill=NAVY)
    for i, t in enumerate(["Ask the patient to state their name", "Compare with the wristband", "Check allergies before any drug", "Report any mismatch at once"]):
        y = 720 + i * 150; disc(d, 150, y + 50, i + 1); d.text((240, y + 15), t, font=font(44, True), fill=NAVY)
    return im


def wellbeing():
    im, d = base("Look after yourself", "Staff wellbeing matters")
    for i, t in enumerate(["Take your breaks", "Stay hydrated", "Ask for support early", "Handover safely"]):
        cx, cy = 280 + (i % 2) * 470, 600 + (i // 2) * 420
        d.ellipse([cx - 190, cy - 160, cx + 190, cy + 160], fill=WHITE, outline=SAGE, width=6); d.ellipse([cx - 40, cy - 110, cx + 40, cy - 30], fill=TEAL2)
        for j, ln in enumerate(wrap(d, t, font(44, True), 330)[:2]):
            d.text((cx - d.textlength(ln, font=font(44, True)) / 2, cy + 10 + j * 50), ln, font=font(44, True), fill=NAVY)
    return im


def quiet():
    im, d = base("Quiet please", "Patients are resting", SAGE)
    d.ellipse([312, 480, 712, 880], fill=TEAL); d.rounded_rectangle([430, 560, 594, 800], 40, fill=WHITE); d.rectangle([470, 640, 560, 660], fill=TEAL)
    centred(d, 960, "Please keep voices low", font(60, True), NAVY); centred(d, 1050, "and phones on silent", font(60), NAVY)
    return im


def anatomy(name, color, labels):
    im, d = base(name, "Anatomy overview", color)
    cx, cy = W // 2, 820
    d.ellipse([cx - 230, cy - 290, cx + 230, cy + 270], fill=(250, 235, 228), outline=color, width=8)
    for k in range(7):
        a = k * math.pi / 3.5; d.line([cx, cy - 60, cx + 200 * math.cos(a), cy - 60 + 190 * math.sin(a)], fill=color, width=14)
    for i, t in enumerate(labels):
        y = 440 + i * 190; xx = 60 if i % 2 == 0 else W - 360
        d.line([xx + (270 if i % 2 == 0 else 0), y + 30, cx + (-120 if i % 2 == 0 else 120), cy - 80 + i * 40], fill=NAVY, width=3); d.text((xx, y), t, font=font(40, True), fill=NAVY)
    return im


posters = [
    hand_hygiene,
    lambda: list_poster("My 5 moments", "for hand hygiene", ["Before touching a patient", "Before a clean or aseptic procedure", "After body fluid exposure risk", "After touching a patient", "After touching patient surroundings"], NAVY, TEAL2),
    lambda: list_poster("Cover your cough", "Protect patients and colleagues", ["Cough or sneeze into a tissue or elbow", "Bin the tissue immediately", "Clean your hands straight away", "Wear a mask if you have symptoms"], TEAL2, SAGE, 240),
    lambda: list_poster("Fire safety", "If you discover a fire", ["Raise the alarm", "Evacuate patients and visitors", "Alert the switchboard", "Contain: close doors", "Extinguish only if safe"], RED, RED),
    wristband, wellbeing, quiet,
    lambda: anatomy("The heart", RED, ["Right atrium", "Left atrium", "Right ventricle", "Left ventricle"]),
    lambda: anatomy("The lungs", TEAL, ["Trachea", "Bronchi", "Right lung", "Left lung"]),
    lambda: list_poster("Visiting guide", "Welcome", ["Clean your hands on entering", "Check in at reception", "Two visitors per bed", "Please do not sit on beds", "Speak to the nurse in charge"], TEAL2, SAGE, 180),
    lambda: list_poster("Sharps safety", "Dispose of your own sharps", ["Never recap a needle", "Use the bin at the point of use", "Fill only to the line", "Report any needlestick at once"], AMBER, AMBER, 240),
    lambda: list_poster("Theatre team brief", "Before every list", ["Sign in: patient, site, consent", "Time out: team, procedure, risks", "Counts: swabs, sharps, instruments", "Sign out: specimens, concerns"], NAVY, TEAL2, 240),
]
for i, make in enumerate(posters, 1):
    make().filter(ImageFilter.SMOOTH).save(out / f"poster_{i:02d}.png"); print("poster", i)
