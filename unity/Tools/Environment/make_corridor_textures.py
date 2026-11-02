"""Corridor wall displays: framed doctor portraits (captioned), certificates, award plaques and a notice board, composed with Pillow.

All content is fictional: invented people (portraits are generated MPFB characters), an invented hospital, invented awards. Every certificate and plaque carries a small line saying it is a
fictional exhibit for the training simulation, so none can be mistaken for a real accreditation. Run from the repository root:
  python unity/Tools/Environment/make_corridor_textures.py     -> unity/Logs/CorridorArt/*.png
"""
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "unity" / "Logs" / "CorridorArt"; OUT.mkdir(parents=True, exist_ok=True)
PORTRAITS = ROOT / "build/mpfb/output/portraits"
HOSPITAL = "ProcSense University Hospital"
DISCLAIMER = "Fictional exhibit for the ProcSense training simulation. Not a real accreditation."
TEAL, NAVY, GOLD = (24, 94, 106), (26, 44, 84), (176, 138, 52)


def font(size, bold=False, serif=False):
    names = (["georgiab.ttf", "timesbd.ttf"] if serif else ["arialbd.ttf", "segoeuib.ttf"]) if bold else (["georgia.ttf", "times.ttf"] if serif else ["arial.ttf", "segoeui.ttf"])
    for n in names + ["DejaVuSerif-Bold.ttf" if serif and bold else "DejaVuSans-Bold.ttf" if bold else "DejaVuSans.ttf"]:
        try:
            return ImageFont.truetype(n, size)
        except OSError:
            continue
    return ImageFont.load_default()


def center(d, y, text, f, fill, W):
    w = d.textlength(text, font=f); d.text(((W - w) / 2, y), text, font=f, fill=fill)


def portrait_print(i, entry):
    W, H = 1000, 1300
    im = Image.new("RGB", (W, H), (246, 245, 241)); d = ImageDraw.Draw(im)
    src = Image.open(PORTRAITS / entry["file"]).convert("RGB").crop((60, 40, 640, 800)).resize((820, 1076), Image.LANCZOS)
    im.paste(src, (90, 70)); d.rectangle([90, 70, 910, 1146], outline=(90, 90, 90), width=3)
    center(d, 1166, entry["name"], font(54, True, True), (28, 40, 70), W)
    d.line([300, 1236, 700, 1236], fill=GOLD, width=3)
    center(d, 1246, entry["dept"] + "  |  " + HOSPITAL, font(26), (70, 80, 95), W)
    im.save(OUT / f"portrait_{i}.png")


def certificate(name, title, sub, recipient, body, issuer, year, fname, seed):
    rnd = random.Random(seed)
    W, H = 1400, 1000
    im = Image.new("RGB", (W, H), (251, 249, 241)); d = ImageDraw.Draw(im)
    for k, (m, w, col) in enumerate(((26, 8, NAVY), (50, 2, GOLD), (60, 2, GOLD))):
        d.rectangle([m, m, W - m, H - m], outline=col, width=w)
    for cx, cy in ((90, 90), (W - 90, 90), (90, H - 90), (W - 90, H - 90)):
        d.ellipse([cx - 26, cy - 26, cx + 26, cy + 26], outline=GOLD, width=3); d.ellipse([cx - 12, cy - 12, cx + 12, cy + 12], fill=GOLD)
    center(d, 112, HOSPITAL.upper(), font(34, True), TEAL, W)
    center(d, 168, title, font(70, True, True), NAVY, W)
    center(d, 262, sub, font(32, False, True), (80, 80, 90), W)
    center(d, 340, "is proudly awarded to", font(28), (90, 90, 90), W)
    center(d, 392, recipient, font(80, True, True), (20, 30, 60), W)
    d.line([300, 506, W - 300, 506], fill=GOLD, width=3)
    y = 540
    for line in body:
        center(d, y, line, font(30, False, True), (60, 60, 70), W); y += 46
    # signatures + seal
    for x0, label in ((190, "Chief Executive"), (W - 520, "Director of Quality & Safety")):
        pts = [(x0 + 10 + i * 16, 790 + math.sin(i * .9 + rnd.random()) * 18 - i * .8) for i in range(16)]
        d.line(pts, fill=(30, 40, 120), width=3); d.line([x0, 826, x0 + 330, 826], fill=(80, 80, 80), width=2); d.text((x0 + 8, 836), label, font=font(22), fill=(80, 80, 80))
    cx, cy = W // 2, 800
    for r, wd in ((78, 5), (64, 2)):
        d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=GOLD, width=wd)
    d.ellipse([cx - 56, cy - 56, cx + 56, cy + 56], fill=(238, 224, 178)); center(d, cy - 28, "PS", font(54, True, True), (140, 108, 30), W)
    center(d, H - 128, f"{issuer}  |  {year}", font(24), (90, 90, 90), W)
    center(d, H - 98, DISCLAIMER, font(20), (120, 120, 120), W)
    im.save(OUT / fname)


