using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
public static class CreateMpfbReview
{
 public const string Path="Assets/Scenes/MPFBPatientReview.unity";
 static Bounds SkinBounds(GameObject model)
 {
  var bound=new Bounds();bool first=true;
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name.Contains(".body")))
  {
   var mesh=r.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var poses=mesh.bindposes;
   var matrices=r.bones.Select((bone,i)=>bone.localToWorldMatrix*poses[i]).ToArray();
   for(int i=0;i<vertices.Length;i++)
   {
    var w=weights[i];var p=matrices[w.boneIndex0].MultiplyPoint3x4(vertices[i])*w.weight0;
    if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(vertices[i])*w.weight1;
    if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(vertices[i])*w.weight2;
    if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(vertices[i])*w.weight3;
    if(first){bound=new Bounds(p,Vector3.zero);first=false;}else bound.Encapsulate(p);
   }
  }
  return bound;
 }
 static void Aim(Transform joint,Transform next,Vector3 direction)
 {joint.rotation=Quaternion.FromToRotation(next.position-joint.position,direction.normalized)*joint.rotation;}
 public static object Refit()
 {
  var wrapper=GameObject.Find("MPFB patient");var model=wrapper.transform.GetChild(0).gameObject;
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/MPFBPatient/mpfb_patient.fbx");
  var source=asset.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  var joints=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  foreach(string poseSide in new[]{"l","r"})
  {
   foreach(string part in new[]{"upperarm_","lowerarm_","hand_"})joints[part+poseSide].localRotation=source[part+poseSide].localRotation;
   float sign=Mathf.Sign(joints["upperarm_"+poseSide].position.x);
   Aim(joints["upperarm_"+poseSide],joints["lowerarm_"+poseSide],new Vector3(sign*.46f,.01f,-1));
   Aim(joints["lowerarm_"+poseSide],joints["hand_"+poseSide],new Vector3(sign*.10f,.01f,-1));
  }
  foreach(var joint in joints.Values)if(PrefabUtility.IsPartOfPrefabInstance(joint))PrefabUtility.RecordPrefabInstancePropertyModifications(joint);
  var bounds=SkinBounds(model);
  wrapper.transform.position+=new Vector3(-bounds.center.x,.72f-bounds.min.y,1-bounds.center.z);
  var bones=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  var upper=bones["upperarm_l"].position.x>0?bones["upperarm_l"]:bones["upperarm_r"];string side=upper.name.EndsWith("_l")?"l":"r";
  var elbow=bones["lowerarm_"+side];var wrist=bones["hand_"+side];var finger=bones["index_02_"+side];var tip=bones["index_03_"+side];
  var cuff=bones["nibp_cuff patient socket"];var probe=bones["spo2 patient socket"];var band=bones["Patient wristband"];
  cuff.position=Vector3.Lerp(upper.position,elbow.position,.68f);probe.position=Vector3.Lerp(finger.position,tip.position,.55f);band.position=wrist.position+(elbow.position-wrist.position).normalized*.028f;
  cuff.rotation=Quaternion.LookRotation(upper.position-elbow.position,Vector3.up);probe.rotation=Quaternion.LookRotation(finger.position-tip.position,Vector3.up);band.rotation=Quaternion.LookRotation(elbow.position-wrist.position,Vector3.up);
  var scene=wrapper.scene;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  return new {bounds=SkinBounds(model).ToString(),cuff=cuff.position.ToString(),probe=probe.position.ToString()};
 }
 public static object Main()
 {
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
  for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save existing scene edits first.");
  if(!File.Exists(Path))AssetDatabase.CopyAsset("Assets/Scenes/PatientBay.unity",Path);
  var scene=EditorSceneManager.OpenScene(Path,OpenSceneMode.Single);
  var bay=scene.GetRootGameObjects().Single(g=>g.name=="PatientBay");
  var old=bay.transform.Find("Patient model trial");old.gameObject.SetActive(false);
  var existing=bay.transform.Find("MPFB patient");if(existing!=null)throw new InvalidOperationException("Review already exists; preserve manual edits.");
  var wrapper=new GameObject("MPFB patient");wrapper.transform.SetParent(bay.transform,false);
  var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/MPFBPatient/mpfb_patient.fbx"),wrapper.transform);
  var animator=model.GetComponent<Animator>();if(animator!=null)animator.enabled=false;
  var initial=SkinBounds(model);wrapper.transform.localScale=Vector3.one*(1.75f/initial.size.y);
  wrapper.transform.rotation=Quaternion.Euler(-90,180,0);
  var bones=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/MPFBPatient/mpfb_patient.fbx");
  var source=asset.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  var joints=model.GetComponentsInChildren<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
  foreach(string poseSide in new[]{"l","r"})
  {
   foreach(string part in new[]{"upperarm_","lowerarm_","hand_"})joints[part+poseSide].localRotation=source[part+poseSide].localRotation;
   float sign=Mathf.Sign(joints["upperarm_"+poseSide].position.x);
   Aim(joints["upperarm_"+poseSide],joints["lowerarm_"+poseSide],new Vector3(sign*.46f,.01f,-1));
   Aim(joints["lowerarm_"+poseSide],joints["hand_"+poseSide],new Vector3(sign*.10f,.01f,-1));
  }
  foreach(var joint in joints.Values)if(PrefabUtility.IsPartOfPrefabInstance(joint))PrefabUtility.RecordPrefabInstancePropertyModifications(joint);
  var bounds=SkinBounds(model);
  wrapper.transform.position+=new Vector3(-bounds.center.x,.72f-bounds.min.y,1-bounds.center.z);
  foreach(var bone in bones.Values)if(PrefabUtility.IsPartOfPrefabInstance(bone))PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
  if(animator!=null)PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
  var upper=bones["upperarm_l"].position.x>0?bones["upperarm_l"]:bones["upperarm_r"];
  string suffix=upper.name.EndsWith("_l")?"l":"r";
  var elbowBone=bones["lowerarm_"+suffix];var wrist=bones["hand_"+suffix];var finger=bones["index_02_"+suffix];var tip=bones["index_03_"+suffix];
  var monitors=bay.transform.Find("Physical bedside monitoring");
  var cuff=monitors.Find("nibp_cuff patient socket");cuff.SetParent(upper,true);cuff.position=Vector3.Lerp(upper.position,elbowBone.position,.68f);cuff.rotation=Quaternion.LookRotation(upper.position-elbowBone.position,Vector3.up);
  var probe=monitors.Find("spo2 patient socket");probe.SetParent(finger,true);probe.position=Vector3.Lerp(finger.position,tip.position,.55f);probe.rotation=Quaternion.LookRotation(finger.position-tip.position,Vector3.up);
  var band=bay.transform.Find("Patient wristband");band.SetParent(wrist,true);band.position=wrist.position+(elbowBone.position-wrist.position).normalized*.028f;band.rotation=Quaternion.LookRotation(elbowBone.position-wrist.position,Vector3.up);
  var ring=band.GetComponent<MeshFilter>();var ringVisual=new GameObject("Fitted band ring");ringVisual.transform.SetParent(band,false);ringVisual.transform.localScale=new Vector3(.72f,.85f,1);
  ringVisual.AddComponent<MeshFilter>().sharedMesh=ring.sharedMesh;ringVisual.AddComponent<MeshRenderer>().sharedMaterial=band.GetComponent<MeshRenderer>().sharedMaterial;
  UnityEngine.Object.DestroyImmediate(ring);UnityEngine.Object.DestroyImmediate(band.GetComponent<MeshRenderer>());
  foreach(var placeholder in new[]{"Patient forearm placeholder","Finger placeholder"}){var p=monitors.Find(placeholder);if(p!=null)p.gameObject.SetActive(false);}
  // Candidate room stays separate; its exit would otherwise return to the fallback bay.
  foreach(var door in bay.GetComponentsInChildren<ProcSense.VR.Boot.RoomDoor>())door.gameObject.SetActive(false);
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  var view=EditorWindow.GetWindow<SceneView>();view.pivot=new Vector3(0,.9f,1);view.rotation=Quaternion.LookRotation(new Vector3(-1.8f,-2,1));view.size=1.1f;view.orthographic=false;view.Repaint();
  return new {scene=Path,bounds=SkinBounds(model).ToString(),cuff=cuff.position.ToString(),probe=probe.position.ToString(),wrist=band.position.ToString(),avatar=animator!=null&&animator.avatar!=null&&animator.avatar.isValid};
 }
}
