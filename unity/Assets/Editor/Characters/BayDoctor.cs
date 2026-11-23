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
            // The bay is about 5 m by 4 m (x -2.5..2.5, z -2.0..2.0, the charting counter against the south wall); the walls have no collider, so a ring search once put him outside the room.
            // These spots were read off the top-down map (BayMap): open floor beside the bed, clear of the curtain, bin and plants. Take the first one that is clear of furniture.
            Vector3? spot = null;
            foreach (var p in new[] { new Vector3(1.95f, 0, -0.4f), new Vector3(1.9f, 0, -1.0f), new Vector3(-1.8f, 0, -1.0f) })
            {
                var hits = Physics.OverlapCapsule(p + Vector3.up * .3f, p + Vector3.up * 1.6f, .35f, ~0, QueryTriggerInteraction.Ignore).Where(h => h.name != "Floor" && h.name != "Patient room exposure trim").ToArray();   // the floor and the lighting trim span the room; they are not furniture
                Debug.Log($"[BayDoctor] spot {p:F2}: {(hits.Length == 0 ? "clear" : string.Join(", ", hits.Select(h => h.name)))}");
                if (spot == null && hits.Length == 0 && (new Vector3(bedCentre.x, 0, bedCentre.z) - p).magnitude > 1.1f) spot = p;
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
