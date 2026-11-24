from procsense_bpy import box, point, rod


def build():
    # Design sizes: a portable oxygen cylinder (CD size, about 14 cm across and 46 cm tall) with a regulator and flow dial, on a bracket.
    rod("Body", (0, 0, .02), (0, 0, .46), .07, "PlasticWhite", 24)
    rod("Shoulder", (0, 0, .46), (0, 0, .50), .035, "PlasticWhite", 20)
    box("Valve", (.06, .05, .05), (0, 0, .53), "Chrome", .006)
    rod("Dial", (0, -.04, .53), (0, -.065, .53), .028, "AccentBlue", 18)
    rod("Outlet", (.03, 0, .55), (.07, 0, .55), .008, "Chrome", 10)
    rod("Gauge", (0, .04, .53), (0, .06, .53), .024, "PlasticGrey", 18)
    box("Bracket", (.16, .16, .02), (0, 0, .02), "SteelBrushed", .004)
    point("ATTACH_O2", (.07, 0, .55))
    point("GRAB_Cylinder", (0, 0, .25))
    point("COL_Cylinder", (0, 0, .28), (.16, .16, .56))
