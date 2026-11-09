#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Desktop render benchmark (P3-PRF-19), not a behaviour test (Explicit). Loads the hospital as the game does (Boot: PatientBay, Corridor and OT together), then for each of the eight
    /// benchmark views and each requested configuration renders a Quest-sized eye buffer (1832 x 1920, 4x MSAA) and records the renderer's own counters with ProfilerRecorder
    /// (draw calls, SetPass calls, batches, triangles, vertices, shadow casters), the CPU time of the Render() call and the GPU time from FrameTimingManager when the desktop driver gives it.
    /// Numbers are desktop GPU/CPU numbers: they rank work and show what was removed, they are never Quest frame rates.
    /// Environment: BENCH_CONFIGS (comma list of PerfPreset ids, default "B0,B1"), BENCH_OUT (csv path), BENCH_FRAMES (measured frames per cell, default 20).
    /// </summary>
    public class DesktopRenderBenchmark
    {
        private static readonly (string id, Vector3 p, Vector3 l, string room)[] Views =
        {
            ("V1 bay near wall", new Vector3(0f, 1.6f, -1.4f), new Vector3(0f, 1.6f, -1.95f), "PatientBay"),
            ("V2 bay overview", new Vector3(2.2f, 1.65f, -1.8f), new Vector3(-1.3f, 1.0f, 0.9f), "PatientBay"),
            ("V3 bay window", new Vector3(1.8f, 1.5f, -0.6f), new Vector3(-2.45f, 1.6f, 0f), "PatientBay"),
            ("V4 corridor long", new Vector3(4.5f, 1.7f, 13.6f), new Vector3(4.5f, 1.5f, -9f), "Corridor"),
            ("V5 corridor people", new Vector3(3.2f, 1.55f, 5.6f), new Vector3(5.45f, 1.2f, 7.9f), "Corridor"),
            ("V6 reception", new Vector3(4.5f, 1.6f, -1f), new Vector3(4.5f, 1.4f, -9f), "Corridor"),
            ("V7 theatre", new Vector3(7f, 1.7f, 5.2f), new Vector3(11f, 1.2f, 5.4f), "OT"),
            ("V8 corridor exterior", new Vector3(4.5f, 1.6f, 9f), new Vector3(4.5f, 1.5f, 14.2f), "Corridor"),
            ("V9 theatre far end", new Vector3(9.5f, 1.7f, 5.2f), new Vector3(13f, 1.3f, 5.4f), "OT"),
            // desktop-only street-facing views (P3-EXT): the window views above look at towers; these look down at the street, traffic and trees where the reduced meshes are most exposed
            ("V10 glass wall down", new Vector3(4.5f, 1.6f, 13.4f), new Vector3(4.5f, -25f, 34f), "Corridor"),
            ("V11 glass wall oblique", new Vector3(4.5f, 1.6f, 13.4f), new Vector3(30f, -10f, 40f), "Corridor"),
            ("V12 bay window down", new Vector3(-1.6f, 1.5f, 0f), new Vector3(-25f, -12f, 5f), "PatientBay"),
        };

        private const int EyeW = 1832, EyeH = 1920;

        [UnityTest, Explicit("desktop benchmark"), Timeout(21600000)]
        public IEnumerator Benchmark_views()
        {
            string cfg = Environment.GetEnvironmentVariable("BENCH_CONFIGS") ?? "B0,B1", outPath = Environment.GetEnvironmentVariable("BENCH_OUT");
            int frames = int.TryParse(Environment.GetEnvironmentVariable("BENCH_FRAMES"), out var f) ? f : 20;
            bool stereo = Environment.GetEnvironmentVariable("BENCH_STEREO") == "1";
            Assert.IsFalse(string.IsNullOrEmpty(outPath), "set BENCH_OUT");
            int quality = int.TryParse(Environment.GetEnvironmentVariable("BENCH_QUALITY"), out var q) ? q : 2;   // 2 = Medium, the level Android builds use (URP_Quest_Android); 5 = Ultra, the desktop level
            int authoredQuality = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(quality, true);
            LogAssert.ignoreFailingMessages = true;   // the Editor logs "First two bone weights should have non-zero sum" for a skinned mesh (recorded as a finding in docs/qa); it must not abort the measurement
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;                                   // RoomLoader streams the other rooms; lights, probes and animators settle
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;

            var go = new GameObject("BenchCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 96f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(EyeW, EyeH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None;
            cam.enabled = false;   // rendered by the explicit Render() calls only; an enabled camera with a target texture also renders itself every frame and the counters then show every frame twice (found 8 Nov, see docs/qa/quest2_desktop_optimisation.md)
            // BENCH_HIDE: comma list of name fragments; renderers whose own or parent name contains one are disabled for the whole run (measures one layer's share)
            foreach (var frag in (Environment.GetEnvironmentVariable("BENCH_HIDE") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    for (var t = r.transform; t != null; t = t.parent) if (t.name.Contains(frag.Trim())) { r.enabled = false; break; }
            ProcSense.VR.Views.ExteriorVisibility.CollectStats = true;
            var toggles = new PerfToggles();
            toggles.Apply(PerfPresets.All[0], cam, "PatientBay"); cam.Render(); yield return null; cam.Render(); Debug.Log("EXTERIOR " + ProcSense.VR.Views.ExteriorVisibility.Report);

            using (var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"))
            using (var casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count"))
            using (var mem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"))
            using (var gfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory"))
            using (var rtBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes"))
            using (var texBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Used Textures Bytes Count"))
            {
                var sb = new StringBuilder("config,view,room,draw_calls,setpass_calls,batches,triangles,vertices,shadow_casters,cpu_render_ms,gpu_ms,used_mem_mb,gfx_mem_mb,city_hidden,done_med_ms,done_avg_ms,done_p99_ms,done_max_ms,fps_med,fps_1pct_low,cpu_avg_ms,cpu_p99_ms,rt_mem_mb,tex_mem_mb,stereo\n");
                foreach (var id in cfg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var preset = PerfPresets.All.First(p => p.Id == id.Trim());
                    // the eye-texture scale of the preset is simulated by the size of the render texture (the XR display that normally owns it does not exist on the desktop)
                    int sw0 = Mathf.RoundToInt(EyeW * Mathf.Clamp(preset.EyeScale, 0.5f, 1.5f)), sh0 = Mathf.RoundToInt(EyeH * Mathf.Clamp(preset.EyeScale, 0.5f, 1.5f));
                    if (rt.width != sw0 || rt.height != sh0) { cam.targetTexture = null; UnityEngine.Object.Destroy(rt); rt = new RenderTexture(sw0, sh0, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt; }
                    foreach (var v in Views)
                    {
                        cam.transform.position = v.p; cam.transform.LookAt(v.l);
                        toggles.Apply(preset, cam, v.room);
                        for (int k = 0; k < 12; k++) { cam.Render(); yield return null; }       // warm-up: shader variants, culling caches
                        var cpu = new List<double>(); var gpuT = new List<double>(); var done = new List<double>(); var timing = new FrameTiming[1];
                        double dc = 0, sp = 0, bt = 0, tr = 0, vt = 0, sc = 0;
                        for (int k = 0; k < frames; k++)
                        {
                            UnityEngine.FrameTimingManager.CaptureFrameTimings();
                            var sw = System.Diagnostics.Stopwatch.StartNew(); RenderEyes(cam, stereo); cpu.Add(sw.Elapsed.TotalMilliseconds);
                            UnityEngine.Rendering.AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32, null).WaitForCompletion();   // returns when the GPU has finished this frame: submit time plus GPU time on the desktop card
                            sw.Stop(); done.Add(sw.Elapsed.TotalMilliseconds);
                            yield return null;
                            dc = draw.LastValue; sp = setPass.LastValue; bt = batches.LastValue; tr = tris.LastValue; vt = verts.LastValue; sc = casters.LastValue;
                            if (UnityEngine.FrameTimingManager.GetLatestTimings(1, timing) > 0 && timing[0].gpuFrameTime > 0) gpuT.Add(timing[0].gpuFrameTime);
                        }
                        string pngDir = Environment.GetEnvironmentVariable("BENCH_PNG_DIR");
                        if (!string.IsNullOrEmpty(pngDir))
                        {
                            Directory.CreateDirectory(pngDir); cam.Render(); yield return null; cam.Render();
                            var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                            File.WriteAllBytes(Path.Combine(pngDir, preset.Id + "_" + v.id.Split(' ')[0] + ".png"), tex.EncodeToPNG()); RenderTexture.active = prev; UnityEngine.Object.Destroy(tex);
                        }
                        Debug.Log("VIEWDBG " + preset.Id + " " + v.id + ": " + ProcSense.VR.Views.ExteriorVisibility.LastDecision + " chunks " + ProcSense.VR.Views.ExteriorVisibility.ChunksDrawn + "/" + ProcSense.VR.Views.ExteriorVisibility.ChunksTotal + " | " + ProcSense.VR.Views.ExteriorVisibility.StatsLine);
                        cpu.Sort(); gpuT.Sort(); done.Sort();
                        double Med(List<double> l) => l.Count == 0 ? double.NaN : l[l.Count / 2];
                        sb.Append(preset.Id).Append(',').Append(v.id).Append(',').Append(v.room).Append(',').Append(F(dc)).Append(',').Append(F(sp)).Append(',').Append(F(bt)).Append(',').Append(F(tr)).Append(',').Append(F(vt)).Append(',').Append(F(sc))
                          .Append(',').Append(F(Med(cpu))).Append(',').Append(F(Med(gpuT))).Append(',').Append(F(mem.LastValue / 1048576.0)).Append(',').Append(F(gfx.LastValue / 1048576.0)).Append(',').Append(ProcSense.VR.Views.ExteriorVisibility.CityHiddenNow ? 1 : 0)
                          .Append(',').Append(F(Med(done))).Append(',').Append(F(done.Count == 0 ? double.NaN : done.Average())).Append(',').Append(F(Pct(done, 0.99))).Append(',').Append(F(done.Count == 0 ? double.NaN : done[done.Count - 1]))
                          .Append(',').Append(F(1000.0 / Med(done))).Append(',').Append(F(1000.0 / Pct(done, 0.99))).Append(',').Append(F(cpu.Count == 0 ? double.NaN : cpu.Average())).Append(',').Append(F(Pct(cpu, 0.99)))
                          .Append(',').Append(F(rtBytes.Valid ? rtBytes.LastValue / 1048576.0 : double.NaN)).Append(',').Append(F(texBytes.Valid ? texBytes.LastValue / 1048576.0 : double.NaN)).Append(',').Append(stereo ? 2 : 1).Append('\n');
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)); File.WriteAllText(outPath, sb.ToString());   // after every configuration, so a long run keeps what it measured
                }
            }
            toggles.Apply(PerfPresets.All[0], cam, "PatientBay");
            cam.targetTexture = null; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
            QualitySettings.SetQualityLevel(authoredQuality, true);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }

        private static double Pct(List<double> sorted, double q) => sorted.Count == 0 ? double.NaN : sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(q * sorted.Count) - 1 < 0 ? 0 : (int)Math.Ceiling(q * sorted.Count) - 1)];

        /// <summary>One frame: a single eye, or both eyes (the stereo pair, 64 mm apart) so a desktop frame carries the work of a headset frame.</summary>
        private static void RenderEyes(Camera cam, bool stereo)
        {
            if (!stereo) { cam.Render(); return; }
            var p = cam.transform.position; var right = cam.transform.right * 0.032f;
            cam.transform.position = p - right; cam.Render(); cam.transform.position = p + right; cam.Render(); cam.transform.position = p;
        }

        private static string F(double d) => double.IsNaN(d) ? "" : d.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
#endif
