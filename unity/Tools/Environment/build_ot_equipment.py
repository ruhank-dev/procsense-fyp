"""Operating-theatre equipment rebuilt with the PatientBay prop kit (filleted edges, lathe-turned parts, castors, smooth tubes), Blender 4.5.14.

Replaces the five primitive OT presentation props (surgical light, operating table, anaesthesia unit, Mayo stand, supply cupboard) under the same ids so the existing prefabs, colliders and room
placement keep working; OtEquipmentUpgrade.cs copies the FBXs over the imported models and re-maps materials by renderer suffix. Metres, Z up, front faces -Y, origin on the floor
(surgical light: origin on the ceiling plane, as before).

  blender -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_ot_equipment.py
Output (ignored): unity/Logs/RoomDressing/<id>.fbx plus ot_validation.json.
"""
import json
import math
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import castor, lathe, tube  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = ROOT / "unity" / "Logs" / "RoomDressing"


def rbox(part, size, loc, mat="PlasticWhite", r=.004, seg=2):        # 2 fillet segments: enough to catch light, keeps triangles in budget
    return pk.rbox(part, size, loc, mat, r, seg)


def cyl(part, a, b, radius, mat="SteelBrushed", verts=40, r=0.0, seg=2):
    return pk.cyl(part, a, b, radius, mat, min(verts, 24), r, seg)


def front_marker():
    geo.point("FRONT_MARKER", (0, -.3, 0))


# ---------------------------------------------------------------- surgical light
def light_head(cx, cy, cz, radius, tag):
    """Dome-shaped LED head: spun shell, diffuser lens, LED spots, central sterile handle."""
    s = radius / .31
    prof = [(.0, 0.0), (.10 * s, .004 * s), (.24 * s, -.002 * s), (.29 * s, .004 * s), (.315 * s, .040 * s), (.30 * s, .095 * s),
            (.22 * s, .118 * s), (.10 * s, .128 * s), (.0, .130 * s)]
    lathe(f"{tag}_shell", [(r, cz + z) for r, z in prof], (cx, cy, 0), "Ivory", verts=48, angle=40)
    # stepped, segmented LED lens: diffuser rings separated by dark gaps, so the face reads as an LED array rather than one white disc
    for i, (rr, h, mat) in enumerate(((.265, .004, "Diffuser"), (.215, .006, "PlasticGrey"), (.205, .008, "Diffuser"), (.135, .010, "PlasticGrey"), (.125, .012, "Diffuser"), (.055, .013, "PlasticGrey"))):
        cyl(f"{tag}_lens{i}", (cx, cy, cz - .004 - .0016 * i), (cx, cy, cz - .004 + h), rr * s, mat, 40)
    cyl(f"{tag}_rim", (cx, cy, cz + .004), (cx, cy, cz + .018), .292 * s, "SteelBrushed", 48, r=.002)
    n = 16 if s > .9 else 12
    for i in range(n):
        a = i * math.tau / n
        cyl(f"{tag}_led{i}", (cx + .235 * s * math.cos(a), cy + .235 * s * math.sin(a), cz - .001), (cx + .235 * s * math.cos(a), cy + .235 * s * math.sin(a), cz + .004), .012 * s, "PlasticGrey", 10)
    cyl(f"{tag}_handle_stem", (cx, cy, cz - .002), (cx, cy, cz - .075 * s), .018 * s, "Ivory", 16, r=.003)
    cyl(f"{tag}_handle_grip", (cx, cy, cz - .075 * s), (cx, cy, cz - .135 * s), .027 * s, "Sage", 20, r=.006)
    cyl(f"{tag}_yoke_pin", (cx, cy, cz + .13 * s), (cx, cy, cz + .20 * s), .020 * s, "SteelBrushed", 16, r=.003)


