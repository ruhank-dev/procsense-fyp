"""Procedural pillow and draped bed cover for the MPFB review bay."""
import bpy,math
from pathlib import Path
root=Path(__file__).resolve().parents[3]
output=root/'build/mpfb/output';output.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_uv_sphere_add(segments=32,ring_count=16,radius=1)
pillow=bpy.context.object;pillow.name='PatientPillow';pillow.scale=(.25,.17,.055)
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
for face in pillow.data.polygons:face.use_smooth=True
bpy.ops.wm.obj_export(filepath=str(output/'patient_pillow.obj'),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=False)
bpy.ops.object.select_all(action='DESELECT')
vertices=[];faces=[];nx,nz=32,36
for j in range(nz+1):
 z=.04+j*1.06/nz
 for i in range(nx+1):
  x=-.46+i*.92/nx
  dome=.12*math.exp(-((abs(x)-.12)/.13)**2)
  height=.765+dome*(.75+.25*z/1.1)+.007*math.sin(x*48+z*14)*math.sin(math.pi*i/nx)
  # Coordinates export to Unity as x, height, length.
  vertices.append((x,z,height))
import json
if (output/'cover-surface.json').exists():
 vertices=json.loads((output/'cover-surface.json').read_text())
for j in range(nz):
 for i in range(nx):
  a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
mesh=bpy.data.meshes.new('Bed cover');mesh.from_pydata(vertices,[],faces);mesh.update()
cover=bpy.data.objects.new('PatientBlanket',mesh);bpy.context.collection.objects.link(cover)
for face in mesh.polygons:face.use_smooth=True
cover.select_set(True);bpy.context.view_layer.objects.active=cover
bpy.ops.wm.obj_export(filepath=str(output/'patient_blanket.obj'),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=False)
