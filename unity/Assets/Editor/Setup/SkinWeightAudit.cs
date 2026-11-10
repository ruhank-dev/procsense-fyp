using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Read-only audit of skinned meshes for Quest (QualitySettings.skinWeights = 2 bones on the Android level): lists every mesh with vertices whose first two bone weights sum to zero
    /// (unweighted vertices, which trigger "First two bone weights should have non-zero sum!"), with the model asset path, counts and the scenes or prefabs that use it.
    /// Batch: -executeMethod ProcSense.EditorTools.SkinWeightAudit.Run -batchmode (writes D:/ProcSenseWork/reports/skinweight_audit.md; changes no asset).
    /// </summary>
    public static class SkinWeightAudit
    {
        private sealed class Row { public string Path, Name; public int Verts, NoWeight, ZeroFirstTwo, Max, Over2; public HashSet<string> Users = new HashSet<string>(); public int Bones; }

        public static void Run()
        {
            var rows = new Dictionary<Mesh, Row>();
            void Scan(SkinnedMeshRenderer smr, string user)
            {
                var m = smr.sharedMesh; if (m == null) return;
                if (!rows.TryGetValue(m, out var r))
                {
                    r = new Row { Path = AssetDatabase.GetAssetPath(m), Name = m.name, Verts = m.vertexCount, Bones = m.bindposeCount };
                    var per = m.GetBonesPerVertex(); var all = m.GetAllBoneWeights(); int idx = 0;
                    for (int v = 0; v < per.Length; v++)
                    {
                        int n = per[v]; float s = 0;
                        if (n == 0) r.NoWeight++;
                        for (int k = 0; k < n && k < 2; k++) s += all[idx + k].weight;
                        if (n > 0 && s <= 0f) r.ZeroFirstTwo++;
                        if (n > r.Max) r.Max = n; if (n > 2) r.Over2++;
                        idx += n;
                    }
                    rows[m] = r;
                }
                r.Users.Add(user);
            }
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var path = AssetDatabase.FindAssets("t:Scene " + name, new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == name);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (var s in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Scan(s, "scene " + name);
            }
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g); var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (go == null) continue;
                foreach (var s in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Scan(s, "prefab " + p);
            }
            var sb = new StringBuilder("# Skin weight audit (generated)\n\n| Mesh | Asset | Vertices | No weights | First two sum 0 | Max bones/vertex | Vertices with >2 bones | Used by |\n|---|---|---|---|---|---|---|---|\n");
            foreach (var r in rows.Values.OrderByDescending(r => r.NoWeight + r.ZeroFirstTwo).ThenBy(r => r.Path))
                sb.AppendLine($"| {r.Name} | {r.Path} | {r.Verts} | {r.NoWeight} | {r.ZeroFirstTwo} | {r.Max} | {r.Over2} | {string.Join("; ", r.Users.Take(4))}{(r.Users.Count > 4 ? " ..." : "")} |");
            sb.AppendLine($"\nMeshes: {rows.Count}; with unweighted vertices: {rows.Values.Count(r => r.NoWeight + r.ZeroFirstTwo > 0)}.");
            Directory.CreateDirectory("D:/ProcSenseWork/reports");
            File.WriteAllText("D:/ProcSenseWork/reports/skinweight_audit.md", sb.ToString().Replace("\r\n", "\n"));
            Debug.Log("[SkinWeightAudit] written");
            EditorApplication.Exit(0);
        }
    }
}
