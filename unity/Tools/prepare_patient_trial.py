import bpy, math
from pathlib import Path
Path('unity/Logs/patient-review').mkdir(parents=True, exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(Path('patientModel/3d-410.blend').resolve()))
original=next(o for o in bpy.data.objects if o.type=='MESH')
bpy.ops.object.select_all(action='DESELECT');original.select_set(True);bpy.context.view_layer.objects.active=original
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
low=original.copy();low.data=original.data.copy();bpy.context.collection.objects.link(low);low.name='PatientReview_Static'
bpy.ops.object.select_all(action='DESELECT');low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.remove_doubles(threshold=.00001);bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')
mod=low.modifiers.new('Review reduction','DECIMATE');mod.ratio=40000/1500000;bpy.ops.object.modifier_apply(modifier=mod.name)
for u in list(low.data.uv_layers):low.data.uv_layers.remove(u)
low.data.uv_layers.new(name='PatientAtlas')
bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.01);bpy.ops.object.mode_set(mode='OBJECT')
mat=bpy.data.materials.new('PatientBaked');mat.use_nodes=True;low.data.materials.clear();low.data.materials.append(mat)
image=bpy.data.images.new('PatientBaked',width=2048,height=2048,alpha=False)
node=mat.node_tree.nodes.new('ShaderNodeTexImage');node.image=image;mat.node_tree.nodes.active=node
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=1;scene.render.bake.margin=16
original.select_set(True);low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.object.bake(type='DIFFUSE',pass_filter={'COLOR'},use_selected_to_active=True,cage_extrusion=.01,max_ray_distance=.04)
image.filepath_raw=str(Path('unity/Logs/patient-review/patient_baked.png').resolve());image.file_format='PNG';image.save()
bpy.ops.object.select_all(action='DESELECT');low.select_set(True);bpy.context.view_layer.objects.active=low
bpy.ops.export_scene.fbx(filepath=str(Path('unity/Logs/patient-review/patient_review.fbx').resolve()),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_anim=False,path_mode='STRIP')
low.data.calc_loop_triangles();print('BAKED_TRIANGLES',len(low.data.loop_triangles))
