using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Static render workload per Quest benchmark view (P3-PRF-15): opens the three rooms as the game does (PatientBay, Corridor and OT together at their world positions), and for each view of
    /// docs/qa/quest2_sannan_benchmark_guide.md counts what lies inside an approximate Quest 2 view frustum: renderers, triangles, submesh draws before batching, unique materials, transparent and
    /// particle renderers, realtime/mixed lights touching the view, shadow casters within the shadow distance; plus whole-scene and city totals. These are exact counts from the scene files, not
    /// timings: they say what the GPU and CPU are asked to draw, never how fast a Quest draws it. No scene is saved.
    /// Batch: -executeMethod ProcSense.EditorTools.QuestRenderStats.Run (task unity-quest-stats); output ../docs/qa/quest2_static_render_stats.md and unity/Logs/quest_render_stats.json.
    /// </summary>
    public static class QuestRenderStats
    {
        private static readonly string[] Scenes = { "Assets/Scenes/PatientBay.unity", "Assets/Scenes/Corridor.unity", "Assets/Scenes/OT.unity" };
        // the benchmark views (same as QuestPerfDiagnostics): camera position and look-at point, world metres
        private static readonly (string id, Vector3 p, Vector3 l)[] Views =
        {
            ("V1 bay near wall", new Vector3(0f, 1.6f, -1.4f), new Vector3(0f, 1.6f, -1.95f)),
            ("V2 bay overview", new Vector3(2.2f, 1.65f, -1.8f), new Vector3(-1.3f, 1.0f, 0.9f)),
            ("V3 bay window", new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f)),
            ("V4 corridor long", new Vector3(4.5f, 1.7f, 13.6f), new Vector3(4.5f, 1.5f, -9f)),
            ("V5 corridor people", new Vector3(3.2f, 1.55f, 5.6f), new Vector3(5.45f, 1.2f, 7.9f)),
            ("V6 reception", new Vector3(4.5f, 1.6f, -1f), new Vector3(4.5f, 1.4f, -9f)),
            ("V7 theatre", new Vector3(7f, 1.7f, 5.2f), new Vector3(11f, 1.2f, 5.4f)),
            ("V8 corridor exterior", new Vector3(4.5f, 1.6f, 9f), new Vector3(4.5f, 1.5f, 14.2f)),
            ("V9 theatre far end", new Vector3(9.5f, 1.7f, 5.2f), new Vector3(13f, 1.3f, 5.4f)),
        };
        // Quest 2 per-eye field of view is about 90-97 degrees; both eyes together cover a little more horizontally. A 100 x 96 degree frustum is used for the union of the two eyes.
        public const float VerticalFov = 96f, Aspect = 1.08f, Near = 0.05f, Far = 1000f, ShadowDistance = 8f;

        public static void Run()
        {
            bool ok = false;
            try { Write(Measure()); ok = true; }
            catch (Exception e) { Debug.LogError("[QuestRenderStats] " + e); }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private sealed class Row { public string View = "", Room = ""; public int Renderers, Draws, Materials, Transparent, Particles, Skinned, ShadowCasters, Lights, CityRenderers; public long Tris, CityTris; public string LightNames = ""; }

        private static (List<Row> rows, List<string> totals) Measure()
        {
            EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
            for (int i = 1; i < Scenes.Length; i++) EditorSceneManager.OpenScene(Scenes[i], OpenSceneMode.Additive);
            var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.enabled && !r.forceRenderingOff).ToArray();
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.enabled && l.lightmapBakeType != LightmapBakeType.Baked).ToArray();
            var layouts = UnityEngine.Object.FindObjectsByType<ProcSense.VR.Boot.RoomLayout>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            var totals = new List<string>();
            foreach (var g in renderers.GroupBy(r => r.gameObject.scene.name))
                totals.Add("| " + g.Key + " | " + g.Count() + " | " + g.Sum(Tris).ToString("N0", CultureInfo.InvariantCulture) + " | " + g.Sum(Draws) + " | " + g.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count() + " | " + g.Count(r => r is SkinnedMeshRenderer) + " |");
            var city = renderers.Where(UnderCity).ToArray();
            totals.Add("| City exterior (inside Corridor) | " + city.Length + " | " + city.Sum(Tris).ToString("N0", CultureInfo.InvariantCulture) + " | " + city.Sum(Draws) + " | " + city.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count() + " | " + city.Count(r => r is SkinnedMeshRenderer) + " |");

            var rows = new List<Row>();
            foreach (var v in Views)
            {
                var view = Matrix4x4.LookAt(v.p, v.l, Vector3.up).inverse; view.SetRow(2, -view.GetRow(2));
                var planes = GeometryUtility.CalculateFrustumPlanes(Matrix4x4.Perspective(VerticalFov, Aspect, Near, Far) * view);
                string room = layouts.FirstOrDefault(l => l.Contains(v.p))?.gameObject.scene.name ?? "?";
                var seen = renderers.Where(r => GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToArray();
                var row = new Row
                {
                    View = v.id, Room = room, Renderers = seen.Length, Tris = seen.Sum(Tris), Draws = seen.Sum(Draws),
                    Materials = seen.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count(),
                    Transparent = seen.Count(r => r.sharedMaterials.Any(m => m != null && m.renderQueue >= 2501)), Particles = seen.Count(r => r is ParticleSystemRenderer),
                    Skinned = seen.Count(r => r is SkinnedMeshRenderer), CityRenderers = seen.Count(UnderCity), CityTris = seen.Where(UnderCity).Sum(Tris),
                    ShadowCasters = renderers.Count(r => r.shadowCastingMode != ShadowCastingMode.Off && (r.bounds.ClosestPoint(v.p) - v.p).magnitude <= ShadowDistance),
                };
                // lights the player's frame pays for: RoomLoader keeps only the current room's clinical key lit; every other realtime or mixed light touching the frustum counts
                var lit = lights.Where(l => l.name != "Soft clinical key" || l.gameObject.scene.name == room)
                                .Where(l => l.type == LightType.Directional || GeometryUtility.TestPlanesAABB(planes, new Bounds(l.transform.position, Vector3.one * 2f * l.range))).ToArray();
                row.Lights = lit.Length;
                row.LightNames = string.Join("; ", lit.GroupBy(l => l.type + " " + l.lightmapBakeType + (l.shadows != LightShadows.None ? " shadowed" : "")).Select(g => g.Count() + " " + g.Key));
                rows.Add(row);
            }
            return (rows, totals);
        }

        private static bool UnderCity(Renderer r) { for (var t = r.transform; t != null; t = t.parent) if (t.name == "City exterior") return true; return false; }

        private static Mesh MeshOf(Renderer r) => r is SkinnedMeshRenderer s ? s.sharedMesh : r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;   // line, trail and particle renderers have no mesh: 0 triangles, 1 draw

        private static long Tris(Renderer r)
        {
            var m = MeshOf(r); if (m == null) return 0;
            long n = 0; for (int i = 0; i < m.subMeshCount; i++) n += m.GetIndexCount(i) / 3; return n;
        }

        private static int Draws(Renderer r) { var m = MeshOf(r); return m == null ? 1 : Math.Max(1, Math.Min(m.subMeshCount, r.sharedMaterials.Length)); }

        private static void Write((List<Row> rows, List<string> totals) d)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Quest 2 static render workload per benchmark view (generated)");
            sb.AppendLine();
            sb.AppendLine("_Generated by `QuestRenderStats` (`task unity-quest-stats`) on " + DateTime.UtcNow.ToString("d MMMM yyyy, h:mm tt", CultureInfo.InvariantCulture) + " UTC from the scene files (PatientBay, Corridor and OT opened together, as the game loads them). Exact counts of what is inside an approximate Quest 2 view frustum (" + VerticalFov + "° vertical, aspect " + Aspect + ", both eyes together); **not timings and not FPS**. Draws are submesh draws before static batching and the SRP Batcher; lights are realtime or mixed lights that touch the view (the current room's clinical key only, as `RoomLoader` does). Do not edit by hand._");
            sb.AppendLine();
            sb.AppendLine("## Per view");
            sb.AppendLine();
            sb.AppendLine("| View | Room | Renderers | Triangles | Draws (unbatched) | Materials | Transparent | Particle systems | Skinned | City renderers | City triangles | Shadow casters within " + ShadowDistance + " m | Realtime/mixed lights |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            foreach (var r in d.rows)
                sb.AppendLine("| " + r.View + " | " + r.Room + " | " + r.Renderers + " | " + r.Tris.ToString("N0", CultureInfo.InvariantCulture) + " | " + r.Draws + " | " + r.Materials + " | " + r.Transparent + " | " + r.Particles + " | " + r.Skinned + " | " + r.CityRenderers + " | " + r.CityTris.ToString("N0", CultureInfo.InvariantCulture) + " | " + r.ShadowCasters + " | " + r.Lights + " (" + r.LightNames + ") |");
            sb.AppendLine();
            sb.AppendLine("## Whole scenes (active, enabled renderers)");
            sb.AppendLine();
            sb.AppendLine("| Scene | Renderers | Triangles | Draws (unbatched) | Materials | Skinned |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var t in d.totals) sb.AppendLine(t);
            File.WriteAllText("../docs/qa/quest2_static_render_stats.md", sb.ToString().Replace("\r\n", "\n"));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/quest_render_stats.json", JsonUtility.ToJson(new Wrapper { rows = d.rows.Select(r => r.View + "|" + r.Tris + "|" + r.Draws + "|" + r.Lights).ToArray() }, true));
            Debug.Log("[QuestRenderStats] wrote ../docs/qa/quest2_static_render_stats.md (" + d.rows.Count + " views)");
        }

        [Serializable] private sealed class Wrapper { public string[] rows; }
    }
}
