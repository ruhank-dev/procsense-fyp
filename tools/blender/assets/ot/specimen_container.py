from procsense_bpy import point, rod


def build():
    # Sourced overall size: 33 mm across, 70 mm tall with the cap (docs/research/equipment/52-specimen-container-and-label.md).
    # The split between pot (58 mm) and screw cap (12 mm), the thread band and the label size are design choices, not sourced.
    rod("Body", (0, 0, 0), (0, 0, .058), .0165, "PlasticClear", 32)
    rod("Thread", (0, 0, .051), (0, 0, .057), .0171, "PlasticClear", 32)
    rod("Lid", (0, 0, .058), (0, 0, .070), .0175, "PlasticWhite", 32)
    rod("Label", (0, 0, .013), (0, 0, .043), .0168, "PlasticWhite", 32)          # a paper band round the pot; the patient's details are drawn at LABEL_Specimen
    point("PIVOT_Lid", (0, 0, .058))
    point("LABEL_Specimen", (0, -.0171, .028))
    point("GRAB_Pot", (0, 0, .03))
    point("COL_Body", (0, 0, .035), (.035, .035, .070))
