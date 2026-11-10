#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Boot;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Explicit helper (not a behaviour test): writes the room bounds, doors, windows, patient, beds and starting positions of the loaded hospital to BENCH_OUT so the review walkthrough path can be planned on real coordinates.</summary>
    public class LayoutProbe
    {
        [UnityTest, Explicit("layout probe")]
        public IEnumerator Dump_layout()
        {
            string outPath = System.Environment.GetEnvironmentVariable("BENCH_OUT"); Assert.IsFalse(string.IsNullOrEmpty(outPath));
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            var sb = new StringBuilder();
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var sc = SceneManager.GetSceneByName(name); if (!sc.isLoaded) { sb.AppendLine(name + " NOT LOADED"); continue; }
                var rs = sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).Where(r => r.enabled && r.name != "").ToList();
                Bounds b = default; bool first = true; foreach (var r in rs) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                sb.AppendLine($"## {name}: bounds centre {b.center} size {b.size} min {b.min} max {b.max}");
                foreach (var t in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(false)))
                {
                    string n = t.name.ToLowerInvariant();
                    if (n.Contains("door") && !n.Contains("handle") || n.Contains("window") && t.GetComponent<Renderer>() != null || n.Contains("patient") && t.parent == null || n.Contains("bed") && t.GetComponent<Renderer>() != null && n.Length < 14 || n.Contains("spawn") || n.Contains("start") || n.Contains("monitor") && t.parent != null && t.parent.parent == null)
                    {
                        var r = t.GetComponent<Renderer>(); sb.AppendLine($"  {t.name} pos {t.position} {(r != null ? "bounds " + r.bounds.min + " .. " + r.bounds.max : "")}");
                    }
                }
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"camera {cam.name} pos {cam.transform.position} rot {cam.transform.eulerAngles} enabled {cam.enabled}");
            foreach (var r in Object.FindObjectsByType<ProcSense.VR.Views.ExteriorVisibility>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine("exterior visibility: " + ProcSense.VR.Views.ExteriorVisibility.Report);
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)); File.WriteAllText(outPath, sb.ToString());
        }

        /// <summary>Explicit helper for the theatre clipping (P3-EXT-11): lists every city renderer with mesh vertices inside a room's interior box (the union of the room's own renderers, city excluded), per room, so the intruding building can be found.</summary>
        [UnityTest, Explicit("layout probe")]
        public IEnumerator Intersect_probe()
        {
            string outPath = System.Environment.GetEnvironmentVariable("BENCH_OUT"); Assert.IsFalse(string.IsNullOrEmpty(outPath));
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            bool IsCity(Transform t) { for (; t != null; t = t.parent) if (t.name == "City exterior") return true; return false; }
            string PathOf(Transform t) { var sb2 = new StringBuilder(t.name); for (t = t.parent; t != null; t = t.parent) sb2.Insert(0, t.name + "/"); return sb2.ToString(); }
            var boxes = new System.Collections.Generic.Dictionary<string, Bounds>();
            var sb = new StringBuilder();
            foreach (var name in new[] { "PatientBay", "Corridor", "OT" })
            {
                var sc = SceneManager.GetSceneByName(name); if (!sc.isLoaded) continue;
                Bounds b = default; bool first = true;
                foreach (var r in sc.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(false)).Where(r => r.enabled && !IsCity(r.transform) && r.bounds.size.magnitude < 40f && r.GetComponent<MeshFilter>() != null))
                { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
                b.Expand(-0.2f); boxes[name] = b; sb.AppendLine($"## {name} interior box min {b.min} max {b.max}");
            }
            var city = SceneManager.GetSceneByName("Corridor").GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Where(r => IsCity(r.transform)).ToList();
            sb.AppendLine("city renderers " + city.Count);
            foreach (var r in city)
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var m = mf.sharedMesh; var vs = new System.Collections.Generic.List<Vector3>(); m.GetVertices(vs); var l2w = r.transform.localToWorldMatrix;
                foreach (var kv in boxes)
                {
                    if (!kv.Value.Intersects(r.bounds)) continue;
                    int n = 0; Vector3 mn = Vector3.one * 1e9f, mx = -Vector3.one * 1e9f; var tris = m.triangles; var box = kv.Value; var c = new Vector3[8];
                    for (int q = 0; q < 8; q++) c[q] = new Vector3((q & 1) == 0 ? box.min.x : box.max.x, (q & 2) == 0 ? box.min.y : box.max.y, (q & 4) == 0 ? box.min.z : box.max.z);
                    for (int ti = 0; ti + 2 < tris.Length; ti += 3)
                    {   // a triangle crosses the box when its bounds overlap the box and its plane separates two box corners
                        var p0 = l2w.MultiplyPoint3x4(vs[tris[ti]]); var p1 = l2w.MultiplyPoint3x4(vs[tris[ti + 1]]); var p2 = l2w.MultiplyPoint3x4(vs[tris[ti + 2]]);
                        var tb = new Bounds(p0, Vector3.zero); tb.Encapsulate(p1); tb.Encapsulate(p2); if (!tb.Intersects(box)) continue;
                        var pl = new Plane(p0, p1, p2); bool pos = false, neg = false; foreach (var cc in c) { float d = pl.GetDistanceToPoint(cc); if (d > 0.01f) pos = true; else if (d < -0.01f) neg = true; }
                        if (pos && neg) { n++; mn = Vector3.Min(mn, tb.min); mx = Vector3.Max(mx, tb.max); }
                    }
                    if (n > 0) sb.AppendLine($"INTRUDES {kv.Key}: {PathOf(r.transform)} triangles crossing the box {n}/{tris.Length / 3} enabled {r.enabled} inside {mn} .. {mx} mats {string.Join("|", r.sharedMaterials.Select(x => x != null ? x.name : "null"))}");
                }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)); File.WriteAllText(Path.ChangeExtension(outPath, ".intersect.txt"), sb.ToString());
        }
    }
}
#endif
