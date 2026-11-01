from procsense_bpy import box, point, rod


def build():
    # A wall-mounted pneumatic-tube station with its carrier. No reference sheet gives sizes (docs/research/equipment has none for the station):
    # every size here is a design size of a typical ward station, to be checked in the realism review (p0-evaluation-performance-s06).
    box("Body", (.30, .20, .45), (0, 0, 1.2), "PlasticWhite", .01)
    box("Door", (.22, .01, .28), (0, -.105, 1.18), "PlasticClear", .004)
    box("StatusPanel", (.10, .006, .05), (0, -.103, 1.38), "ScreenOff", .002)
    box("SendButton", (.03, .006, .03), (.1, -.103, 1.38), "AccentBlue", .004)
    rod("Carrier", (0, -.04, 1.08), (0, -.04, 1.30), .04, "PlasticClear", 24)
    rod("CarrierCapTop", (0, -.04, 1.30), (0, -.04, 1.32), .042, "AccentBlue", 24)
    rod("CarrierCapBottom", (0, -.04, 1.06), (0, -.04, 1.08), .042, "AccentBlue", 24)
    rod("Pipe", (0, .02, 1.425), (0, .02, 2.4), .055, "PlasticGrey", 24)
    point("PIVOT_Door", (-.11, -.105, 1.18))
    point("SOCKET_Carrier", (0, -.04, 1.19))
    point("SCREEN_Status", (0, -.107, 1.38))
    point("GRAB_Carrier", (0, -.04, 1.19))
    point("COL_Body", (0, 0, 1.2), (.30, .20, .45))
