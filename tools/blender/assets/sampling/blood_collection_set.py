import math
import bpy
from procsense_bpy import box, finish, point, rod, tube


def build():
    # Sourced: a 21 G needle is 0.8 mm across with a green hub (ISO 6009); the tourniquet strap is 25 mm wide and about 457 mm long
    # (docs/research/equipment/16-blood-collection-set-and-tourniquet.md). The holder, butterfly wings and tubing length are design sizes.
    box("Body", (.457, .025, .0012), (0, -.08, .0006), "AccentBlue", .0003)          # the tourniquet laid flat (the loose state)
    # the applied state: a closed band round an upper arm (about 90 mm across), named Wrapped so Unity hides it until the strap is applied (as the cuff's)
    count, radius, depth, thickness = 32, .045, .025, .0012
    vertices = [(.3 + r * math.cos(i * math.tau / count), y - .2, .05 + r * math.sin(i * math.tau / count))
                for y in [-depth / 2, depth / 2] for r in [radius, radius + thickness] for i in range(count)]
    faces = []
    for i in range(count):
        j = (i + 1) % count
        faces += [(i, j, 2 * count + j, 2 * count + i), (count + j, count + i, 3 * count + i, 3 * count + j),
                  (j, i, count + i, count + j), (2 * count + i, 2 * count + j, 3 * count + j, 3 * count + i)]
    mesh = bpy.data.meshes.new("Applied tourniquet")
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new("Wrapped", mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    finish(obj, "Wrapped", "AccentBlue")
    rod("Holder", (0, 0, .008), (0, .06, .008), .008, "PlasticClear", 24)
    rod("HolderFlange", (0, .058, .008), (0, .062, .008), .013, "PlasticClear", 24)
    rod("NeedleHub", (0, -.012, .008), (0, 0, .008), .004, "NeedleGreen", 12)
    rod("Needle", (0, -.04, .008), (0, -.012, .008), .0004, "SteelBrushed", 8)
    # the butterfly with its tubing
    box("WingL", (.012, .016, .0008), (.088, .02, .002), "NeedleGreen", .0002)
    box("WingR", (.012, .016, .0008), (.112, .02, .002), "NeedleGreen", .0002)
    rod("ButterflyNeedle", (.1, -.005, .002), (.1, .012, .002), .0004, "SteelBrushed", 8)
    tube("ButterflyTubing", [(.1, .028, .002), (.1, .1, .002), (.13, .16, .002), (.17, .18, .002)], .0012, "PlasticClear")
    point("GRAB_Holder", (0, .03, .008))
    point("SOCKET_Tube", (0, .06, .008))
    point("GRAB_Butterfly", (.1, .02, .003))
    point("GRAB_Tourniquet", (0, -.08, .001))
    point("COL_Body", (0, -.08, .002), (.457, .025, .006))
    point("COL_Holder", (0, .02, .008), (.03, .1, .03))
