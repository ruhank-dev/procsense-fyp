"""Final-look evidence sheets from real Unity PlayMode captures.
Usage: python assemble_final.py <final_tag> <previous_patient_tag> [out_dir]   (capture dirs live in unity/Logs/Lighting as cap_<tag>)"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

L = Path(__file__).resolve().parents[2] / "Logs" / "Lighting"
final, prev = sys.argv[1], sys.argv[2]
out = Path(sys.argv[3]) if len(sys.argv) > 3 else L / f"evidence_{final}"
out.mkdir(parents=True, exist_ok=True)
F = ImageFont.truetype("arialbd.ttf", 26)
N, P = L / f"cap_{final}", L / f"cap_{prev}"
TARGET = L / "final_target" / "reference_final.png"


def cell(path, label, size):
    im = Image.open(path).convert("RGB").resize(size, Image.LANCZOS)
    c = Image.new("RGB", (size[0], size[1] + 38), (14, 14, 14)); c.paste(im, (0, 38)); ImageDraw.Draw(c).text((8, 5), label, font=F, fill="white")
    return c


def sheet(name, items, size, cols=None):
    cs = [cell(p, t, size) for t, p in items]
    cols = cols or len(cs); rows = (len(cs) + cols - 1) // cols
    S = Image.new("RGB", (cols * (size[0] + 6), rows * (size[1] + 44)), (0, 0, 0))
    for i, c in enumerate(cs): S.paste(c, ((i % cols) * (size[0] + 6), (i // cols) * (size[1] + 44)))
    S.save(out / name); print("wrote", name)


sheet("00_target_vs_unity_same_camera.png", [("TARGET reference (owner)", TARGET), (f"UNITY PlayMode ({final})", N / "reference.png")], (960, 540))
sheet("01_patient_previous_vs_final.png", [(f"face - previous ({prev})", P / "patient_face.png"), (f"face - FINAL ({final})", N / "patient_face.png"),
                                          (f"legs/bed contact - previous", P / "patient_legs.png"), (f"legs/bed contact - FINAL", N / "patient_legs.png"),
                                          (f"hand + gown - previous", P / "patient_hands.png"), (f"hand + gown - FINAL", N / "patient_hands.png"),
                                          (f"torso - previous", P / "patient_torso.png"), (f"torso - FINAL", N / "patient_torso.png")], (800, 450), 2)
sheet("02_furniture_and_decor.png", [("sofa + window", N / "room_sofa_window.png"), ("framed prints", N / "room_prints.png"), ("wall TV (edge-on at reference camera, screen toward patient)", N / "room_wall_tv.png"), ("window and skyline", N / "window_exterior.png")], (800, 450), 2)
sheet("03_equipment_and_contact.png", [("monitor stand", N / "metal_monitor_stand.png"), ("bed rail", N / "metal_bed_rail.png"), ("equipment shadows", N / "shadows_equipment.png"), ("patient on the bed", N / "patient_bed_contact.png")], (800, 450), 2)
sheet("04_overview.png", [("overview", N / "overview.png"), ("floor and light", N / "floor_sun.png")], (960, 540))
