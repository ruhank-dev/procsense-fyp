import math
from procsense_bpy import box, rod, tube, point, base


def build():
    base(1.9)
    rod("AdjustmentCollar", (0,.045,1.35), (0,.045,1.43), .03, "PlasticGrey")
    rod("Clamp", (0,.045,1.08), (0,-.055,1.08), .031, "PlasticGrey")
    box("Body", (.214,.124,.068), (0,-.105,1.08), bevel=.007)
    box("Door", (.197,.01,.05), (0,-.172,1.08), "PlasticGrey", .004)
    box("Display", (.079,.002,.029), (-.041,-.178,1.083), "ScreenOff", .001)
    for i in range(4):
        box(f"Key{i}", (.011,.003,.011), (.015+i*.019,-.179,1.083), "AccentBlue", .002)
    box("DoorLatch", (.015,.008,.027), (.094,-.18,1.076), "PlasticWhite")
    for i in range(4):
        angle = math.tau*i/4
        x,y = math.cos(angle), math.sin(angle)
        tube(f"Hook{i}", [(0,.045,1.88),(.17*x,.045+.17*y,1.88),(.17*x,.045+.17*y,1.82),(.13*x,.045+.13*y,1.82)], .006, "Chrome")
        point(f"SOCKET_Bag_{i+1}", (.15*x,.045+.15*y,1.83))
    point("SCREEN_Pump", (-.041,-.18,1.083))
    point("PIVOT_Door", (-.098,-.172,1.08))
    point("GRAB_Pole", (0,.045,1.2))
    point("COL_Body", (0,-.105,1.08), (.214,.124,.068))
