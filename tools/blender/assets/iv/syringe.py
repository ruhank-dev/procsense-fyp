from procsense_bpy import box, point, rod


def build():
    # Design sizes: a 5 mL syringe, 11 cm long and 1.6 cm across the flange, with a needle (25 mm, orange hub for 25 G) and a plunger.
    rod("Body", (0, 0, .008), (.075, 0, .008), .0085, "PlasticClear", 20)                       # barrel
    rod("Fluid", (.004, 0, .008), (.05, 0, .008), .0066, "BrothAmber", 16)
    rod("Plunger", (.075, 0, .008), (.115, 0, .008), .0028, "PlasticWhite", 10)
    box("Thumb", (.004, .02, .016), (.117, 0, .008), "PlasticWhite", .001)
    box("Flange", (.003, .028, .01), (.074, 0, .008), "PlasticWhite", .001)
    rod("Tip", (-.008, 0, .008), (0, 0, .008), .0032, "PlasticClear", 10)
    rod("NeedleHub", (-.016, 0, .008), (-.008, 0, .008), .0042, "LidOrange", 12)
    rod("Needle", (-.04, 0, .008), (-.016, 0, .008), .0007, "Chrome", 8)
    point("GRAB_Syringe", (.045, 0, .008))
    point("ATTACH_Needle", (-.04, 0, .008))
    point("COL_Syringe", (.04, 0, .008), (.17, .03, .03))
