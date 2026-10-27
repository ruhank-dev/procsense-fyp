"""Hospital bed frame for the PatientBay (Blender 4.5.14, metres, Blender Z up, bed head toward +Y, bed-local origin).

Revision 2 of the frame. Fixes the head board that sat inside the wall panel (the oak panel face is at z = 1.926, so the
board now stands in front of it, z 1.896 - 1.926; the mattress keeps its original 2.0 m), puts the rubber wall stops on
the board back where there is air behind them, and rebuilds the rails after the reference bed: thick rounded borders,
open hand grips split by a sloping bridge, blue inlay panels and lime release tabs, a keypad and a docked handset.
The base is an open diagonal lift. Everything is joined into one mesh whose UVs point at an atlas of flat swatches, so
plastic, painted metal, stainless, rubber and accents keep separate smoothness/metallic values in one draw call.

  blender-4.5.14 -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_hospital_bed.py
Outputs (ignored) in unity/Logs/HospitalBed: hospital_bed.blend/.fbx, frame_albedo.png, frame_metal_smooth.png, validation.json.
"""
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bed_common as bc  # noqa: E402
import bed_frame_parts as fp  # noqa: E402
from bed_frame_parts import box, cable, ring_prism, rod, slab, superellipse  # noqa: E402

TRI_BUDGET = 12500
HEAD_Y, FOOT_Y = .911, -1.02          # board centres (z 1.896-1.926 and -1.04..-1.0 in world z)


def chassis():
    for x in (-.29, .29):
        slab("Base_beam", .06, 1.46, .07, .02, (x, 0, .205), "Z", "PaintedMetal", .006, segs=2)
    for y in (-.62, .62):
        slab("Base_cross", .60, .07, .055, .016, (0, y, .205), "Z", "PaintedMetal", .005)
    for x in (-.29, .29):
        for y in (-.66, .66):
            box("Castor_cover", (.075, .14, .04), (x, y, .152), "Plastic", .012, 2)
            rod("Castor_stem", (x, y, .17), (x, y, .13), .011)
            for sd in (-1, 1):
                box("Fork_plate", (.007, .05, .1), (x + sd * .03, y, .1), "PaintedMetal", .002)
            rod("Axle", (x - .036, y, .07), (x + .036, y, .07), .008, "Stainless", 8)
            rod("Tyre", (x - .02, y, .07), (x + .02, y, .07), .07, "Rubber", 20, .0015)
            rod("Wheel_hub", (x - .0235, y, .07), (x + .0235, y, .07), .03, "Plastic", 12)
    rod("Pedal_bar", (-.34, -.80, .17), (.34, -.80, .17), .011)
    for x in (-.22, .22):
        slab("Brake_pedal", .11, .06, .016, .014, (x, -.80, .165), "Z", "DarkTrim", .002)


def lift():
    """Open diagonal lift mechanism (reference): arms crossing around a compact housing, dark pivots and actuator."""
    box("Lift_housing", (.34, .34, .18), (0, -.12, .335), "PaintedMetal", .014, 2)
    for x in (-.15, .15):
        rod("Lift_arm", (x, -.56, .255), (x, .16, .47), .026, "PaintedMetal", 14, .002)
        rod("Lift_arm", (x, .56, .255), (x, -.16, .47), .026, "PaintedMetal", 14, .002)
        for y, z in ((-.56, .255), (.56, .255), (.16, .47), (-.16, .47)):
            rod("Pivot", (x - .03, y, z), (x + .03, y, z), .036, "DarkTrim", 14)
            box("Clevis", (.075, .05, .056), (x, y, z - .012), "PaintedMetal", .006)
            rod("Pin", (x - .04, y, z), (x + .04, y, z), .009, "Stainless", 8)
    rod("Actuator", (0, .44, .27), (0, .10, .44), .02, "DarkTrim")


def deck():
    slab("Deck_tray", .96, 1.82, .028, .03, (0, 0, .536), "Z", "DarkTrim", .004)
    for sx in (-1, 1):
        slab("Deck_fairing", 1.76, .078, .026, .025, (sx * .48, 0, .506), "X", "Plastic", .004, None, 2, 4)
        for y in (-.30, .30):
            box("Fairing_groove", (.004, .006, .07), (sx * .4935, y, .506), "DarkTrim", .0008)
        for y in (-.84, .84):
            box("Mattress_retainer", (.03, .13, .095), (sx * .49, y, .5975), "Plastic", .008)


