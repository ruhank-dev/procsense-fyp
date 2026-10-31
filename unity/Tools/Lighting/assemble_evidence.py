"""Builds the golden-hour evidence sheets from real Unity PlayMode captures.
Usage: python assemble_evidence.py <final_capture_dir_name> [out_dir]   (directories live in unity/Logs/Lighting; the baseline is baseline_cap)"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

L = Path(__file__).resolve().parents[2] / "Logs" / "Lighting"
final = sys.argv[1]
prev = sys.argv[3] if len(sys.argv) > 3 else None
out = Path(sys.argv[2]) if len(sys.argv) > 2 else L / f"evidence_{final}"
out.mkdir(parents=True, exist_ok=True)
F = ImageFont.truetype("arialbd.ttf", 26)


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


B, N = L / "baseline_cap", L / f"cap_{final}"
if prev:
    sheet("00_four_way_original_previous_new_reference.png", [("1. ORIGINAL Unity", B / "reference.png"), ("2. PREVIOUS golden hour (vinyl)", L / f"cap_{prev}" / "reference.png"), ("3. NEW integrated (wood + exterior + sun)", N / "reference.png"), ("4. TARGET reference", L / "reference.png")], (800, 450), 2)
sheet("01_original_vs_updated_vs_reference.png", [("ORIGINAL Unity lighting", B / "reference.png"), ("UPDATED golden hour (Unity)", N / "reference.png"), ("REFERENCE (owner's image)", L / "reference.png")], (800, 450))
sheet("01b_new_vs_reference.png", [("NEW integrated environment (Unity)", N / "reference.png"), ("TARGET reference", L / "reference.png")], (960, 540))
sheet("02_sunlight_and_shadows.png", [("floor sun - ORIGINAL", B / "floor_sun.png"), ("floor sun - UPDATED", N / "floor_sun.png"), ("wall shadows - ORIGINAL", B / "sun_wall_shadows.png"), ("wall shadows - UPDATED", N / "sun_wall_shadows.png")], (800, 450), 2)
sheet("03_metal_reflections.png", [("monitor stand - ORIGINAL", B / "metal_monitor_stand.png"), ("monitor stand - UPDATED", N / "metal_monitor_stand.png"), ("bed rail - ORIGINAL", B / "metal_bed_rail.png"), ("bed rail - UPDATED", N / "metal_bed_rail.png")], (800, 450), 2)
sheet("04_indirect_light_and_contact_shadows.png", [("corner - ORIGINAL", B / "indirect_corner.png"), ("corner - UPDATED", N / "indirect_corner.png"), ("equipment shadows - ORIGINAL", B / "shadows_equipment.png"), ("equipment shadows - UPDATED", N / "shadows_equipment.png")], (800, 450), 2)
sheet("05_window_exterior.png", [("window - ORIGINAL", B / "window_exterior.png"), ("window - UPDATED", N / "window_exterior.png")], (960, 540))
sheet("05b_floor_closeups.png", [("floor sun - NEW", N / "floor_sun.png"), ("equipment shadows on wood - NEW", N / "shadows_equipment.png")], (960, 540))
sheet("06_overview.png", [("overview - ORIGINAL", B / "overview.png"), ("overview - UPDATED", N / "overview.png")], (960, 540))
