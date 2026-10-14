"""Blender 4.5.14 presentation props; metre dimensions are illustrative, not clinical specifications.

Run from the repository root with the pinned Blender, background factory startup.
Outputs to unity/Logs/BedsideBuild; import through HospitalBedside.ImportAndApply().
"""
import json
from pathlib import Path
import sys

import bpy
import bmesh
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT / "tools" / "blender"))
import procsense_bpy as geo

OUT = ROOT / "unity" / "Logs" / "BedsideBuild"
geo.PALETTE.update({
    "Ivory": ((.79, .82, .78, 1), 0, .55),
    "Sage": ((.35, .49, .44, 1), 0, .6),
    "Oak": ((.66, .51, .32, 1), 0, .6),
    "Diffuser": ((.95, .98, .94, 1), 0, .4),
})


def headwall():
    geo.box("Housing", (2.35, .10, .30), (0, 0, 0), "Ivory", .012)
    geo.box("Service_strip", (2.26, .009, .045), (0, -.059, -.105), "Sage", .004)
    geo.box("Upper_seam", (2.24, .008, .008), (0, -.058, .11), "PlasticGrey", .001)
    for i, x in enumerate([-.78, -.57, -.36]):
        geo.box(f"Outlet_plate_{i}", (.145, .014, .17), (x, -.063, .015), "PlasticWhite", .006)
        geo.rod(f"Outlet_metal_{i}", (x, -.078, .02), (x, -.099, .02), .039, "SteelBrushed", 20)
        geo.rod(f"Outlet_recess_{i}", (x, -.100, .02), (x, -.104, .02), .022, "PlasticGrey", 20)
    for i, x in enumerate([.25, .47, .69]):
        geo.box(f"Power_plate_{i}", (.155, .014, .18), (x, -.063, .015), "PlasticWhite", .006)
        for dx, dz in [(-.024, -.01), (.024, -.01), (0, .048)]:
            geo.box(f"Socket_slot_{i}_{dx}_{dz}", (.013, .002, .022), (x+dx, -.072, .015+dz), "PlasticGrey", .001)
    geo.box("Reading_light_housing", (1.50, .105, .085), (0, -.03, .36), "SteelBrushed", .006)
    geo.box("Reading_light_diffuser", (1.40, .075, .012), (0, -.054, .316), "Diffuser", .002)
    geo.point("FRONT_MARKER", (0, -.25, 0))
    geo.point("COL_Main", (0, 0, 0), (2.35, .10, .30))


def cabinet():
    geo.box("Body", (.48, .46, .72), (0, 0, .49), "Ivory", .008)
    geo.box("Oak_top", (.52, .50, .035), (0, 0, .8825), "Oak", .008)
    geo.box("Drawer_reveal", (.444, .005, .21), (0, -.234, .735), "PlasticGrey", .001)
    geo.box("Drawer_face", (.428, .022, .195), (0, -.243, .738), "Sage", .005)
    geo.box("Cupboard_reveal", (.444, .006, .47), (0, -.235, .389), "PlasticGrey", .001)
    geo.box("Cupboard_face", (.428, .022, .445), (0, -.244, .389), "Ivory", .005)
    for z in [.77, .545]:
        geo.box("Recessed_handle", (.17, .007, .026), (0, -.259, z), "PlasticGrey", .004)
        geo.rod("Handle", (-.08, -.273, z), (.08, -.273, z), .006, "SteelBrushed", 12)
    geo.box("Lower_plinth", (.41, .40, .035), (0, 0, .12), "PlasticGrey", .003)
    for x in [-.185, .185]:
        for y in [-.17, .17]:
            geo.rod("Castor_stem", (x, y, .045), (x, y, .11), .011, "SteelBrushed", 12)
            geo.rod("Castor", (x-.013, y, .045), (x+.013, y, .045), .045, "RubberBlack", 16)
    geo.point("FRONT_MARKER", (0, -.4, .5))
    geo.point("COL_Main", (0, 0, .45), (.50, .50, .90))


def tablet_table():
    geo.box("Rounded_top", (.62, .40, .035), (0, .30, 1.025), "Oak", .012)
    geo.box("Underside", (.57, .35, .018), (0, .30, .998), "Ivory", .005)
    geo.rod("Top_support", (0, .10, .988), (0, .40, .988), .018, "SteelBrushed", 12)
    geo.rod("Tablet_mount", (0, -.015, 1.00), (0, -.015, 1.18), .012, "SteelBrushed", 12)
    geo.box("Tablet_backing", (.575, .016, .39), (0, -.020, 1.15), "PlasticGrey", .008)
    geo.box("Telescopic_column", (.06, .065, .73), (0, .10, .49), "SteelBrushed", .004)
    geo.box("Upper_column", (.045, .048, .23), (0, .10, .88), "PlasticWhite", .003)
    geo.box("Adjustment_collar", (.085, .09, .065), (0, .10, .77), "Sage", .004)
    geo.rod("Adjustment_knob", (.042, .10, .77), (.075, .10, .77), .021, "PlasticGrey", 16)
    for x in [-.245, .245]:
        geo.box("Base_rail", (.035, .40, .032), (x, 0, .078), "SteelBrushed", .004)
        geo.rod("Base_crossbar", (-.245, .10, .082), (.245, .10, .082), .019, "SteelBrushed", 12)
        for y in [-.16, .16]:
            geo.rod("Castor", (x-.012, y, .035), (x+.012, y, .035), .035, "RubberBlack", 16)
    geo.point("FRONT_MARKER", (0, -.35, .5))
    geo.point("COL_Top", (0, .30, 1.025), (.62, .40, .035))
    geo.point("COL_Base", (0, 0, .07), (.54, .43, .14))


def export(name, build):
    geo.reset(name)
    build()
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    # Group static parts by material rather than exporting dozens of draw surfaces.
    for material in sorted({o.data.materials[0].name for o in meshes}):
        group = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data.materials[0].name == material]
        bpy.ops.object.select_all(action="DESELECT")
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = f"EQ_{name}_{material}"
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    triangles = 0
    for obj in meshes:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        assert all(e.is_manifold for e in bm.edges), obj.name + " has open geometry"
        assert obj.data.uv_layers, obj.name + " lacks UVs"
        triangles += sum(len(f.verts)-2 for f in bm.faces)
        bm.free()
    assert triangles < 5000, (name, triangles)
    coords = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    low = [min(c[i] for c in coords) for i in range(3)]
    high = [max(c[i] for c in coords) for i in range(3)]
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / (name + ".blend")))
    bpy.ops.export_scene.fbx(filepath=str(OUT / (name + ".fbx")), object_types={"MESH", "EMPTY"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True,
                             use_custom_props=True, add_leaf_bones=False, bake_anim=False)
    return {"name": name, "triangles": triangles, "renderers": len(meshes), "bounds_blender": [low, high]}


assert bpy.app.version[:3] == (4, 5, 14), bpy.app.version_string
OUT.mkdir(parents=True, exist_ok=True)
results = [export(name, build) for name, build in [
    ("bay_headwall", headwall), ("bedside_cabinet", cabinet), ("tablet_table", tablet_table)]]
(OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
print("BEDSIDE VALIDATION", json.dumps(results))
