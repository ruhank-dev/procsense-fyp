"""Reception furniture for the corridor: a semi-circular desk (raised visitor ledge + lower worktop behind it, open at both ends so staff can walk behind), an office chair and a computer workstation.
Blender 4.5.14, same conventions as prop_kit (metres, Z up, front faces -Y, one mesh per material named EQ_<asset>_M_<Material>, closed geometry with UVs).

  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_reception.py

Desk frame: origin on the wall plane at the arc's centre, the wall behind (+Y), visitors in front (-Y). Outputs under unity/Logs/Reception (git-ignored). Dimensions are illustrative.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import cyl, rbox, tube  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = HERE.parents[1] / "Logs" / "Reception"
geo.PALETTE.update({
    "DeskQuartz": ((.90, .91, .90, 1), 0, .22), "DeskWhite": ((.86, .87, .86, 1), 0, .45), "DeskBlueGrey": ((.30, .38, .47, 1), 0, .55), "DeskTeal": ((.05, .30, .34, 1), 0, .4),
    "DeskKick": ((.04, .045, .05, 1), 0, .6), "ChairFabric": ((.07, .25, .29, 1), 0, .9), "ScreenOn": ((.02, .02, .03, 1), 0, .08), "KeyGrey": ((.22, .23, .25, 1), 0, .55),
})

R_OUT, R_LEDGE, R_WORK_IN = 1.62, 1.30, .78          # visitor face, inner face of the desk body, inner edge of the worktop
HALF = math.radians(50)                              # the arc spans +-50 degrees, leaving a ~0.9 m gap to the wall at each end
H_KICK, H_BAND, H_LEDGE, H_TOP = .09, .62, 1.04, 1.075
H_WORK = .735


def arc_point(r, a, z):
    return (r * math.sin(a), -r * math.cos(a), z)


def sector(part, r0, r1, a0, a1, z0, z1, mat, bevel=0.0, steps=48):
    """A closed ring-sector prism (rounded in plan view by `steps` segments), r0..r1, angles a0..a1 measured from -Y, heights z0..z1."""
    bm = bmesh.new()
    rings = []
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        rings.append([bm.verts.new(arc_point(r0, a, z0)), bm.verts.new(arc_point(r1, a, z0)), bm.verts.new(arc_point(r1, a, z1)), bm.verts.new(arc_point(r0, a, z1))])
    for i in range(steps):
        A, B = rings[i], rings[i + 1]
        for k in range(4):
            bm.faces.new((A[k], A[(k + 1) % 4], B[(k + 1) % 4], B[k]))
    bm.faces.new(rings[0]); bm.faces.new(list(reversed(rings[-1])))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(part, me); bpy.context.collection.objects.link(o); bpy.context.view_layer.objects.active = o
    return pk._finish(o, part, mat, bevel, 3)


def desk():
    a0, a1 = -HALF, HALF
    sector("Kick", R_LEDGE + .02, R_OUT - .075, a0 + .02, a1 - .02, 0, H_KICK, "DeskKick")
    sector("Lower_panel", R_LEDGE, R_OUT, a0, a1, H_KICK, H_BAND, "DeskBlueGrey", bevel=.004)
    sector("Accent_band", R_LEDGE, R_OUT + .004, a0, a1, H_BAND, H_BAND + .028, "DeskTeal", bevel=.002)
    sector("Upper_panel", R_LEDGE, R_OUT, a0, a1, H_BAND + .028, H_LEDGE, "DeskWhite", bevel=.004)
    sector("Ledge_top", R_LEDGE - .03, R_OUT + .025, a0 - .012, a1 + .012, H_LEDGE, H_TOP, "DeskQuartz", bevel=.008)
    sector("Worktop", R_WORK_IN, R_LEDGE - .03, a0 + .03, a1 - .03, H_WORK, H_WORK + .035, "DeskQuartz", bevel=.006)
    # pedestal drawer units under the worktop near each end, a chrome leg near the middle of the inner edge
    for sgn in (-1, 1):
        a = sgn * math.radians(34); r = 1.03
        x, y, _ = arc_point(r, a, 0)
        box = rbox("Pedestal", (.46, .52, H_WORK - .02), (x, y, (H_WORK - .02) / 2), "DeskBlueGrey", .006)
        box.rotation_euler[2] = -a
        for k in range(3):
            fx, fy, _ = arc_point(r - .265, a, 0)
            h = rbox("Drawer_line", (.40, .004, .005), (fx, fy, .12 + k * .2), "DeskKick", .001); h.rotation_euler[2] = -a
            hx, hy, _ = arc_point(r - .272, a, 0)
            g = cyl("Drawer_handle", (hx - .09 * math.cos(a), hy - .09 * math.sin(a), .17 + k * .2), (hx + .09 * math.cos(a), hy + .09 * math.sin(a), .17 + k * .2), .006, "SteelBrushed", 16)
    for sgn in (-1, 1):                                           # two chrome legs well to either side of where the knees go
        lx, ly, _ = arc_point(R_WORK_IN + .07, sgn * math.radians(21), 0)
        cyl("Worktop_leg", (lx, ly, 0), (lx, ly, H_WORK), .022, "SteelBrushed", 24, r=.003)
        rbox("Leg_foot", (.12, .12, .012), (lx, ly, .006), "SteelBrushed", .003)
    # sign: a slim brushed plate on the ledge's front, a recess line along the top edge
    rbox("Sign_plate", (.60, .006, .09), arc_point(R_OUT + .008, 0, .86), "SteelBrushed", .002)
    geo.point("COL_Desk", (0, -(R_LEDGE + R_OUT) / 2, H_TOP / 2), (2 * R_OUT * math.sin(HALF), R_OUT - R_LEDGE, H_TOP))
    geo.point("ANCHOR_Receptionist", (0, -.50, 0))
    geo.point("ANCHOR_Workstation", (0, -1.17, H_WORK + .035))
    geo.point("FRONT_MARKER", (0, -R_OUT - .3, 0))


def chair():
    """Office chair: five-star base with castors, gas lift, upholstered seat and back, two arms. Origin on the floor under the seat; front faces -Y."""
    for i in range(5):
        a = i * math.tau / 5 + .3
        x, y = .27 * math.cos(a), .27 * math.sin(a)
        tube(f"Star_leg{i}", [(0, 0, .105), (x * .5, y * .5, .095), (x, y, .075)], .014, "PlasticGrey", corner=.05, res=8)
        pk.castor(f"Castor{i}", x, y, 0.0, radius=.028, width=.02)
    cyl("Gas_lift", (0, 0, .09), (0, 0, .43), .026, "SteelBrushed", 24, r=.003)
    cyl("Lift_sleeve", (0, 0, .09), (0, 0, .27), .034, "PlasticGrey", 24, r=.004)
    rbox("Seat_pan", (.44, .43, .05), (0, 0, .44), "PlasticGrey", .012)
    rbox("Seat_cushion", (.45, .44, .07), (0, 0, .49), "ChairFabric", .025)
    cyl("Back_stem", (0, .18, .46), (0, .22, .66), .014, "PlasticGrey", 16)
    b = rbox("Back_cushion", (.42, .075, .48), (0, .235, .86), "ChairFabric", .028)
    b.rotation_euler[0] = math.radians(-6)
    for sgn in (-1, 1):
        cyl("Arm_post", (sgn * .235, .02, .5), (sgn * .235, .02, .64), .012, "PlasticGrey", 16)
        rbox("Arm_pad", (.06, .28, .035), (sgn * .235, .0, .66), "RubberBlack", .012)
    geo.point("FRONT_MARKER", (0, -.4, 0))


def workstation():
    """24-inch all-in-one display with a slim stand, keyboard, mouse and a desk telephone, laid out for a worktop at the origin. The display faces -Y."""
    rbox("Base", (.20, .18, .012), (0, .02, .006), "PlasticGrey", .004)
    rbox("Neck", (.05, .022, .24), (0, .045, .13), "PlasticGrey", .008)
    rbox("Neck_hinge", (.09, .03, .06), (0, .04, .245), "PlasticGrey", .01)
    rbox("Bezel", (.57, .022, .345), (0, 0, .31), "PlasticGrey", .006)
    rbox("Display", (.545, .002, .320), (0, -.0115, .31), "ScreenOn", .001)
    rbox("Chin_logo", (.03, .002, .006), (0, -.012, .1475 + .008), "SteelBrushed", .0006)
    rbox("Kbd_base", (.44, .15, .016), (0, -.30, .008), "KeyGrey", .006)
    for r in range(5):
        rbox(f"Kbd_row{r}", (.41 - (.012 if r in (0, 4) else 0), .022, .008), (0, -.355 + r * .027, .019), "PlasticGrey", .002)
    rbox("Space_bar", (.16, .02, .01), (0, -.253, .02), "PlasticGrey", .003)
    rbox("Mouse", (.062, .108, .036), (.34, -.30, .018), "KeyGrey", .016)
    rbox("Mouse_pad", (.20, .22, .004), (.34, -.30, .002), "RubberBlack", .002)
    rbox("Phone_base", (.17, .21, .05), (-.40, -.05, .025), "KeyGrey", .01)
    rbox("Phone_handset", (.07, .20, .035), (-.40, -.05, .068), "PlasticGrey", .012)
    for r in range(4):
        for c in range(3):
            rbox("Phone_key", (.022, .016, .006), (-.44 + c * .036 + .0, -.10 + r * .022, .053), "PlasticWhite", .002)
    geo.point("SCREEN_Display", (0, -.0125, .31))
    geo.point("FRONT_MARKER", (0, -.5, 0))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [("reception_desk", desk), ("reception_chair", chair), ("reception_workstation", workstation)]
    results = [pk.export(n, f, OUT, tri_budget=60000) for n, f in jobs]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("RECEPTION", json.dumps([(r["name"], r["triangles"], r["bounds_blender"]) for r in results]))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
