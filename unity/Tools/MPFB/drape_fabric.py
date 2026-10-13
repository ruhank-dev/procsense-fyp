"""Fit a skinned gown and softly folded blanket to the actual bedside pose.
Deterministic surface fitting replaces unstable free cloth on triangulated FBX seams.
The result has no runtime cloth component and preserves gown bone weights.
"""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
root=Path(__file__).resolve().parents[3]
output=root/'build/mpfb/output'
data=json.loads((output/'fabric-fit.json').read_text())
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
def mesh_object(name,vertices,faces):
 mesh=bpy.data.meshes.new(name);mesh.from_pydata(vertices,[],faces);mesh.update()
 obj=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(obj);return obj
def from_snapshot(name,snapshot):
 vertices=[(p[0],p[2],p[1]) for p in snapshot['vertices']];t=snapshot['triangles']
 return mesh_object(name,vertices,[(t[i],t[i+2],t[i+1]) for i in range(0,len(t),3)])
body=from_snapshot('Patient fitting surface',data['body'])
surface=BVHTree.FromPolygons([v.co for v in body.data.vertices],[p.vertices[:] for p in body.data.polygons])
gown=from_snapshot('Draped patient gown',data['gown'])
for i,name in enumerate(data['gown']['bones']):
 group=gown.vertex_groups.new(name=name)
 for vertex,weights in enumerate(data['gown']['weights']):
  weight=sum(weights[k+1] for k in range(0,8,2) if int(weights[k])==i)
  if weight>0:group.add([vertex],weight,'REPLACE')
# Weld FBX export seams before smoothing and preserve interpolated bone groups.
bm=bmesh.new();bm.from_mesh(gown.data)
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00005)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(gown.data);bm.free()
bpy.context.view_layer.objects.active=gown
subdivision=gown.modifiers.new('Soft garment surface','SUBSURF');subdivision.levels=1
bpy.ops.object.modifier_apply(modifier=subdivision.name)
for vertex in gown.data.vertices:
 point=vertex.co.copy();near,normal,_,distance=surface.find_nearest(point)
 if near is None:raise RuntimeError('Missing body fitting surface')
 direction=point-near
 if direction.length<.0001:direction=normal
 direction.normalize()
 clearance=.014+.003*(1+math.sin(point.y*35+point.x*24))
 if point.y<1.48:vertex.co=near+direction*max(clearance,min(distance,.022))
 vertex.co.z=max(vertex.co.z,.705)
for polygon in gown.data.polygons:polygon.use_smooth=True
# Bed cover fits over the gown but under the hands; arm triangles are excluded.
arm_parts=('arm','hand','index','middle','pinky','ring','thumb')
def arm_vertex(index):
 weights=data['body']['weights'][index]
 return any(weights[k+1]>.05 and any(part in data['body']['bones'][int(weights[k])] for part in arm_parts) for k in range(0,8,2))
fit_vertices=[v.co.copy() for v in body.data.vertices]+[v.co.copy() for v in gown.data.vertices]
fit_faces=[p.vertices[:] for p in body.data.polygons if not any(arm_vertex(i) for i in p.vertices)]
offset=len(body.data.vertices)
fit_faces += [tuple(i+offset for i in p.vertices) for p in gown.data.polygons]
cover_surface=BVHTree.FromPolygons(fit_vertices,fit_faces)
# Conservative envelope spans both legs instead of tracing the leg valley.
def body_height(length):
 heights=[]
 for x in [-.27,-.24,-.20,-.15,-.10,-.05,0,.05,.10,.15,.20,.24,.27]:
  hit,_,_,_=cover_surface.ray_cast(Vector((x,length,2)),Vector((0,0,-1)),2)
  if hit:heights.append(hit.z)
 return max(heights,default=.77)+.023
def cover_height(x,length):
 # Small neighboring footprint prevents triangles cutting through steep gown sides.
 heights=[]
 for dx in (-.025,0,.025):
  for dz in (-.018,0,.018):
   hit,_,_,_=cover_surface.ray_cast(Vector((x+dx,length+dz,2)),Vector((0,0,-1)),2)
   if hit:heights.append(hit.z)
 return max(heights) if heights else None
foot_points=[v.co for v in body.data.vertices if v.co.y<.36 and abs(v.co.x)<.32]
foot_roof=max(p.z for p in foot_points)+.030
foot_turn=min(p.y for p in foot_points)-.045
start=min(-.08,foot_turn-.15)
samples=[body_height(start+j*(1.18-start)/72) for j in range(73)]
for _ in range(4):
 old=samples[:]
 for j in range(1,72):samples[j]=max(old[j],(old[j-1]+2*old[j]+old[j+1])/4)
