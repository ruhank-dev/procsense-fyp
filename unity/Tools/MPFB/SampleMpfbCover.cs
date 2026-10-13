using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
public static class SampleMpfbCover
{
 public static object Main()
 {
  var r=GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Single(x=>x.name.Contains(".body"));
  var source=r.sharedMesh;var points=source.vertices;var weights=source.boneWeights;var matrices=r.bones.Select((b,i)=>b.localToWorldMatrix*source.bindposes[i]).ToArray();
  var world=new Vector3[points.Length];
  bool IsArm(BoneWeight w){return new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3}.Any(i=>new[]{"arm","hand","index","middle","pinky","ring","thumb"}.Any(part=>r.bones[i].name.Contains(part)));}
  for(int i=0;i<world.Length;i++){var w=weights[i];world[i]=matrices[w.boneIndex0].MultiplyPoint3x4(points[i])*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(points[i])*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(points[i])*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(points[i])*w.weight3;}
  var input=source.triangles;var triangles=new List<int>();
  for(int i=0;i<input.Length;i+=3)if(!IsArm(weights[input[i]])&&!IsArm(weights[input[i+1]])&&!IsArm(weights[input[i+2]])){triangles.Add(input[i]);triangles.Add(input[i+1]);triangles.Add(input[i+2]);}
  var mesh=new Mesh();mesh.vertices=world;mesh.triangles=triangles.ToArray();mesh.RecalculateBounds();
  var scratch=new GameObject("Temporary linen fit");scratch.hideFlags=HideFlags.HideAndDontSave;var collider=scratch.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
  var values=new List<float[]>();const int nx=32,nz=36;
  var heights=new float[nz+1,nx+1];
  for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++){float x=-.46f+i*.92f/nx,z=.04f+j*1.06f/nz;heights[j,i]=collider.Raycast(new Ray(new Vector3(x,2,z),Vector3.down),out var hit,3)?hit.point.y+.023f:.745f;}
  // Span the leg valley and soften the drop onto the mattress.
  for(int pass=0;pass<5;pass++){var old=(float[,])heights.Clone();for(int j=0;j<=nz;j++)for(int i=1;i<nx;i++)heights[j,i]=Mathf.Max(old[j,i],(old[j,i-1]+old[j,i+1])*.5f-.008f);}
  for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)values.Add(new[]{-.46f+i*.92f/nx,.04f+j*1.06f/nz,heights[j,i]});
  File.WriteAllText("../build/mpfb/output/cover-surface.json",Newtonsoft.Json.JsonConvert.SerializeObject(values));
  UnityEngine.Object.DestroyImmediate(scratch);UnityEngine.Object.DestroyImmediate(mesh);
  return new {samples=values.Count,minimum=values.Min(v=>v[2]),maximum=values.Max(v=>v[2])};
 }
}
