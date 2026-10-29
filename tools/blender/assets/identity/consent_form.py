from procsense_bpy import box, point, rod


def build():
    # Sourced: A4 pages, 210 x 297 mm (ISO 216, docs/research/equipment/07-consent-form.md). Three stapled pages and the pen size are design choices.
    box("Body", (.210, .297, .0012), (0, 0, .0006), "Paper", .0002)
    for i in range(1, 3):
        box(f"PageEdge{i}", (.206, .293, .0001), (.001, -.001, .0004 * i), "Paper", .00002)
    rod("Staple", (-.095, .135, .0014), (-.085, .143, .0014), .0004, "Chrome", 8)
    rod("Pen", (.12, -.10, .005), (.12, .04, .005), .005, "AccentBlue", 16)
    rod("PenTip", (.12, -.10, .005), (.12, -.112, .005), .002, "Chrome", 12)
    point("SCREEN_Form", (0, 0, .0013))
    point("GRAB_Form", (-.09, -.13, .0006))
    point("GRAB_Pen", (.12, -.03, .005))
    point("COL_Body", (0, 0, .001), (.210, .297, .004))
    point("COL_Pen", (.12, -.036, .005), (.012, .152, .012))
