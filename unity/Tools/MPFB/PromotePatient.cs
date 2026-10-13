using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class PromotePatient
{
    public static object Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play first.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("Save current scene edits first.");
        const string main = "Assets/Scenes/PatientBay.unity", review = "Assets/Scenes/MPFBPatientReview.unity";
        string originalGuid = AssetDatabase.AssetPathToGUID(main);
        string sourceBand = "Assets/Models/Characters/PatientTrial/patient_wristband.obj";
        string destinationBand = "Assets/Models/Characters/MPFBPatient/patient_wristband.obj";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(sourceBand) != null)
        {
            string error = AssetDatabase.MoveAsset(sourceBand, destinationBand);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
        }
        var scene = EditorSceneManager.OpenScene(review, OpenSceneMode.Single);
        var roots = scene.GetRootGameObjects();
        var patient = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Single(t => t.name == "MPFB patient");
        patient.gameObject.SetActive(true);
        foreach (var old in roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "Patient model trial").ToArray())
            UnityEngine.Object.DestroyImmediate(old.gameObject);
        foreach (var door in roots.SelectMany(g => g.GetComponentsInChildren<ProcSense.VR.Boot.RoomDoor>(true))) door.gameObject.SetActive(true);
        // Preserve the MPFB snapshot too; only the retired CGTrader graphics are removed.
        EditorSceneManager.SaveScene(scene);
        if (!EditorSceneManager.SaveScene(scene, main)) throw new InvalidOperationException("Could not save the promoted bay.");
        EditorSceneManager.OpenScene(main, OpenSceneMode.Single);
        if (AssetDatabase.AssetPathToGUID(main) != originalGuid) throw new InvalidOperationException("PatientBay GUID changed.");
        var remaining = AssetDatabase.GetDependencies(main).Where(p => p.StartsWith("Assets/Models/Characters/PatientTrial/")).ToArray();
        if (remaining.Length > 0) throw new InvalidOperationException("Old patient assets are still referenced: " + string.Join(", ", remaining));
        string[] obsolete = {
            "Assets/Models/Characters/PatientTrial/patient_review.fbx",
            "Assets/Models/Characters/PatientTrial/patient_albedo.png",
            "Assets/Models/Characters/PatientTrial/patient_trial.mat"
        };
        var consumers = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath)
            .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }).Select(AssetDatabase.GUIDToAssetPath));
        foreach (string consumer in consumers)
            if (AssetDatabase.GetDependencies(consumer).Intersect(obsolete).Any()) throw new InvalidOperationException("Retired model still referenced by " + consumer);
        foreach (string asset in obsolete) AssetDatabase.DeleteAsset(asset);
        AssetDatabase.SaveAssets();
        return new { scene = main, guidPreserved = true, band = destinationBand, retiredPatient = true, snapshotRetained = review };
    }
}