vertices=[];nx,ny=60,72
for j in range(ny+1):
 length=start+j*(1.18-start)/ny;top=samples[j]
 # The foot turn starts beyond the toes, not at a fixed coordinate through them.
 foot_blend=max(0,min(1,(.44-length)/.16));foot_blend=foot_blend*foot_blend*(3-2*foot_blend)
 top=max(top,top*(1-foot_blend)+foot_roof*foot_blend)
 for i in range(nx+1):
  x=-.60+i*1.20/nx;a=abs(x)
  plateau=.20+.10*foot_blend
  if a<=plateau:height=top-.004*(a/plateau)**2
  elif a<=.44:
   t=(a-plateau)/(.44-plateau);height=.715+(top-.719)*(1-t*t)
  else:
   t=(a-.44)/.16;height=.715-.18*math.sin(t*math.pi/2)
  if length<foot_turn:
   # Smoothly carry the foot-height envelope forward before dropping at the end.
   top_at_feet=foot_roof
   height+=max(0,top_at_feet-top)*max(0,1-(a/.48)**4)
   t=max(0,min(1,(foot_turn-length)/(foot_turn-start)));height-=.26*t*t
  # Hands now rest outside the cover; no artificial side notch is needed.
  hit=cover_height(x,length)
  if hit is not None and a<.38:height=max(height,hit+.023)
  # Last 18 cm rests down onto the actual gown/mattress contour.
  # A flat maximum-height section made the old waist edge stand up like a wall.
  upper=max(0,min(1,(length-1.00)/.18));upper=upper*upper*(3-2*upper)
  if hit is not None and a<.38:edge=hit+.025
  elif a<=.44:edge=.708
  else:edge=.708-.18*math.sin((a-.44)/.16*math.pi/2)
  height=height*(1-upper)+edge*upper
  if hit is not None and a<.38:height=max(height,hit+.023)
  fold=.005*math.sin(x*32+length*5)+.003*math.sin(length*29+x*7)
  vertices.append((x,length,height+fold*(1-.7*upper)))
faces=[]
for j in range(ny):
 for i in range(nx):
  a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
blanket=mesh_object('Draped bed blanket',vertices,faces)
for polygon in blanket.data.polygons:polygon.use_smooth=True
# Check the actual triangulated surface over every skin vertex in the foot region.
# Checking just the ankle bone omitted the toe/ball skin in the earlier fit.
blanket.data.calc_loop_triangles()
cover_tree=BVHTree.FromPolygons([v.co for v in blanket.data.vertices],[t.vertices[:] for t in blanket.data.loop_triangles])
clearances=[]
for point in foot_points:
 hit,_,_,_=cover_tree.ray_cast(Vector((point.x,point.y,2)),Vector((0,0,-1)),3)
 if hit is None:raise RuntimeError('Foot skin falls outside the blanket footprint')
 clearances.append(hit.z-point.z)
if min(clearances)<.008:raise RuntimeError('Foot skin penetrates the triangulated blanket')
(output/'fabric-coverage.json').write_text(json.dumps({'footVertices':len(foot_points),'minimumFootClearanceMetres':min(clearances),'blanketStart':start,'footTurn':foot_turn},indent=2))
def write_mesh(obj,path,bone_names=None):
 mesh=obj.data;mesh.calc_loop_triangles()
 payload={'vertices':[[v.co.x,v.co.z,v.co.y] for v in mesh.vertices],
  'triangles':[i for t in mesh.loop_triangles for i in (t.vertices[0],t.vertices[2],t.vertices[1])]}
 if bone_names:
  weights=[]
  for v in mesh.vertices:
   groups=[(bone_names.index(obj.vertex_groups[g.group].name),g.weight) for g in v.groups if obj.vertex_groups[g.group].name in bone_names]
   groups=sorted(groups,key=lambda g:g[1],reverse=True)[:4];total=sum(w for _,w in groups)
   if total<=0:raise RuntimeError('Missing garment skin weight')
   groups=[(i,w/total) for i,w in groups]+[(0,0)]*(4-len(groups))
   weights.append([n for pair in groups for n in pair])
  payload['bones']=bone_names;payload['weights']=weights
 path.write_text(json.dumps(payload))
write_mesh(gown,output/'draped-gown.json',data['gown']['bones'])
write_mesh(blanket,output/'draped-blanket.json')
bpy.ops.wm.save_as_mainfile(filepath=str(output/'patient-fabric.blend'))
print('FABRIC_COMPLETE',len(gown.data.vertices),len(blanket.data.vertices),flush=True)
