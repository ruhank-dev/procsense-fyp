from procsense_bpy import box, point, rod


def build():
    # Sourced: the bottle is 127 mm tall (FDA 510(k) K161810); blue cap aerobic, purple anaerobic (docs/research/equipment/18-blood-culture-bottles.md).
    # The diameter is not published: 45 mm is a design size that fits an instrument vial station.
    for name, x, cap, suffix in (("Body", 0, "CultureBlue", ""), ("Anaerobic", .06, "CulturePurple", "_Anaerobic")):
        rod(name, (x, 0, 0), (x, 0, .107), .0225, "PlasticClear", 32)
        rod("Neck" + suffix, (x, 0, .107), (x, 0, .117), .012, "PlasticClear", 24)
        rod("Cap" + suffix, (x, 0, .117), (x, 0, .127), .014, cap, 24)
        rod("Broth" + suffix, (x, 0, .002), (x, 0, .045), .021, "BrothAmber", 32)
        box("Barcode" + suffix, (.03, .002, .05), (x, -.0228, .06), "Paper", .0003)
        point("LABEL_Bottle" + suffix, (x, -.024, .06))
        point("GRAB_Bottle" + suffix, (x, 0, .06))
    point("COL_Body", (0, 0, .0635), (.045, .045, .127))
    point("COL_Anaerobic", (.06, 0, .0635), (.045, .045, .127))