def boards():
    # head board in front of the wall panel (back face on the panel plane z = 1.926), blue inlay like the reference
    slab("Head_board", .96, .74, .03, .08, (0, HEAD_Y, .87), "Y", "Plastic", .005, (.09, .09, .02, .02), 2, 6)
    slab("Head_inlay", .72, .36, .006, .05, (0, HEAD_Y - .0165, .93), "Y", "BlueInlay", .002)
    slab("Head_trim", .72, .03, .006, .012, (0, HEAD_Y - .0165, .715), "Y", "DarkTrim", .0015)
    for x in (-.26, .26):
        box("Head_tab", (.08, .01, .03), (x, HEAD_Y - .019, .675), "Lime", .002)
    box("Head_grip", (.28, .026, .036), (0, HEAD_Y, 1.17), "DarkTrim", .004)
    # rubber wall stops on the board back, above the wall panel where the upper wall is 2.4 cm behind the board
    for x in (-.40, .40):
        rod("Wall_stop", (x, HEAD_Y + .015, 1.19), (x, HEAD_Y + .039, 1.19), .022, "Rubber", 14, .0015)
    slab("Foot_board", .96, .24, .04, .045, (0, FOOT_Y, .62), "Y", "Plastic", .004, (.05, .05, .02, .02), 2, 5)
    slab("Foot_inlay", .70, .085, .006, .03, (0, FOOT_Y + .023, .63), "Y", "BlueInlay", .002)
    for x in (-.3, .3):
        box("Foot_tab", (.07, .01, .026), (x, FOOT_Y + .025, .545), "Lime", .002)
    box("Foot_grip", (.24, .03, .03), (0, FOOT_Y, .715), "DarkTrim", .003)
    box("Nurse_panel", (.17, .012, .07), (0, FOOT_Y - .026, .60), "DarkTrim", .004)
    for i in range(3):
        box("Nurse_button", (.03, .006, .022), (-.05 + i * .05, FOOT_Y - .033, .60), "Keypad", .003)
    for y in (.885, -.935):
        for x in (-.38, .38):
            box("Board_bracket", (.07, .03, .05), (x, y, .545), "Stainless", .003)


def rail(part, sx, y, z, outer, inner, thick, parked):
    """Reference style rail: thick rounded border, open grip, blue inlay + dark strip + lime release tab on both faces."""
    x0 = sx * (.545 if parked else .55)
    ring_prism(part, outer, inner, thick, (x0, y, z), "X", "Plastic", .004, 1 if parked else 2)
    long_, hi_, off = (.30, .028, .075) if parked else (.42, .07, .145)
    for side in (-1, 1):
        xf = x0 + side * (thick / 2 + .0025)
        box(part + "_inlay", (.005, long_, hi_), (xf, y, z - off), "BlueInlay", .002)
        box(part + "_strip", (.005, long_, .014 if parked else .022), (xf, y, z - off - (.03 if parked else .06)), "DarkTrim", .0015)
        box(part + "_tab", (.01, .08, .028), (xf + side * .003, y + (.07 if parked else .1), z - off - (.05 if parked else .08)), "Lime", .002)


def rails():
    sx = -1
    # raised head-half rail with a sloping bridge across the grip opening
    rail("Raised_rail", sx, .58, .79, superellipse(.32, .235, 4.2, 36), superellipse(.205, .075, 2.8, 36, .05), .04, False)
    slab("Rail_bridge", .46, .028, .03, .012, (sx * .55, .58, .84), "X", "Plastic", .003, rot=(math.radians(-22), 0, 0))
    xin = sx * .55 + .02
    box("Keypad_housing", (.014, .16, .055), (xin + .007, .47, .93), "DarkTrim", .004)
    for i in range(4):
        box("Keypad_button", (.006, .026, .026), (xin + .016, .41 + i * .04, .935), "Keypad", .003)
    # docked handset with display and cable (reference)
    slab("Handset", .06, .17, .03, .016, (xin + .015, .78, .72), "X", "Plastic", .004)
    box("Handset_screen", (.004, .042, .03), (xin + .032, .78, .76), "Screen", .002)
    for i in range(3):
        box("Handset_button", (.004, .016, .014), (xin + .032, .765 + i * .016, .695), "Keypad", .0012)
    cable("Handset_cable", [(xin + .015, .78, .635), (xin + .02, .78, .58), (xin + .008, .765, .52), (xin - .01, .735, .49)])
    # lowered (parked) rails: both foot-half rails and the working-side head-half rail
    for sxx, y in ((-1, -.50), (1, -.50), (1, .57)):
        rail("Lowered_rail", sxx, y, .53, superellipse(.29, .115, 3.2, 36), superellipse(.205, .058, 2.5, 36, .018), .036, True)
        for dy in (-.17, .17):
            rod("Rail_pivot", (sxx * .493, y + dy, .51), (sxx * .53, y + dy, .52), .014, "DarkTrim", 12)
    for y in (.38, .78):
        rod("Rail_arm", (sx * .48, y, .53), (sx * .53, y, .64), .012, "DarkTrim", 10)


