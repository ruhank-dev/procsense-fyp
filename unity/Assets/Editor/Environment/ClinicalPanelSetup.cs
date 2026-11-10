using System;
using System.Linq;
using ProcSense.VR.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ProcSense.EditorTools
{
    public static class ClinicalPanelSetup
    {
        [MenuItem("ProcSense/Setup/Place checklist and debrief stations")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes and stop Play/baking before placing stations.");
            var bay = SceneManager.GetSceneByName("PatientBay"); var ot = SceneManager.GetSceneByName("OT");
            if (!bay.isLoaded || !ot.isLoaded) throw new InvalidOperationException("Open PatientBay and OT first.");
            foreach (var scene in new[] { bay, ot })
                if (scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ClinicalPanelStation>(true)).Any())
                    throw new InvalidOperationException("A station exists; edit its anchors instead of replacing it.");
            ClinicalPanelStation Station(Scene scene)
            {
                var root = new GameObject("Checklist and debrief station"); SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Place clinical station"); return root.AddComponent<ClinicalPanelStation>();
            }
            Transform Anchor(ClinicalPanelStation station, string name, Vector3 at, float yaw)
            {
                var anchor = new GameObject(name).transform; anchor.SetParent(station.transform, false);
                anchor.position = at; anchor.rotation = Quaternion.Euler(0, yaw, 0); return anchor;
            }
            var ward = Station(bay);
            ward.WardAnchor = Anchor(ward, "SCREEN_WardChecklist", new Vector3(.15f, 1.65f, -1.68f), 180);
            ward.DebriefAnchor = Anchor(ward, "SCREEN_Debrief", new Vector3(2.28f, 1.85f, .60f), 90);
            var theatre = Station(ot);
            theatre.WhoAnchor = Anchor(theatre, "SCREEN_WHOBoard", new Vector3(13.42f, 1.65f, 5.20f), 90);
            foreach (var scene in new[] { bay, ot }) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }
    }
}
