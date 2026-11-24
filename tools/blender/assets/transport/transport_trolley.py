import math
from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes (no listing taken): a hospital transport trolley, 62 cm wide and 190 cm long, mattress top at 83 cm, with side rails, a raised head end,
    # push handles at the foot end, four castors and an IV pole socket. Blender Y is the long axis, head end at +Y; Unity's forward is the head end.
    box("Body", (.62, 1.85, .10), (0, 0, .78), "AccentBlue", .03)                       # mattress
    box("Pillow", (.42, .26, .08), (0, .74, .87), "PlasticWhite", .03)
    box("Deck", (.60, 1.80, .04), (0, 0, .71), "SteelBrushed", .004)
    box("ChassisL", (.04, 1.50, .06), (-.24, 0, .50), "SteelBrushed", .004)
    box("ChassisR", (.04, 1.50, .06), (.24, 0, .50), "SteelBrushed", .004)
    box("CrossHead", (.52, .04, .05), (0, .66, .50), "SteelBrushed", .004)
    box("CrossFoot", (.52, .04, .05), (0, -.66, .50), "SteelBrushed", .004)
    for i, (sx, sy) in enumerate([(-1, 1), (1, 1), (-1, -1), (1, -1)]):
        rod(f"Leg{i}", (.24 * sx, .66 * sy, .12), (.24 * sx, .66 * sy, .69), .022, "SteelBrushed", 12)
        rod(f"Wheel{i}", (.24 * sx - .018, .66 * sy, .075), (.24 * sx + .018, .66 * sy, .075), .075, "RubberBlack", 20)
        rod(f"Fork{i}", (.24 * sx, .66 * sy, .075), (.24 * sx, .66 * sy, .14), .014, "SteelBrushed", 10)
    for side in (-1, 1):
        for k, y in enumerate((-.45, .45)):
            rod(f"RailPost{side}_{k}", (.33 * side, y, .76), (.33 * side, y, .98), .014, "SteelBrushed", 10)
        rod(f"RailTop{side}", (.33 * side, -.45, .98), (.33 * side, .45, .98), .016, "SteelBrushed", 10)
        rod(f"RailMid{side}", (.33 * side, -.45, .87), (.33 * side, .45, .87), .012, "SteelBrushed", 10)
    for side in (-1, 1):
        rod(f"Handle{side}", (.23 * side, -.93, .86), (.23 * side, -.93, 1.02), .017, "RubberBlack", 12)
    rod("HandleBar", (-.23, -.93, 1.02), (.23, -.93, 1.02), .017, "RubberBlack", 12)
    box("HeadBoard", (.58, .04, .22), (0, .94, .86), "AccentBlue", .01)
    box("FootBoard", (.58, .04, .18), (0, -.94, .84), "AccentBlue", .01)
    rod("PoleSocket", (.29, -.70, .74), (.29, -.70, .86), .02, "SteelBrushed", 12)
    point("GRAB_Trolley", (0, 0, .83))
    point("ATTACH_Pole", (.29, -.70, .86))
    point("COL_Trolley", (0, 0, .5), (.66, 1.95, 1.0))