def atlas():
    cells = np.zeros((4, 4, 3))
    ms = np.zeros((4, 4, 4))
    for _, (cell, hx, metal, smooth) in fp.SWATCHES.items():
        r, c = divmod(cell, 4)
        cells[r, c] = bc.hex_rgb(hx)
        ms[r, c] = (metal, 0, 0, smooth)
    up = lambda a: np.kron(a, np.ones((64, 64, 1)))
    # array row 0 is the top row of the PNG while write_png expects row 0 = bottom, hence the flip
    bc.write_png(bc.OUT / "frame_albedo.png", up(cells)[::-1])
    bc.write_png(bc.OUT / "frame_metal_smooth.png", up(ms)[::-1])


def join_and_uv():
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    mesh = obj.data
    while mesh.uv_layers:                  # primitives bring their own default UVs; the swatch UVs must be the only layer
        mesh.uv_layers.remove(mesh.uv_layers[0])
    layer = mesh.uv_layers.new(name="UVMap")
    names = [m.name[2:] for m in mesh.materials]
    for poly in mesh.polygons:
        cell = fp.SWATCHES[names[poly.material_index]][0]
        u, v = (cell % 4 + .5) / 4, 1 - (cell // 4 + .5) / 4
        for li in poly.loop_indices:
            layer.data[li].uv = (u, v)
    mesh.materials.clear()
    mat = bpy.data.materials.new("M_BedFrame")
    mat.use_nodes = True
    mesh.materials.append(mat)
    for poly in mesh.polygons:
        poly.material_index = 0
    obj.name = mesh.name = f"EQ_{fp.ASSET}_M_BedFrame"
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    return obj


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system, bpy.context.scene.unit_settings.scale_length = "METRIC", 1
    for f in (chassis, lift, deck, boards, rails):
        f()
    per = {}
    for o in bpy.context.scene.objects:
        if o.type == "MESH":
            k = o.name.replace("EQ_hospital_bed_", "").split(".")[0]
            per[k] = per.get(k, 0) + sum(len(q.vertices) - 2 for q in o.data.polygons)
    print("PER PART", sorted(per.items(), key=lambda kv: -kv[1])[:22])
    obj = join_and_uv()
    pt = bpy.data.objects.new("FRONT_MARKER", None)
    bpy.context.collection.objects.link(pt)
    pt.location = (0, -.3, 0)
    pt.empty_display_size = .025
    # triangulate the n-gons deterministically (quads stay quads), as the export will; separate touching shells are not welded
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4], quad_method="BEAUTY", ngon_method="BEAUTY")
    bm.to_mesh(obj.data)
    obj.data.update()
    bm.free()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bad = sum(1 for e in bm.edges if not e.is_manifold)
    loose = sum(1 for v in bm.verts if not v.link_edges)
    tris = sum(len(f.verts) - 2 for f in bm.faces)
    ngons = sum(1 for f in bm.faces if len(f.verts) > 4)
    bm.free()
    assert bad == 0 and loose == 0, (bad, loose)
    assert tris <= TRI_BUDGET, tris
    co = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = [round(min(c[i] for c in co), 3) for i in range(3)]
    hi = [round(max(c[i] for c in co), 3) for i in range(3)]
    bc.OUT.mkdir(parents=True, exist_ok=True)
    atlas()
    bpy.ops.wm.save_as_mainfile(filepath=str(bc.OUT / f"{fp.ASSET}.blend"))
    bpy.ops.export_scene.fbx(filepath=str(bc.OUT / f"{fp.ASSET}.fbx"), object_types={"MESH", "EMPTY"}, axis_forward="-Z", axis_up="Y",
                            apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                            use_custom_props=True, add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE")
    rep = {"triangles": tris, "renderers": 1, "non_manifold_edges": bad, "loose_vertices": loose, "ngon_faces": ngons,
           "bounds_blender_min": lo, "bounds_blender_max": hi,
           "head_board_world_z": [round(HEAD_Y + 1 - .015, 3), round(HEAD_Y + 1 + .015, 3)]}
    (bc.OUT / "validation.json").write_text(json.dumps(rep, indent=2) + "\n")
    print("HOSPITAL BED", json.dumps(rep))


assert bpy.app.version[:3] == (4, 5, 14), bpy.app.version
main()
