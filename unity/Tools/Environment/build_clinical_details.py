"""Bay hygiene, charting and visitor props; uses the validated room exporter."""
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_room_dressing as room

g, box = room.g, room.box
room.OUT = room.ROOT / "unity" / "Logs" / "ClinicalDetails"
room.OUT.mkdir(parents=True, exist_ok=True)


def sink():
    box("Cabinet", (.82, .48, .73), (0, 0, .49))
    box("Toe_kick", (.75, .41, .12), (0, .02, .095), "PlasticGrey")
    for x in [-.20, .20]:
        box("Door_reveal", (.39, .012, .65), (x, -.246, .49), "PlasticGrey")
        box("Door", (.375, .022, .63), (x, -.258, .49), "Sage")
    # Open recessed bowl: sloped sides and a solid bottom, no fake flat black hole.
    box("Counter_left", (.14, .56, .045), (-.38, 0, .885), "PlasticWhite")
    box("Counter_right", (.14, .56, .045), (.38, 0, .885), "PlasticWhite")
    for y in [-.225, .225]:
        box("Counter_rim", (.64, .11, .045), (0, y, .885), "PlasticWhite")
    vertices = []
    for x, y, z in [(.31, .17, .885), (.30, .16, .885), (.225, .11, .77), (.235, .12, .755)]:
        vertices.extend([(-x, -y, z), (x, -y, z), (x, y, z), (-x, y, z)])
    faces = []
    for i in range(4):
        j = (i+1) % 4
        faces.extend([(i, j, j+4, i+4), (i+4, j+4, j+8, i+8),
                      (i+12, j+12, j, i)])
    faces.extend([(8, 9, 10, 11), (15, 14, 13, 12)])
    mesh = bpy.data.meshes.new("Continuous basin")
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new("Bowl", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    g.finish(obj, "Continuous_bowl", "PlasticWhite")
    g.rod("Drain", (0, 0, .771), (0, 0, .773), .032, "SteelBrushed", 20)
    g.rod("Drain_centre", (0, 0, .773), (0, 0, .775), .009, "PlasticGrey", 12)
    g.tube("Mixer", [(0, .205, .90), (0, .205, 1.11), (0, .04, 1.11), (0, .02, 1.08)], .018)
    g.rod("Elbow_lever", (.025, .205, 1.03), (.16, .205, 1.06), .009)
    box("Splashback", (.90, .025, .40), (0, .27, 1.08), "PlasticWhite")


def gloves():
    box("Wall_frame", (.42, .075, .29), (0, 0, 0), "SteelBrushed")
    for x in [-.135, 0, .135]:
        box("Box", (.124, .065, .245), (x, -.045, 0), "Sage")
        box("Opening", (.076, .008, .09), (x, -.083, 0), "PlasticGrey")
        box("Glove_fold", (.055, .012, .055), (x, -.09, -.015), "PlasticWhite", .008)
    g.rod("Lower_retainer", (-.22, -.09, -.10), (.22, -.09, -.10), .006)


def towels():
    box("Housing", (.29, .13, .33), (0, 0, 0))
    box("Level_window", (.035, .006, .14), (.09, -.069, -.01), "PlasticGrey")
    box("Outlet", (.22, .09, .018), (0, -.02, -.17), "PlasticGrey", .003)
    box("Paper_sheet", (.18, .008, .11), (0, -.04, -.215), "PlasticWhite", .001)


def sharps():
    box("Wall_bracket", (.23, .025, .24), (0, .10, -.025), "SteelBrushed")
    box("Container", (.24, .21, .29), (0, 0, -.015), "Amber", .018)
    box("Lid", (.26, .23, .035), (0, 0, .145), "Amber")
    box("Restricted_aperture", (.11, .07, .012), (0, -.035, .166), "PlasticGrey", .008)
    box("Label", (.16, .006, .10), (0, -.109, -.025), "PlasticWhite")
    # Simple universal warning triangle, avoiding invented patient or clinical facts.
    for a, b in [((-.035,-.115,-.05),(.035,-.115,-.05)),((.035,-.115,-.05),(0,-.115,.015)),((0,-.115,.015),(-.035,-.115,-.05))]:
        g.rod("Warning_border", a, b, .004, "PlasticGrey", 8)


def charting():
    box("Laminate_top", (1.02, .46, .035), (0, 0, .98), "Ivory", .012)
    for x in [-.38, .38]:
        g.tube("Wall_bracket", [(x, .20, .55), (x, .20, .95), (x, -.17, .95)], .018)
    box("Keyboard", (.36, .14, .018), (-.08, -.11, 1.008), "PlasticGrey", .006)
    for j in range(4):
        for i in range(10):
            box("Key", (.026, .020, .003), (-.225+i*.032, -.16+j*.028, 1.019), "SteelBrushed", 0)
    box("Mouse", (.048, .075, .025), (.29, -.09, 1.018), "PlasticGrey", .01)
    box("Monitor_foot", (.20, .12, .018), (0, .07, 1.008), "PlasticGrey")
    g.rod("Monitor_post", (0, .10, 1.01), (0, .10, 1.19), .018)
    box("Monitor", (.47, .055, .30), (0, .11, 1.30), "PlasticGrey", .012)
    box("Inactive_display", (.425, .008, .25), (0, .078, 1.30), "ScreenOff", .003)


def chair():
    for x in [-.23, .23]:
        for y in [-.21, .21]:
            g.rod("Leg", (x, y, .035), (x, y, .48), .016)
            box("Foot_cap", (.039, .039, .035), (x, y, .025), "RubberBlack", .005)
    box("Seat", (.50, .47, .065), (0, 0, .48), "Upholstery", .02)
    for x in [-.22, .22]:
        g.rod("Back_support", (x, .18, .46), (x, .23, .88), .014)
        g.tube("Armrest", [(x, .16, .50), (x, .16, .68), (x, -.16, .68)], .016)
    box("Back_pad", (.48, .065, .33), (0, .22, .77), "Upholstery", .02)


def clock():
    g.rod("Rim", (0, -.025, 0), (0, .025, 0), .20, "PlasticGrey", 48)
    g.rod("Face", (0, -.032, 0), (0, -.026, 0), .182, "PlasticWhite", 48)
    for i in range(12):
        a = i*math.tau/12
        g.rod("Hour_mark", (.148*math.sin(a), -.037, .148*math.cos(a)), (.172*math.sin(a), -.037, .172*math.cos(a)), .0035, "PlasticGrey", 8)
    g.rod("Hour_hand", (0, -.042, 0), (-.086, -.042, .050), .005, "PlasticGrey", 8)
    g.rod("Minute_hand", (0, -.045, 0), (.12, -.045, .07), .003, "PlasticGrey", 8)
    g.rod("Centre", (0, -.048, 0), (0, -.041, 0), .012, "PlasticGrey", 16)


def bumper():
    box("Wall_spacer", (1.2, .045, .11), (0, .025, 0), "PlasticGrey")
    box("Protective_rail", (1.3, .055, .15), (0, -.02, 0), "Ivory", .012)
    for x in [-.65, .65]:
        box("End_cap", (.04, .065, .15), (x, -.02, 0), "Sage")


results = [room.export(n, f) for n, f in [
    ("clinical_sink", sink), ("glove_dispenser", gloves), ("towel_dispenser", towels),
    ("sharps_container", sharps), ("charting_counter", charting), ("visitor_chair", chair),
    ("wall_clock", clock), ("wall_bumper", bumper)]]
(room.OUT / "validation.json").write_text(json.dumps(results, indent=2)+"\n")
print("CLINICAL DETAILS VALIDATION", json.dumps(results))
