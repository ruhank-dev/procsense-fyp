from procsense_bpy import box, rod, point


def build():
    # Generic cabled clip. Overall nominal envelope 33 x 56 x 32 mm;
    # jaw thickness and hinge geometry are illustrative, not clinical fit criteria.
    box("Body", (.033,.056,.008), (0,0,.028), "PlasticGrey", .002)
    box("LowerJaw", (.031,.054,.004), (0,0,.002), "PlasticGrey", .001)
    box("UpperPad", (.022,.038,.003), (0,-.004,.0245), "RubberBlack", .001)
    box("LowerPad", (.022,.038,.003), (0,-.004,.0055), "RubberBlack", .001)
    rod("Hinge", (-.017,.023,.016), (.017,.023,.016), .012, "SteelBrushed")
    box("GripMark", (.016,.019,.001), (0,.010,.0325), "AccentBlue", .0002)
    point("GRAB_Probe", (0,0,.016))
    point("SOCKET_Finger", (0,-.006,.015))
    point("PIVOT_Jaw", (0,.023,.016))
    point("ATTACH_Cable", (0,.03,.016))
    point("COL_Body", (0,0,.016), (.05,.07,.04))
