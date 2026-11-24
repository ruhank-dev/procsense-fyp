from procsense_bpy import box, point, rod


def build():
    # Design sizes: a portable transfer monitor, 24 x 10 x 18 cm with a carry handle and a 12 cm screen, clamped to a short pole that fixes to the trolley.
    # Origin at the bottom of the pole; the screen faces -Y (Unity turns it to face the porter).
    rod("Pole", (0, 0, 0), (0, 0, .50), .013, "SteelBrushed", 12)
    box("Clamp", (.05, .05, .04), (0, 0, .30), "PlasticGrey", .006)
    box("Body", (.24, .10, .18), (0, 0, .59), "PlasticGrey", .012)
    box("Screen", (.20, .004, .125), (0, -.052, .60), "ScreenOff", .001)
    box("Bezel", (.22, .003, .015), (0, -.0525, .525), "AccentBlue", .0005)
    rod("Handle", (-.07, 0, .68), (.07, 0, .68), .01, "RubberBlack", 12)
    rod("HandleL", (-.07, 0, .675), (-.07, 0, .70), .01, "RubberBlack", 10)
    rod("HandleR", (.07, 0, .675), (.07, 0, .70), .01, "RubberBlack", 10)
    rod("AlarmLight", (.0, 0, .68), (.0, 0, .685), .012, "AlarmRed", 12)
    point("SCREEN_Transport", (0, -.054, .60))
    point("ATTACH_Pole", (0, 0, 0))
    point("COL_Monitor", (0, 0, .35), (.26, .12, .72))
