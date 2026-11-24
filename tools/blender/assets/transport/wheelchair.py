from procsense_bpy import box, point, rod, tube


def build():
    # Design sizes: a standard folding wheelchair, 58 cm wide over the arms, seat 46 cm wide and 46 cm deep at 50 cm high, back 45 cm tall,
    # 60 cm rear wheels, push handles behind. Blender Y is front to back, facing +Y; Unity's forward is the way the patient faces.
    box("Body", (.46, .46, .08), (0, 0, .50), "FabricNavy", .02)                         # seat cushion
    box("Backrest", (.46, .05, .42), (0, -.24, .78), "FabricNavy", .02)
    for side in (-1, 1):
        rod(f"Armrest{side}", (.27 * side, -.22, .72), (.27 * side, .12, .72), .02, "RubberBlack", 12)
        rod(f"ArmPost{side}", (.27 * side, .12, .50), (.27 * side, .12, .72), .015, "SteelBrushed", 10)
        rod(f"SideFrame{side}", (.25 * side, -.22, .46), (.25 * side, .24, .46), .014, "SteelBrushed", 10)
        rod(f"BackPost{side}", (.25 * side, -.24, .46), (.25 * side, -.26, 1.00), .014, "SteelBrushed", 10)
        rod(f"Push{side}", (.25 * side, -.26, 1.00), (.25 * side, -.38, 1.00), .019, "RubberBlack", 12)
        rod(f"Axle{side}", (.25 * side, -.12, .30), (.30 * side, -.12, .30), .012, "SteelBrushed", 10)
        rod(f"RearWheel{side}", (.30 * side - .012 * side, -.12, .30), (.30 * side + .012 * side, -.12, .30), .30, "RubberBlack", 28)
        rod(f"HandRim{side}", (.33 * side - .004, -.12, .30), (.33 * side + .004, -.12, .30), .25, "SteelBrushed", 28)
        rod(f"FrontFork{side}", (.22 * side, .26, .10), (.22 * side, .26, .46), .013, "SteelBrushed", 10)
        rod(f"Caster{side}", (.22 * side - .012, .26, .10), (.22 * side + .012, .26, .10), .10, "RubberBlack", 20)
        rod(f"FootStrut{side}", (.20 * side, .24, .44), (.18 * side, .46, .20), .013, "SteelBrushed", 10)
    box("Footplate", (.40, .16, .015), (0, .48, .185), "RubberBlack", .004)
    point("SOCKET_Seat", (0, 0, .54))
    point("GRAB_Wheelchair", (0, 0, .50))
    point("COL_Wheelchair", (0, -.02, .5), (.62, .95, 1.0))
