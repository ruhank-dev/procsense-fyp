using System.Linq;
using ProcSense.VR.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// P3-CTX-02 placement for the patient bay: puts an Interactable on each object the trainee can point at, with its entity id, type and voice name, then reports anything that is wrong with it and saves the scene.
    /// Run: Unity -batchmode -projectPath unity -executeMethod ProcSense.EditorTools.InteractablePlacement.PatientBay (idempotent: running it again changes nothing). Reads nothing from Core and adds no behaviour.
    /// </summary>
    public static class InteractablePlacement
    {
        // object name in the scene, entity id, type, voice name
        private static readonly (string objectName, string id, EntityType type, string voice, bool atRuntime)[] Bay =
        {
            ("Rocketbox patient", "patient", EntityType.Patient, "patient", false),
            ("Monitor", "monitor_bay", EntityType.Equipment, "", false),
            ("Notes", "notes_folder", EntityType.Records, "", false),
            ("Records workstation", "records_workstation", EntityType.Records, "", true),
            ("PHONE_CriticalResults", "phone_results", EntityType.Records, "", true),
        };

        public static void PatientBay()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            int placed = 0, problems = 0;
            foreach (var (objectName, id, type, voice, atRuntime) in Bay)
            {
                var go = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.gameObject).FirstOrDefault(g => g.name == objectName && g.scene == scene);
                if (go == null) { Debug.LogWarning("[Interactables] not found in PatientBay: " + objectName); problems++; continue; }
                var i = go.GetComponent<Interactable>() ?? go.AddComponent<Interactable>();
                i.Configure(id, type, voice, null, null, atRuntime);
                EditorUtility.SetDirty(i);
                placed++;
                foreach (var p in i.Problems()) { Debug.LogWarning("[Interactables] " + p); problems++; }
            }
            var ids = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(x => x.EntityId).ToList();
            foreach (var dup in ids.GroupBy(x => x).Where(g => g.Count() > 1)) { Debug.LogWarning("[Interactables] id used twice: " + dup.Key); problems++; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Interactables] patient bay: {placed} placed, {problems} problems");
        }
    }
}
