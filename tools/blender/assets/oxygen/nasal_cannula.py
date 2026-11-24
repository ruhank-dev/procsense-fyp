from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes: prongs 1 cm long, 2.2 cm apart, tubing looping over the ears to a 2 m supply line.
    box("Body", (.05, .02, .012), (0, 0, .006), "PlasticClear", .003)
    rod("Prong_L", (-.011, 0, .006), (-.011, 0, .02), .0035, "PlasticClear", 12)
    rod("Prong_R", (.011, 0, .006), (.011, 0, .02), .0035, "PlasticClear", 12)
    tube("Loop_L", [(-.025, 0, .006), (-.07, .02, .03), (-.085, .09, .0)], .0025, "PlasticClear")
    tube("Loop_R", [(.025, 0, .006), (.07, .02, .03), (.085, .09, .0)], .0025, "PlasticClear")
    box("Slide", (.02, .008, .008), (0, .06, -.04), "PlasticWhite", .001)
    tube("Supply", [(0, .06, -.04), (0, .25, -.1), (.2, .4, -.1), (.5, .4, -.1)], .0025, "PlasticClear")
    point("SOCKET_Face", (0, 0, .012))
    point("ATTACH_O2", (.5, .4, -.1))
    point("GRAB_Cannula", (0, 0, .006))
    point("COL_Cannula", (0, .04, 0), (.18, .1, .06))
