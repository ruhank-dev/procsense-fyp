using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only audit of every skinned character (Quest overhaul, Agent D): triangles, blendshapes, shaders, shadow and probe flags, LODGroups, cloth, in the character prefabs and the three scenes. Writes D:\ProcSenseWork\reports\character_audit.md. Batch: -executeMethod ProcSense.EditorTools.CharacterAudit.Run</summary>
    public static class CharacterAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder("# Character audit (generated)\n\n");
            sb.AppendLine("## Prefabs\n\n| Prefab | SMRs | Triangles | Blendshapes | Shaders | Shadow modes | Probes (light/reflection) | LODGroup | Cloth | Textures (max px) |\n|---|---|---|---|---|---|---|---|---|---|");
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Characters" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(p); if (go == null) continue;
                sb.AppendLine(Row(p, go));
            }
            foreach (var sc in new[] { "PatientBay", "Corridor", "OT" })
            {
                var path = AssetDatabase.FindAssets("t:Scene " + sc, new[] { "Assets/Scenes" }).Select(AssetDatabase.GUIDToAssetPath).First(x => Path.GetFileNameWithoutExtension(x) == sc);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                sb.AppendLine("\n## Scene " + sc + "\n\n| Root | SMRs | Triangles | Blendshapes | Shaders | Shadow modes | Probes (light/reflection) | LODGroup | Cloth | Textures (max px) |\n|---|---|---|---|---|---|---|---|---|---|");
                var roots = new HashSet<Transform>();
                foreach (var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var t = smr.transform; var top = t; while (top.parent != null && top.parent.GetComponentInParent<Animator>(true) != null) top = top.parent;
                    var a = smr.GetComponentInParent<Animator>(true); roots.Add(a != null ? a.transform : top);
                }
                foreach (var r in roots.OrderBy(x => x.name)) sb.AppendLine(Row(r.name, r.gameObject));
                var shadowOnly = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly).Select(r => r.name).Distinct().ToList();
                sb.AppendLine($"\nShadowsOnly renderers: {string.Join(", ", shadowOnly)}");
            }
            Directory.CreateDirectory(@"D:\ProcSenseWork\reports");
            File.WriteAllText(@"D:\ProcSenseWork\reports\character_audit.md", sb.ToString());
            Debug.Log("[CharacterAudit] written");
            EditorApplication.Exit(0);
        }

        private static string Row(string name, GameObject go)
        {
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            long tris = 0; int bs = 0; var sh = new HashSet<string>(); var sm = new HashSet<string>(); var pr = new HashSet<string>(); int maxTex = 0;
            foreach (var s in smrs)
            {
                if (s.sharedMesh != null) { for (int i = 0; i < s.sharedMesh.subMeshCount; i++) tris += (long)s.sharedMesh.GetIndexCount(i) / 3; bs += s.sharedMesh.blendShapeCount; }
                sm.Add(s.shadowCastingMode + (s.receiveShadows ? "+rcv" : "")); pr.Add(s.lightProbeUsage + "/" + s.reflectionProbeUsage);
                foreach (var m in s.sharedMaterials) { if (m == null) continue; sh.Add(m.shader != null ? m.shader.name.Replace("Universal Render Pipeline/", "URP/") + (m.renderQueue >= 2500 ? "(T)" : "") : "null"); foreach (var tn in m.GetTexturePropertyNames()) { var tx = m.GetTexture(tn) as Texture2D; if (tx != null) maxTex = Mathf.Max(maxTex, Mathf.Max(tx.width, tx.height)); } }
            }
            var cloth = go.GetComponentsInChildren<Cloth>(true).Length;
            return $"| {name} | {smrs.Length} | {tris} | {bs} | {string.Join(", ", sh)} | {string.Join(", ", sm)} | {string.Join(", ", pr)} | {go.GetComponentsInChildren<LODGroup>(true).Length} | {cloth} | {maxTex} |";
        }
    }
}
