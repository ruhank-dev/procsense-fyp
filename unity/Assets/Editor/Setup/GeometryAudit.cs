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
    /// Read-only geometry and batching audit for the Quest 2 optimisation audit (lane 2): per scene the heaviest meshes by instance count and triangles (LOD and instancing candidates), static flags,
    /// material instancing flags, skinned meshes, and the city variants (renderers, triangles, shadow casting, static flags, colliders). Writes docs/qa/audit/lane2_geometry_data.md and changes no asset.
    /// Batch: -executeMethod ProcSense.EditorTools.GeometryAudit.Run -batchmode -nographics
    /// </summary>
    public static class GeometryAudit
    {
        private static int Tris(Mesh m) { if (m == null) return 0; int t = 0; for (int i = 0; i < m.subMeshCount; i++) t += (int)(m.GetIndexCount(i) / 3); return t; }

        public static void Run()
        {
            var sb = new StringBuilder("# Lane 2 geometry data (generated, read-only)\n\n");
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var path = AssetDatabase.FindAssets("t:Scene " + name, new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).First(p => Path.GetFileNameWithoutExtension(p) == name);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var mfs = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null).ToList();
                var sk = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                bool IsCity(Component c) { for (var t = c.transform; t != null; t = t.parent) if (t.name.StartsWith("City exterior") || t.name.StartsWith("Window exterior")) return true; return false; }
                var interior = mfs.Where(f => !IsCity(f)).ToList(); var city = mfs.Where(IsCity).ToList();
                long triI = interior.Sum(f => (long)Tris(f.sharedMesh));
                sb.AppendLine($"## {name}\n");
                sb.AppendLine($"- Mesh renderers: interior {interior.Count} (triangles {triI:N0} if all drawn), city/backdrop {city.Count} (triangles {city.Sum(f => (long)Tris(f.sharedMesh)):N0}); skinned {sk.Length} (triangles {sk.Sum(s => (long)Tris(s.sharedMesh)):N0}, bones {sk.Sum(s => s.bones.Length):N0})");
                int bstat = interior.Count(f => GameObjectUtility.GetStaticEditorFlags(f.gameObject).HasFlag(StaticEditorFlags.BatchingStatic));
                int occee = interior.Count(f => GameObjectUtility.GetStaticEditorFlags(f.gameObject).HasFlag(StaticEditorFlags.OccludeeStatic));
                int lmap = interior.Count(f => f.GetComponent<MeshRenderer>().lightmapIndex >= 0 && f.GetComponent<MeshRenderer>().lightmapIndex < 65534);
                int readable = interior.Select(f => f.sharedMesh).Distinct().Count(m => m.isReadable);
                sb.AppendLine($"- Interior: BatchingStatic {bstat}, OccludeeStatic {occee}, lightmapped {lmap}; distinct meshes {interior.Select(f => f.sharedMesh).Distinct().Count()} of which readable {readable}; casting shadows {interior.Count(f => f.GetComponent<MeshRenderer>().shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)}");
                var mats = interior.SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials).Where(m => m != null).Distinct().ToList();
                sb.AppendLine($"- Interior materials {mats.Count}: enableInstancing on {mats.Count(m => m.enableInstancing)}; shaders {string.Join(", ", mats.Select(m => m.shader.name).Distinct().Take(8))}");
                sb.AppendLine("\nTop 20 meshes by total triangles (all instances):\n\n| Mesh | Instances | Tris each | Total | Materials per renderer | BatchingStatic |\n|---|---|---|---|---|---|");
                foreach (var g in interior.GroupBy(f => f.sharedMesh).OrderByDescending(g => (long)g.Count() * Tris(g.Key)).Take(20))
                    sb.AppendLine($"| {g.Key.name} | {g.Count()} | {Tris(g.Key):N0} | {(long)g.Count() * Tris(g.Key):N0} | {g.First().GetComponent<MeshRenderer>().sharedMaterials.Length} | {g.Count(f => GameObjectUtility.GetStaticEditorFlags(f.gameObject).HasFlag(StaticEditorFlags.BatchingStatic))} |");
                var rep = interior.GroupBy(f => f.sharedMesh).Where(g => g.Count() >= 6).ToList();
                sb.AppendLine($"\nMeshes with 6 or more instances (instancing/LOD candidates): {rep.Count}; instances {rep.Sum(g => g.Count())}; of which on materials with instancing on: {rep.Sum(g => g.Count(f => f.GetComponent<MeshRenderer>().sharedMaterials.All(m => m != null && m.enableInstancing)))}");
                foreach (var g in rep.OrderByDescending(g => (long)g.Count() * Tris(g.Key)).Take(12)) sb.AppendLine($"- {g.Key.name}: {g.Count()} x {Tris(g.Key):N0} tris");
                if (sk.Length > 0)
                {
                    sb.AppendLine("\nSkinned meshes:\n\n| Object | Triangles | Bones | Quality | updateWhenOffscreen | Blendshapes |\n|---|---|---|---|---|---|");
                    foreach (var s in sk.OrderByDescending(s => Tris(s.sharedMesh)).Take(14)) sb.AppendLine($"| {s.name} | {Tris(s.sharedMesh):N0} | {s.bones.Length} | {s.quality} | {s.updateWhenOffscreen} | {(s.sharedMesh != null ? s.sharedMesh.blendShapeCount : 0)} |");
                }
                if (name == "Corridor")
                {
                    sb.AppendLine("\n### City variants (Corridor)\n\n| Variant | Renderers | Triangles | Shadow casting on | Static flags (BatchingStatic / Occludee / Occluder) | Colliders | Materials | Readable meshes |\n|---|---|---|---|---|---|---|---|");
                    var q = Object.FindFirstObjectByType<ProcSense.VR.Views.QuestExterior>(FindObjectsInactive.Include);
                    var so = q != null ? new SerializedObject(q) : null;
                    foreach (var (label, prop) in new[] { ("Original", "originals"), ("Optimised", "optimised") })
                    {
                        if (so == null) break;
                        var rs = new List<Renderer>(); var arr = so.FindProperty(prop); for (int i = 0; i < arr.arraySize; i++) { var r = arr.GetArrayElementAtIndex(i).objectReferenceValue as Renderer; if (r != null) rs.Add(r); }
                        var ms = rs.Select(r => r.GetComponent<MeshFilter>()).Where(f => f != null && f.sharedMesh != null).ToList();
                        Func(label, rs, ms, sb);
                    }
                }
                sb.AppendLine();
            }
            File.WriteAllText("../docs/qa/audit/lane2_geometry_data.md", sb.ToString().Replace("\r\n", "\n"));
            Debug.Log("[GeometryAudit] written");
            EditorApplication.Exit(0);
        }

        private static void Func(string label, List<Renderer> rs, List<MeshFilter> ms, StringBuilder sb)
        {
            StaticEditorFlags F(Renderer r) => GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            sb.AppendLine($"| {label} | {rs.Count} | {ms.Sum(f => (long)Tris(f.sharedMesh)):N0} | {rs.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)} | {rs.Count(r => F(r).HasFlag(StaticEditorFlags.BatchingStatic))} / {rs.Count(r => F(r).HasFlag(StaticEditorFlags.OccludeeStatic))} / {rs.Count(r => F(r).HasFlag(StaticEditorFlags.OccluderStatic))} | {rs.Count(r => r.GetComponent<Collider>() != null)} | {rs.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count()} | {ms.Select(f => f.sharedMesh).Distinct().Count(m => m.isReadable)} of {ms.Select(f => f.sharedMesh).Distinct().Count()} |");
        }
    }
}
