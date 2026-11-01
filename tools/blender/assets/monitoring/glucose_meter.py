from procsense_bpy import box, point, rod


def build():
    # Sourced: the meter is 76 x 48 x 16 mm (Accu-Chek Guide listing, docs/research/equipment/15-glucose-ketone-meter.md).
    # The lancet device and strip sizes are design choices (no listing was taken for them).
    box("Body", (.048, .076, .016), (0, 0, .008), "PlasticGrey", .004)
    box("Screen", (.036, .03, .001), (0, .012, .0162), "ScreenOff", .0003)
    box("Button", (.014, .008, .002), (0, -.02, .0165), "AccentBlue", .0008)
    box("StripPort", (.008, .002, .003), (0, -.038, .008), "RubberBlack", .0003)
    box("Strip", (.006, .03, .0006), (0, -.054, .008), "PlasticWhite", .0001)
    rod("Lancet", (.06, -.03, .008), (.06, .06, .008), .007, "PlasticWhite", 16)
    rod("LancetButton", (.06, .06, .008), (.06, .068, .008), .004, "AccentBlue", 12)
    point("SCREEN_Meter", (0, .012, .0168))
    point("ATTACH_Strip", (0, -.039, .008))
    point("GRAB_Meter", (0, 0, .008))
    point("GRAB_Lancet", (.06, .0, .008))
    point("COL_Body", (0, 0, .008), (.048, .076, .016))
    point("COL_Lancet", (.06, .019, .008), (.014, .098, .014))
