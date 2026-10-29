from procsense_bpy import box, rod, tube, point, base


def build():
    base(1.12)
    box("Body", (.327, .182, .288), (0, 0, 1.29), bevel=.014)
    box("Bezel", (.300, .014, .230), (0, -.095, 1.30), "PlasticGrey", .009)
    box("Display", (.2611, .003, .1632), (-.008, -.104, 1.316), "ScreenOff", .001)
    for i in range(5):
        box(f"Key{i}", (.024, .006, .012), (-.095+i*.041, -.105, 1.198), "AccentBlue")
    rod("Dial", (.127, -.098, 1.23), (.127, -.116, 1.23), .016, "PlasticWhite", 24)
    tube("Handle", [(-.09, .01, 1.425), (-.09, .01, 1.485), (.09, .01, 1.485), (.09, .01, 1.425)], .011, "PlasticGrey")
    for i, port in enumerate(["ECG", "SpO2", "NIBP", "Temp"]):
        z = 1.21+i*.044
        rod("Port"+port, (-.164, -.02, z), (-.169, -.02, z), .011, "PlasticGrey")
        point("ATTACH_"+port, (-.173, -.02, z))
    for i in range(6):
        box(f"RearVent{i}", (.13, .003, .003), (0, .092, 1.26+i*.015), "PlasticGrey", .0005)
    box("BasketFloor", (.23, .16, .008), (0, .03, .79), "SteelBrushed")
    for x in [-.115, .115]:
        tube("BasketSide"+str(x), [(x,-.05,.79),(x,-.05,.89),(x,.11,.89),(x,.11,.79)], .004)
    for i in range(7):
        x = -.11+i*.0366
        rod(f"BasketWire{i}", (x,-.05,.79), (x,-.05,.89), .0025)
    rod("BasketRim", (-.115,-.05,.89), (.115,-.05,.89), .004)
    box("AlarmLight", (.06, .02, .012), (0, -.02, 1.44), "AlarmRed", .003)   # the alarm light on top; Unity lights it by priority
    point("LIGHT_Alarm", (0, -.02, 1.447))
    point("SCREEN_Main", (-.008, -.107, 1.316))
    point("GRAB_Handle", (0, .01, 1.485))
    point("COL_Body", (0, 0, 1.29), (.327, .182, .288))