def plaque(title, lines, fname):
    W, H = 1200, 900
    im = Image.new("RGB", (W, H), (30, 22, 18)); d = ImageDraw.Draw(im)
    for y in range(H):                                    # subtle wood grain
        v = int(8 * math.sin(y * .09) + 6 * math.sin(y * .31)); d.line([0, y, W, y], fill=(40 + v, 28 + v, 22 + v))
    d.rectangle([40, 40, W - 40, H - 40], outline=(110, 82, 40), width=6)
    # brass plate
    d.rounded_rectangle([110, 150, W - 110, H - 130], radius=18, fill=(172, 136, 66), outline=(205, 172, 98), width=5)
    center(d, 190, HOSPITAL.upper(), font(34, True), (70, 48, 12), W)
    center(d, 270, title, font(46, True, True), (50, 34, 8), W)
    y = 380
    for ln in lines:
        center(d, y, ln, font(40, False, True), (62, 42, 10), W); y += 62
    center(d, H - 118, DISCLAIMER, font(18), (200, 170, 100), W)
    im.save(OUT / fname)


def notice(W, H, title, lines, paper, accent, bold_first=True):
    im = Image.new("RGB", (W, H), paper); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, int(H * .17)], fill=accent); d.text((int(W * .06), int(H * .035)), title, font=font(int(H * .075), True), fill=(255, 255, 255))
    y = int(H * .22)
    for k, ln in enumerate(lines):
        big = ln.startswith("#")
        text = ln[1:] if big else ln
        f = font(int(H * (.056 if big else .047)), big)
        d.text((int(W * .07), y), text, font=f, fill=(24, 30, 50) if not big else tuple(int(c * .7) for c in accent))
        y += int(H * (.07 if big else .062))
    return im


