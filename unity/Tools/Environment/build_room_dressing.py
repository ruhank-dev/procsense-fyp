"""Metre-based presentation furniture for the existing rooms, Blender 4.5.14.

Run from repo root; generated sources/validation stay in ignored Logs/RoomDressing.
Import and place through HospitalRoomDressing, never regenerate the room shell.
"""
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
import procsense_bpy as g

OUT = ROOT / "unity" / "Logs" / "RoomDressing"
g.PALETTE.update({
    "Ivory": ((.79, .82, .78, 1), 0, .55),
    "Sage": ((.35, .49, .44, 1), 0, .6),
    "Curtain": ((.57, .69, .63, 1), 0, .95),
    "Upholstery": ((.10, .22, .23, 1), 0, .85),
    "Amber": ((.64, .42, .07, 1), 0, .65),
})


def box(name, size, p, mat="Ivory", bevel=.008):
    return g.box(name, size, p, mat, bevel)


def wheel(x, y, r=.065):
    g.rod("Castor_fork", (x, y, r), (x, y, r*2+.04), .016, "SteelBrushed", 12)
    g.rod("Rubber_castor", (x-.022, y, r), (x+.022, y, r), r, "RubberBlack", 16)
    g.rod("Hub_cap", (x-.025, y, r), (x+.025, y, r), r*.42, "SteelBrushed", 12)


def bed():
    box("Chassis", (.74, 1.60, .10), (0, 0, .20), "PlasticGrey")
    box("Lift_column", (.38, .58, .22), (0, .12, .34))
    box("Deck", (.97, 1.97, .075), (0, 0, .515))
    for x in [-.37, .37]:
        for y in [-.73, .73]:
            wheel(x, y)
    for y, height in [(.91, 1.24), (-.94, .72)]:
        for x in [-.43, .43]:
            g.rod("Board_mount", (x, y, .49), (x, y, height-.14), .018)
        box("Moulded_board", (.95, .065, .28), (0, y, height-.14))
        box("Board_inlay", (.76, .008, .14), (0, y-.039, height-.14), "Sage")
        box("Carry_grip_recess", (.23, .012, .035), (0, y-.044, height-.065), "PlasticGrey")
    # Lowered split rails at the foot leave both arms, wristband and sensors exposed.
    for x in [-.64, .64]:
        for y in [-.69, -.31]:
            g.rod("Rail_bracket", (x*.70, y, .48), (x, y, .48), .018)
            g.rod("Rail_post", (x, y, .48), (x, y, .61), .016)
        g.rod("Lowered_rail", (x, -.77, .61), (x, -.23, .61), .024, "Ivory")
    box("Brake_pedal", (.18, .045, .018), (.28, -.80, .13), "Sage", .003)


