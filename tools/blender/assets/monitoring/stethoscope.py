import math
from procsense_bpy import point, rod, tube


def build():
    # Sourced: tube length 690 mm; diaphragm 43 mm and bell 35 mm across (Littmann Classic III listings, docs/research/equipment/14-stethoscope.md).
    # The headset spring, ear tips and the laid-out curve of the tube are design choices.
    rod("Body", (0, 0, 0), (0, 0, .012), .0215, "SteelBrushed", 32)             # the diaphragm side of the chest piece
    rod("Diaphragm", (0, 0, .012), (0, 0, .013), .0205, "PlasticGrey", 32)
    rod("Bell", (0, 0, -.012), (0, 0, 0), .0175, "SteelBrushed", 32)
    rod("Stem", (0, .02, .0), (0, .04, .0), .004, "SteelBrushed", 12)
    # the tube laid in a gentle S from the stem to the Y (about 690 mm along its curve)
    pts = [(0, .04 + .09 * i, .006 * math.sin(i)) for i in range(5)] + [(.04 * math.sin(i * .6), .40 + .07 * i, 0) for i in range(1, 5)]
    tube("Tube", pts, .005, "RubberBlack")
    y0 = pts[-1][1]
    for side in (-1, 1):
        tube(f"EarTube{side}", [(0, y0, 0), (side * .03, y0 + .06, 0), (side * .045, y0 + .14, .01)], .0025, "SteelBrushed")
        rod(f"EarTip{side}", (side * .045, y0 + .14, .01), (side * .045, y0 + .155, .012), .006, "RubberBlack", 16)
    point("GRAB_ChestPiece", (0, 0, 0))
    point("SOCKET_Neck", (0, y0, 0))
    point("COL_Body", (0, .3, 0), (.1, .7, .03))
