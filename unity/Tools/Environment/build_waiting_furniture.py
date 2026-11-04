"""Corridor and reception furniture, Blender 4.5.14: connected perforated stainless-steel waiting bench (3 seats), patient stretcher, round side table.

No verified free, redistributable model of a perforated steel waiting bench was found (web search and ChatGPT, 3 Nov 2026; see docs/research/asset_sources_and_licenses.md), so these are original project models
built with the PatientBay prop kit. The bench shells carry a perforation pattern as an alpha-cut texture (PerfSteel material, 10 mm pitch, UV 0.1 m per tile) so the holes are real see-through openings without modelling thousands of
vertices; edges, armrests, legs and feet are real geometry. Metres, Z up, front faces -Y, origin on the floor.

  blender -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_waiting_furniture.py   ->  unity/Logs/RoomDressing/<id>.fbx + furniture_validation.json
"""
import json
import math
import sys
from pathlib import Path

import bpy

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import castor, tube  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = ROOT / "unity" / "Logs" / "RoomDressing"
geo.PALETTE.update({"PerfSteel": ((.72, .74, .76, 1), 1, .28), "FootCap": ((.05, .32, .40, 1), 0, .6), "VinylTeal": ((.07, .30, .33, 1), 0, .45)})


def rbox(part, size, loc, mat="PlasticWhite", r=.004, seg=2):
    return pk.rbox(part, size, loc, mat, r, seg)


def cyl(part, a, b, radius, mat="SteelBrushed", verts=24, r=0.0, seg=2):
    return pk.cyl(part, a, b, radius, mat, min(verts, 24), r, seg)


def world_uv(obj, tile=.1):
    """Cube-project UVs so 1 UV unit = `tile` metres: the perforation texture then keeps a true 10 mm hole pitch on every shell."""
    bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.cube_project(cube_size=tile); bpy.ops.object.mode_set(mode="OBJECT")


def shell(part, size, loc, tilt_deg=0, r=.012):
    """Perforated sheet shell with rolled edges: rounded slab, tilted about its centre."""
    o = rbox(part, size, loc, "PerfSteel", r, 3)
    if tilt_deg:
        o.rotation_euler = (math.radians(tilt_deg), 0, 0)
        bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    world_uv(o); return o


def bench():
    seat_w, gap = .50, .035
    total = 3 * seat_w + 2 * gap + .06
    x0 = -total / 2 + .03 + seat_w / 2
    for i in range(3):
        x = x0 + i * (seat_w + gap)
        shell(f"Seat{i}", (seat_w, .47, .018), (x, 0, .455), -3)
        shell(f"Back{i}", (seat_w, .018, .42), (x, .235, .70), -10)
        for dx in (-seat_w / 2 + .008, seat_w / 2 - .008):                                       # polished rolled side rims on seat and back
            cyl(f"Rim_seat{i}", (x + dx, -.235, .462), (x + dx, .235, .458), .009, "Chrome", 16)
            cyl(f"Rim_back{i}", (x + dx, .24, .50), (x + dx, .285, .91), .009, "Chrome", 16)
        cyl(f"Rim_front{i}", (x - seat_w / 2 + .008, -.235, .462), (x + seat_w / 2 - .008, -.235, .462), .009, "Chrome", 16)
        cyl(f"Rim_top{i}", (x - seat_w / 2 + .008, .285, .91), (x + seat_w / 2 - .008, .285, .91), .009, "Chrome", 16)
    # black structural beam under the seats, shared by all three
    rbox("Beam", (total - .05, .10, .07), (0, .06, .385), "RubberBlack", .012)
    rbox("Back_spine", (total - .12, .03, .08), (0, .27, .52), "RubberBlack", .008)
    for sx in (-1, 1):
        x = sx * (total / 2 - .09)
        # A-frame leg: splayed chromed tubes meeting the beam, with teal foot caps
        tube("Leg_front", [(x, -.04, .40), (x * 1.03, -.22, .20), (x * 1.04, -.30, .035)], .019, "Chrome", corner=.08)
        tube("Leg_rear", [(x, .14, .40), (x * 1.03, .30, .20), (x * 1.04, .36, .035)], .019, "Chrome", corner=.08)
        rbox("Leg_plate", (.06, .30, .035), (x, .05, .40), "Chrome", .01)
        for y in (-.30, .36):
            cyl("Foot_cap", (x * 1.04, y, 0), (x * 1.04, y, .045), .026, "FootCap", 16, r=.004)
        # curved armrest at each end
        ax = sx * (total / 2 - .02)
        tube("Armrest", [(ax, .22, .52), (ax, .20, .66), (ax, -.05, .70), (ax, -.20, .64), (ax, -.22, .50)], .017, "Chrome", corner=.10, res=12)
    geo.point("FRONT_MARKER", (0, -.3, 0))