def privacy():
    g.tube("Ceiling_track", [(-1.55, 1.78, 2.48), (-1.55, -.78, 2.48), (.95, -.78, 2.48)], .018)
    for x, y in [(-1.55, 1.60), (-1.55, -.70), (.85, -.78)]:
        g.rod("Ceiling_hanger", (x, y, 2.48), (x, y, 2.76), .009)
    # Closed thin fabric, hanging under its track with gathered vertical folds.
    n, rows = 96, 6
    verts, faces = [], []
    for j in range(rows+1):
        v = j/rows
        for i in range(n+1):
            u = i/n
            verts.append((-1.55+.055*math.sin(u*math.tau*12), .50+1.12*u,
                          2.36-v*(2.13+.014*math.cos(u*math.tau*12))))
    for j in range(rows):
        for i in range(n):
            a = j*(n+1)+i
            faces.append((a, a+1, a+n+2, a+n+1))
    mesh = bpy.data.meshes.new("Gathered fabric")
    mesh.from_pydata(verts, [], faces)
    obj = bpy.data.objects.new("Curtain", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    m = obj.modifiers.new("Fabric thickness", "SOLIDIFY")
    m.thickness = .004
    bpy.ops.object.modifier_apply(modifier=m.name)
    g.finish(obj, "Gathered_curtain", "Curtain")
    for i in range(13):
        y = .50+1.12*i/12
        g.rod("Carrier", (-1.55, y, 2.36), (-1.55, y, 2.47), .005, "Ivory", 8)


def handrub():
    box("Mount", (.16, .025, .32), (0, .02, 0), "SteelBrushed")
    box("Dispenser", (.125, .10, .225), (0, -.035, .015))
    box("Level_window", (.028, .006, .10), (0, -.089, .01), "Sage", .003)
    box("Press_lever", (.10, .08, .025), (0, -.083, -.105), "PlasticGrey", .004)
    box("Drip_tray", (.16, .15, .015), (0, -.05, -.19), "PlasticGrey", .003)


def wastebin():
    box("Pedal_bin", (.32, .30, .43), (0, 0, .245))
    box("Lid", (.34, .32, .035), (0, 0, .475), "Amber")
    box("Hinge", (.18, .045, .045), (0, .15, .46), "PlasticGrey")
    box("Pedal", (.14, .09, .025), (0, -.18, .045), "PlasticGrey")
    box("Base", (.30, .28, .035), (0, 0, .025), "PlasticGrey")


def bench():
    for x in [-.55, .55]:
        box("Leg", (.055, .40, .39), (x, .02, .235), "SteelBrushed")
    box("Seat_base", (1.48, .45, .045), (0, 0, .43), "SteelBrushed")
    for x in [-.49, 0, .49]:
        box("Seat_pad", (.46, .43, .065), (x, -.015, .48), "Upholstery", .016)
        box("Back_pad", (.46, .07, .36), (x, .19, .77), "Upholstery", .016)
    for x in [-.75, .75]:
        g.tube("Armrest", [(x, .16, .46), (x, .16, .68), (x, -.18, .68)], .016)


def trolley():
    for x in [-.29, .29]:
        for y in [-.21, .21]:
            wheel(x, y, .045)
            g.rod("Upright", (x, y, .11), (x, y, 1.02), .014)
    for z in [.21, .58, .92]:
        box("Shelf", (.60, .44, .025), (0, 0, z), "SteelBrushed", .004)
        for x in [-.30, .30]:
            box("Shelf_lip", (.015, .44, .06), (x, 0, z+.027), "SteelBrushed", .002)
    g.tube("Push_handle", [(-.29, .21, 1.02), (0, .21, 1.06), (.29, .21, 1.02)], .014)
    for x in [-.15, .15]:
        box("Linen_pack", (.24, .32, .09), (x, 0, .64), "Curtain", .012)


def operating_table():
    box("Floor_base", (.68, 1.13, .13), (0, 0, .085), "PlasticGrey", .025)
    box("Pedestal", (.36, .48, .54), (0, 0, .40), "SteelBrushed", .015)
    box("Lift_sleeve", (.30, .40, .21), (0, 0, .73), "Ivory")
    for y, length in [(-.73, .46), (-.10, .76), (.65, .65)]:
        box("Segment_chassis", (.60, length, .045), (0, y, .83), "SteelBrushed")
        box("Segment_pad", (.57, length-.025, .075), (0, y, .89), "Upholstery", .018)
    for x in [-.34, .34]:
        g.rod("Accessory_rail", (x, -.86, .81), (x, .88, .81), .012)
    box("Foot_control", (.21, .13, .045), (.30, -.45, .07), "PlasticGrey")


def anaesthesia():
    box("Lower_unit", (.66, .54, .61), (0, 0, .46))
    box("Worktop", (.75, .62, .045), (0, -.035, .80), "SteelBrushed")
    for z in [.30, .51, .69]:
        box("Drawer", (.38, .018, .14), (-.09, -.282, z), "Sage")
        box("Pull", (.16, .025, .018), (-.09, -.30, z+.02), "SteelBrushed", .003)
    for x in [-.28, .28]:
        for y in [-.23, .23]:
            wheel(x, y, .055)
    box("Upper_panel", (.66, .19, .42), (0, .12, 1.10))
    box("Dark_display", (.29, .012, .19), (-.11, .017, 1.14), "ScreenOff")
    for x in [.13, .23]:
        g.rod("Dial", (x, .015, 1.17), (x, -.013, 1.17), .026, "PlasticGrey")
        g.rod("Flow_gauge", (x, -.005, .95), (x, -.005, 1.08), .014, "SteelBrushed")
    g.rod("Monitor_post", (0, .14, 1.31), (0, .14, 1.44), .018)
    box("Monitor_housing", (.40, .075, .29), (0, .10, 1.55), "PlasticGrey")
    box("Monitor_off", (.34, .008, .23), (0, .058, 1.55), "ScreenOff")
    g.tube("Breathing_hose", [(.34, -.08, 1.02), (.47, -.12, .87), (.45, -.26, .66), (.30, -.30, .70)], .022, "Curtain")


def mayo():
    for x in [-.22, .22]:
        box("Base_foot", (.035, .45, .035), (x, 0, .09), "SteelBrushed", .004)
        for y in [-.18, .18]:
            wheel(x, y, .035)
    g.rod("Base_crossbar", (-.22, .12, .10), (.22, .12, .10), .018)
    g.rod("Upright", (0, .12, .10), (0, .12, 1.0), .022)
    box("Tray", (.65, .45, .025), (0, 0, 1.015), "SteelBrushed", .006)
    for x in [-.32, .32]:
        box("Tray_edge", (.015, .45, .035), (x, 0, 1.04), "SteelBrushed", .002)
    for y in [-.22, .22]:
        box("Tray_edge", (.65, .015, .035), (0, y, 1.04), "SteelBrushed", .002)


def cupboard():
    box("Cabinet", (1.2, .46, 1.83), (0, 0, .97))
    box("Toe_kick", (1.12, .40, .10), (0, 0, .055), "PlasticGrey")
    for x in [-.295, .295]:
        box("Door_gap", (.574, .008, 1.71), (x, -.237, .99), "PlasticGrey")
        box("Door_panel", (.56, .025, 1.69), (x, -.25, .99), "Ivory")
        hx = x*.28
        g.rod("Handle", (hx, -.29, .92), (hx, -.29, 1.17), .009)


def surgical_light():
    g.rod("Ceiling_mount", (0, 0, 2.73), (0, 0, 2.40), .055, "Ivory")
    g.tube("Articulated_arm", [(0, 0, 2.40), (.65, 0, 2.40), (.65, -.5, 2.23)], .038, "Ivory")
    g.rod("Lamp_disc", (.65, -.5, 2.13), (.65, -.5, 2.23), .30, "Ivory", 32)
    for a in range(6):
        x = .65+.18*math.cos(a*math.tau/6)
        y = -.5+.18*math.sin(a*math.tau/6)
        g.rod("Lens", (x, y, 2.116), (x, y, 2.132), .065, "PlasticWhite", 12)
    g.rod("Sterile_handle", (.65, -.5, 2.03), (.65, -.5, 2.13), .021, "Sage")


def export(name, build):
    g.reset(name)
    build()
    g.point("FRONT_MARKER", (0, -.3, 0))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for mat in sorted({o.data.materials[0].name for o in meshes}):
        group = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data.materials[0].name == mat]
        bpy.ops.object.select_all(action="DESELECT")
        for o in group:
            o.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = f"EQ_{name}_{mat}"
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    tris = 0
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        assert all(e.is_manifold for e in bm.edges), obj.name
        assert obj.data.uv_layers, obj.name
        tris += sum(len(f.verts)-2 for f in bm.faces)
        bm.free()
    assert tris < 5000, (name, tris)
    coords = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    bounds = [[min(c[i] for c in coords) for i in range(3)], [max(c[i] for c in coords) for i in range(3)]]
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / (name+".blend")))
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name+".fbx")), object_types={"MESH", "EMPTY"},
                            axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                            apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                            use_custom_props=True, add_leaf_bones=False, bake_anim=False)
    return {"name": name, "triangles": tris, "renderers": len(meshes), "bounds_blender": bounds}


assert bpy.app.version[:3] == (4, 5, 14)
OUT.mkdir(parents=True, exist_ok=True)
if __name__ == "__main__":
    results = [export(n, f) for n, f in [
        ("hospital_bed", bed), ("privacy_curtain", privacy), ("handrub", handrub),
        ("pedal_bin", wastebin), ("waiting_bench", bench), ("clinical_trolley", trolley),
        ("operating_table", operating_table), ("anaesthesia_unit", anaesthesia),
        ("mayo_stand", mayo), ("supply_cupboard", cupboard), ("surgical_light", surgical_light)]]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2)+"\n")
    print("ROOM DRESSING VALIDATION", json.dumps(results))
