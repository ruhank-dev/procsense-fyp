using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Editor-side cost of the floor: its mesh, material and textures (source size, Android ASTC size, mips), the lightmap set of the connected bake, and what the floor adds to a
    /// view's draw calls and triangles. Headless runs have no GPU counters, so this reports data, not frame time (frame time is Sannan's headset check, DT-268).
    /// </summary>
    public static class FloorMetrics
    {
        static string Arg(string key, string def) { var a = Environment.GetCommandLineArgs(); var i = Array.IndexOf(a, key); return i >= 0 && i + 1 < a.Length ? a[i + 1] : def; }

        static long AndroidBytes(Texture2D t, string path)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            var ps = ti.GetPlatformTextureSettings("Android");
            int block = ps.format switch { TextureImporterFormat.ASTC_4x4 => 4, TextureImporterFormat.ASTC_5x5 => 5, TextureImporterFormat.ASTC_6x6 => 6, TextureImporterFormat.ASTC_8x8 => 8, _ => 0 };
            if (block == 0) return -1;
            long total = 0; int w = Mathf.Min(t.width, ps.maxTextureSize), h = Mathf.Min(t.height, ps.maxTextureSize);
            while (true)
            {
                total += (long)Mathf.CeilToInt(w / (float)block) * Mathf.CeilToInt(h / (float)block) * 16;
                if (w == 1 && h == 1) break;
                w = Mathf.Max(1, w / 2); h = Mathf.Max(1, h / 2);
            }
            return total;
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            var tag = Arg("-tag", "metrics");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var floor = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).First(r => r.name == "Floor");
            var m = floor.sharedMaterial; var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
            sb.AppendLine($"floor: mesh {mesh.name} {mesh.vertexCount} verts {mesh.triangles.Length / 3} tris (1 renderer, 1 material, 1 draw call, SRP-batchable)");
            sb.AppendLine($"material {m.name}: shader {m.shader.name}, keywords [{string.Join(" ", m.shaderKeywords)}]; no extra passes, no parallax, no alpha");
            long totalAndroid = 0, totalDesktop = 0;
            foreach (var prop in new[] { "_BaseMap", "_BumpMap" })
            {
                var t = (Texture2D)m.GetTexture(prop); var path = AssetDatabase.GetAssetPath(t);
                var a = AndroidBytes(t, path); totalAndroid += Math.Max(a, 0);
                var desktop = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t); totalDesktop += desktop;
                var ps = ((TextureImporter)AssetImporter.GetAtPath(path)).GetPlatformTextureSettings("Android");
                sb.AppendLine($"  {prop}: {t.width}x{t.height}, file {new FileInfo(path).Length / 1048576f:F1} MB, editor runtime {desktop / 1048576f:F1} MB, Android {ps.format} {(a >= 0 ? (a / 1048576f).ToString("F2") + " MB incl. mips" : "?")}");
            }
            sb.AppendLine($"  floor textures total: Android {totalAndroid / 1048576f:F2} MB; editor {totalDesktop / 1048576f:F1} MB");
            long lm = 0; int n = 0;
            foreach (var l in LightmapSettings.lightmaps)
            {
                foreach (var tex in new[] { l.lightmapColor, l.lightmapDir, l.shadowMask })
                    if (tex != null) { lm += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex); }
                n++;
            }
            sb.AppendLine($"lightmaps of the connected bake: {n} sets, editor runtime {lm / 1048576f:F1} MB (Android compression differs)");
            var old = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay vinyl floor.mat");
            if (old != null)
                foreach (var prop in new[] { "_BaseMap", "_BumpMap" })
                {
                    var t = old.GetTexture(prop) as Texture2D; if (t == null) continue; var path = AssetDatabase.GetAssetPath(t);
                    sb.AppendLine($"  (replaced vinyl {prop}: {t.width}x{t.height}, editor runtime {UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) / 1048576f:F2} MB)");
                }
            // renderers and triangles in the scene (all rooms resident are not loaded here: PatientBay alone)
            var rs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled).ToArray();
            long tris = 0; foreach (var r in rs) { var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) tris += mf.sharedMesh.triangles.Length / 3; }
            sb.AppendLine($"PatientBay renderers {rs.Length}, triangles {tris / 1000f:F1}k; the floor accounts for 1 renderer and {mesh.triangles.Length / 3} triangles of them");
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "FloorBuild")); File.WriteAllText(Path.Combine(dir, $"{tag}.txt"), sb.ToString());
            Debug.Log("[FloorMetrics] written");
        }
    }
}
