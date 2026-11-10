using System;
using System.Linq;
using ProcSense.VR.Records;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>Targeted DT-266 placement; never rebuilds furniture, lighting or navigation.</summary>
    public static class RecordsWorkstationSetup
    {
        [MenuItem("ProcSense/Setup/Place bay records workstation")]
        public static void Place()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
                throw new InvalidOperationException("Stop Play and baking before placing the records workstation.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or resolve unsaved scene changes before placing the records workstation.");
            var scene = SceneManager.GetSceneByName("PatientBay");
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open PatientBay first.");
            if (scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<RecordsWorkstation>(true)).Any())
                throw new InvalidOperationException("A workstation already exists; edit its anchors instead of replacing it.");
            var screen = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true))
                .Single(r => r.name == "EQ_charting_counter_M_ScreenOff");
            var root = new GameObject("Records workstation");
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Place bay records workstation");
            var station = root.AddComponent<RecordsWorkstation>();
            Transform Anchor(string name, Vector3 position, float scale = 1f)
            {
                var anchor = new GameObject(name).transform;
                anchor.SetParent(root.transform, false);
                anchor.position = position;
                anchor.rotation = Quaternion.Euler(0, 180, 0);
                anchor.localScale = Vector3.one * scale;
                return anchor;
            }
            station.EmrAnchor = Anchor("SCREEN_EMR", new Vector3(screen.bounds.center.x, screen.bounds.center.y, screen.bounds.max.z + .006f), .53f);
            station.ResultsAnchor = Anchor("SCREEN_Results", new Vector3(1.94f, 1.78f, -1.92f));
            station.PhoneAnchor = Anchor("PHONE_CriticalResults", new Vector3(1.94f, 1.37f, -1.915f));
            station.ReportAnchor = Anchor("TRAY_Reports", new Vector3(1.32f, 1.002f, -1.65f));
            station.ReportAnchor.rotation = Quaternion.identity;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
        }
    }
}
