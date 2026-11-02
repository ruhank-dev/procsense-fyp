"""Hospital wall television (55 inch class): thin bezel, slim body, tilt bracket and wall plate. Blender 4.5.14.

Same conventions as prop_kit (metres, Z up, front faces -Y, one mesh per material named EQ_<asset>_M_<Material>, closed geometry with UVs).
Origin: centre of the screen on the wall plane (y = 0 is the wall, the screen looks toward -Y). The panel is tilted DOWN by TILT degrees about the bracket pivot, so the
screen faces a patient lying in a bed lower than the set. Run from the repo root:
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_hospital_tv.py
Outputs under unity/Logs/HospitalTv (git-ignored): .blend, .fbx, validation.json. Presentation dimensions are illustrative, not a manufacturer specification.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import cyl, rbox  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = HERE.parents[1] / "Logs" / "HospitalTv"
geo.PALETTE.update({
    "TvBlack": ((.012, .013, .015, 1), 0, .3), "TvScreen": ((.004, .005, .007, 1), 0, .08), "TvAlu": ((.55, .56, .58, 1), .9, .38),
})

W, H = 1.217, .685            # 55 inch, 16:9
TILT = 7.0                    # degrees of downward tilt
PIVOT_Y = -.060               # tilt axis, on the bracket


def build():
    before = {o.name for o in bpy.data.objects}
    # panel group (tilts): frame, rear bulge, glass, VESA plate, standby LED, rear heat-sink fins
    rbox("frame", (W, .014, H), (0, -.092, 0), "TvBlack", .0035, 4)
    rbox("rear_cover", (W * .64, .019, H * .58), (0, -.0755, .02), "TvBlack", .006, 4)
    rbox("glass", (W - .014, .0016, H - .014), (0, -.0996, 0), "TvScreen", .0008, 2)
    rbox("chin_trim", (W - .02, .0012, .0035), (0, -.0997, -H / 2 + .0105), "TvAlu", .0004, 2)
    cyl("standby_led", (W / 2 - .07, -.0996, -H / 2 + .0105), (W / 2 - .07, -.1008, -H / 2 + .0105), .0016, "AlarmRed", 12)
    rbox("vesa_plate", (.30, .004, .30), (0, -.0625, 0), "TvAlu", .0015, 3)
    for x in (-.1, .1):
        for z in (-.1, .1):
            cyl(f"vesa_bolt{x}{z}", (x, -.0645, z), (x, -.0675, z), .0045, "TvAlu", 16, r=.0008)
    for i in range(9):                                                         # vent slots on the rear cover
        rbox(f"vent{i}", (W * .5, .0012, .003), (0, -.0662, .13 + i * .0085), "TvBlack", .0005, 2)
    panel = [o for o in bpy.data.objects if o.name not in before and o.type == "MESH"]
    for o in panel:
        pk.tilt(o, TILT, (0, PIVOT_Y, 0), "X")
    # fixed bracket: wall plate, two pivot arms, pivot barrel
    rbox("wall_plate", (.34, .006, .30), (0, -.003, 0), "TvAlu", .0015, 3)
    for x in (-.12, .12):
        for z in (-.12, .12):
            cyl(f"wall_screw{x}{z}", (x, -.006, z), (x, -.0085, z), .0055, "SteelBrushed", 16, r=.001)
    for x in (-.085, .085):
        rbox(f"arm{x}", (.034, .052, .022), (x, -.032, 0), "TvAlu", .004, 3)
    cyl("pivot_barrel", (-.11, PIVOT_Y, 0), (.11, PIVOT_Y, 0), .0065, "TvAlu", 24, r=.001)
    for x in (-.11, .11):
        cyl(f"pivot_cap{x}", (x, PIVOT_Y, 0), (x + (.003 if x > 0 else -.003), PIVOT_Y, 0), .0075, "TvAlu", 24, r=.0006)
    # cable pass-through: a short black lead from the cover to the wall plate
    pk.tube("power_lead", [(.18, -.0705, -.09), (.18, -.050, -.17), (.05, -.020, -.20), (.05, -.007, -.21)], .0035, "TvBlack", corner=.03, res=8)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    res = pk.export("hospital_tv", build, OUT, tri_budget=30000)
    # clearance check: the lowest rear point of the tilted panel must stay clear of the wall
    ys = []
    for o in bpy.data.objects:
        if o.type == "MESH" and o.name.startswith("EQ_hospital_tv") and not any(k in o.name for k in ("TvAlu",)):
            ys += [(o.matrix_world @ v.co).y for v in o.data.vertices]
    res["max_y_panel_or_bracket"] = max(ys) if ys else None
    (OUT / "validation.json").write_text(json.dumps([res], indent=2) + "\n")
    print("HOSPITAL TV", json.dumps({k: res[k] for k in ("name", "triangles", "max_y_panel_or_bracket")}))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
