"""Build the fitted wristband; preserve the patient pose unchanged."""
import bpy
from pathlib import Path
root = Path(__file__).resolve().parents[2]
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
# Fitted procedural wristband: exported separately so patient appearance stays replaceable.
import math
count = 48
verts = [(rx*math.cos(i*math.tau/count), depth, rz*math.sin(i*math.tau/count))
         for depth in [-.012, .012] for rx, rz in [(.045,.033),(.047,.035)] for i in range(count)]
faces = []
for i in range(count):
 j = (i+1)%count
 faces.extend([(i,j,2*count+j,2*count+i),(count+j,count+i,3*count+i,3*count+j),
               (j,i,count+i,count+j),(2*count+i,2*count+j,3*count+j,3*count+i)])
mesh = bpy.data.meshes.new('Demo wristband')
mesh.from_pydata(verts, [], faces)
ring = bpy.data.objects.new('PatientWristband', mesh)
bpy.context.collection.objects.link(ring)
bpy.ops.object.select_all(action='DESELECT')
ring.select_set(True)
bpy.context.view_layer.objects.active = ring
bpy.ops.wm.obj_export(filepath=str(root/'unity/Logs/patient-review/patient_wristband.obj'), export_selected_objects=True, forward_axis='NEGATIVE_Z', up_axis='Y', export_materials=False)
