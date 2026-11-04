"""PatientBay reconstruction props (Blender 4.5.14, background): rebuilt headwall and other props, same node names as the
shipped FBX so the scene's prefabs, colliders and interactions keep working. Outputs to unity/Logs/BayRebuild.

Run from the repository root:
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup -P unity/Tools/Environment/build_bay_props.py -- [asset ...]
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import prop_kit as pk  # noqa: E402
from prop_kit import cyl, extrude, lathe, rbox, rounded_rect, tube, castor  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = pk.ROOT / "unity" / "Logs" / "BayRebuild"


def headwall():
    rbox("Housing", (2.35, .10, .30), (0, 0, 0), "Ivory", .016)
    rbox("Top_cap", (2.37, .112, .018), (0, -.004, .156), "PlasticWhite", .007)
    rbox("Bottom_cap", (2.37, .112, .016), (0, -.004, -.157), "PlasticWhite", .006)
    rbox("Service_strip", (2.26, .010, .046), (0, -.058, -.105), "Sage", .004)
    rbox("Upper_seam", (2.24, .008, .008), (0, -.058, .11), "PlasticGrey", .002)
    rbox("Mid_groove", (2.24, .006, .006), (0, -.058, -.052), "PlasticGrey", .002)
    # medical gas terminals: white plate, coloured collar, steel quick-connect, dark socket
    for i, (x, collar) in enumerate(zip([-.92, -.74, -.56, -.38], ["GasGreen", "GasWhite", "GasYellow", "GasBlue"])):
        rbox(f"Gas_plate_{i}", (.125, .016, .15), (x, -.066, .022), "PlasticWhite", .007)
        lathe(f"Gas_collar_{i}", [(.044, 0), (.044, .004), (.037, .007), (.037, .010)], (x, -.074, .022), collar, axis="Y", verts=32)
        lathe(f"Gas_port_{i}", [(.030, 0), (.030, .007), (.024, .011), (.024, .016)], (x, -.080, .022), "SteelBrushed", axis="Y", verts=28)
        lathe(f"Gas_bore_{i}", [(.014, 0), (.014, .018)], (x, -.082, .022), "PlasticGrey", axis="Y", verts=28)
        rbox(f"Gas_label_{i}", (.062, .002, .014), (x, -.075, -.052), "PlasticGrey", .0008)
    # 13 A duplex sockets (white plate, two rocker switches, earth/live/neutral shutters)
    for i, x in enumerate([.10, .34, .58]):
        rbox(f"Power_plate_{i}", (.152, .016, .152), (x, -.066, .022), "PlasticWhite", .006)
        for j, dz in enumerate((.040, -.026)):
            rbox(f"Socket_face_{i}_{j}", (.108, .004, .056), (x, -.076, .022 + dz), "GasWhite", .002)
            rbox(f"Socket_earth_{i}_{j}", (.008, .003, .018), (x, -.0795, .022 + dz + .012), "PlasticGrey", .0012)
            for dx in (-.020, .020):
                rbox(f"Socket_pin_{i}_{j}_{dx}", (.016, .003, .008), (x + dx, -.0795, .022 + dz - .008), "PlasticGrey", .001)
            rbox(f"Socket_switch_{i}_{j}", (.018, .004, .014), (x + .040, -.0795, .022 + dz), "PlasticGrey", .0015)
    # nurse call: round plate with red button and call-reset lamp
    lathe("Call_plate", [(.058, 0), (.058, .008), (.052, .013)], (.86, -.074, .022), "PlasticWhite", axis="Y", verts=32)
    lathe("Call_button", [(.030, 0), (.030, .006), (.024, .012)], (.86, -.087, .022), "AlarmRed", axis="Y", verts=28)
    rbox("Call_lamp", (.012, .004, .012), (.86, -.086, .062), "GasGreen", .002)
    # data and earth bonding points
    rbox("Data_plate", (.075, .014, .098), (1.04, -.065, .022), "PlasticWhite", .005)
    for k, dz in enumerate((.022, -.022)):
        rbox(f"Data_port_{k}", (.030, .004, .020), (1.04, -.074, .022 + dz), "PlasticGrey", .0015)
    lathe("Earth_stud", [(.012, 0), (.012, .004), (.007, .007), (.007, .014)], (1.17, -.074, .022), "SteelBrushed", axis="Y", verts=24)
    # reading / examination light above the bed
    rbox("Reading_light_housing", (1.50, .105, .085), (0, -.03, .36), "SteelBrushed", .022)
    rbox("Reading_light_diffuser", (1.40, .075, .012), (0, -.054, .316), "Diffuser", .004)
    for s in (-1, 1):
        rbox(f"Reading_light_endcap_{s}", (.018, .112, .092), (s * .758, -.03, .36), "PlasticWhite", .007)
        rbox(f"Reading_light_bracket_{s}", (.030, .060, .030), (s * .60, .010, .33), "SteelBrushed", .004)
    geo.point("FRONT_MARKER", (0, -.25, 0))
    geo.point("COL_Main", (0, 0, 0), (2.35, .10, .30))


import build_bay_props_b as pb  # noqa: E402
import build_bay_props_c as pc  # noqa: E402
BUILDERS = {"bay_headwall": headwall, **pb.BUILDERS_B, **pc.BUILDERS_C}

if __name__ == "__main__":
    assert bpy.app.version[:3] == (4, 5, 14), bpy.app.version_string
    wanted = [a for a in sys.argv[sys.argv.index("--") + 1:]] if "--" in sys.argv else list(BUILDERS)
    results = [pk.export(n, BUILDERS[n], OUT, allow_open=("Curtain",)) if n == "privacy_curtain" else pk.export(n, BUILDERS[n], OUT) for n in wanted]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("BAYREBUILD", json.dumps(results))
