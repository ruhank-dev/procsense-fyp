from procsense_bpy import box, point, rod


def build():
    # Sourced overall size: 145 x 35 x 52 mm, height x width x depth (Braun ThermoScan 7 listing, docs/research/equipment/12-tympanic-thermometer.md).
    # The split between handle and head, the probe-cover cone and the buttons are design choices.
    box("Body", (.035, .052, .145), (0, 0, .0725), "PlasticWhite", .008)
    box("Display", (.024, .002, .018), (0, -.0265, .105), "ScreenOff", .0005)
    box("Button", (.014, .003, .008), (0, -.0265, .075), "AccentBlue", .001)
    rod("ProbeBase", (0, -.004, .145), (0, -.004, .157), .011, "PlasticWhite", 24)
    rod("ProbeTip", (0, -.004, .157), (0, -.004, .168), .005, "PlasticWhite", 24)
    rod("ProbeCover", (0, -.004, .150), (0, -.004, .166), .0075, "PlasticClear", 24)
    point("SCREEN_Temp", (0, -.0277, .105))
    point("GRAB_Handle", (0, 0, .05))
    point("ATTACH_Ear", (0, -.004, .168))
    point("COL_Body", (0, 0, .084), (.035, .052, .168))
