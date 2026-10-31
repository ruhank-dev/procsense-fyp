"""Composite: real reference photographs (left) vs a render of the model (right), labelled. Usage: compare_sheet.py out.png render.png [render2.png]"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REF = Path(__file__).resolve().parents[2] / "Logs" / "MonitorRefs"
out, *renders = sys.argv[1:]
refs = [("PHOTO  Philips IntelliVue MX400", REF / "ref_philips_mx400.jpg"), ("PHOTO  Philips IntelliVue MX700", REF / "ref_philips_mx700.jpg")]
H = 640
cells = [(t, Image.open(p).convert("RGB")) for t, p in refs] + [(f"MODEL  {Path(r).stem}", Image.open(r).convert("RGB")) for r in renders]
f = ImageFont.truetype("arialbd.ttf", 26)
ims = []
for t, im in cells:
    im = im.resize((int(im.width * H / im.height), H))
    c = Image.new("RGB", (im.width, H + 40), (24, 24, 24)); c.paste(im, (0, 40)); ImageDraw.Draw(c).text((10, 6), t, font=f, fill=(255, 255, 255))
    ims.append(c)
W = sum(i.width for i in ims) + 10 * (len(ims) - 1)
sheet = Image.new("RGB", (W, H + 40), (0, 0, 0)); x = 0
for i in ims:
    sheet.paste(i, (x, 0)); x += i.width + 10
sheet.save(out); print(out, sheet.size)
