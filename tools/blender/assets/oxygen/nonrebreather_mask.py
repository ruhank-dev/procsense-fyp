from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes: mask as the simple mask, plus a 600 mL reservoir bag (about 12 x 4 x 22 cm, drawn full) and one-way valves.
    box("Body", (.14, .08, .11), (0, 0, .055), "PlasticClear", .02)
    box("Reservoir", (.12, .04, .22), (0, .08, -.07), "PlasticClear", .01)
    rod("Inlet", (0, .035, .02), (0, .06, .02), .009, "PlasticClear", 16)
    rod("Valve_L", (-.04, -.03, .07), (-.04, -.044, .07), .01, "RubberBlack", 12)
    rod("Valve_R", (.04, -.03, .07), (.04, -.044, .07), .01, "RubberBlack", 12)
    box("Nosepiece", (.03, .02, .008), (0, -.02, .112), "SteelBrushed", .001)
    box("Strap_L", (.012, .12, .004), (-.075, .06, .06), "PlasticWhite", .001)
    box("Strap_R", (.012, .12, .004), (.075, .06, .06), "PlasticWhite", .001)
    tube("Supply", [(0, .1, -.17), (0, .25, -.17), (.2, .4, -.17), (.5, .4, -.17)], .004, "PlasticClear")
    point("SOCKET_Face", (0, -.04, .055))
    point("ATTACH_O2", (.5, .4, -.17))
    point("GRAB_Mask", (0, 0, .055))
    point("COL_Mask", (0, .04, -.02), (.15, .12, .3))
