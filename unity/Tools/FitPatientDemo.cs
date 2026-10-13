using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Views;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;
public static class FitPatientDemo {
 public static object Main(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play first.");
 var scene=SceneManager.GetSceneByPath("Assets/Scenes/PatientBay.unity");
 if(!scene.isLoaded || scene.isDirty)throw new InvalidOperationException("Open and save PatientBay first; unsaved edits will not be overwritten.");
 var bay=scene.GetRootGameObjects().Single(g=>g.name=="PatientBay");
 var model=bay.transform.Find("Patient model trial");
 var demo=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/PatientTrial/patient_review.fbx");
 if(demo==null)throw new InvalidOperationException("Import the preserved patient first.");
 model.GetComponent<MeshFilter>().sharedMesh=demo.GetComponentInChildren<MeshFilter>().sharedMesh;
 PrefabUtility.RecordPrefabInstancePropertyModifications(model.GetComponent<MeshFilter>());
 var root=bay.transform.Find("Physical bedside monitoring");
 var cuff=root.Find("nibp_cuff patient socket");cuff.position=new Vector3(.225f,.833f,1.20f);cuff.rotation=Quaternion.Euler(0,0,-6);
 var probe=root.Find("spo2 patient socket");probe.position=new Vector3(.218f,.914f,.937f);probe.rotation=Quaternion.Euler(0,-25,-15);
 var arm=root.Find("Patient forearm placeholder");arm.position=new Vector3(.23f,.875f,1.08f);
 var finger=root.Find("Finger placeholder");finger.position=probe.position;finger.rotation=probe.rotation;
 var wrap=root.GetComponentsInChildren<Transform>(true).Single(t=>t.name.EndsWith("bp_cuff_Wrapped"));wrap.localScale=new Vector3(1.05f,1.28f,.80f);
 var old=bay.transform.Find("Wristband");if(old!=null)old.gameObject.SetActive(false);
 var wrist=bay.transform.Find("Patient wristband");
 if(wrist==null){
 var g=new GameObject("Patient wristband");g.transform.SetParent(bay.transform,false);g.transform.position=new Vector3(.23f,.892f,1.065f);
 var mesh=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/PatientTrial/patient_wristband.obj").GetComponentInChildren<MeshFilter>().sharedMesh;
 g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Equipment/M_PlasticWhite.mat");
 var col=g.AddComponent<BoxCollider>();col.size=new Vector3(.10f,.085f,.055f);
 g.AddComponent<XRSimpleInteractable>();g.AddComponent<XRSelectEmitter>().Configure("read_wristband","patient",fallbackLabel:"Read wristband");
 var canvas=UiKit.WorldCanvas("Wristband label",g.transform,new Vector2(360,150),.00024f);
 canvas.transform.localPosition=new Vector3(0,.037f,0);canvas.transform.localRotation=Quaternion.Euler(90,0,0);
 canvas.GetComponentInChildren<UnityEngine.UI.Image>().color=Color.white;
 var text=UiKit.Label(canvas.transform,"Identifiers","READ WRISTBAND",new Vector2(10,8),new Vector2(340,134),28,TextAlignmentOptions.Center,Color.black);
 canvas.gameObject.AddComponent<EquipmentSurfaceView>().Configure(EquipmentSurfaceView.Surface.Identity,text);
 }
 bay.transform.Find("Patient wristband").position=new Vector3(.23f,.892f,1.065f);
 bay.transform.Find("Patient wristband").GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/PatientTrial/patient_wristband.obj").GetComponentInChildren<MeshFilter>().sharedMesh;
 EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 return "Demo sockets and fixed live wristband fitted";
 }
}
