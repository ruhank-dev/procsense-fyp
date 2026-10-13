using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class ImportMpfbPatient
{
 [Serializable] public class Entry { public string mesh; public string name; public string texture; public float[] color; public bool cutout; }
 [Serializable] public class Entries { public Entry[] items; }
 public static object Main()
 {
  const string folder="Assets/Models/Characters/MPFBPatient";
  string output=Path.GetFullPath("../build/mpfb/output");
  Directory.CreateDirectory(folder+"/Textures");Directory.CreateDirectory(folder+"/Materials");
  File.Copy(output+"/mpfb_patient.fbx",folder+"/mpfb_patient.fbx",true);
  foreach(string file in Directory.GetFiles(output+"/textures"))File.Copy(file,folder+"/Textures/"+Path.GetFileName(file),true);
  AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
  foreach(string file in Directory.GetFiles(folder+"/Textures","*.png"))
  {
   var texture=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
   texture.maxTextureSize=2048;texture.mipmapEnabled=true;texture.textureCompression=TextureImporterCompression.Compressed;
   var android=texture.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=2048;android.format=TextureImporterFormat.ASTC_6x6;texture.SetPlatformTextureSettings(android);texture.SaveAndReimport();
  }
  var entries=Newtonsoft.Json.JsonConvert.DeserializeObject<Entry[]>(File.ReadAllText(output+"/materials.json"));
  var importer=(ModelImporter)AssetImporter.GetAtPath(folder+"/mpfb_patient.fbx");
  importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=false;
  importer.importBlendShapes=true;importer.importNormals=ModelImporterNormals.Import;importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
  foreach(var e in entries)
  {
   string path=folder+"/Materials/"+e.name.Replace('.','_')+".mat";
   var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
   mat.name=e.name;mat.SetColor("_BaseColor",e.texture!=null?Color.white:new Color(e.color[0],e.color[1],e.color[2],e.color[3]));
   if(e.texture!=null)mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/"+e.texture));
   mat.SetFloat("_Smoothness",e.mesh.Contains("high-poly")?.6f:e.mesh.Contains("body")?.22f:.15f);
   mat.SetFloat("_Metallic",0);
   if(e.cutout){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_ALPHATEST_ON");mat.renderQueue=2450;}
   EditorUtility.SetDirty(mat);
   importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),e.name),mat);
  }
  importer.SaveAndReimport();AssetDatabase.SaveAssets();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/mpfb_patient.fbx");
  var avatar=AssetDatabase.LoadAllAssetsAtPath(folder+"/mpfb_patient.fbx").OfType<Avatar>().FirstOrDefault();
  return new {avatarValid=avatar!=null&&avatar.isValid,humanoid=avatar!=null&&avatar.isHuman,renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>().Select(r=>new {name=r.name,triangles=Enumerable.Range(0,r.sharedMesh.subMeshCount).Sum(i=>(long)r.sharedMesh.GetIndexCount(i)/3),shapes=r.sharedMesh.blendShapeCount,materials=r.sharedMaterials.Select(m=>m!=null?m.shader.name:"NULL").ToArray()}).ToArray()};
 }
}
