"""PatientBay reconstruction props, part B (sink, chair, workstation, clock, cabinet, overbed table, bins, dispensers).

Imported by build_bay_props.py; same conventions and node names as the shipped props (see prop_kit.py).
"""
import math

import bmesh
import bpy

import prop_kit as pk
from prop_kit import castor, cyl, extrude, lathe, rbox, rounded_rect, tube
import procsense_bpy as geo


def sink():
    rbox("Cabinet", (.82, .48, .73), (0, 0, .49), "Ivory", .010)
    rbox("Toe_kick", (.75, .41, .12), (0, .02, .095), "PlasticGrey", .006)
    for x in (-.20, .20):
        rbox("Door_reveal", (.39, .012, .65), (x, -.246, .49), "PlasticGrey", .002)
        rbox("Door", (.375, .022, .63), (x, -.258, .49), "Oak", .008)
        hx = x + (.15 if x < 0 else -.15)
        tube("Door_handle", [(hx, -.285, .70), (hx, -.285, .56)], .007, "SteelBrushed")
    rbox("Counter_left", (.14, .58, .045), (-.38, 0, .885), "PlasticWhite", .014)
    rbox("Counter_right", (.14, .58, .045), (.38, 0, .885), "PlasticWhite", .014)
    for y in (-.235, .235):
        rbox("Counter_rim", (.64, .11, .045), (0, y, .885), "PlasticWhite", .014)
    vertices = []
    for x, y, z in [(.31, .17, .905), (.30, .16, .900), (.225, .11, .77), (.235, .12, .755)]:
        vertices.extend([(-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)])
    faces = []
    for i in range(4):
        j = (i + 1) % 4
        faces.extend([(i, j, j + 4, i + 4), (i + 4, j + 4, j + 8, i + 8), (i + 12, j + 12, j, i)])
    faces.extend([(8, 9, 10, 11), (15, 14, 13, 12)])
    mesh = bpy.data.meshes.new("Continuous basin")
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new(); bm.from_mesh(mesh); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(mesh); bm.free()
    obj = bpy.data.objects.new("Bowl", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    thick = obj.modifiers.new("Wall thickness", "SOLIDIFY")                 # a real basin wall, closed all round
    thick.thickness = .004
    thick.offset = 1
    bpy.ops.object.modifier_apply(modifier=thick.name)
    pk._finish(obj, "Continuous_bowl", "SteelBrushed", 0.0, angle=60)
    lathe("Drain", [(.032, 0), (.032, .003), (.018, .005), (.018, .007)], (0, 0, .771), "Chrome", verts=24)
    lathe("Drain_centre", [(.009, 0), (.009, .008)], (0, 0, .777), "PlasticGrey", verts=16)
    rbox("Splashback", (.90, .025, .40), (0, .27, 1.08), "PlasticWhite", .010)
    cyl("Mixer_body", (0, .215, .905), (0, .215, .985), .026, "Chrome", 28, r=.004)
    tube("Mixer_neck", [(0, .215, .98), (0, .215, 1.12), (0, .09, 1.14), (0, .05, 1.10)], .014, "Chrome", corner=.05)
    cyl("Mixer_nozzle", (0, .05, 1.10), (0, .05, 1.05), .019, "Chrome", 24, r=.004)
    tube("Elbow_lever", [(.02, .215, 1.00), (.12, .215, 1.03), (.20, .215, 1.09)], .0085, "Chrome", corner=.03)
    cyl("Lever_grip", (.20, .215, 1.09), (.245, .215, 1.12), .014, "PlasticGrey", 20, r=.004)
    rbox("Trap_cover", (.12, .12, .22), (0, .16, .60), "PlasticGrey", .012)


def visitor_chair():
    for x in (-.23, .23):
        for y in (-.21, .21):
            cyl("Foot_cap", (x, y, .0), (x, y, .035), .021, "RubberBlack", 24, r=.006)
    rbox("Seat_pan", (.50, .47, .02), (0, 0, .455), "PlasticGrey", .006)
    rbox("Seat", (.52, .48, .075), (0, .0, .50), "Upholstery", .03)
    back = rbox("Back_pad", (.50, .075, .36), (0, .235, .78), "Upholstery", .028)
    pk.tilt(back, -9, (0, .235, .60))
    for x in (-.235, .235):
        tube("Side_frame", [(x, -.21, .03), (x, -.21, .50), (x, -.20, .66), (x, .15, .68)], .0155, "SteelBrushed", corner=.07)
        tube("Rear_leg", [(x, .21, .03), (x, .215, .46), (x, .235, .62), (x, .255, .90)], .0155, "SteelBrushed", corner=.08)
        rbox("Arm_pad", (.05, .30, .032), (x, -.03, .685), "PlasticGrey", .014)
    for y in (-.21, .21):
        cyl("Cross_rail", (-.235, y, .17), (.235, y, .17), .0105, "SteelBrushed", 16, r=.002)
    rbox("Seat_bracket_front", (.46, .02, .03), (0, -.215, .435), "SteelBrushed", .005)
    geo.point("FRONT_MARKER", (0, -.4, .5))
    geo.point("COL_Chair", (0, 0, .45), (.52, .48, .9))


def charting_counter():
    rbox("Laminate_top", (1.02, .46, .038), (0, 0, .98), "Ivory", .014)
    rbox("Top_edge_band", (1.025, .465, .008), (0, 0, .962), "PlasticWhite", .003)
    for x in (-.38, .38):
        tube("Wall_bracket", [(x, .20, .55), (x, .20, .95), (x, -.17, .95)], .018, "SteelBrushed", corner=.05)
    rbox("Monitor", (.50, .028, .31), (0, .105, 1.325), "PlasticGrey", .010)
    rbox("Monitor_back", (.30, .045, .20), (0, .130, 1.32), "PlasticGrey", .018)
    rbox("Inactive_display", (.47, .004, .276), (0, .089, 1.325), "ScreenOff", .002)
    rbox("Stand_neck", (.07, .035, .18), (0, .125, 1.12), "PlasticGrey", .014)
    lathe("Monitor_foot", [(.115, 0), (.115, .008), (.09, .014)], (0, .11, 1.0), "PlasticGrey", verts=40)
    rbox("Keyboard", (.40, .15, .018), (-.04, -.11, 1.008), "PlasticGrey", .006)
    rows = [(10, -.205), (10, -.205), (10, -.2), (9, -.19)]
    for j, (n, x0) in enumerate(rows):
        for i in range(n):
            rbox("Key", (.026, .022, .006), (x0 + i * .032, -.165 + j * .029, 1.019), "SteelBrushed", .003)
    rbox("Space_bar", (.17, .022, .006), (-.04, -.054, 1.019), "SteelBrushed", .003)
    rbox("Mouse", (.056, .092, .032), (.28, -.09, 1.020), "PlasticGrey", .020)
    cyl("Mouse_wheel", (.28, -.075, 1.037), (.28, -.065, 1.037), .0045, "SteelBrushed", 12)
    tube("Mouse_cable", [(.28, -.136, 1.014), (.28, -.19, 1.0), (.30, -.215, .985)], .0022, "RubberBlack", corner=.04)
    tube("Keyboard_cable", [(-.04, -.036, 1.008), (-.04, .03, 1.0), (-.10, .20, .985), (-.10, .24, .985)], .0028, "RubberBlack", corner=.05)
    tube("Monitor_cable", [(0, .15, 1.30), (0, .18, 1.05), (.08, .22, .995), (.20, .24, .985)], .004, "RubberBlack", corner=.05)
    geo.point("FRONT_MARKER", (0, -.4, .5))


def wall_clock():
    lathe("Rim", [(.205, -.014), (.205, .014), (.196, .026), (.19, .026), (.19, .008)], (0, 0, 0), "PlasticGrey", axis="Y", verts=64)
    lathe("Face", [(.19, 0), (.19, .004)], (0, -.008, 0), "PlasticWhite", axis="Y", verts=64)
    for i in range(60):
        a = i * math.tau / 60
        major = i % 5 == 0
        r0, r1 = (.152, .180) if major else (.168, .180)
        w = .005 if major else .0025
        cyl("Mark", (r0 * math.sin(a), -.014, r0 * math.cos(a)), (r1 * math.sin(a), -.014, r1 * math.cos(a)), w, "PlasticGrey", 6)
    cyl("Hour_hand", (0, -.020, 0), (-.095, -.020, .055), .0075, "PlasticGrey", 10)
    cyl("Minute_hand", (0, -.023, 0), (.13, -.023, .075), .005, "PlasticGrey", 10)
    cyl("Second_hand", (0, -.026, 0), (.03, -.026, -.12), .0022, "AlarmRed", 8)
    lathe("Centre", [(.011, 0), (.011, .004), (.007, .007)], (0, -.034, 0), "PlasticGrey", axis="Y", verts=24)
    geo.point("FRONT_MARKER", (0, -.4, 0))


def bumper():
    rbox("Wall_spacer", (1.2, .045, .11), (0, .025, 0), "PlasticGrey", .008)
    rbox("Protective_rail", (1.3, .055, .15), (0, -.02, 0), "Ivory", .024)
    rbox("Rail_groove", (1.24, .008, .012), (0, -.047, 0), "PlasticGrey", .003)
    for x in (-.65, .65):
        rbox("End_cap", (.04, .065, .15), (x, -.02, 0), "Sage", .016)


def sharps():
    rbox("Wall_bracket", (.23, .025, .24), (0, .10, -.025), "SteelBrushed", .008)
    rbox("Container", (.24, .21, .29), (0, 0, -.015), "Amber", .030)
    rbox("Lid", (.26, .23, .035), (0, 0, .145), "Amber", .014)
    rbox("Restricted_aperture", (.11, .07, .012), (0, -.035, .166), "PlasticGrey", .006)
    rbox("Lock_tab", (.05, .03, .022), (0, -.115, .15), "PlasticWhite", .008)
    rbox("Label", (.16, .006, .10), (0, -.109, -.025), "PlasticWhite", .002)
    for a, b in [((-.035, -.115, -.05), (.035, -.115, -.05)), ((.035, -.115, -.05), (0, -.115, .015)), ((0, -.115, .015), (-.035, -.115, -.05))]:
        geo.rod("Warning_border", a, b, .004, "PlasticGrey", 8)


def gloves():
    rbox("Wall_frame", (.42, .075, .29), (0, 0, 0), "SteelBrushed", .014)
    for x in (-.135, 0, .135):
        rbox("Box", (.124, .065, .245), (x, -.045, 0), "Sage", .016)
        rbox("Opening", (.076, .008, .09), (x, -.083, 0), "PlasticGrey", .014)
        rbox("Glove_fold", (.055, .012, .055), (x, -.09, -.015), "PlasticWhite", .014)
    cyl("Lower_retainer", (-.22, -.09, -.10), (.22, -.09, -.10), .006, "SteelBrushed", 12)


def towels():
    rbox("Housing", (.29, .13, .33), (0, 0, 0), "Ivory", .028)
    rbox("Cover_seam", (.28, .006, .004), (0, -.066, .06), "PlasticGrey", .001)
    rbox("Level_window", (.035, .006, .14), (.09, -.069, -.01), "PlasticGrey", .003)
    rbox("Outlet", (.22, .09, .018), (0, -.02, -.17), "PlasticGrey", .006)
    rbox("Paper_sheet", (.18, .008, .11), (0, -.04, -.215), "PlasticWhite", .001)
    cyl("Lock", (-.10, -.069, .12), (-.10, -.075, .12), .011, "SteelBrushed", 20, r=.002)


def cabinet():
    rbox("Body", (.48, .46, .72), (0, 0, .49), "Ivory", .014)
    rbox("Oak_top", (.52, .50, .035), (0, 0, .8825), "Oak", .012)
    rbox("Oak_top_edge", (.524, .504, .008), (0, 0, .862), "PlasticWhite", .003)
    rbox("Drawer_reveal", (.444, .005, .21), (0, -.234, .735), "PlasticGrey", .002)
    rbox("Drawer_face", (.428, .022, .195), (0, -.243, .738), "Sage", .008)
    rbox("Cupboard_reveal", (.444, .006, .47), (0, -.235, .389), "PlasticGrey", .002)
    rbox("Cupboard_face", (.428, .022, .445), (0, -.244, .389), "Ivory", .008)
    for z in (.77, .545):
        rbox("Recessed_handle", (.17, .007, .026), (0, -.259, z), "PlasticGrey", .006)
        cyl("Handle", (-.08, -.273, z), (.08, -.273, z), .006, "SteelBrushed", 14, r=.002)
    rbox("Lower_plinth", (.41, .40, .035), (0, 0, .12), "PlasticGrey", .006)
    for x in (-.185, .185):
        for y in (-.17, .17):
            castor("Castor", x, y, 0.0, radius=.04, width=.024)
    geo.point("FRONT_MARKER", (0, -.4, .5))


def tablet_table():
    extrude("Rounded_top", rounded_rect(.62, .40, .05, 8), .035, (0, .30, 1.0075), "Oak", .012, plane="XY")
    rbox("Underside", (.57, .35, .018), (0, .30, .998), "Ivory", .008)
    cyl("Top_support", (0, .10, .988), (0, .40, .988), .018, "SteelBrushed", 24, r=.003)
    cyl("Tablet_mount", (0, -.015, 1.00), (0, -.015, 1.18), .012, "SteelBrushed", 20, r=.003)
    rbox("Tablet_backing", (.575, .016, .39), (0, -.020, 1.15), "PlasticGrey", .014)
    rbox("Telescopic_column", (.06, .065, .73), (0, .10, .49), "SteelBrushed", .008)
    rbox("Upper_column", (.045, .048, .23), (0, .10, .88), "PlasticWhite", .008)
    rbox("Adjustment_collar", (.085, .09, .065), (0, .10, .77), "Sage", .012)
    cyl("Adjustment_knob", (.042, .10, .77), (.075, .10, .77), .021, "PlasticGrey", 24, r=.004)
    for x in (-.245, .245):
        rbox("Base_rail", (.035, .40, .032), (x, 0, .078), "SteelBrushed", .008)
    cyl("Base_crossbar", (-.245, .10, .082), (.245, .10, .082), .019, "SteelBrushed", 24, r=.003)
    for x in (-.245, .245):
        for y in (-.16, .16):
            castor("Castor", x, y, 0.0, radius=.032, width=.022)
    geo.point("FRONT_MARKER", (0, -.35, .5))
    geo.point("COL_Top", (0, .30, 1.025), (.62, .40, .035))
    geo.point("COL_Base", (0, 0, .07), (.54, .43, .14))


def pedal_bin():
    rbox("Pedal_bin", (.32, .30, .43), (0, 0, .245), "Ivory", .045)
    rbox("Lid", (.34, .32, .035), (0, 0, .475), "Amber", .016)
    cyl("Hinge", (-.09, .15, .46), (.09, .15, .46), .022, "PlasticGrey", 24, r=.004)
    rbox("Lid_grip", (.10, .03, .025), (0, -.165, .485), "PlasticGrey", .008)
    rbox("Pedal", (.14, .09, .025), (0, -.18, .045), "PlasticGrey", .010)
    tube("Pedal_rod", [(0, -.15, .06), (0, -.15, .18)], .006, "SteelBrushed", corner=.01)
    rbox("Base", (.30, .28, .035), (0, 0, .025), "PlasticGrey", .010)


def handrub():
    rbox("Mount", (.16, .025, .32), (0, .02, 0), "SteelBrushed", .010)
    rbox("Dispenser", (.125, .10, .225), (0, -.035, .015), "Ivory", .024)
    rbox("Level_window", (.028, .006, .10), (0, -.089, .01), "Sage", .004)
    rbox("Press_lever", (.10, .08, .025), (0, -.083, -.105), "PlasticGrey", .010)
    rbox("Nozzle", (.03, .05, .02), (0, -.1, -.13), "PlasticGrey", .008)
    rbox("Drip_tray", (.16, .15, .015), (0, -.05, -.19), "PlasticGrey", .006)


# Ceiling track of the bed's privacy curtain: a U that surrounds the bed on three sides (head wall is the fourth). Waypoints are room coordinates (x, z); the fabric follows the SAME filleted path.
TRACK_PTS = [(-1.30, 1.80), (-1.30, -.78), (1.52, -.78), (1.52, 1.80)]
TRACK_CORNER = .30
TRACK_Z = 2.48
CURTAIN_HEIGHT = 2.13
CURTAIN_HEM = .23


def track_path(res=24):
    """Dense polyline of the track centre line using the same quadratic fillet as prop_kit.tube. Returns [(x, y)] and cumulative arc length."""
    import numpy as np
    P = [np.array(p, dtype=float) for p in TRACK_PTS]
    out = [P[0]]
    for i in range(1, len(P) - 1):
        a, b, c = P[i - 1], P[i], P[i + 1]
        da, dc = a - b, c - b
        k = min(TRACK_CORNER, np.linalg.norm(da) / 2.2, np.linalg.norm(dc) / 2.2)
        p0 = b + da / np.linalg.norm(da) * k
        p2 = b + dc / np.linalg.norm(dc) * k
        for t in range(res + 1):
            s = t / res
            out.append((1 - s) ** 2 * p0 + 2 * (1 - s) * s * b + s * s * p2)
    out.append(P[-1])
    out = np.array(out)
    # densify straight stretches so the fabric bends smoothly
    dense = [out[0]]
    for q in out[1:]:
        prev = dense[-1]
        n = max(1, int(np.linalg.norm(q - prev) / .05))
        for j in range(1, n + 1):
            dense.append(prev + (q - prev) * j / n)
    dense = np.array(dense)
    seg = np.linalg.norm(np.diff(dense, axis=0), axis=1)
    return dense, np.concatenate([[0.0], np.cumsum(seg)])


def privacy_curtain():
    """U-shaped ceiling track (filleted corners, hangers), and one long pleated single-sheet curtain bent along it (pleated cloth from curtain_gen). The fabric is the CLOSED shape: Unity's CurtainCloth gathers it toward the head-left end when the curtain is opened."""
    import numpy as np
    import curtain_gen as cg
    tube("Ceiling_track", [(x, z, TRACK_Z) for x, z in TRACK_PTS], .018, "SteelBrushed", corner=TRACK_CORNER)
    for x, y in [(-1.30, 1.62), (-1.30, .45), (-1.30, -.62), (-.75, -.78), (.75, -.78), (1.42, -.78), (1.52, .45), (1.52, 1.62)]:
        cyl("Ceiling_hanger", (x, y, TRACK_Z), (x, y, 2.76), .009, "SteelBrushed", 16, r=.002)
        cyl("Ceiling_rose", (x, y, 2.745), (x, y, 2.765), .028, "PlasticWhite", 24, r=.004)
    for x, y in [(-1.30, 1.80), (1.52, 1.80)]:                            # end stops
        cyl("Track_end_stop", (x, y, TRACK_Z - .02), (x, y, TRACK_Z + .02), .022, "PlasticWhite", 20, r=.004)
    dense, S = track_path()
    L = float(S[-1])
    import json, pathlib
    out_dir = pathlib.Path(__file__).resolve().parents[2] / "Logs" / "BayRebuild"; out_dir.mkdir(parents=True, exist_ok=True)
    (out_dir / "privacy_curtain_path.json").write_text(json.dumps({"length": L, "points": [[round(float(x), 4), round(float(y), 4)] for x, y in dense]}))
    pleats = int(round(L * 7.6))                                           # wider, softer pleats than the old gathered stack; ten samples per pleat keeps them round
    nu = pleats * 10 + 1
    verts, norms, uv, uv2, tris = cg.make(L, CURTAIN_HEIGHT, pleats, .05, nu=nu, nv=18, seed=5, heading=.5, single_sided=True)
    # bend the strip along the path: width -> arc length, canonical Z (toward the room) -> the path normal pointing at the bed
    s = np.clip(verts[:, 0], 0, L)
    px = np.interp(s, S, dense[:, 0]); py = np.interp(s, S, dense[:, 1])
    d = dense[1:] - dense[:-1]
    tang = d / np.linalg.norm(d, axis=1, keepdims=True)
    tang = np.vstack([tang, tang[-1]])
    tx = np.interp(s, S, tang[:, 0]); ty = np.interp(s, S, tang[:, 1])
    tl = np.hypot(tx, ty); tx /= tl; ty /= tl
    nx, ny = -ty, tx                                                       # left of travel (travel is head-left -> foot -> head-right, so left is the bed side)
    P = np.stack([px + nx * verts[:, 2], py + ny * verts[:, 2], CURTAIN_HEM + verts[:, 1]], -1)
    N = np.stack([tx * norms[:, 0] + nx * norms[:, 2], ty * norms[:, 0] + ny * norms[:, 2], norms[:, 1]], -1)
    me = bpy.data.meshes.new("Curtain")
    me.from_pydata(P.tolist(), [], np.asarray(tris).reshape(-1, 3).tolist())
    me.update()
    uvl = me.uv_layers.new(name="UVMap")
    uvl2 = me.uv_layers.new(name="Lightmap")
    for li, loop in enumerate(me.loops):
        uvl.data[li].uv = uv[loop.vertex_index]
        uvl2.data[li].uv = uv2[loop.vertex_index]
    obj = bpy.data.objects.new("Curtain", me)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me); bm.free()
    obj.name = f"EQ_{geo.ASSET}_Curtain"
    obj.data.materials.append(geo.material("Curtain"))
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(70))


