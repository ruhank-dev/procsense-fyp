using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only sun path test: temporary mesh colliders on every shadow-casting renderer of the three open scenes, then rays from a grid across each west door glass toward the sun (what blocks) and away from it (where the light lands).</summary>
    public static class SunPathDiagnosis
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            foreach (var n in new[] { "PatientBay", "OT" }) EditorSceneManager.OpenScene("Assets/Scenes/" + n + ".unity", OpenSceneMode.Additive);
            var temp = new List<Collider>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue; var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            Physics.SyncTransforms();
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).First(l => l.name == "Corridor sun"); var travel = sun.transform.forward; var toSun = -travel;
            Debug.Log($"[SunPath] sun travel {travel:F3} elevation {sun.transform.eulerAngles.x:F0} yaw {sun.transform.eulerAngles.y:F0} type {sun.type} bake {sun.lightmapBakeType} shadows {sun.shadows}");
            foreach (var g in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude).Where(r => r.name.EndsWith("M_DoorGlass") && r.bounds.center.x < 3f && r.bounds.center.z > 3f))
            {
                var b = g.bounds; var back = new Dictionary<string, int>(); var fwd = new Dictionary<string, int>(); int total = 0;
                for (int iy = 0; iy < 5; iy++) for (int iz = 0; iz < 5; iz++)
                {
                    var p = new Vector3(b.center.x, Mathf.Lerp(b.min.y + .05f, b.max.y - .05f, iy / 4f), Mathf.Lerp(b.min.z + .05f, b.max.z - .05f, iz / 4f)); total++;
                    string Hit(Vector3 d, float from) { return Physics.Raycast(p + d * from, d, out var h, 60f) ? h.collider.name + "<" + h.collider.transform.parent?.name + ">" : "free"; }
                    string a = Hit(toSun, .02f), f = Hit(travel, .02f);
                    back[a] = back.GetValueOrDefault(a) + 1; fwd[f] = fwd.GetValueOrDefault(f) + 1;
                }
                Debug.Log($"[SunPath] glass z={b.center.z:F1} size {b.size:F2} | toward sun: " + string.Join(", ", back.Select(k => k.Key + " x" + k.Value)) + " | light lands on: " + string.Join(", ", fwd.Select(k => k.Key + " x" + k.Value)));
            }
            foreach (var c in temp) Object.DestroyImmediate(c);
            EditorSceneManager.CloseScene(EditorSceneManager.GetSceneByName("OT"), true); EditorSceneManager.CloseScene(EditorSceneManager.GetSceneByName("PatientBay"), true);
        }
    
        /// <summary>Temporary: makes the corridor sun Realtime (hard shadows) and removes the bake-only blockers' effect on nothing else, so a capture shows what the real shadow-casting geometry lets through. Re-run CorridorSun.Apply afterwards.</summary>
        public static void RealtimeSun()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).First(l => l.name == "Corridor sun");
            sun.lightmapBakeType = LightmapBakeType.Realtime; sun.shadows = LightShadows.Soft; sun.intensity = 6f;
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); Debug.Log("[SunPath] corridor sun set to Realtime for the capture test");
        }


        /// <summary>Capture-only test (no bake): sets the Mixed corridor sun's live intensity to 0.8 to separate the realtime direct part from the baked bounce when judging clipping.</summary>
        public static void SetSun()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).First(l => l.name == "Corridor sun"); sun.intensity = 1.9f; var plaster = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/Warm painted plaster.mat"); if (plaster != null) { plaster.SetColor("_BaseColor", new Color(.80f, .765f, .70f)); EditorUtility.SetDirty(plaster); AssetDatabase.SaveAssets(); }
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); Debug.Log("[SunPath] sun intensity 1.7 mode " + sun.lightmapBakeType);
        }
}
}
