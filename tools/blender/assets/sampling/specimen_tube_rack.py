from procsense_bpy import box, point, rod

# the tube types of content/investigations/investigations.json, with their cap colour (ISO 6710 convention, docs/research/equipment/17-specimen-tubes.md)
TUBES = [("edta_purple", "CapPurple"), ("serum_gold", "CapGold"), ("citrate_blue", "CapBlueLight"), ("grey_fluoride", "CapGrey"), ("pink_edta", "CapPink")]


def build():
    # Sourced: a rack of 102 x 102 x 57 mm for 13 mm tubes; tubes 13 x 75, 13 x 100 and 16 x 100 mm (diameter x length) (17-specimen-tubes.md).
    # One 13 x 100 mm tube of each type stands in the front row; the fill level is a separate mesh Unity scales (Fill_<type>).
    box("Body", (.102, .102, .057), (0, 0, .0285), "PlasticWhite", .003)
    for i, (kind, cap) in enumerate(TUBES):
        x = -.04 + i * .02
        rod(f"Tube_{kind}", (x, -.03, .02), (x, -.03, .112), .0065, "PlasticClear", 16)
        rod(f"Fill_{kind}", (x, -.03, .022), (x, -.03, .07), .0058, "BloodDark", 16)
        rod(f"Cap_{kind}", (x, -.03, .112), (x, -.03, .128), .0075, cap, 16)
        box(f"Label_{kind}", (.010, .0006, .05), (x, -.0368, .07), "Paper", .0001)
        point(f"GRAB_Tube_{kind}", (x, -.03, .09))
        point(f"LABEL_Tube_{kind}", (x, -.0385, .07))
        point(f"SOCKET_Rack_{i + 1}", (x, -.03, .02))
    point("GRAB_Rack", (0, .05, .03))
    point("COL_Body", (0, 0, .0285), (.102, .102, .057))
