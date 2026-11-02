using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026: inventory of every material used by the PatientBay, Corridor and OT scenes (renderer count, shader, which PBR maps are assigned, colour, smoothness, metallic, tiling),
    /// written to Logs/material_audit.csv so each can be mapped to a real PBR set. Read-only: opens the scenes without saving.
    /// </summary>
    public static class MaterialAudit
    {
        public static void Run()
        {
            var sb = new StringBuilder("scene;material;path;shader;renderers;sampleObject;baseMap;normal;mask;occlusion;color;smooth;metal;tiling\n");
            foreach (var scene in new[] { "PatientBay", "Corridor", "OT" })
            {
                var sc = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                var use = new System.Collections.Generic.Dictionary<Material, (int n, string sample)>();
                foreach (var r in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(true)))
                {
                    if (r is ParticleSystemRenderer) continue;
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        use.TryGetValue(m, out var e); use[m] = (e.n + 1, e.sample ?? r.gameObject.name);
                    }
                }
                foreach (var kv in use.OrderByDescending(k => k.Value.n))
                {
                    var m = kv.Key; string T(string p) => m.HasProperty(p) && m.GetTexture(p) != null ? "Y" : "-";
                    string col = m.HasProperty("_BaseColor") ? ColorUtility.ToHtmlStringRGB(m.GetColor("_BaseColor")) : "";
                    string tile = m.HasProperty("_BaseMap") ? m.GetTextureScale("_BaseMap").ToString("F2") : "";
                    sb.AppendLine($"{scene};{m.name};{AssetDatabase.GetAssetPath(m)};{m.shader.name};{kv.Value.n};{kv.Value.sample};{T("_BaseMap")};{T("_BumpMap")};{T("_MetallicGlossMap")};{T("_OcclusionMap")};{col};" +
                                  $"{(m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness").ToString("F2") : "")};{(m.HasProperty("_Metallic") ? m.GetFloat("_Metallic").ToString("F2") : "")};{tile}");
                }
            }
            File.WriteAllText("Logs/material_audit.csv", sb.ToString());
            Debug.Log("[MaterialAudit] wrote Logs/material_audit.csv");
        }

        /// <summary>Wall and floor/ceiling surfaces with world bounds, so decoration placement can be planned from real coordinates (Logs/wall_dump.csv).</summary>
        public static void WallDump()
        {
            var sb = new StringBuilder("scene;object;parent;cx;cy;cz;sx;sy;sz" + System.Environment.NewLine);
            foreach (var scene in new[] { "PatientBay", "Corridor", "OT" })
            {
                var sc = EditorSceneManager.OpenScene("Assets/Scenes/" + scene + ".unity", OpenSceneMode.Single);
                foreach (var r in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
                {
                    var n = r.gameObject.name; if (!(n.StartsWith("Wall") || n.StartsWith("Floor") || n.StartsWith("Ceiling") || n.Contains("Glass wall") || n.StartsWith("Door leaf") || n.StartsWith("Panel") || n.StartsWith("Window"))) continue;
                    var b = r.bounds; sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0};{1};{2};{3:F2};{4:F2};{5:F2};{6:F2};{7:F2};{8:F2}", scene, n, r.transform.parent != null ? r.transform.parent.name : "", b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z));
                }
            }
            File.WriteAllText("Logs/wall_dump.csv", sb.ToString()); Debug.Log("[MaterialAudit] wrote Logs/wall_dump.csv");
        }
    }
}
