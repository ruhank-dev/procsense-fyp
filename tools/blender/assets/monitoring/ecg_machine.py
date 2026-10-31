from procsense_bpy import box, point, rod, tube


def build():
    # Sourced: the machine is 205 x 310 x 405 mm, height x width x depth (Philips PageWriter TC30 listing); an electrode pad is about 38 x 44 mm (3M Red Dot 2670-3)
    # (docs/research/equipment/13-ecg-machine-and-electrodes.md). The printer slot, keyboard and lead layout are design choices; the printout width is not published (A4 used).
    box("Body", (.310, .405, .205), (0, 0, .1025), "PlasticWhite", .012)
    box("Screen", (.24, .004, .15), (0, -.2045, .125), "ScreenOff", .002)
    box("Keyboard", (.26, .12, .006), (0, -.11, .208), "PlasticGrey", .002)
    box("PrinterSlot", (.22, .03, .004), (0, .15, .207), "RubberBlack", .001)
    box("Printout", (.21, .12, .0005), (0, .23, .21), "Paper", .0001)
    rod("TrunkPort", (.155, .1, .06), (.175, .1, .06), .012, "PlasticGrey", 16)
    # ten lead wires (RA, LA, RL, LL, V1 to V6) from the trunk cable, each ending at ATTACH_Lead_n
    for i in range(10):
        x, y = .19 + (i % 5) * .035, .02 + (i // 5) * .06
        tube(f"Lead{i}", [(.175, .1, .06), (x, y, .03), (x, y - .05, .01)], .0015, "RubberBlack")
        point(f"ATTACH_Lead_{i + 1}", (x, y - .05, .01))
    for i in range(10):
        box(f"Electrode{i}", (.038, .044, .001), (-.02 + (i % 5) * .045, -.32 + (i // 5) * .05, .0005), "PlasticWhite", .0003)
        point(f"SOCKET_Electrode_{i + 1}", (-.02 + (i % 5) * .045, -.32 + (i // 5) * .05, .001))
    point("SCREEN_ECG", (0, -.207, .125))
    point("GRAB_Handle", (0, .19, .19))
    point("COL_Body", (0, 0, .1025), (.310, .405, .205))
