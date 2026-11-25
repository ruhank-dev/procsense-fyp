using ProcSense.VR.ActionEmitters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Gives the IV cannula kit and the thermometer the same blue-then-green placement bubble the BP cuff and the SpO2 probe have. Idempotent.</summary>
    public static class PlacementGuideSetup
    {
        [MenuItem("ProcSense/Equipment/Add placement guides to the IV kit and thermometer")]
        public static void Run()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SensorPlacementSetup.MaterialPath);
            if (material == null) { Debug.LogError("PlacementGuideSetup: " + SensorPlacementSetup.MaterialPath + " is missing (run Configure Sensor Placement Guides first)"); return; }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var iv in root.GetComponentsInChildren<IvKitUse>(true)) { iv.ConfigurePlacementHighlight(material); EditorUtility.SetDirty(iv); n++; }
                foreach (var c in root.GetComponentsInChildren<ContactActionEmitter>(true)) { c.ConfigurePlacementHighlight(material); EditorUtility.SetDirty(c); n++; }
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("PlacementGuideSetup: " + n + " items now show a placement bubble");
        }
    }
}
