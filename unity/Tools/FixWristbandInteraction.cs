using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public static class FixWristbandInteraction
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play first.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save current scene edits first.");
        string original = SceneManager.GetActiveScene().path;
        const string path = "Assets/Prefabs/Equipment/vital_signs_monitor.prefab";
        var prefab = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var grab = prefab.GetComponent<XRGrabInteractable>();
            grab.colliders.Clear();
            grab.colliders.AddRange(prefab.GetComponents<Collider>());
            PrefabUtility.SaveAsPrefabAsset(prefab, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        foreach (string scenePath in new[] { "Assets/Scenes/PatientBay.unity" })
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var card in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<ProcSense.VR.Views.IdentityCardView>(true)))
                card.gameObject.SetActive(false);
            foreach (var grab in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<XRGrabInteractable>(true)).Where(g => g.name == "vital_signs_monitor"))
            {
                grab.colliders.Clear();
                grab.colliders.AddRange(grab.GetComponents<Collider>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(grab);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        var dev = EditorSceneManager.OpenScene("Assets/Scenes/DevOnly.unity", OpenSceneMode.Single);
        foreach (var simulator in UnityEngine.Object.FindObjectsByType<XRDeviceSimulator>())
            if (simulator.GetComponent<ProcSense.VR.XR.SimulatorHapticGuard>() == null)
                simulator.gameObject.AddComponent<ProcSense.VR.XR.SimulatorHapticGuard>();
        EditorSceneManager.MarkSceneDirty(dev);
        EditorSceneManager.SaveScene(dev);
        EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        return "Physical band replaces the card; simulator-only haptics guarded; monitor/control collider ownership separated.";
    }
}
