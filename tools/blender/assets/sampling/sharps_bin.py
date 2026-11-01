from procsense_bpy import box, point


def build():
    # Sourced: the 7 L bin is 248 mm long and 205 mm high with a 77 x 45 mm opening; yellow body, orange lid (Sharpsguard listings,
    # docs/research/equipment/33-sharps-bin.md). Its depth is not in the listing: 150 mm is a design size.
    box("Body", (.248, .150, .205), (0, 0, .1025), "BinYellow", .006)
    box("Lid", (.252, .154, .012), (0, 0, .209), "LidOrange", .003)
    box("Aperture", (.077, .045, .003), (.05, 0, .2165), "RubberBlack", .001)
    box("Flap", (.080, .048, .003), (.05, 0, .2195), "LidOrange", .001)
    box("FillLine", (.25, .002, .004), (0, -.076, .15), "RubberBlack", 0)
    point("PIVOT_Lid", (.05, .024, .219))
    point("SOCKET_Sharps", (.05, 0, .212))
    point("GRAB_Bin", (-.09, 0, .215))
    point("COL_Body", (0, 0, .1075), (.252, .154, .215))
