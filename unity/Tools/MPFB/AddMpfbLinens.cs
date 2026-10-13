using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class AddMpfbLinens
{
 public static object Main()
 {
  const string dir="Assets/Models/Characters/MPFBPatient";
  foreach(var id in new[]{"patient_pillow","patient_blanket"})
  {
   File.Copy("../build/mpfb/output/"+id+".obj",dir+"/"+id+".obj",true);
   AssetDatabase.ImportAsset(dir+"/"+id+".obj",ImportAssetOptions.ForceSynchronousImport);
   var importer=(ModelImporter)AssetImporter.GetAtPath(dir+"/"+id+".obj");importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
  }
  string matPath=dir+"/Materials/BedLinen.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
  if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
  mat.SetColor("_BaseColor",new Color(.90f,.93f,.95f));mat.SetFloat("_Smoothness",.12f);mat.SetFloat("_Cull",0);EditorUtility.SetDirty(mat);
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var bay=scene.GetRootGameObjects().Single(g=>g.name=="PatientBay");
  foreach(var id in new[]{"patient_pillow","patient_blanket"})
  {
   if(bay.transform.Find(id)!=null)continue;
   var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(dir+"/"+id+".obj"),bay.transform);obj.name=id;
   foreach(var r in obj.GetComponentsInChildren<Renderer>()){r.sharedMaterial=mat;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
   if(id=="patient_pillow")obj.transform.position=new Vector3(0,.785f,1.76f);
   else if(obj.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.center.z<0)obj.transform.rotation=Quaternion.Euler(0,180,0);
  }
  bay.transform.Find("Bed").GetComponent<Renderer>().sharedMaterial=mat;
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  return "Review pillow, cover and mattress linen installed";
 }
}