def stretcher():
    L, W = 1.95, .70
    rbox("Base", (.62, 1.30, .05), (0, 0, .13), "SteelBrushed", .012)
    rbox("Column", (.18, .40, .34), (0, 0, .33), "PlasticWhite", .03)
    rbox("Deck", (W, L, .04), (0, 0, .64), "SteelBrushed", .01)
    rbox("Mattress_lower", (W - .02, 1.30, .09), (0, -.28, .705), "VinylTeal", .035, 4)
    rbox("Mattress_back", (W - .02, .62, .09), (0, .66, .735), "VinylTeal", .035, 4)
    rbox("Pillow", (.42, .26, .10), (0, .78, .81), "PlasticWhite", .04, 4)
    for sx in (-1, 1):
        x = sx * (W / 2 + .01)
        cyl("Rail_top", (x, -.78, .98), (x, .62, .98), .014, "Chrome", 16)
        for y in (-.70, -.10, .55):
            cyl("Rail_post", (x, y, .66), (x, y, .98), .012, "Chrome", 14)
        rbox("Rail_drop", (.02, .16, .04), (x, -.10, .76), "RubberBlack", .006)
    tube("Head_handle", [(-.30, .98, .68), (-.30, 1.06, .80), (.30, 1.06, .80), (.30, .98, .68)], .016, "Chrome", corner=.05)
    tube("Foot_handle", [(-.30, -.98, .68), (-.30, -1.06, .80), (.30, -1.06, .80), (.30, -.98, .68)], .016, "Chrome", corner=.05)
    for x, y in ((-.27, -.55), (.27, -.55), (-.27, .55), (.27, .55)):
        castor("Castor", x, y, 0.0, radius=.065, width=.032)
    cyl("IV_pole", (.33, .85, .66), (.33, .85, 1.75), .011, "Chrome", 14)
    for i in range(4):
        a = i * math.pi / 2; cyl(f"IV_hook{i}", (.33, .85, 1.75), (.33 + .08 * math.cos(a), .85 + .08 * math.sin(a), 1.70), .005, "Chrome", 8)
    rbox("Brake_pedal", (.20, .05, .02), (.2, -.42, .09), "AlarmRed", .006)
    geo.point("FRONT_MARKER", (0, -.3, 0))


def side_table():
    cyl("Top", (0, 0, .545), (0, 0, .575), .27, "Oak", 40, r=.006)
    cyl("Top_rim", (0, 0, .535), (0, 0, .545), .275, "SteelBrushed", 40)
    cyl("Stem", (0, 0, .04), (0, 0, .54), .028, "Chrome", 20)
    cyl("Base", (0, 0, 0), (0, 0, .04), .20, "Chrome", 36, r=.006)
    geo.point("FRONT_MARKER", (0, -.3, 0))


def wall_sconce():
    """Oval wall luminaire (hospital corridor uplight/downlight): flattened elliptical backplate and an opal ellipsoid diffuser with a brushed rim ring. Origin on the wall plane, front -Y."""
    def ellipsoid(part, radii, loc, mat):
        bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=1.0, location=loc)
        o = bpy.context.object; o.scale = radii
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        return pk._finish(o, part, mat, 0.0, 4, 40)
    ellipsoid("Backplate", (.105, .016, .245), (0, -.008, 0), "SteelBrushed")
    ellipsoid("Diffuser", (.078, .05, .205), (0, -.036, 0), "Diffuser")
    ellipsoid("Rim", (.09, .018, .225), (0, -.02, 0), "SteelBrushed")
    geo.point("FRONT_MARKER", (0, -.3, 0))


BUILDERS = {"wall_sconce": wall_sconce, "perforated_bench": bench, "patient_stretcher": stretcher, "round_side_table": side_table}

if __name__ == "__main__":
    assert bpy.app.version[:3] == (4, 5, 14)
    res = [pk.export(n, b, OUT, tri_budget=20000) for n, b in BUILDERS.items()]
    (OUT / "furniture_validation.json").write_text(json.dumps(res, indent=2) + "\n")
    print("FURNITURE VALIDATION", json.dumps(res))