def surgical_light():
    cyl("Ceiling_plate", (0, 0, 2.73), (0, 0, 2.705), .17, "Ivory", 40, r=.008)
    lathe("Canopy", [(0.0, 2.705), (.14, 2.707), (.10, 2.64), (.07, 2.58), (.065, 2.50), (0.0, 2.50)], (0, 0, 0), "Ivory", verts=32)
    cyl("Bearing", (0, 0, 2.50), (0, 0, 2.455), .085, "SteelBrushed", 32, r=.004)
    # main arm to the large head over the table
    tube("Arm_main", [(0, 0, 2.48), (.34, -.25, 2.48), (.65, -.50, 2.48), (.65, -.50, 2.40)], .033, "Ivory", corner=.05)
    cyl("Joint_main", (.65, -.50, 2.40), (.65, -.50, 2.31), .045, "SteelBrushed", 24, r=.004)
    tube("Spring_main", [(.65, -.50, 2.31), (.65, -.50, 2.26)], .022, "Ivory")
    light_head(.65, -.50, 2.06, .31, "Head_main")
    # satellite arm, lower, over the foot of the table
    tube("Arm_sat", [(0, 0, 2.43), (.30, .17, 2.43), (.65, .35, 2.43), (.65, .35, 2.36)], .030, "Ivory", corner=.05)
    cyl("Joint_sat", (.65, .35, 2.36), (.65, .35, 2.28), .040, "SteelBrushed", 24, r=.004)
    tube("Spring_sat", [(.65, .35, 2.28), (.65, .35, 2.19)], .020, "Ivory")
    light_head(.65, .35, 2.00, .25, "Head_sat")
    front_marker()


# ---------------------------------------------------------------- operating table
def operating_table():
    rbox("Floor_base", (.60, 1.0, .035), (0, 0, .0175), "SteelBrushed", .015)
    rbox("Base_cover", (.52, .86, .075), (0, 0, .072), "PlasticWhite", .026)
    for x in (-.25, .25):
        for y in (-.44, .44):
            cyl("Base_foot", (x, y, 0), (x, y, .006), .03, "RubberBlack", 16)
    rbox("Column_lower", (.30, .38, .30), (0, 0, .27), "SteelBrushed", .05)
    for i in range(5):
        rbox(f"Bellows_{i}", (.285 - .006 * (i % 2), .365 - .006 * (i % 2), .035), (0, 0, .43 + .042 * i), "PlasticWhite" if i % 2 else "Ivory", .012)
    rbox("Column_upper", (.26, .31, .17), (0, 0, .70), "Ivory", .04)
    rbox("Control_panel", (.15, .012, .08), (0, -.162, .70), "PlasticGrey", .006)
    for i, x in enumerate((-.045, 0, .045)):
        cyl(f"Control_button{i}", (x, -.168, .70), (x, -.175, .70), .013, "Sage" if i != 1 else "AlarmRed", 12)
    rbox("Top_frame", (.50, 1.78, .04), (0, 0, .795), "SteelBrushed", .012)
    rbox("Top_bridge", (.40, .50, .05), (0, 0, .76), "PlasticWhite", .02)
    segments = [(-.80, .32, "Head"), (-.45, .36, "Back_upper"), (-.02, .44, "Back_lower"), (.40, .36, "Seat"), (.80, .40, "Leg")]
    pos = -.88
    for length, name in [(.30, "Head"), (.34, "Back_upper"), (.42, "Back_lower"), (.34, "Seat"), (.38, "Leg")]:
        rbox(f"Pad_{name}", (.54, length - .012, .08), (0, pos + length / 2, .875), "Upholstery", .028, 5)
        pos += length
    rbox("Headrest", (.28, .22, .10), (0, -.84, .93), "Upholstery", .04, 5)
    for x in (-.31, .31):
        cyl("Side_rail", (x, -.84, .835), (x, .90, .835), .0125, "SteelBrushed", 20, r=.002)
        for y in (-.60, -.10, .40, .85):
            rbox(f"Rail_bracket", (.03 if x > 0 else .03, .035, .045), (x * .93, y, .82), "SteelBrushed", .006)
    rbox("Foot_control", (.22, .14, .045), (.30, -.50, .0225 + .0), "PlasticGrey", .018)
    rbox("Foot_pedal", (.16, .05, .018), (.30, -.52, .054), "Sage", .006)
    front_marker()


