from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes (no listing taken): an adult simple face mask is about 14 x 11 x 8 cm with a 2 m supply tube and an elastic strap.
    box("Body", (.14, .08, .11), (0, 0, .055), "PlasticClear", .02)
    rod("Inlet", (0, .035, .02), (0, .075, .02), .009, "PlasticClear", 16)
    rod("Vent_L", (-.04, -.03, .07), (-.04, -.042, .07), .007, "PlasticWhite", 12)
    rod("Vent_R", (.04, -.03, .07), (.04, -.042, .07), .007, "PlasticWhite", 12)
    box("Nosepiece", (.03, .02, .008), (0, -.02, .112), "SteelBrushed", .001)
    box("Strap_L", (.012, .12, .004), (-.075, .06, .06), "PlasticWhite", .001)
    box("Strap_R", (.012, .12, .004), (.075, .06, .06), "PlasticWhite", .001)
    tube("Supply", [(0, .075, .02), (0, .2, .02), (0, .35, .01), (.2, .5, .01), (.5, .5, .01)], .004, "PlasticClear")
    point("SOCKET_Face", (0, -.04, .055))
    point("ATTACH_O2", (.5, .5, .01))
    point("GRAB_Mask", (0, 0, .055))
    point("COL_Mask", (0, 0, .055), (.15, .1, .12))
