import math
import bpy
from procsense_bpy import box, point, finish


def build():
    box("Body", (.30,.16,.003), (0,0,.0015), "FabricNavy", .001)
    box("Closure", (.055,.15,.001), (.12,0,.0035), "RubberBlack", .0002)
    # Separate closed annulus: a visual wrapped state, no cloth/pressure simulation.
    count, radius, depth, thickness = 48, .30/math.tau, .16, .003
    vertices = [(r*math.cos(i*math.tau/count), y, r*math.sin(i*math.tau/count))
                for y in [-depth/2,depth/2] for r in [radius,radius+thickness] for i in range(count)]
    faces = []
    for i in range(count):
        j = (i+1)%count
        faces += [(i,j,2*count+j,2*count+i), (count+j,count+i,3*count+i,3*count+j),
                  (j,i,count+i,count+j),(2*count+i,2*count+j,3*count+j,3*count+i)]
    mesh = bpy.data.meshes.new("Wrapped cuff")
    mesh.from_pydata(vertices, [], faces)
    obj = bpy.data.objects.new("Wrapped",mesh)
    bpy.context.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    finish(obj,"Wrapped","FabricNavy")
    point("GRAB_Cuff", (0,0,0))
    point("SOCKET_Arm", (0,0,0))
    point("ATTACH_Hose", (.05,0,0))
    point("COL_Body", (0,0,0), (.30,.16,.015))
