from procsense_bpy import box, point, rod


def build():
    # ISO A4 page area; cover allowance and thickness are provisional visual choices.
    box("Body", (.226, .313, .002), (0, 0, .001), "AccentBlue", .0005)
    box("Pages", (.210, .297, .006), (.003, 0, .005), "PlasticWhite", .0002)
    box("Cover", (.226, .313, .002), (0, 0, .009), "AccentBlue", .0005)
    box("Spine", (.005, .313, .01), (-.111, 0, .005), "AccentBlue", .0005)
    for index, y in enumerate([-.09, 0, .09]):
        rod(f"Binder{index}", (-.104, y, .004), (-.092, y, .006), .0015, "Chrome", 12)
    point("PIVOT_Cover", (-.112, 0, .009))
    point("SCREEN_Notes", (.003, 0, .0085))
    point("LABEL_Cover", (0, 0, .0105))
    point("GRAB_Folder", (.08, 0, .005))
    point("COL_Body", (0, 0, .005), (.226, .313, .01))
