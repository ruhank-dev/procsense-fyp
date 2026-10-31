using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Re-bakes the connected three-room lighting after the PatientBay floor changed colour (the lightmapper bounces light off the floor's albedo).
    /// Same procedure as PatientBayLookUpgrade: PatientBay active with Corridor and OT open additively; their scene files are never saved or modified.
    /// The lighting settings asset is not edited. Reflection captures are refreshed afterwards by RoomReflectionUpgrade.Run / LocalProbe (separate runs).
    /// </summary>
    public static class FloorRebake
    {
        const string LightingPath = "Assets/Settings/HospitalLighting/PatientBay.lighting";

        public static void Run()
        {
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Additive);
            EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(bay);
            Lightmapping.lightingSettings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            var floor = bay.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name == "Floor");
            Debug.Log($"[FloorRebake] floor material {floor.sharedMaterial.name}; baking");
            if (!Lightmapping.Bake()) throw new InvalidOperationException("Bake failed");
            EditorSceneManager.MarkSceneDirty(bay); EditorSceneManager.SaveScene(bay);
            AssetDatabase.SaveAssets();
            Debug.Log($"[FloorRebake] done: lightmaps={LightmapSettings.lightmaps.Length} probes={LightmapSettings.lightProbes?.count}");
        }
    }
}