# ---------------------------------------------------------------- anaesthesia workstation
def anaesthesia():
    rbox("Body", (.62, .52, .70), (0, 0, .50), "Ivory", .032)
    rbox("Plinth", (.58, .48, .08), (0, 0, .13), "PlasticGrey", .02)
    for i, z in enumerate((.62, .50, .38, .26)):
        rbox(f"Drawer_{i}", (.50, .014, .105), (-.0, -.262, z), "Sage" if i == 0 else "PlasticWhite", .008)
        rbox(f"Drawer_handle_{i}", (.20, .014, .016), (0, -.275, z + .035), "SteelBrushed", .004)
    rbox("Worktop", (.74, .60, .04), (0, -.02, .875), "SteelBrushed", .012)
    for x in (-.375, .375):
        cyl("Shelf_rail", (x, -.30, .905), (x, .26, .905), .008, "SteelBrushed", 14)
    rbox("Column", (.60, .22, .72), (0, .16, 1.255), "Ivory", .035)
    rbox("Gas_panel", (.46, .012, .20), (0, .045, 1.04), "PlasticGrey", .006)
    for i, x in enumerate((-.15, -.05, .05, .15)):
        cyl(f"Flow_tube{i}", (x, .036, .96), (x, .036, 1.12), .012, "PlasticWhite", 14)
        cyl(f"Flow_knob{i}", (x, .026, .93), (x, .018, .93), .018, "Sage" if i < 2 else "AccentBlue", 18)
    for i, (x, mat) in enumerate(((-.20, "Sage"), (-.05, "PlasticGrey"), (.10, "Amber"))):
        rbox(f"Vaporiser_{i}", (.115, .13, .20), (x, .10, 1.0 + .0), mat, .014)
    # vaporisers sit on the shelf: lift them there
    for i, x in enumerate((-.20, -.05, .10)):
        cyl(f"Vap_knob{i}", (x, .10, 1.10), (x, .10, 1.13), .036, "SteelBrushed", 20, r=.004)
    lathe("Bellows_housing", [(0.0, .895), (.092, .895), (.092, 1.20), (.10, 1.215), (.0, 1.215)], (.26, -.05, 0), "PlasticWhite", verts=32)
    cyl("Bellows_top", (.26, -.05, 1.215), (.26, -.05, 1.235), .105, "PlasticGrey", 32, r=.005)
    rbox("Ventilator_panel", (.30, .02, .20), (-.15, .0485, 1.40), "PlasticWhite", .008)
    rbox("Ventilator_display", (.22, .006, .11), (-.15, .0365, 1.42), "ScreenOff", .003)
    for i, x in enumerate((-.24, -.15, -.06)):
        cyl(f"Ventilator_knob{i}", (x, .0385, 1.33), (x, .028, 1.33), .016, "Sage", 14)
    tube("Monitor_arm", [(0, .20, 1.60), (0, .12, 1.66), (0, -.02, 1.66)], .018, "PlasticGrey", corner=.04)
    rbox("Monitor_body", (.44, .06, .31), (0, -.08, 1.64), "PlasticGrey", .022)
    rbox("Monitor_screen", (.395, .012, .255), (0, -.1135, 1.64), "ScreenOff", .004)
    for x, y, z in ((.30, -.30, .77), (.26, -.05, 1.0), (.30, -.05, 1.23)):
        cyl("Hose_collar", (x, y, z - .015), (x, y, z + .03), .03, "SteelBrushed", 18, r=.003)
    for i, x in enumerate((-.20, -.05, .10)):
        rbox(f"Vaporiser_plate{i}", (.10, .006, .05), (x, .035, 1.03), "PlasticWhite", .003)
    rbox("Monitor_hinge", (.06, .05, .06), (0, -.02, 1.66), "SteelBrushed", .012)
    tube("Hose_a", [(.26, -.05, 1.0), (.36, -.14, .96), (.40, -.24, .80), (.30, -.30, .77)], .02, "RubberBlack", corner=.08)
    tube("Hose_b", [(.30, -.05, 1.23), (.38, -.1, 1.28), (.42, -.2, 1.0)], .016, "RubberBlack", corner=.06)
    for x, y in ((-.27, -.21), (.27, -.21), (-.27, .21), (.27, .21)):
        castor("Castor", x, y, 0.0, radius=.05, width=.03)
    front_marker()


