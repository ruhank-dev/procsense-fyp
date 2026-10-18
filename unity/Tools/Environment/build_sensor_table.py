"""Compact sensor table; preserves the existing .8425m return surface height."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_room_dressing as room

g, box = room.g, room.box
room.OUT = room.ROOT / "unity" / "Logs" / "SensorTableBuild"
room.OUT.mkdir(parents=True, exist_ok=True)


def sensor_table():
    box("Rounded_worktop", (.48, .43, .04), (0, 0, .8225), "Ivory", .012)
    box("Underside_frame", (.43, .38, .035), (0, 0, .785), "SteelBrushed", .006)
    # Low raised back/side edges leave the approach edge open for both sensors.
    box("Back_lip", (.47, .012, .025), (0, .208, .85), "Sage", .003)
    for x in [-.233, .233]:
        box("Side_lip", (.012, .40, .018), (x, .005, .85), "Ivory", .003)
    for x in [-.185, .185]:
        for y in [-.16, .16]:
            room.wheel(x, y, .035)
            g.rod("Stainless_leg", (x, y, .105), (x, y, .785), .012, "SteelBrushed", 12)
    box("Lower_shelf", (.40, .35, .022), (0, 0, .195), "Ivory", .007)
    for x in [-.185, .185]:
        g.rod("Shelf_support", (x, -.16, .172), (x, .16, .172), .01, "SteelBrushed", 12)
    box("Brake_tab", (.07, .028, .012), (.185, -.19, .08), "Sage", .002)


result = room.export("sensor_equipment_table", sensor_table)
(room.OUT / "validation.json").write_text(json.dumps(result, indent=2)+"\n")
print("SENSOR TABLE VALIDATION", json.dumps(result))
