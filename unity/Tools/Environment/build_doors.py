"""Hospital doors for the whole building (owner review 1 Nov 2026: the old doors were stacked boxes): a pressed-steel frame with stop, rubber seal, architraves and threshold, and four leaf types
(patient room veneer, diagnostic suite, laboratory, operating theatre) with bevelled slabs, cut-out vision panels with steel surrounds and glass, kick plates, bar pulls and push plates or levers with roses, and knuckle hinges.

Authored in Unity door coordinates (x across the opening, y up, z through the wall; the pull side is -z) and converted: Unity (x, y, z) = (-bx, bz, -by).
  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_doors.py
Outputs under unity/Logs/Doors (git-ignored): door_frame.fbx, door_leaf_bay|diag|lab|ot.fbx, validation.json. Dimensions follow the existing openings (1.06 x 2.09 leaf).
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

OUT = HERE.parents[1] / "Logs" / "Doors"
geo.PALETTE.update({
    "DoorBay": ((.58, .45, .31, 1), 0, .42), "DoorDiag": ((.30, .38, .47, 1), 0, .38), "DoorLab": ((.90, .91, .90, 1), 0, .32), "DoorOT": ((.72, .76, .78, 1), .55, .30),
    "FrameSteel": ((.80, .82, .82, 1), .1, .38), "TrimWhite": ((.92, .92, .90, 1), 0, .5), "DoorGlass": ((.70, .85, .90, 1), 0, .05), "Seal": ((.02, .02, .02, 1), 0, .85),
})


def U(x, y, z):
    return (-x, -z, y)


def ub(part, size, loc, mat, r=.003, seg=3):
    """Rounded box given in Unity axes."""
    return rbox(part, (size[0], size[2], size[1]), U(*loc), mat, r, seg)


def ucyl(part, a, b, radius, mat="SteelBrushed", verts=24):
    return cyl(part, U(*a), U(*b), radius, mat, verts)


def cut(slab, size, loc):
    """Boolean-subtract a Unity-axes box from a slab."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=U(*loc))
    c = bpy.context.object; c.dimensions = (size[0], size[2], size[1])
    mod = slab.modifiers.new("cut", "BOOLEAN"); mod.object = c; mod.operation = "DIFFERENCE"; mod.solver = "EXACT"
    bpy.context.view_layer.objects.active = slab
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(c, do_unlink=True)


ZP, ZB = .0275, .0825           # pull face / push face of the closed leaf
ZC = (ZP + ZB) / 2


