from procsense_bpy import box, point, rod


def build():
    # Sourced: the 60 mL pot is 33 mm across and 70 mm tall; a 10-parameter dipstick is 5 mm wide and about 110 mm long
    # (docs/research/equipment/19-urine-pot-dipstick-pregnancy-test.md). The pregnancy cassette size is not published: a design size is used.
    rod("Body", (0, 0, 0), (0, 0, .070), .0165, "PlasticClear", 32)
    rod("Lid", (0, 0, .058), (0, 0, .070), .0175, "PlasticWhite", 32)
    box("Dipstick", (.005, .110, .0006), (.05, 0, .0003), "PlasticWhite", .0001)
    # ten reagent pads, each its own mesh so Unity can colour it from the written result
    for i in range(10):
        box(f"Pad{i}", (.005, .005, .0004), (.05, -.045 + i * .008, .0008), "PadBlank", .0001)
    box("Cassette", (.02, .065, .005), (.09, 0, .0025), "PlasticWhite", .001)
    box("ResultWindow", (.006, .018, .0005), (.09, .008, .0052), "PadBlank", .0001)
    box("SampleWell", (.008, .008, .0005), (.09, -.02, .0052), "PlasticGrey", .0001)
    point("PIVOT_Lid", (0, 0, .058))
    point("LABEL_Pot", (0, -.0168, .03))
    point("GRAB_Pot", (0, 0, .03))
    point("GRAB_Dipstick", (.05, .05, .0006))
    point("GRAB_Cassette", (.09, -.025, .005))
    point("SCREEN_Result", (.09, .008, .0056))
    point("COL_Body", (0, 0, .035), (.035, .035, .070))
