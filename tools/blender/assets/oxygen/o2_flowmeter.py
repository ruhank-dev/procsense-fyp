from procsense_bpy import box, point, rod


def build():
    # Design sizes: a wall outlet plate (8 x 8 cm) with a 15 cm flowmeter tube (ball float), a knob and a barbed outlet.
    box("Body", (.08, .03, .22), (0, 0, .11), "PlasticWhite", .004)
    rod("Tube", (0, -.02, .05), (0, -.02, .19), .011, "PlasticClear", 20)
    rod("Float", (0, -.02, .12), (0, -.02, .13), .0085, "Chrome", 14)
    box("Scale", (.01, .002, .13), (.018, -.033, .12), "PlasticGrey", .0005)
    rod("Knob", (0, -.025, .035), (0, -.045, .035), .016, "AccentBlue", 16)
    rod("Outlet", (0, -.02, .2), (0, -.04, .2), .007, "Chrome", 12)
    point("GRAB_Flowmeter", (0, 0, .11))
    point("PIVOT_Knob", (0, -.025, .035))
    point("ATTACH_O2", (0, -.04, .2))
    point("SCREEN_Flow", (0, -.034, .12))
    point("COL_Flowmeter", (0, -.01, .11), (.09, .08, .23))