def leaf(skin, vision, pull, kick=.30, steel_kick_both=True, wide_kick=False):
    vx, vy, vw, vh = vision
    slab = ub("slab", (1.052, 2.09, ZB - ZP), (0, 1.055, ZC), skin, r=.005, seg=3)
    cut(slab, (vw, vh, .2), (vx, vy, ZC))
    t = .014
    for nm, sz, lc in (("vt", (vw + 2 * t, t, ZB - ZP + .010), (vx, vy + vh / 2 + t / 2, ZC)), ("vb", (vw + 2 * t, t, ZB - ZP + .010), (vx, vy - vh / 2 - t / 2, ZC)),
                       ("vl", (t, vh, ZB - ZP + .010), (vx - vw / 2 - t / 2, vy, ZC)), ("vr", (t, vh, ZB - ZP + .010), (vx + vw / 2 + t / 2, vy, ZC))):
        ub("vs_" + nm, sz, lc, "SteelBrushed", r=.002, seg=2)
    ub("glass", (vw + .01, vh + .01, .008), (vx, vy, ZC), "DoorGlass", r=.002, seg=2)
    kh = .52 if wide_kick else kick
    for z, sgn in ((ZP - .0025, -1), (ZB + .0025, 1)):
        ub(f"kick{sgn}", (.99, kh, .005), (0, kh / 2 + .012, z), "SteelBrushed", r=.002, seg=2)
    hx = .425
    if pull == "bar":
        for y in (.86, 1.20):
            ucyl(f"standoff{y}", (hx, y, ZP - .002), (hx, y, ZP - .055), .0085)
        ucyl("pullbar", (hx, .80, ZP - .055), (hx, 1.26, ZP - .055), .0125, "SteelBrushed", 28)
        ub("pushplate", (.15, .34, .005), (hx, 1.05, ZB + .0025), "SteelBrushed", r=.003, seg=2)
    elif pull == "lever":
        for z, d in ((ZP, -1), (ZB, 1)):
            ucyl(f"rose{d}", (hx, 1.0, z), (hx, 1.0, z + d * .012), .028, "SteelBrushed", 30)
            tube(f"lever{d}", [U(hx, 1.0, z + d * .012), U(hx, 1.0, z + d * .052), U(hx - .12, 1.0, z + d * .052)], .0095, "SteelBrushed", .02, 8)
    else:                                                                                           # 'plate': push plates both sides + a short pull bar (theatre)
        ub("pushA", (.20, .55, .005), (hx - .02, 1.05, ZB + .0025), "SteelBrushed", r=.003, seg=2)
        ub("pushB", (.20, .55, .005), (hx - .02, 1.05, ZP - .0025), "SteelBrushed", r=.003, seg=2)
    for y in (.35, 1.05, 1.80):                                                                      # knuckle hinges on the hinge side (-x)
        ucyl(f"barrel{y}", (-.522, y - .05, ZC), (-.522, y + .05, ZC), .0085, "SteelBrushed", 18)
        ub(f"hl{y}", (.034, .10, .003), (-.505, y, ZP - .001), "SteelBrushed", r=.001, seg=2)
        ub(f"hr{y}", (.034, .10, .003), (-.505, y, ZB + .001), "SteelBrushed", r=.001, seg=2)


def frame():
    f = "FrameSteel"
    W, H, D, zc = .075, 2.20, .13, .035
    ub("jamb_l", (W, H, D), (-.5675, H / 2, zc), f, r=.004, seg=3)
    ub("jamb_r", (W, H, D), (.5675, H / 2, zc), f, r=.004, seg=3)
    ub("head", (1.22, W, D), (0, 2.1825, zc), f, r=.004, seg=3)
    for side, z in (("p", zc - D / 2 - .006), ("b", zc + D / 2 + .006)):                               # architraves on both faces of the wall
        ub(f"arch_l{side}", (.045, 2.23, .012), (-.64, 1.115, z), "TrimWhite", r=.003, seg=2)
        ub(f"arch_r{side}", (.045, 2.23, .012), (.64, 1.115, z), "TrimWhite", r=.003, seg=2)
        ub(f"arch_t{side}", (1.32, .045, .012), (0, 2.235, z), "TrimWhite", r=.003, seg=2)
    for sx in (-.526, .526):
        ub(f"seal{sx}", (.008, 2.10, .007), (sx, 1.06, ZP - .004), "Seal", r=.002, seg=2)
    ub("seal_head", (1.05, .008, .007), (0, 2.102, ZP - .004), "Seal", r=.002, seg=2)
    ub("threshold", (1.06, .010, .11), (0, .005, ZC), "SteelBrushed", r=.003, seg=2)


VARIANTS = {
    "bay": dict(skin="DoorBay", vision=(.16, 1.38, .50, 1.05), pull="lever"),
    "diag": dict(skin="DoorDiag", vision=(.16, 1.38, .52, 1.05), pull="bar"),
    "lab": dict(skin="DoorLab", vision=(0.0, 1.50, .60, .80), pull="bar"),
    "ot": dict(skin="DoorOT", vision=(0.0, 1.45, .62, .86), pull="plate", wide_kick=True),
}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [("door_frame", frame)] + [(f"door_leaf_{k}", (lambda v=v: leaf(**v))) for k, v in VARIANTS.items()]
    results = [pk.export(n, f, OUT, tri_budget=60000) for n, f in jobs]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("DOORS", json.dumps([(r["name"], r["triangles"]) for r in results]))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
