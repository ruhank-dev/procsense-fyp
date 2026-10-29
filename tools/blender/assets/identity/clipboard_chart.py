from procsense_bpy import box, point, rod


def build():
    # Sourced: the observation chart is A3 folded to A4, 210 x 297 mm on the board (ISO 216, docs/research/equipment/06-observation-chart.md).
    # The board (230 x 320 x 3 mm) and the clip are design sizes that hold an A4 chart; they are not sourced.
    box("Body", (.230, .320, .003), (0, 0, .0015), "Hardboard", .001)
    box("Chart", (.210, .297, .0008), (0, -.008, .0034), "Paper", .0002)
    box("ClipBase", (.10, .03, .006), (0, .145, .006), "Chrome", .001)
    box("ClipJaw", (.09, .02, .002), (0, .133, .0095), "Chrome", .0005)
    rod("ClipHinge", (-.045, .155, .009), (.045, .155, .009), .003, "SteelBrushed", 12)
    point("PIVOT_Clip", (0, .155, .009))
    point("SCREEN_Chart", (0, -.008, .0039))
    point("GRAB_Board", (0, -.15, .0015))
    point("COL_Body", (0, 0, .005), (.230, .320, .012))