def board():
    W, H = 2048, 1456
    rnd = random.Random(11)
    cork = Image.new("RGB", (W, H), (176, 132, 84)); px = cork.load()
    noise = Image.effect_noise((W, H), 38).convert("L"); fine = Image.effect_noise((W, H), 90).convert("L")
    for y in range(H):
        for x in range(W):
            n = noise.getpixel((x, y)) - 128; f = fine.getpixel((x, y)) - 128
            v = int(n * .38 + f * .16)
            px[x, y] = (max(0, min(255, 176 + v)), max(0, min(255, 132 + v)), max(0, min(255, 86 + v)))
    cork = cork.filter(ImageFilter.GaussianBlur(.8))
    notes = [
        (80, 70, 520, 700, -1.2, notice(520, 700, "VISITING HOURS", ["#Every day", "10:00 - 12:00", "16:00 - 19:00", "", "Two visitors per patient", "Children with an adult", "Please use hand gel", "when you arrive and", "when you leave."], (255, 255, 255), TEAL)),
        (640, 90, 470, 520, 1.6, notice(470, 520, "PATIENT GUIDANCE", ["#Need help?", "Press the call button.", "", "Please ask before you", "get out of bed.", "Tell us if you feel", "dizzy or in pain."], (255, 247, 196), (196, 108, 36))),
        (1180, 60, 380, 560, -2.2, notice(380, 560, "HAND HYGIENE", ["#5 moments", "1 Before touching", "  a patient", "2 Before a procedure", "3 After body fluids", "4 After touching", "  a patient", "5 After touching", "  surroundings"], (226, 241, 250), (28, 86, 140))),
        (1650, 100, 330, 470, 2.0, notice(330, 470, "QUIET ZONE", ["#Please keep", "voices low.", "", "Patients are", "resting."], (234, 247, 236), (44, 122, 78))),
        (110, 800, 460, 540, 1.1, notice(460, 540, "DEPARTMENTS", ["#Level 1", "Imaging  CT / MRI", "Ultrasound / X-ray", "Laboratory", "Operating Theatre", "Patient Bays", "", "Reception: ahead"], (255, 255, 255), NAVY)),
        (700, 760, 560, 640, -1.6, notice(560, 640, "SAFETY REMINDER", ["#Fire exits", "Know your nearest exit.", "Keep corridors clear.", "", "#Spills", "Report wet floors", "to a member of staff.", "", "Report hazards early."], (255, 236, 226), (176, 44, 36))),
        (1340, 700, 560, 380, 1.9, notice(560, 380, "STAFF NOTICES", ["#Daily briefing 08:00", "Simulation scenarios run", "all week. Handover sheets", "in the nurses' station."], (250, 250, 240), TEAL)),
        (1340, 1110, 380, 250, -1.0, notice(380, 250, "LOST PROPERTY", ["Ask at Reception."], (255, 255, 255), (96, 96, 110))),
    ]
    for x, y, w, h, ang, sheet in notes:
        shadow = Image.new("RGBA", (w + 40, h + 40), (0, 0, 0, 0)); sd = ImageDraw.Draw(shadow); sd.rectangle([20, 24, w + 20, h + 24], fill=(0, 0, 0, 120)); shadow = shadow.filter(ImageFilter.GaussianBlur(9)).rotate(ang, expand=True, resample=Image.BICUBIC)
        cork.paste(shadow, (x - 24 - (shadow.width - w - 40) // 2, y - 20 - (shadow.height - h - 40) // 2), shadow)
        rot = sheet.convert("RGBA").rotate(ang, expand=True, resample=Image.BICUBIC, fillcolor=(0, 0, 0, 0))
        mask = Image.new("L", sheet.size, 255).rotate(ang, expand=True, resample=Image.BICUBIC)
        cork.paste(rot, (x - (rot.width - w) // 2, y - (rot.height - h) // 2), mask)
        pin = ImageDraw.Draw(cork); cx, cy = x + w // 2 + rnd.randint(-8, 8) + (w // 2 - 40) * rnd.choice([-1, 1]) * 0 + (w // 3), y + 8
        col = rnd.choice([(188, 36, 36), (36, 76, 188), (40, 140, 70), (230, 190, 40)])
        pin.ellipse([cx - 14, cy - 12, cx + 14, cy + 16], fill=(20, 20, 20, 90)); pin.ellipse([cx - 13, cy - 15, cx + 13, cy + 11], fill=col, outline=tuple(int(c * .6) for c in col), width=2); pin.ellipse([cx - 6, cy - 10, cx - 1, cy - 5], fill=(255, 255, 255))
    cork.save(OUT / "bulletin_board.png")


def reception_screen():
    """The receptionist's display: a fictional front-desk register (invented names, no real data)."""
    W, H = 1920, 1080
    im = Image.new("RGB", (W, H), (236, 241, 244)); d = ImageDraw.Draw(im)
    d.rectangle([0, 0, W, 96], fill=(24, 74, 96)); d.text((40, 24), "ProcSense Hospital  |  Front Desk", font=font(44, True), fill=(255, 255, 255)); d.text((W - 420, 30), "Sat 31 Oct   09:14", font=font(36), fill=(220, 238, 244))
    d.rectangle([40, 130, 1280, 1020], fill=(255, 255, 255), outline=(200, 205, 210), width=3); d.text((70, 148), "Today's arrivals", font=font(40, True), fill=(30, 50, 70))
    heads = (("Name", 70), ("Appointment", 560), ("Time", 940), ("Status", 1080))
    for h, x in heads:
        d.text((x, 214), h, font=font(28, True), fill=(90, 100, 115))
    d.line([60, 256, 1260, 256], fill=(190, 195, 200), width=2)
    rows = [("Walker, Imogen", "CT scan", "09:15", "Checked in"), ("Haddad, Samir", "Ultrasound", "09:30", "Waiting"), ("Okafor, Beverly", "Blood tests", "09:40", "Waiting"),
            ("Larsen, Nils", "MRI", "10:00", "Expected"), ("Choi, Yuna", "X-ray", "10:10", "Expected"), ("Petrov, Anton", "Clinic review", "10:30", "Expected"), ("Mwangi, Grace", "Pre-op visit", "11:00", "Expected")]
    for k, (n, a, tm, s) in enumerate(rows):
        y = 280 + k * 84; col = {"Checked in": (36, 128, 78), "Waiting": (196, 120, 24), "Expected": (110, 120, 135)}[s]
        if k % 2 == 0:
            d.rectangle([60, y - 8, 1260, y + 66], fill=(244, 247, 249))
        d.text((70, y + 10), n, font=font(32), fill=(25, 35, 50)); d.text((560, y + 10), a, font=font(32), fill=(25, 35, 50)); d.text((940, y + 10), tm, font=font(32), fill=(25, 35, 50))
        d.rounded_rectangle([1075, y + 6, 1250, y + 56], radius=24, fill=col); d.text((1096, y + 12), s, font=font(26, True), fill=(255, 255, 255))
    d.rectangle([1320, 130, 1880, 560], fill=(255, 255, 255), outline=(200, 205, 210), width=3); d.text((1350, 150), "Visitors in building", font=font(34, True), fill=(30, 50, 70)); d.text((1350, 230), "14", font=font(160, True), fill=(24, 94, 106))
    d.text((1350, 450), "Visiting 10:00 - 12:00", font=font(28), fill=(90, 100, 115))
    d.rectangle([1320, 590, 1880, 1020], fill=(255, 255, 255), outline=(200, 205, 210), width=3); d.text((1350, 610), "Directions", font=font(34, True), fill=(30, 50, 70))
    for k, ln in enumerate(("Patient Bay 1  -  corridor, west", "Imaging  -  CT / MRI / Ultrasound / X-ray", "Laboratory  -  north end", "Operating Theatre  -  staff only", "Lost property  -  ask at Reception")):
        d.text((1350, 680 + k * 62), ln, font=font(26), fill=(40, 50, 65))
    im.save(OUT / "reception_screen.png")


def tv_welcome():
    """What the television behind the reception shows: a calm welcome and information screen."""
    W, H = 1920, 1080
    im = Image.new("RGB", (W, H), (14, 40, 52)); d = ImageDraw.Draw(im)
    for y in range(H):
        v = int(30 * y / H); d.line([0, y, W, y], fill=(14 + v // 3, 44 + v // 2, 60 + v))
    d.ellipse([-300, 600, 700, 1500], fill=(22, 82, 96)); d.ellipse([1300, -500, 2300, 500], fill=(26, 92, 108))
    center(d, 250, "Welcome", font(150, True, True), (255, 255, 255), W)
    center(d, 440, "ProcSense University Hospital", font(64), (210, 235, 240), W)
    d.line([760, 540, 1160, 540], fill=(176, 138, 52), width=4)
    for k, (a, b) in enumerate((("Visiting hours", "10:00 - 12:00   |   16:00 - 19:00"), ("Reception", "Ask us for directions to any department"), ("Hand hygiene", "Please use the gel dispensers on entry and exit"))):
        y = 620 + k * 120
        d.text((420, y), a, font=font(42, True), fill=(176, 205, 215)); d.text((860, y), b, font=font(42), fill=(255, 255, 255))
    center(d, 1010, "Fictional hospital for the ProcSense training simulation", font(26), (140, 170, 180), W)
    im.save(OUT / "tv_welcome.png")


def main():
    entries = json.loads((PORTRAITS / "portraits.json").read_text())
    for i, e in enumerate(entries):
        portrait_print(i, e)
    certificate("c0", "CERTIFICATE OF EXCELLENCE", "in Patient Safety and Clinical Care", "Dr. Amara Osei", ["For outstanding leadership in cardiac care", "and the safe introduction of the early-warning", "observation programme on the medical wards."], "Quality & Safety Programme", "2026", "cert_0.png", 1)
    certificate("c1", "TEACHING EXCELLENCE AWARD", "for dedication to trainee education", "Dr. Marcus Lindqvist", ["In recognition of exceptional teaching", "and mentorship of junior surgeons", "in the simulation training programme."], "Medical Education", "2026", "cert_1.png", 2)
    certificate("c2", "INFECTION PREVENTION CHAMPION", "hand hygiene and clinical practice", "Medical-Surgical Nursing Team", ["For sustained excellence in infection", "prevention and for achieving the", "hospital's highest hand-hygiene compliance."], "Infection Prevention & Control", "2026", "cert_2.png", 3)
    certificate("c3", "HOSPITAL RECOGNITION AWARD", "Team of the Year", "Imaging & Diagnostics Team", ["For reducing waiting times while", "maintaining the highest standards of", "patient safety and diagnostic quality."], "Chief Executive's Awards", "2026", "cert_3.png", 4)
    plaque("PATIENT EXPERIENCE AWARD", ["Awarded for compassionate care", "and clear communication", "Medical-Surgical Division", "2026"], "plaque_0.png")
    plaque("SAFETY FIRST", ["Gold Standard", "Zero preventable harm", "Operating Theatre Suite", "2026"], "plaque_1.png")
    board()
    reception_screen()
    tv_welcome()
    print("corridor art ok", sorted(p.name for p in OUT.glob("*.png")))


main()