def sensor_table():
    """Compact bedside sensor/tray table; keeps the .8425 m return surface height."""
    rbox("Rounded_worktop", (.48, .43, .04), (0, 0, .8225), "Ivory", .016)
    rbox("Underside_frame", (.43, .38, .035), (0, 0, .785), "SteelBrushed", .008)
    rbox("Back_lip", (.47, .012, .025), (0, .208, .85), "Sage", .005)
    for x in (-.233, .233):
        rbox("Side_lip", (.012, .40, .018), (x, .005, .85), "Ivory", .004)
    for x in (-.185, .185):
        for y in (-.16, .16):
            cyl("Stainless_leg", (x, y, .105), (x, y, .785), .012, "SteelBrushed", 24, r=.002)
            castor("Castor", x, y, 0.0, radius=.035, width=.022)
    rbox("Lower_shelf", (.40, .35, .022), (0, 0, .195), "Ivory", .010)
    for x in (-.185, .185):
        cyl("Shelf_support", (x, -.16, .172), (x, .16, .172), .01, "SteelBrushed", 16, r=.002)
    rbox("Brake_tab", (.07, .028, .012), (.185, -.19, .08), "Sage", .004)
    tube("Push_rail", [(-.185, .16, .785), (-.185, .22, .80), (.185, .22, .80), (.185, .16, .785)], .008, "SteelBrushed", corner=.03)


BUILDERS_B = {
    "sensor_equipment_table": sensor_table,
    "privacy_curtain": privacy_curtain,
    "clinical_sink": sink, "visitor_chair": visitor_chair, "charting_counter": charting_counter, "wall_clock": wall_clock,
    "wall_bumper": bumper, "sharps_container": sharps, "glove_dispenser": gloves, "towel_dispenser": towels,
    "bedside_cabinet": cabinet, "tablet_table": tablet_table, "pedal_bin": pedal_bin, "handrub": handrub,
}