# ---------------------------------------------------------------- Mayo stand
def mayo():
    rbox("Tray_floor", (.62, .42, .012), (0, 0, 1.012), "SteelBrushed", .004)
    for x in (-.31, .31):
        rbox("Tray_wall", (.012, .42, .035), (x, 0, 1.035), "SteelBrushed", .004)
    for y in (-.21, .21):
        rbox("Tray_wall", (.62, .012, .035), (0, y, 1.035), "SteelBrushed", .004)
    rbox("Tray_support", (.30, .20, .035), (0, .02, .975), "SteelBrushed", .01)
    cyl("Column_outer", (0, .10, .13), (0, .10, .60), .030, "SteelBrushed", 28, r=.003)
    cyl("Column_inner", (0, .10, .60), (0, .10, .97), .021, "Chrome", 28, r=.002)
    cyl("Lock_knob", (.03, .10, .55), (.075, .10, .55), .016, "PlasticGrey", 16, r=.004)
    for x in (-.21, .21):
        rbox("Base_leg", (.04, .50, .04), (x, .02, .10), "SteelBrushed", .008)
    rbox("Base_bridge", (.46, .05, .04), (0, .10, .10), "SteelBrushed", .008)
    for x, y in ((-.21, -.20), (.21, -.20), (-.21, .24), (.21, .24)):
        castor("Castor", x, y, 0.0, radius=.04, width=.024)
    # sterile drape and instruments
    rbox("Sterile_drape", (.40, .30, .012), (-.04, 0, 1.024), "Curtain", .005)
    for i in range(6):
        cyl(f"Instrument_{i}", (-.12 + .04 * i, -.06, 1.034), (-.12 + .04 * i + .02 * (i % 2), .08, 1.034), .005, "Chrome", 10)
        rbox(f"Instrument_ring_{i}", (.014, .016, .006), (-.12 + .04 * i + .02 * (i % 2), .095, 1.034), "Chrome", .002)
    front_marker()


# ---------------------------------------------------------------- supply cupboard
def cupboard():
    rbox("Carcass", (1.2, .46, 1.80), (0, 0, .98), "Ivory", .018)
    rbox("Plinth", (1.12, .40, .09), (0, 0, .055), "PlasticGrey", .008)
    rbox("Top_cap", (1.22, .48, .03), (0, 0, 1.895), "PlasticWhite", .012)
    for x in (-.295, .295):
        rbox("Door_reveal", (.585, .008, 1.72), (x, -.236, .99), "PlasticGrey", .002)
        rbox("Door_panel", (.565, .024, 1.70), (x, -.248, .99), "Ivory", .012)
        rbox("Door_inset", (.46, .006, 1.40), (x, -.2625, 1.03), "PlasticWhite", .004)
        hx = x * .14
        rbox("Handle_mount_a", (.024, .034, .028), (hx, -.278, 1.12), "SteelBrushed", .004)
        rbox("Handle_mount_b", (.024, .034, .028), (hx, -.278, .80), "SteelBrushed", .004)
        cyl("Handle_bar", (hx, -.292, .78), (hx, -.292, 1.14), .011, "SteelBrushed", 16, r=.003)
    for i in range(7):
        rbox(f"Vent_slot_{i}", (.30, .006, .008), (0, -.262, .22 + .02 * i), "PlasticGrey", .002)
    cyl("Lock_cylinder", (.022, -.262, .98), (.022, -.275, .98), .014, "Chrome", 16)
    front_marker()


BUILDERS = {"surgical_light": surgical_light, "operating_table": operating_table, "anaesthesia_unit": anaesthesia,
            "mayo_stand": mayo, "supply_cupboard": cupboard}

if __name__ == "__main__":
    assert bpy.app.version[:3] == (4, 5, 14)
    results = [pk.export(n, b, OUT, tri_budget=14000) for n, b in BUILDERS.items()]
    (OUT / "ot_validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("OT EQUIPMENT VALIDATION", json.dumps(results))
