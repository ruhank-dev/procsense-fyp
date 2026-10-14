import bpy,sys,json
from pathlib import Path
root=Path(__file__).resolve().parents[3];src=next((root/'build/mpfb/source').glob('*/src'))
bpy.context.preferences.extensions.repos.new(name='ProcSense MPFB',module='procsense',custom_directory=str(src))
import addon_utils
addon_utils.enable('bl_ext.procsense.mpfb',default_set=True)
from bl_ext.procsense.mpfb.services.locationservice import LocationService
import bl_ext.procsense.mpfb as mpfb
prefs=bpy.context.preferences.addons['bl_ext.procsense.mpfb'].preferences
prefs.mpfb_second_root=str(root/'build/mpfb/asset-library');LocationService.update_second_root()
from bl_ext.procsense.mpfb.services.humanservice import HumanService
from bl_ext.procsense.mpfb.services.exportservice import ExportService
from bl_ext.procsense.mpfb.services.targetservice import TargetService
from bl_ext.procsense.mpfb.services.faceservice import FaceService
from bl_ext.procsense.mpfb.services.objectservice import ObjectService
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
preset=json.loads((root/'unity/Tools/MPFB/patient-preset.json').read_text())
settings=HumanService.get_default_deserialization_settings();settings['subdiv_levels']=0;settings['override_clothes_model']='GAMEENGINE';settings['override_eyes_model']='GAMEENGINE'
human=HumanService.deserialize_from_dict(preset,settings)
rig=human.parent
print('RIG',rig.name,len(rig.data.bones),[b.name for b in rig.data.bones])
TargetService.bake_targets(human)
FaceService.load_targets(human,load_microsoft_visemes=False,load_meta_visemes=True,load_arkit_faceunits=True)
FaceService.interpolate_targets(human)
print('SHAPES',len(human.data.shape_keys.key_blocks))
for o in list(rig.children):
 if o.type=='MESH':
  if o==human:
   for mod in list(o.modifiers):
    if mod.type=='MASK' and mod.vertex_group!='body':o.modifiers.remove(mod)
  ExportService.bake_modifiers_remove_helpers(o,bake_masks=True,bake_subdiv=False,remove_helpers=True,also_proxy=False)
  for m in list(o.modifiers):
   if m.type=='SUBSURF':o.modifiers.remove(m)
  o.data.calc_loop_triangles();print('PART',o.name,len(o.data.vertices),len(o.data.loop_triangles))
(root/'build/mpfb/output').mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'build/mpfb/output/patient-source.blend'))
(root/'build/mpfb/output/preset.json').write_text(json.dumps(preset,indent=2))
# Convert the CC0 short-sleeved shirt component into a loose patient gown.
import bmesh,math,shutil
from mathutils import Vector,Matrix
cloth=next(o for o in rig.children if 'male_casualsuit04' in o.name)
bm=bmesh.new();bm.from_mesh(cloth.data)
remove=[v for v in bm.verts if v.co.z<.97 and abs(v.co.x)>.01]
# Identify connected components, keeping the shirt and discarding the jeans.
components=[];remaining=set(bm.verts)
while remaining:
 seed=remaining.pop();component={seed};todo=[seed]
 while todo:
  vertex=todo.pop()
  for edge in vertex.link_edges:
   other=edge.other_vert(vertex)
   if other in remaining:remaining.remove(other);component.add(other);todo.append(other)
 components.append(component)
shirt=max(components,key=lambda group:max(v.co.z for v in group))
bmesh.ops.delete(bm,geom=[v for group in components if group is not shirt for v in group],context='VERTS')
bm.to_mesh(cloth.data);bm.free()
# Baked child shape keys are unnecessary for clothing away from the face.
cloth.shape_key_clear()
for vertex in cloth.data.vertices:
 co=vertex.co
 t=max(0,min(1,(1.08-co.z)/.18));t=t*t*(3-2*t)
 co.z-=.38*t
 co.x*=1+.15*t
 co.y*=1+.5*t
 co.y+=(-.05 if co.y<0 else .03)*t
 # Loosen the fabric using its existing vertex normals, keeping skin separate.
 co+=vertex.normal*.014
cloth.name='PatientGown'
material=bpy.data.materials.new('PatientGown_Blue');material.use_nodes=True
bsdf=material.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Base Color'].default_value=(.27,.48,.62,1);bsdf.inputs['Roughness'].default_value=.85
cloth.data.materials.clear();cloth.data.materials.append(material)
for obj in rig.children:
 if obj.type=='MESH':
  for face in obj.data.polygons:face.use_smooth=True
# Preserve the upright bind-pose asset, plus explicit bone rotations for the bed.
pose={}
def aim_bone(name,direction):
 pb=rig.pose.bones[name]
 axis=pb.matrix.to_3x3()@Vector((0,1,0))
 q=axis.rotation_difference(Vector(direction).normalized())@pb.matrix.to_quaternion()
 matrix=q.to_matrix().to_4x4();matrix.translation=pb.head
 pb.matrix=matrix;bpy.context.view_layer.update()
for side,sign in [('l',1),('r',-1)]:
 aim_bone('upperarm_'+side,(sign*.12,-.04,-1))
 aim_bone('lowerarm_'+side,(sign*.02,-.04,-1))
 aim_bone('hand_'+side,(0,-.04,-1))
for bone in rig.pose.bones:
 pose[bone.name]=list(bone.rotation_quaternion) if bone.rotation_mode=='QUATERNION' else list(bone.matrix_basis.to_quaternion())
output=root/'build/mpfb/output'
(output/'bed-pose.json').write_text(json.dumps(pose,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(output/'patient-bedside.blend'))
# Material mapping and a local texture bundle for the Unity importer.
textures=output/'textures';textures.mkdir(exist_ok=True)
materials=[]
for obj in rig.children:
 if obj.type!='MESH':continue
 for mat in obj.data.materials:
  if not mat or not mat.use_nodes:continue
  nodes=mat.node_tree.nodes
  diffuse=next((n for n in nodes if n.type=='TEX_IMAGE' and n.name=='DiffuseTexture'),None)
  texture=None
  if diffuse and diffuse.image:
   src=Path(bpy.path.abspath(diffuse.image.filepath));texture=src.name
   if not src.exists():raise RuntimeError('Missing texture '+str(src))
   shutil.copyfile(src,textures/texture)
  principled=next((n for n in nodes if n.type=='BSDF_PRINCIPLED'),None)
  color=list(principled.inputs['Base Color'].default_value) if principled else [1,1,1,1]
  materials.append({'mesh':obj.name,'name':mat.name,'texture':texture,'color':color,'cutout':any(part in obj.name for part in ['hair','short02','eyebrow','eyelashes'])})
(output/'materials.json').write_text(json.dumps(materials,indent=2))
# Export neutral bind-pose so Unity can validate the Humanoid avatar.
for bone in rig.pose.bones:bone.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for obj in rig.children:obj.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(output/'mpfb_patient.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},use_mesh_modifiers=False,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=True,bake_anim=False,path_mode='STRIP')
report={'mpfb':'2.0.18','bones':len(rig.data.bones),'meshes':[]}
for obj in rig.children:
 if obj.type=='MESH':
  obj.data.calc_loop_triangles()
  report['meshes'].append({'name':obj.name,'triangles':len(obj.data.loop_triangles),'shapes':list(obj.data.shape_keys.key_blocks.keys()) if obj.data.shape_keys else []})
(output/'validation.json').write_text(json.dumps(report,indent=2))
print('EXPORT_COMPLETE',str(output/'mpfb_patient.fbx'))
