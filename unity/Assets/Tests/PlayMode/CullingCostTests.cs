using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Views;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Desktop CPU cost of the Quest culling components (P3-PRF, round 3). Flies a fixed deterministic path (bay, corridor, theatre, with continuous turning) with one camera rendering a small target,
    /// so the frame is dominated by CPU work, and compares configurations: everything off; window/door city culling; + room culling; + character culler. Records, per configuration, median and
    /// 99th percentile of the frame and of the Camera.Render call, the Stopwatch cost of each culling handler per frame, forceRenderingOff reads and writes per frame, and GC bytes per frame.
    /// Env: BENCH_TAG (file name), BENCH_ROUNDS (default 3), BENCH_FRAMES (default 600). Output: D:\ProcSenseWork\reports\cull_cost_TAG.csv. Explicit: a measurement, not a pass/fail test.
    /// </summary>
    public class CullingCostTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [UnityTearDown] public IEnumerator Cleanup() { ResetAll(); return EditorXRTestEnvironment.Cleanup(); }

        private static void ResetAll() { ExteriorVisibility.ResetToDefault(); RoomVisibility.ResetToDefault(); QuestExterior.ResetToDefault(); QuestCullingProfile.ResetToDefault(); CharacterPerfCuller.Enabled = CharacterPerfCuller.DefaultEnabled; CullCost.On = false; ExteriorVisibility.FastPath = true; RoomVisibility.FastPath = true; CharacterPerfCuller.FastTick = true; }

        private static readonly Vector3[] Way = { new Vector3(1.2f, 1.6f, -0.2f), new Vector3(3.6f, 1.6f, -0.8f), new Vector3(4.5f, 1.6f, 3f), new Vector3(4.5f, 1.6f, 5.2f), new Vector3(8.8f, 1.6f, 5.2f), new Vector3(4.5f, 1.6f, 5.2f), new Vector3(4.5f, 1.6f, 13.3f), new Vector3(4.5f, 1.6f, 3f) };

        private static void Pose(Transform tr, float t)
        {
            float u = t / 6f; int i = Mathf.FloorToInt(u) % Way.Length, j = (i + 1) % Way.Length; float f = Mathf.SmoothStep(0, 1, u - Mathf.Floor(u));
            tr.position = Vector3.Lerp(Way[i], Way[j], f) + new Vector3(0, Mathf.Sin(t * 10.5f) * .012f, 0);
            float yaw = 70f * Mathf.Sin(t * 0.9f) + (Mathf.Repeat(t, 24f) > 20f ? (Mathf.Repeat(t, 24f) - 20f) * 90f : 0f) + 90f * i;   // slow scanning plus a full turn every 24 s
            tr.rotation = Quaternion.Euler(Mathf.Sin(t * 1.3f) * 12f, yaw, 0);
        }

        private struct Cfg { public string Name; public bool Ext, Room, Char, Fast; }

        [UnityTest, Explicit("measurement"), Timeout(7200000)]
        public IEnumerator Cost_of_culling_components()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            string tag = Environment.GetEnvironmentVariable("BENCH_TAG") ?? "run"; int rounds = int.Parse(Environment.GetEnvironmentVariable("BENCH_ROUNDS") ?? "3"), frames = int.Parse(Environment.GetEnvironmentVariable("BENCH_FRAMES") ?? "600");
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("CostCamera"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 96f; go.AddComponent<UniversalAdditionalCameraData>(); cam.targetTexture = new RenderTexture(512, 512, 24);
            var culler = new GameObject("CullerForBench").AddComponent<CharacterPerfCuller>();
            QuestExterior.Current = QuestExterior.Variant.Optimised; QuestCullingProfile.OcclusionEnabled = false;
            var cfgs = new[] { new Cfg { Name = "A_all_off" },
                new Cfg { Name = "B_exterior_orig", Ext = true }, new Cfg { Name = "B_exterior_fast", Ext = true, Fast = true },
                new Cfg { Name = "C_ext+room_orig", Ext = true, Room = true }, new Cfg { Name = "C_ext+room_fast", Ext = true, Room = true, Fast = true },
                new Cfg { Name = "D_all_orig", Ext = true, Room = true, Char = true }, new Cfg { Name = "D_all_fast", Ext = true, Room = true, Char = true, Fast = true } };
            var rows = new List<string>(); var sb = new StringBuilder("config,round,frame_med_ms,frame_p99_ms,render_med_ms,render_p99_ms,ext_ms_per_frame,portal_ms_per_frame,room_ms_per_frame,char_ms_per_frame,rescan_ms_per_call,reads_per_frame,writes_per_frame,gc_bytes_per_frame\n");
            using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            var sw = new Stopwatch(); float t = 0f;
            for (int round = 0; round < rounds; round++)
                foreach (var c in cfgs)
                {
                    ExteriorVisibility.FastPath = c.Fast; RoomVisibility.FastPath = c.Fast; CharacterPerfCuller.FastTick = c.Fast;
                    ExteriorVisibility.Enabled = c.Ext; RoomVisibility.Enabled = c.Room; CharacterPerfCuller.Enabled = c.Char;
                    for (int w = 0; w < 90; w++) { Pose(cam.transform, t += 1f / 60f); cam.Render(); yield return null; }
                    CullCost.Reset(); CullCost.On = true;
                    var frame = new List<double>(); var render = new List<double>(); long gcSum = 0; double last = Time.realtimeSinceStartupAsDouble;
                    for (int f = 0; f < frames; f++)
                    {
                        Pose(cam.transform, t += 1f / 60f); sw.Restart(); cam.Render(); render.Add(sw.Elapsed.TotalMilliseconds);
                        yield return null; double now = Time.realtimeSinceStartupAsDouble; frame.Add((now - last) * 1000.0); last = now; gcSum += gc.LastValue;
                    }
                    CullCost.On = false;
                    double Med(List<double> l) { var s = l.OrderBy(x => x).ToList(); return s[s.Count / 2]; } double P99(List<double> l) { var s = l.OrderBy(x => x).ToList(); return s[(int)(s.Count * .99)]; }
                    int rc = Math.Max(1, CullCost.Calls[CullCost.CharRescan]);
                    sb.AppendLine(string.Join(",", c.Name, round, Med(frame).ToString("F3"), P99(frame).ToString("F3"), Med(render).ToString("F3"), P99(render).ToString("F3"), (CullCost.Ms(CullCost.ExtCamera) / frames).ToString("F4"), (CullCost.Ms(CullCost.ExtPortal) / frames).ToString("F4"), (CullCost.Ms(CullCost.RoomCamera) / frames).ToString("F4"), (CullCost.Ms(CullCost.CharUpdate) / frames).ToString("F4"), (CullCost.Ms(CullCost.CharRescan) / rc).ToString("F3"), ((double)CullCost.Reads / frames).ToString("F1"), ((double)CullCost.Writes / frames).ToString("F2"), (gcSum / (double)frames).ToString("F0")));
                }
            Directory.CreateDirectory(@"D:\ProcSenseWork\reports"); File.WriteAllText(@"D:\ProcSenseWork\reports\cull_cost_" + tag + ".csv", sb.ToString());
            cam.targetTexture.Release(); UnityEngine.Object.Destroy(go); UnityEngine.Object.Destroy(culler.gameObject);
        }

        private static T[] FindObjectsByType<T>(FindObjectsInactive i, FindObjectsSortMode s) where T : UnityEngine.Object => UnityEngine.Object.FindObjectsByType<T>(i, s);
    }
}
