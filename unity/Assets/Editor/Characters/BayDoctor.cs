using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// P3-INT-04: stands a Rocketbox doctor in the patient bay, beside the charting counter and turned toward the bed, so the trainee can walk up and address them (escalation, review, decisions).
    /// The name "Doctor at the bay" is what InteractableBinder's "Doctor*" rule marks; the runtime gives them a CharacterPresenter. Idempotent: run again to move them. Batch: -executeMethod ProcSense.EditorTools.BayDoctor.Place (Unity closed).
    /// </summary>
    public static class BayDoctor
    {
        const string Name = "Doctor at the bay";

        public static void Place()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var old = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == Name); if (old != null) Object.DestroyImmediate(old.gameObject);
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
            var records = all.FirstOrDefault(t => t.name == "charting_counter"); var bed = all.FirstOrDefault(t => t.name == "Bed");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxSetup.Prefabs}/RB_Medical_Male_01.prefab");
            if (records == null || bed == null || prefab == null) { Debug.LogError("[BayDoctor] charting counter, bed or doctor prefab missing"); return; }

            var bedCentre = bed.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; }).center;
            var from = new Vector3(records.position.x, 0, records.position.z); var toBed = new Vector3(bedCentre.x, 0, bedCentre.z) - from;
            Physics.SyncTransforms(); Debug.Log($"[BayDoctor] records {records.position:F2} bed {bedCentre:F2}");
            // try a ring of spots around the workstation, nearest the bed first, and take the first one that is clear of furniture
            Vector3? spot = null;
            foreach (float distance in new[] { 1.0f, 1.3f, 0.8f, 1.6f })
                for (int a = 0; a < 360 && spot == null; a += 30)
                {
                    var p = from + Quaternion.AngleAxis(a, Vector3.up) * toBed.normalized * distance;
                    if (!Physics.CheckCapsule(p + Vector3.up * .3f, p + Vector3.up * 1.6f, .35f) && (new Vector3(bedCentre.x, 0, bedCentre.z) - p).magnitude > 1.1f) spot = p;
                }
            if (spot == null) { Debug.LogError("[BayDoctor] no clear spot beside the counter"); return; }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab); go.name = Name; go.transform.SetPositionAndRotation(spot.Value, Quaternion.LookRotation(new Vector3(bedCentre.x, 0, bedCentre.z) - spot.Value));
            go.GetComponent<Animator>().runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{RocketboxSetup.Ctrl}/RB_Idle_Loop.controller");
            var legacy = go.GetComponent<ProcSense.VR.Views.StaffIdleView>(); if (legacy != null) Object.DestroyImmediate(legacy);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[BayDoctor] placed at {spot.Value:F2}, records {records.position:F2}, bed {bedCentre:F2}");
        }
    }
}
