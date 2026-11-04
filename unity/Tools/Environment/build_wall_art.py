"""Wall displays for the corridor: framed portrait, framed certificate (landscape), award plaque, notice board. Blender 4.5.14, prop_kit conventions
(metres, Z up, front faces -Y, one mesh per material named EQ_<asset>_M_<Material>, closed geometry with UVs). Origin: centre of the back face (the wall plane); the display looks toward -Y.
The printed surface is its own mesh (material Print) so Unity assigns a different picture per instance; its front face carries the whole 0..1 UV range.

  build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/Environment/build_wall_art.py
"""
import json
import sys
from pathlib import Path

import bmesh
import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import prop_kit as pk  # noqa: E402
from prop_kit import cyl, rbox  # noqa: E402
import procsense_bpy as geo  # noqa: E402

OUT = HERE.parents[1] / "Logs" / "WallArt"
geo.PALETTE.update({
    "FrameWalnut": ((.16, .09, .05, 1), 0, .42), "MatWhite": ((.93, .92, .89, 1), 0, .9), "Glass": ((.85, .93, .97, 1), 0, .04), "AluFrame": ((.66, .68, .70, 1), .9, .38),
    "Print": ((1, 1, 1, 1), 0, .7), "BrassScrew": ((.62, .46, .18, 1), .9, .35),
})


def print_surface(part, w, h, y_front):
    """A thin closed slab whose -Y face carries the full UV range (every other face is collapsed to one UV point so the picture is never smeared)."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = (v.co.x * w, y_front + v.co.y * .002 + .001, v.co.z * h)
    uvl = bm.loops.layers.uv.new("UVMap")
    for f in bm.faces:
        for l in f.loops:
            c = l.vert.co
            l[uvl].uv = ((c.x / w) + .5, (c.z / h) + .5) if f.normal.y < -.5 else (0.0, 0.0)
    me = bpy.data.meshes.new(part); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(f"EQ_{geo.ASSET}_{part}", me); bpy.context.collection.objects.link(o)
    o.data.materials.append(geo.material("Print"))
    for p in me.polygons:
        p.use_smooth = False
    return o


def frame(W, H, bar, depth, print_w, print_h, mat_name="FrameWalnut"):
    d = depth
    rbox("Frame_top", (W, d, bar), (0, -d / 2, H / 2 - bar / 2), mat_name, .004)
    rbox("Frame_bottom", (W, d, bar), (0, -d / 2, -H / 2 + bar / 2), mat_name, .004)
    rbox("Frame_left", (bar, d, H - 2 * bar), (-W / 2 + bar / 2, -d / 2, 0), mat_name, .004)
    rbox("Frame_right", (bar, d, H - 2 * bar), (W / 2 - bar / 2, -d / 2, 0), mat_name, .004)
    iw, ih = W - 2 * bar + .004, H - 2 * bar + .004
    rbox("Backing", (iw, .006, ih), (0, -.003, 0), "MatWhite", .0008)
    rbox("Mat", (iw, .004, ih), (0, -.008, 0), "MatWhite", .0008)
    print_surface("Print", print_w, print_h, -.0105)
    rbox("Glass", (iw, .002, ih), (0, -d + .012, 0), "Glass", .0006)
    for x in (-W / 2 + .06, W / 2 - .06):
        cyl("Hanger", (x, 0, H / 2 - .05), (x, .004, H / 2 - .05), .008, "AluFrame", 14)


def frame_portrait():
    frame(.50, .64, .04, .03, .380, .494)                     # print 1000 x 1300


def frame_landscape():
    frame(.64, .47, .036, .028, .52, .371)                    # print 1400 x 1000


def plaque():
    W, H = .44, .34
    rbox("Board", (W, .028, H), (0, -.014, 0), "FrameWalnut", .008)
    print_surface("Print", W - .04, H - .04, -.0295)
    for x in (-W / 2 + .018, W / 2 - .018):
        for z in (-H / 2 + .018, H / 2 - .018):
            cyl("Screw", (x, -.0295, z), (x, -.0325, z), .0045, "BrassScrew", 14, r=.0008)


def board():
    W, H = 1.34, .985
    bar = .028
    rbox("Frame_top", (W, .04, bar), (0, -.02, H / 2 - bar / 2), "AluFrame", .004)
    rbox("Frame_bottom", (W, .04, bar), (0, -.02, -H / 2 + bar / 2), "AluFrame", .004)
    rbox("Frame_left", (bar, .04, H - 2 * bar), (-W / 2 + bar / 2, -.02, 0), "AluFrame", .004)
    rbox("Frame_right", (bar, .04, H - 2 * bar), (W / 2 - bar / 2, -.02, 0), "AluFrame", .004)
    rbox("Backing", (W - 2 * bar + .004, .03, H - 2 * bar + .004), (0, -.015, 0), "MatWhite", .001)
    print_surface("Print", W - 2 * bar, H - 2 * bar, -.0305)
    rbox("Ledge", (W - .1, .035, .016), (0, -.0175, -H / 2 - .008), "AluFrame", .004)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    jobs = [("frame_portrait", frame_portrait), ("frame_landscape", frame_landscape), ("award_plaque", plaque), ("notice_board", board)]
    results = [pk.export(n, f, OUT, tri_budget=20000) for n, f in jobs]
    (OUT / "validation.json").write_text(json.dumps(results, indent=2) + "\n")
    print("WALL_ART", json.dumps([(r["name"], r["triangles"], r["bounds_blender"]) for r in results]))


assert bpy.app.version[:3] == (4, 5, 14)
if __name__ == "__main__":
    main()
