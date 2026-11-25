from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes: a peripheral IV cannula kit on a small tray, 22 x 14 x 3 cm: a cannula (45 mm catheter with a coloured hub and wings),
    # a clear dressing, a tourniquet band and a sharps-safe cap. The cannula is the grab point; the tray holds the rest.
    box("Body", (.22, .14, .02), (0, 0, .01), "PlasticWhite", .004)                              # tray
    box("Rim_N", (.22, .008, .018), (0, .066, .028), "PlasticWhite", .002)
    box("Rim_S", (.22, .008, .018), (0, -.066, .028), "PlasticWhite", .002)
    rod("Catheter", (-.07, 0, .035), (-.02, 0, .035), .0022, "PlasticClear", 10)               # the 45 mm catheter, 18 G
    rod("Hub", (-.02, 0, .035), (.012, 0, .035), .0055, "NeedleGreen", 14)                       # the coloured hub (green = 18 G)
    box("WingL", (.016, .014, .002), (-.004, -.012, .031), "NeedleGreen", .0006)
    box("WingR", (.016, .014, .002), (-.004, .012, .031), "NeedleGreen", .0006)
    rod("Port", (.012, 0, .035), (.03, 0, .035), .004, "PlasticClear", 12)
    rod("Cap", (.03, 0, .035), (.04, 0, .035), .0045, "AccentBlue", 12)
    box("Dressing", (.07, .055, .0015), (.06, .035, .021), "PlasticClear", .0004)
    rod("Tourniquet", (.04, -.045, .026), (.095, -.045, .026), .007, "AccentBlue", 14)
    point("GRAB_Kit", (0, 0, .03))
    point("ATTACH_Arm", (-.07, 0, .035))
    point("COL_Kit", (0, 0, .025), (.23, .15, .06))
