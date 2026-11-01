from procsense_bpy import box, point, rod


def build():
    # ISO A4 page area; cover allowance and thickness are provisional visual choices.
    box("Body", (.226, .313, .002), (0, 0, .001), "AccentBlue", .0005)
    # the report pocket inside the back cover, under the pages (SOCKET_Report: a printed report is filed here)
    box("ReportPocket", (.200, .14, .0006), (.004, -.08, .0024), "PlasticClear", .0001)
    # six pages, each its own mesh so it can turn about its hinge (PIVOT_Page_n); each face is a live surface in Unity (SCREEN_Page_n)
    for i in range(6):
        z = .0032 + i * .0008
        box(f"Page{i}", (.210, .297, .0006), (.003, 0, z), "Paper", .0001)
        point(f"PIVOT_Page_{i}", (-.102, 0, z))
        point(f"SCREEN_Page_{i}", (.003, 0, z + .0004))
    for i, y in enumerate([.10, .03, -.04]):
        box(f"DividerTab{i}", (.012, .03, .0006), (.114, y, .0036 + i * .0016), "PlasticWhite", .0001)
    box("Cover", (.226, .313, .002), (0, 0, .009), "AccentBlue", .0005)
    box("Spine", (.005, .313, .01), (-.111, 0, .005), "AccentBlue", .0005)
    for index, y in enumerate([-.09, 0, .09]):
        rod(f"Binder{index}", (-.104, y, .004), (-.092, y, .006), .0015, "Chrome", 12)
    point("PIVOT_Cover", (-.112, 0, .009))
    point("SCREEN_Notes", (.003, 0, .0085))
    point("SOCKET_Report", (.004, -.08, .0028))
    point("LABEL_Cover", (0, 0, .0105))
    point("GRAB_Folder", (.08, 0, .005))
    point("COL_Body", (0, 0, .005), (.226, .313, .01))
