from procsense_bpy import box, point


def build():
    # Sourced: the same 279 x 25 mm adult strap as the wristband; red, with the identifiers in black on a white panel (NPSA SPN 24, docs/research/equipment/02-allergy-band.md).
    # The red is the institutional default the walkthrough names; a hospital may choose another colour (change the material, not the mesh).
    box("Body", (.279, .025, .001), (0, 0, .0005), "BandRed", .0002)
    box("AdhesiveClosure", (.025, .023, .0004), (.125, 0, .0012), "BandRed", .0001)
    box("LabelSurface", (.075, .022, .0002), (-.035, 0, .0011), "PlasticWhite", .00005)
    point("LABEL_Identity", (-.035, 0, .0013))
    point("SOCKET_Wrist", (0, 0, 0))
    point("GRAB_Band", (0, 0, .002))
    point("COL_Body", (0, 0, .001), (.279, .025, .004))
