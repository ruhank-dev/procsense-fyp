"""PatientBay reconstruction props, part C: the workstation (monitor, keyboard, mouse, desk) at everyday-object fidelity.

Replaces the earlier charting_counter. Same asset name, node-material families and footprint; keycaps are individual tapered caps
in a sloped case, the mouse is a lofted ergonomic shell, the monitor has a slim bezel, a curved back and a rounded oval base.
"""
import math

import bmesh
import bpy
from mathutils import Vector

import prop_kit as pk
from prop_kit import cyl, extrude, lathe, rbox, rounded_rect, tube
import procsense_bpy as geo

geo.PALETTE.update({"Keycap": ((.035, .04, .045, 1), 0, .55), "KeyLegend": ((.62, .64, .66, 1), 0, .5)})


def _object_from_bmesh(name, bm, mat, bevel=0.0, angle=40, seg=3):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    return pk._finish(o, name, mat, bevel, seg, angle)


def keycap(x, y, z, w, d, h=.008, top=.74, mat="Keycap"):
    """Tapered cap: a frustum with a gentle dish, filleted on every edge."""
    bm = bmesh.new()
    res = bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        up = v.co.z > 0
        s = top if up else 1.0
        v.co.x = v.co.x * w * s + x
        v.co.y = v.co.y * d * (top + .06 if up else 1.0) + y
        v.co.z = (v.co.z + .5) * h + z
    return _object_from_bmesh("Key", bm, mat, min(.0013, h * .3), 36, 2)


def mouse(x, y, z, length=.112, width=.062, height=.038):
    """Ergonomic mouse shell: a squashed, egg-shaped dome with a flat base, split buttons and a scroll wheel."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=40, v_segments=20, radius=1.0)
    for v in bm.verts:
        u = -v.co.y                                                      # -1 front (away from the user) .. +1 back (palm end)
        egg = 1.0 - .10 * u                                               # wider toward the palm end
        v.co.x = v.co.x * width / 2 * egg
        v.co.y = v.co.y * length / 2
        zz = v.co.z * height
        hump = 1.0 + .22 * math.exp(-((u - .35) ** 2) / .18)             # palm hump toward the back
        v.co.z = max(zz * hump if zz > 0 else zz * .15, -.0)
        v.co.z = max(v.co.z, 0.0)
        v.co.x += x; v.co.y += y; v.co.z += z
    o = _object_from_bmesh("Mouse", bm, "PlasticGrey", 0.0, 55)

    def dome(yoff):                                                      # shell height above the base at x = 0, yoff toward the front
        u = -yoff / (length / 2)
        return height * math.sqrt(max(0.0, 1 - u * u)) * (1.0 + .22 * math.exp(-((u - .35) ** 2) / .18))
    yf = length * .20
    cyl("Mouse_wheel", (x - .0042, y + yf, z + dome(yf) + .0012), (x + .0042, y + yf, z + dome(yf) + .0012), .0075, "SteelBrushed", 18, r=.0008)
    return o


def workstation():
    # desk top with a thick, rounded front edge
    rbox("Laminate_top", (1.02, .46, .038), (0, 0, .98), "Ivory", .016)
    rbox("Top_edge_band", (1.025, .465, .008), (0, 0, .962), "PlasticWhite", .003)
    for x in (-.38, .38):
        tube("Wall_bracket", [(x, .20, .55), (x, .20, .95), (x, -.17, .95)], .018, "SteelBrushed", corner=.05)
    top_z = 1.0                                                          # desk surface
    # monitor: slim-bezel panel with a curved back shell, glass, chin, curved neck and a rounded oval foot
    rbox("Monitor", (.56, .020, .34), (0, .105, 1.36), "PlasticGrey", .008)
    rbox("Monitor_back", (.40, .042, .24), (0, .128, 1.37), "PlasticGrey", .018)
    rbox("Monitor_vent", (.12, .004, .010), (0, .152, 1.43), "PlasticGrey", .002)
    rbox("Inactive_display", (.536, .004, .314), (0, .093, 1.36), "ScreenOff", .002)
    rbox("Monitor_chin_logo", (.04, .003, .006), (0, .093, 1.205), "SteelBrushed", .001)
    rbox("Monitor_button", (.012, .004, .005), (.24, .093, 1.197), "SteelBrushed", .001)
    tube("Stand_neck", [(0, .135, 1.215), (0, .150, 1.12), (0, .130, 1.045), (0, .115, 1.012)], .016, "PlasticGrey", corner=.05)
    rbox("Stand_hinge", (.08, .03, .05), (0, .135, 1.215), "PlasticGrey", .012)
    extrude("Monitor_foot", rounded_rect(.26, .19, .085, 8), .012, (0, .115, top_z), "PlasticGrey", .004, plane="XY")
    # keyboard: sloped case, 87 individual keycaps (tenkeyless), feet and a coiled cable
    case = rbox("Keyboard", (.37, .135, .016), (-.02, -.105, top_z + .008), "PlasticGrey", .006)
    pk.tilt(case, -4.5, (-.02, -.17, top_z), "X")
    kz = lambda yy: top_z + .016 + (yy + .17) * math.tan(math.radians(4.5))   # key base height on the slope
    pitch = .0185
    rows = [(14, -.062, 0.0), (14, -.0865, 0.0), (14, -.105, .0), (13, -.1235, .0), (12, -.142, .0), (8, -.1605, .0)]
    x0 = -.02 - 6.5 * pitch
    for r, (n, yy, off) in enumerate(rows):
        if r < 5:
            for i in range(n):
                keycap(x0 + i * pitch, yy, kz(yy), .0162, .0162)
        else:                                                            # bottom row: 3 modifiers, a 6-unit space bar, 5 keys
            for i in (0, 1, 2, 9, 10, 11, 12, 13):
                keycap(x0 + i * pitch, yy, kz(yy), .0162, .0162)
            keycap(x0 + 5.5 * pitch, yy, kz(yy), 6 * pitch - .0023, .0162)
    for sx in (-.15, .11):
        rbox("Keyboard_foot", (.022, .008, .006), (sx, -.045, top_z + .002), "RubberBlack", .002)
    tube("Keyboard_cable", [(-.02, -.038, top_z + .016), (-.02, .02, top_z + .004), (-.08, .17, top_z + .0035), (-.08, .22, top_z + .0035)], .0026, "RubberBlack", corner=.05)
    mouse(.27, -.10, top_z)
    tube("Mouse_cable", [(.27, -.044, top_z + .012), (.27, .0, top_z + .0035), (.30, .06, top_z + .0035), (.34, .14, top_z + .0035)], .0021, "RubberBlack", corner=.04)
    tube("Monitor_cable", [(0, .165, 1.16), (0, .19, 1.04), (.08, .22, top_z + .004), (.22, .24, top_z + .004)], .0045, "RubberBlack", corner=.05)
    geo.point("FRONT_MARKER", (0, -.4, .5))
    return None


BUILDERS_C = {"charting_counter": workstation}
