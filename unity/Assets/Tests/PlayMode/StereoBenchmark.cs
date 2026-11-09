#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Diagnostics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Two-eye desktop benchmark (round 3, lane C). Not a behaviour test (Explicit). Renders BOTH eyes of a headset frame (two cameras, 64 mm apart, each its own 1832 x 1920 4x MSAA target; BENCH_EYE_SCALE
    /// scales both) for several workloads, with configurations interleaved round by round so slow drift in machine load hits every configuration equally. Per frame it records the wall time of the frame
    /// loop, the time until the GPU has finished both eyes (1-pixel readback), the CPU time to submit both eyes, and the FrameTimingManager main-thread and render-thread times when the Editor reports them.
    /// Writes a summary csv (BENCH_OUT) and a per-frame csv (BENCH_OUT with .frames.csv). tools/unity/bench_report.py compares configurations (paired per round, bootstrap interval, Wilcoxon sign test).
    /// Desktop GPU numbers: they rank work and show what was removed, they are never Quest frame rates. The Meta XR Simulator and an OpenXR runtime are not installed on this machine (see docs/qa/quest2_benchmark_method.md), so two explicit eye renders stand in for the headset's stereo pass.
    /// Environment: BENCH_CONFIGS, BENCH_OUT, BENCH_ROUNDS (5), BENCH_FRAMES (60 per cell), BENCH_EYE_SCALE (1.0), BENCH_WORKLOADS (static,turn,walk; default all), BENCH_QUALITY (2).
    /// </summary>
    public class StereoBenchmark
    {
        private const int EyeW = 1832, EyeH = 1920;

        private struct Cell { public string Workload, Id, Room; public Vector3 P, L; public float YawPerFrame; }

        private static readonly Cell[] Cells =
        {
            new Cell { Workload = "static", Id = "V2", Room = "PatientBay", P = new Vector3(2.2f, 1.65f, -1.8f), L = new Vector3(-1.3f, 1.0f, 0.9f) },
            new Cell { Workload = "static", Id = "V3", Room = "PatientBay", P = new Vector3(1.8f, 1.5f, -0.6f), L = new Vector3(-2.45f, 1.6f, 0f) },
            new Cell { Workload = "static", Id = "V4", Room = "Corridor", P = new Vector3(4.5f, 1.7f, 13.6f), L = new Vector3(4.5f, 1.5f, -9f) },
            new Cell { Workload = "static", Id = "V7", Room = "OT", P = new Vector3(7f, 1.7f, 5.2f), L = new Vector3(11f, 1.2f, 5.4f) },
            new Cell { Workload = "static", Id = "V10", Room = "Corridor", P = new Vector3(4.5f, 1.6f, 13.4f), L = new Vector3(4.5f, -25f, 34f) },
            // rapid head turns: 6 degrees per frame (about 360 degrees per second at 60 frames per second), full circle in 60 frames
            new Cell { Workload = "turn", Id = "T-bay", Room = "PatientBay", P = new Vector3(0.6f, 1.65f, -0.4f), L = new Vector3(0.6f, 1.65f, -2f), YawPerFrame = 6f },
            new Cell { Workload = "turn", Id = "T-corridor", Room = "Corridor", P = new Vector3(4.5f, 1.65f, 9f), L = new Vector3(4.5f, 1.65f, 14f), YawPerFrame = 6f },
            new Cell { Workload = "turn", Id = "T-theatre", Room = "OT", P = new Vector3(8.5f, 1.65f, 5.2f), L = new Vector3(11f, 1.65f, 5.2f), YawPerFrame = 6f },
            // walking: 1.4 m/s along the corridor, head turning gently (frame by frame movement exercises the cull caches)
            new Cell { Workload = "walk", Id = "W-corridor", Room = "Corridor", P = new Vector3(4.5f, 1.65f, 0f), L = new Vector3(4.5f, 1.65f, 14f), YawPerFrame = 0f },
        };

        [UnityTest, Explicit("desktop benchmark"), Timeout(21600000)]
        public IEnumerator Benchmark_stereo()
        {
            string cfg = Environment.GetEnvironmentVariable("BENCH_CONFIGS") ?? "B20,B28,B29", outPath = Environment.GetEnvironmentVariable("BENCH_OUT");
            int rounds = Int("BENCH_ROUNDS", 5), frames = Int("BENCH_FRAMES", 60), quality = Int("BENCH_QUALITY", 2);
            float scale = float.TryParse(Environment.GetEnvironmentVariable("BENCH_EYE_SCALE"), NumberStyles.Float, CultureInfo.InvariantCulture, out var sc0) ? sc0 : 1f;
            var wl = (Environment.GetEnvironmentVariable("BENCH_WORKLOADS") ?? "static,turn,walk").Split(',');
            Assert.IsFalse(string.IsNullOrEmpty(outPath), "set BENCH_OUT");
            int authored = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(quality, true);
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;

            int w = Mathf.RoundToInt(EyeW * scale), h = Mathf.RoundToInt(EyeH * scale);
            var eyes = new Camera[2]; var rts = new RenderTexture[2]; var gos = new GameObject[2];
            for (int e = 0; e < 2; e++)
            {
                gos[e] = new GameObject("BenchEye" + e); eyes[e] = gos[e].AddComponent<Camera>(); eyes[e].nearClipPlane = 0.05f; eyes[e].farClipPlane = 1000f; eyes[e].fieldOfView = 96f; eyes[e].clearFlags = CameraClearFlags.Skybox;
                rts[e] = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; eyes[e].targetTexture = rts[e];
                gos[e].AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None; eyes[e].enabled = false;
            }
            var toggles = new PerfToggles(); var ids = cfg.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
            ProcSense.VR.Views.ExteriorVisibility.CollectStats = false;   // true would switch the pose cache of the fast path off and measure the slow path
            toggles.Apply(PerfPresets.All[0], eyes[0], "PatientBay"); RenderPair(eyes, Vector3.zero, Quaternion.identity); yield return null;

            var summary = new StringBuilder("config,workload,cell,round,frames,draw_calls,setpass_calls,batches,triangles,vertices,shadow_casters,wall_med_ms,wall_p99_ms,wall_max_ms,done_med_ms,done_p99_ms,done_max_ms,submit_med_ms,main_med_ms,render_med_ms,gpu_med_ms,used_mem_mb,gfx_mem_mb,rt_mem_mb,eye_w,eye_h,fd_events,pipe_med_ms,pipe_min_ms,other_busy\n");
            var vis = new StringBuilder("config,cell,shader,material,renderers,triangles" + (char)10);
            var perFrame = new StringBuilder("config,workload,cell,round,frame,wall_ms,done_ms,submit_ms,main_ms,render_ms,gpu_ms\n");
            var proc = System.Diagnostics.Process.GetCurrentProcess(); try { proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.AboveNormal; } catch { }
            Debug.Log("BENCHLOAD " + LoadSnapshot());

            using (var draw = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count"))
            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var verts = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count"))
            using (var casters = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count"))
            using (var mem = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"))
            using (var gfx = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Gfx Used Memory"))
            using (var rtBytes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Render Textures Bytes"))
            {
                for (int round = 0; round < rounds; round++)
                {
                    foreach (var cell in Cells.Where(c => wl.Contains(c.Workload)))
                    {
                        // interleaved: the order of configurations rotates every round
                        for (int ci = 0; ci < ids.Length; ci++)
                        {
                            string id = ids[(ci + round) % ids.Length];
                            bool grp = id.EndsWith("-grp"); string baseId = grp ? id.Substring(0, id.Length - 4) : id; bool slow = baseId.EndsWith("-slow"); var preset = PerfPresets.All.First(p => p.Id == (slow ? baseId.Substring(0, baseId.Length - 5) : baseId));
                            GroupCityByMaterial(grp);
                            toggles.Apply(preset, eyes[0], cell.Room);
                            // "-slow": the same preset with the fast paths (pose cache, plane tests, skipped character samples) switched off, the reference for what they save
                            ProcSense.VR.Views.ExteriorVisibility.FastPath = !slow; ProcSense.VR.Views.RoomVisibility.FastPath = !slow; ProcSense.VR.Views.CharacterPerfCuller.FastTick = !slow;
                            var basePos = cell.P; var baseRot = Quaternion.LookRotation((cell.L - cell.P).normalized);
                            Vector3 Pos(int k) => cell.Workload == "walk" ? basePos + Vector3.forward * (1.4f / 60f) * k : basePos;
                            Quaternion Rot(int k) => cell.Workload == "walk" ? baseRot * Quaternion.Euler(0, 12f * Mathf.Sin(k * 0.05f), 0) : baseRot * Quaternion.Euler(0, cell.YawPerFrame * k, 0);
                            int busy = OtherBusy();
                            int warm = 30; for (int k = 0; k < warm; k++) { RenderPair(eyes, Pos(0), Rot(0)); yield return null; }
                            int fd = round == 0 ? FrameDebuggerEvents(eyes, Pos(0), Rot(0)) : -1;
                            if (round == 0) AppendVisible(vis, id, cell.Id);
                            var wall = new List<double>(); var done = new List<double>(); var submit = new List<double>(); var main = new List<double>(); var rend = new List<double>(); var gpu = new List<double>(); var timing = new FrameTiming[1];
                            double dc = 0, sp = 0, bt = 0, tr = 0, vt = 0, sc = 0; var last = System.Diagnostics.Stopwatch.StartNew();
                            for (int k = 0; k < frames; k++)
                            {
                                FrameTimingManager.CaptureFrameTimings();
                                var sw = System.Diagnostics.Stopwatch.StartNew(); RenderPair(eyes, Pos(k), Rot(k)); double sub = sw.Elapsed.TotalMilliseconds;
                                for (int e = 0; e < 2; e++) AsyncGPUReadback.Request(rts[e], 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32, null).WaitForCompletion();
                                double dn = sw.Elapsed.TotalMilliseconds; yield return null;
                                wall.Add(last.Elapsed.TotalMilliseconds); last.Restart(); submit.Add(sub); done.Add(dn);
                                dc = draw.LastValue; sp = setPass.LastValue; bt = batches.LastValue; tr = tris.LastValue; vt = verts.LastValue; sc = casters.LastValue;
                                double mm = double.NaN, rr = double.NaN, gg = double.NaN;
                                if (FrameTimingManager.GetLatestTimings(1, timing) > 0) { if (timing[0].cpuMainThreadFrameTime > 0) { mm = timing[0].cpuMainThreadFrameTime; main.Add(mm); } if (timing[0].cpuRenderThreadFrameTime > 0) { rr = timing[0].cpuRenderThreadFrameTime; rend.Add(rr); } if (timing[0].gpuFrameTime > 0) { gg = timing[0].gpuFrameTime; gpu.Add(gg); } }
                                perFrame.Append(id).Append(',').Append(cell.Workload).Append(',').Append(cell.Id).Append(',').Append(round).Append(',').Append(k).Append(',').Append(F(wall[wall.Count - 1])).Append(',').Append(F(dn)).Append(',').Append(F(sub)).Append(',').Append(F(mm)).Append(',').Append(F(rr)).Append(',').Append(F(gg)).Append('\n');
                            }
                            // GPU-bound time: eight stereo frames queued back to back, one wait at the end; with the CPU submit (about 9 ms for both eyes) shorter than the GPU work, time / 8 is the GPU time per frame
                            var pipe = new List<double>(); yield return null;
                            for (int rep = 0; rep < 5; rep++)
                            {
                                var sp2 = System.Diagnostics.Stopwatch.StartNew();
                                for (int k = 0; k < 8; k++) RenderPair(eyes, Pos(k), Rot(k));
                                for (int e = 0; e < 2; e++) AsyncGPUReadback.Request(rts[e], 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32, null).WaitForCompletion();
                                pipe.Add(sp2.Elapsed.TotalMilliseconds / 8); yield return null;
                            }
                            pipe.Sort();
                            busy = Math.Max(busy, OtherBusy());
                            wall.Sort(); done.Sort(); submit.Sort(); main.Sort(); rend.Sort(); gpu.Sort();
                            summary.Append(id).Append(',').Append(cell.Workload).Append(',').Append(cell.Id).Append(',').Append(round).Append(',').Append(frames).Append(',').Append(F(dc)).Append(',').Append(F(sp)).Append(',').Append(F(bt)).Append(',').Append(F(tr)).Append(',').Append(F(vt)).Append(',').Append(F(sc))
                                .Append(',').Append(F(Med(wall))).Append(',').Append(F(Pct(wall, 0.99))).Append(',').Append(F(wall.Count == 0 ? double.NaN : wall[wall.Count - 1])).Append(',').Append(F(Med(done))).Append(',').Append(F(Pct(done, 0.99))).Append(',').Append(F(done.Count == 0 ? double.NaN : done[done.Count - 1]))
                                .Append(',').Append(F(Med(submit))).Append(',').Append(F(Med(main))).Append(',').Append(F(Med(rend))).Append(',').Append(F(Med(gpu))).Append(',').Append(F(mem.LastValue / 1048576.0)).Append(',').Append(F(gfx.LastValue / 1048576.0)).Append(',').Append(F(rtBytes.Valid ? rtBytes.LastValue / 1048576.0 : double.NaN)).Append(',').Append(w).Append(',').Append(h).Append(',').Append(fd).Append(',').Append(F(Med(pipe))).Append(',').Append(F(pipe[0])).Append(',').Append(busy).Append('\n');
                        }
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath)); File.WriteAllText(outPath, summary.ToString()); File.WriteAllText(Path.ChangeExtension(outPath, ".frames.csv"), perFrame.ToString()); File.WriteAllText(Path.ChangeExtension(outPath, ".visible.csv"), vis.ToString());
                    Debug.Log("BENCHLOAD round " + round + " " + LoadSnapshot());
                }
            }
            toggles.Apply(PerfPresets.All[0], eyes[0], "PatientBay");
            for (int e = 0; e < 2; e++) { eyes[e].targetTexture = null; UnityEngine.Object.Destroy(rts[e]); UnityEngine.Object.Destroy(gos[e]); }
            QualitySettings.SetQualityLevel(authored, true); ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }

        private static void RenderPair(Camera[] eyes, Vector3 pos, Quaternion rot)
        {
            for (int e = 0; e < 2; e++)
            {
                eyes[e].transform.SetPositionAndRotation(pos + rot * new Vector3(e == 0 ? -0.032f : 0.032f, 0, 0), rot);
                eyes[e].Render();
            }
        }

        /// <summary>Number of draw events the Frame Debugger sees for one stereo frame (reflection on the Editor's FrameDebuggerUtility); -1 when this Editor does not expose it headless.</summary>
        private static int FrameDebuggerEvents(Camera[] eyes, Vector3 pos, Quaternion rot)
        {
            try
            {
                var t = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.FrameDebuggerUtility"); if (t == null) return -1;
                var enable = t.GetMethod("SetEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(bool), typeof(int) }, null); if (enable == null) return -1;
                enable.Invoke(null, new object[] { true, 0 }); RenderPair(eyes, pos, rot);
                var count = t.GetProperty("count", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); int n = count != null ? (int)count.GetValue(null) : -1;
                enable.Invoke(null, new object[] { false, 0 }); return n;
            }
            catch { return -1; }
        }

        /// <summary>Number of other processes that load the machine while a cell is measured: other Unity Editors, builds (gradle, il2cpp), ffmpeg, Blender. Cells measured with anything else running are marked other_busy &gt; 0 and left out of the comparison (bench_stereo_report.py).</summary>
        private static int OtherBusy()
        {
            int me = System.Diagnostics.Process.GetCurrentProcess().Id, n = 0, editors = 0;
            foreach (var p in System.Diagnostics.Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName.ToLowerInvariant();
                    if (p.Id == me) continue;
                    if (name == "unity") editors++; else if (name == "ffmpeg" || name == "blender" || name == "java" || name == "il2cpp") n++;
                }
                catch { }
            }
            return n + Math.Max(0, editors - 1);   // a test run is two Unity.exe processes (this Editor and one helper, counted here without this process); any more is another lane or tool
        }

        private static string LoadSnapshot()
        {
            var top = System.Diagnostics.Process.GetProcesses().Select(p => { try { return (name: p.ProcessName, cpu: p.TotalProcessorTime.TotalSeconds); } catch { return (name: "?", cpu: 0.0); } }).OrderByDescending(x => x.cpu).Take(6);
            return "top cpu-seconds: " + string.Join(", ", top.Select(x => x.name + "=" + x.cpu.ToString("0")));
        }

        /// <summary>What the last render drew: per shader and material, how many visible renderers and triangles (round 0 only); comparing configurations shows which objects cause extra SetPass calls.</summary>
        private static void AppendVisible(StringBuilder sb, string config, string cell)
        {
            var g = new Dictionary<(string, string), (int n, long t)>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff || !r.isVisible) continue;
                long tris = 0; var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) tris += mf.sharedMesh.GetIndexCount(i) / 3;
                foreach (var m in r.sharedMaterials) { if (m == null) continue; var k = (m.shader != null ? m.shader.name : "?", m.name); g.TryGetValue(k, out var v); g[k] = (v.n + 1, v.t + tris); }
            }
            foreach (var kv in g.OrderByDescending(x => x.Value.n)) sb.Append(config).Append(',').Append(cell).Append(',').Append(kv.Key.Item1.Replace(',', ';')).Append(',').Append(kv.Key.Item2.Replace(',', ';')).Append(',').Append(kv.Value.n).Append(',').Append(kv.Value.t).Append((char)10);
        }

        private static readonly Dictionary<Renderer, int> AuthoredPriority = new Dictionary<Renderer, int>();

        /// <summary>Experiment "-grp": gives every renderer of the city the same rendererPriority per material, so the opaque sort draws all chunks of one material together instead of interleaving materials by distance (fewer SetPass calls); off restores the authored priorities.</summary>
        private static void GroupCityByMaterial(bool on)
        {
            var ids = new Dictionary<Material, int>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool city = false; for (var t = r.transform; t != null; t = t.parent) if (t.name == "City exterior") { city = true; break; }
                if (!city) continue;
                if (!AuthoredPriority.ContainsKey(r)) AuthoredPriority[r] = r.rendererPriority;
                var m = r.sharedMaterial; if (on && m != null) { if (!ids.TryGetValue(m, out var k)) ids[m] = k = ids.Count + 1; r.rendererPriority = k; } else r.rendererPriority = AuthoredPriority[r];
            }
        }

        private static int Int(string name, int def) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : def;
        private static double Med(List<double> s) => s.Count == 0 ? double.NaN : s[s.Count / 2];
        private static double Pct(List<double> s, double q) => s.Count == 0 ? double.NaN : s[Math.Max(0, Math.Min(s.Count - 1, (int)Math.Ceiling(q * s.Count) - 1))];
        private static string F(double d) => double.IsNaN(d) ? "" : d.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
#endif
