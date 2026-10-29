from procsense_bpy import box, point


def build():
    # Sourced: an A4 sheet, 210 x 297 mm (ISO 216, docs/research/equipment/21-results-display-and-lab-report.md). The slight curl of the top corner is a visual choice.
    # The report text is drawn live at SCREEN_Sheet in Unity, never baked.
    box("Body", (.210, .297, .0004), (0, 0, .0002), "Paper", .0001)
    box("CurlCorner", (.05, .05, .0004), (.08, .124, .0016), "Paper", .0001)
    point("SCREEN_Sheet", (0, 0, .0005))
    point("GRAB_Corner", (.09, -.13, .0004))
    point("COL_Body", (0, 0, .001), (.210, .297, .004))
