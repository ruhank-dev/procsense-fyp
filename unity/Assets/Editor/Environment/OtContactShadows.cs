using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (ChatGPT QA VQ-OT-002): the OT equipment had no readable grounding. Same technique as PatientBay (soft footprint quad under each free-standing item plus a tight spot under every castor or foot,
    /// merged into one mesh and one draw call, unlit transparent, no shadow casting). Runtime geometry only; nothing is baked, so it can be re-run without a bake. Idempotent.
    /// </summary>
    public static class OtContactShadows
    {
        const string MeshPath = "Assets/Models/Environment/OT contact shadows.asset";

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Single);
            var old = GameObject.Find("OT contact shadows"); if (old != null) Object.DestroyImmediate(old);
            var mat = AssetDatabase.FindAssets("PatientBay contact shadow t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var quads = new List<Matrix4x4>();
            foreach (var (name, pad) in new[] { ("operating_table", 1.12f), ("anaesthesia_unit", 1.25f), ("mayo_stand", 1.3f), ("clinical_trolley", 1.2f), ("supply_cupboard", 1.06f) })
            {
                var t = all.FirstOrDefault(x => x != null && x.name == name && x.GetComponentsInChildren<MeshRenderer>().Length > 0);
                if (t == null) { Debug.Log("[OtContactShadows] target not found: " + name); continue; }
                var rs = t.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled && r.bounds.min.y < .3f).ToArray(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float fy = b.min.y + .006f;
                quads.Add(Matrix4x4.TRS(new Vector3(b.center.x, fy, b.center.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(b.size.x * pad, b.size.z * pad, 1f)));
                foreach (var p in PatientBayLookUpgrade.WheelSpots(t)) quads.Add(Matrix4x4.TRS(new Vector3(p.x, fy + .001f, p.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(.16f, .16f, 1f)));
                Debug.Log($"[OtContactShadows] {name}: footprint {b.size.x:F2}x{b.size.z:F2}");
            }
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null) { mesh = new Mesh { name = "OT contact shadows" }; AssetDatabase.CreateAsset(mesh, MeshPath); }
            mesh.Clear(); mesh.CombineMeshes(quads.Select(m => new CombineInstance { mesh = quad, transform = m }).ToArray(), true, true); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go = new GameObject("OT contact shadows", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[OtContactShadows] applied: {quads.Count} quads");
        }
        /// <summary>Same technique for the corridor furniture and people (benches, side tables, wheelchairs, stretcher, plants, extinguishers, every seated or standing person): soft footprints and tight foot or castor spots, one merged mesh.</summary>
        public static void ApplyCorridor()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var old = GameObject.Find("Corridor contact shadows"); if (old != null) Object.DestroyImmediate(old);
            var mat = AssetDatabase.FindAssets("PatientBay contact shadow t:Material").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<Material>).First();
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(false)).ToArray();
            var quads = new List<Matrix4x4>(); int n = 0;
            foreach (var t in all)
            {
                bool person = t.name.StartsWith("Person_"), item = t.name == "perforated_bench" || t.name == "round_side_table" || t.name == "wheelchair_01" || t.name == "patient_stretcher" || t.name.StartsWith("potted_plant") || t.name.StartsWith("korean_fire");
                if (!(person || item) || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.bounds.min.y < .5f).ToArray(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float fy = .006f; float pad = person ? 1.0f : 1.12f;
                var size = person ? new Vector3(.6f, .6f, 1f) : new Vector3(Mathf.Max(.25f, b.size.x * pad), Mathf.Max(.25f, b.size.z * pad), 1f);
                quads.Add(Matrix4x4.TRS(new Vector3(b.center.x, fy, b.center.z), Quaternion.Euler(90f, 0f, 0f), size)); n++;
                foreach (var p in PatientBayLookUpgrade.WheelSpots(t)) quads.Add(Matrix4x4.TRS(new Vector3(p.x, fy + .001f, p.z), Quaternion.Euler(90f, 0f, 0f), new Vector3(.16f, .16f, 1f)));
            }
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var path = "Assets/Models/Environment/Corridor contact shadows.asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh { name = "Corridor contact shadows" }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.CombineMeshes(quads.Select(m => new CombineInstance { mesh = quad, transform = m }).ToArray(), true, true); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var go = new GameObject("Corridor contact shadows", typeof(MeshFilter), typeof(MeshRenderer)); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false; mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"[OtContactShadows] corridor: {n} footprints, {quads.Count} quads");
        }
    }
}