using System;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class NurseVisitSetup
    {
        [MenuItem("ProcSense/Setup/Install visible nurse visit")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes and stop Play/baking first.");
            var scene = SceneManager.GetSceneByName("PatientBay");
            if (!scene.isLoaded) throw new InvalidOperationException("Open PatientBay first.");
            if (scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NurseWristbandController>(true)).Any())
                throw new InvalidOperationException("A nurse visit owner already exists; preserve it.");
            var wrist = GameObject.Find("Patient wristband")?.transform;
            if (wrist == null) throw new InvalidOperationException("Missing authored wrist anchor.");
            var clipboard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/clipboard_chart.prefab");
            var band = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/wristband.prefab");
            var allergy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Equipment/allergy_band.prefab");
            if (clipboard == null || band == null || allergy == null) throw new InvalidOperationException("Import existing nurse props first.");
            var root = new GameObject("Visible nurse wristband visit"); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Install visible nurse visit");
            Transform Anchor(string name, Vector3 at)
            { var child = new GameObject(name).transform; child.SetParent(root.transform, false); child.position = at; return child; }
            var view = root.AddComponent<NurseWristbandController>();
            view.Entry = Anchor("ANCHOR_NurseEntry", new Vector3(4.25f, 0, -.8f));
            view.InsideDoor = Anchor("ANCHOR_NurseBayDoor", new Vector3(2.35f, 0, -.8f));
            view.Bedside = Anchor("ANCHOR_NurseBedside", new Vector3(1.15f, 0, 1.05f));
            view.Exit = Anchor("ANCHOR_NurseExit", new Vector3(4.25f, 0, -.8f));
            view.Wrist = wrist; view.ClipboardPrefab = clipboard; view.BandPrefab = band; view.AllergyPrefab = allergy;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
    }
}
