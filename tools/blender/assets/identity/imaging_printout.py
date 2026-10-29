from procsense_bpy import box, point


def build():
    # The imaging printout: the same A4 sheet (ISO 216) on glossy photo paper; the image and its report are drawn live at SCREEN_Sheet in Unity.
    box("Body", (.210, .297, .0004), (0, 0, .0002), "PaperGloss", .0001)
    box("ImageArea", (.18, .2, .0001), (0, .03, .00045), "ScreenOff", .00002)
    point("SCREEN_Sheet", (0, .03, .0006))
    point("GRAB_Corner", (.09, -.13, .0004))
    point("COL_Body", (0, 0, .001), (.210, .297, .004))
