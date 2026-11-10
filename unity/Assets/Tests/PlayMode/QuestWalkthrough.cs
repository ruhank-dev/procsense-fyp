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
using ProcSense.VR.Views;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Review walkthrough of the real Unity hospital (P3-17/P3-19), not a behaviour test (Explicit). One scripted head path, rendered the same way for every configuration: the patient bay,
    /// the door, the corridor, the theatre, the glass wall and back, with slow pans, fast turns, 90 and 180 degree snaps, a full 360 degree look-around, three head heights and walking head bob.
    /// Per frame it records the workload counters and the time to finish the frame, and with BENCH_DIFF=1 it also renders the same pose with all culling off and with the original exterior and
    /// measures how many pixels differ, so a missing building, wall or room shows up as a number and a saved image, not only to the eye.
    /// Environment: BENCH_CLIP_DIR (output), BENCH_CONFIGS (preset ids, default "B17"), BENCH_W/BENCH_H (default 1920x1080), BENCH_FPS (simulated path rate, default 30),
    /// BENCH_FRAMES_MAX (stop early), BENCH_DIFF=1, BENCH_JPG=0 (skip writing frames; timing and differences only), BENCH_SEG (comma list of segment names to run).
    /// </summary>
    public class QuestWalkthrough
    {
        private struct Key { public float t; public Vector3 pos; public Vector3 look; public float turn; public float extraYaw; public string seg; public string door; }

        private static Key K(float t, Vector3 pos, Vector3 look, float turn = -1f, float extraYaw = 0f, string seg = null, string door = null) => new Key { t = t, pos = pos, look = look, turn = turn, extraYaw = extraYaw, seg = seg, door = door };
        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        /// <summary>The head path: time (s), eye position, the point looked at, the turn time in seconds (-1 = the whole segment, a slow pan; 0.25 = a fast turn), extra yaw degrees (360 = one full circle).</summary>
        private static List<Key> Path()
        {
            const float H = 1.62f;
            return new List<Key>
            {
                // patient bay: look at the patient, the monitor, the window
                K(0f,  V(1.8f, H, -1.5f), V(0f, 1.0f, 1.0f), seg: "bay"),
                K(4f,  V(1.4f, H, -0.4f), V(0f, 0.9f, 1.2f)),
                K(8f,  V(1.0f, 1.55f, 0.8f), V(0f, 0.9f, 1.5f)),
                K(11f, V(0.95f, 1.5f, 1.35f), V(1.16f, 1.18f, 1.5f)),
                K(14f, V(1.0f, H, 0.2f), V(-2.4f, 1.9f, 0f)),
                K(17f, V(-0.7f, H, 0f), V(-2.4f, 1.7f, 0f)),
                K(20f, V(-1.6f, H, 0f), V(-40f, -6f, 4f)),                     // at the window, looking out and down at the street
                K(22f, V(-1.6f, H, 0f), V(-40f, -30f, -6f), 1.6f),              // look further down
                K(24f, V(-1.6f, H, 0f), V(2.5f, 1.5f, -0.8f), 0.25f),           // snap about 150 degrees towards the door
                K(25.5f, V(-1.6f, H, 0f), V(-40f, 3f, 0f), 0.25f),              // snap back to the window
                K(27f, V(-1.6f, H, 0f), V(2.5f, 1.5f, -0.8f), 0.3f, door: "PatientBay"),
                // through the bay door into the corridor
                K(32f, V(2.2f, H, -0.8f), V(4.5f, 1.6f, 2f), seg: "corridor"),
                K(35f, V(3.8f, H, -0.6f), V(4.5f, 1.6f, 8f)),
                K(41f, V(4.5f, 1.58f, 2.6f), V(4.5f, 1.5f, 14f)),              // walking along the corridor
                K(45f, V(4.5f, 1.6f, 5.2f), V(4.5f, 1.5f, 14f)),
                K(46.5f, V(4.5f, 1.6f, 5.2f), V(8f, 1.5f, 5.2f), 0.5f, door: "OT"),           // 90 degree turn to the theatre door
                // theatre
                K(51f, V(6.8f, H, 5.2f), V(13f, 1.3f, 5.4f), seg: "theatre"),
                K(55f, V(8.8f, H, 5.2f), V(13f, 1.3f, 5.4f)),
                K(56f, V(8.8f, H, 5.2f), V(4.5f, 1.6f, 5.2f), 0.2f),           // sudden 180 degree turn
                K(57.5f, V(8.8f, H, 5.2f), V(13f, 1.3f, 5.4f), 0.2f),          // and back
                K(63f, V(8.8f, H, 5.2f), V(13f, 1.3f, 5.4f), 4.5f, 360f),     // a full 360 degree look-around
                K(66f, V(8.0f, 1.25f, 5.2f), V(10.5f, 0.9f, 5.4f)),            // lower head (sitting height)
                K(69f, V(8.0f, 1.85f, 5.2f), V(10.5f, 1.6f, 5.4f)),            // taller head
                K(72f, V(6.6f, H, 5.2f), V(4.5f, 1.6f, 5.2f), 0.6f),           // back towards the door
                // corridor to the glass wall
                K(77f, V(4.5f, H, 5.2f), V(4.5f, 1.5f, 14f), seg: "glasswall"),
                K(84f, V(4.5f, 1.6f, 11.5f), V(4.5f, 1.5f, 14.2f)),
                K(89f, V(4.5f, 1.6f, 13.3f), V(4.5f, 1.3f, 14.2f)),
                K(92f, V(4.5f, 1.6f, 13.3f), V(4.5f, -25f, 34f)),              // down at the street
                K(95f, V(4.5f, 1.6f, 13.3f), V(30f, -10f, 40f), 1.5f),         // along the glass
                K(98f, V(4.5f, 1.6f, 13.3f), V(-20f, -10f, 40f), 0.3f),        // fast turn the other way
                K(100f, V(4.5f, 1.6f, 13.3f), V(4.5f, 1.5f, 0f), 0.25f),       // turn away from the window, back to the corridor
                K(101.5f, V(4.5f, 1.6f, 13.3f), V(4.5f, 8f, 60f), 0.25f),      // and back at the window, upwards at the towers
                // return to the bay
                K(108f, V(4.5f, H, 3f), V(3f, 1.5f, -0.8f), seg: "return"),
                K(112f, V(3.6f, H, -0.8f), V(0f, 1.0f, 1.0f)),
                K(116f, V(1.6f, H, -0.8f), V(0f, 1.0f, 1.0f)),
                K(120f, V(1.2f, H, -0.2f), V(-2.4f, 1.8f, 0f)),
                // round 3 additions (docs/qa/quest2_final_verification.md): the places where the city culling is most exposed
                // bay window: the benchmark views V3 (across to the window) and V12 (down at the street), slow
                K(124f, V(1.8f, 1.5f, -0.6f), V(-2.45f, 1.6f, 0f), seg: "baywindow"),
                K(128f, V(-1.6f, 1.5f, 0f), V(-25f, -12f, 5f), 2.5f),
                K(131f, V(-1.6f, 1.5f, 0f), V(-25f, -35f, 4f), 2.5f),
                K(134f, V(-1.6f, 1.5f, 0f), V(-20f, -6f, 22f), 2.5f),
                // back through the bay door into the corridor (a straight line from the window to the corridor would leave the building)
                K(136f, V(1.2f, H, -0.8f), V(4.5f, 1.6f, -0.8f)),
                K(138f, V(3.6f, H, -0.8f), V(4.5f, 1.6f, 8f)),
                // corridor, next to the glass wall: a full 360 degree look-around, then down at the street
                K(144f, V(4.5f, H, 12.8f), V(4.5f, 1.5f, 14.2f), seg: "glass360"),
                K(150f, V(4.5f, H, 12.8f), V(4.5f, 1.5f, 14.2f), 5f, 360f),
                K(153f, V(4.5f, H, 12.8f), V(4.5f, -25f, 34f), 0.4f),
                // walk back along the middle of the corridor and through the bay door (the straight line from here to the door start crossed the bay wall: black frames)
                K(155.2f, V(4.5f, H, 6.5f), V(4.5f, 1.5f, -4f), 0.5f),
                K(156.8f, V(4.4f, H, 0.6f), V(3.6f, 1.5f, -0.8f), 0.5f),
                K(157.5f, V(3.8f, H, -0.8f), V(1.6f, 1.5f, -0.8f), 0.4f),
                // walking through the open bay door with fast turns (doorway reset and head-motion pad together)
                K(158f, V(1.6f, H, -0.8f), V(4.5f, 1.6f, -0.8f), seg: "doorfast"),
                K(161f, V(3.6f, H, -0.8f), V(4.5f, 1.6f, 8f), 0.25f),
                K(162f, V(3.6f, H, -0.8f), V(2f, 1.5f, 0.6f), 0.2f),
                K(163f, V(3.2f, H, -0.8f), V(5.5f, 1.5f, -6f), 0.2f),
                K(165f, V(2.2f, H, -0.8f), V(-2.4f, 1.5f, -0.8f), 0.25f),
                K(167f, V(1.2f, H, -0.8f), V(-2.4f, 1.5f, 0.6f), 0.25f),   // ends looking into the bay: the open door leaf is not in view
            };
        }

        private static string RoomAt(Vector3 p) => p.x < 2.5f ? "PatientBay" : (p.x > 6.5f && p.z > 4.2f && p.z < 6.3f ? "OT" : "Corridor");

        private static (Vector3 pos, float yaw, float pitch) Sample(List<Key> ks, float t)
        {
            int i = ks.FindLastIndex(k => k.t <= t); if (i < 0) i = 0; if (i >= ks.Count - 1) { var l = ks[ks.Count - 1]; Aim(l.pos, l.look, out float y0, out float p0); return (l.pos, y0 + l.extraYaw, p0); }
            var a = ks[i]; var b = ks[i + 1]; float seg = b.t - a.t, u = (t - a.t) / seg;
            Aim(a.pos, a.look, out float ya, out float pa); Aim(b.pos, b.look, out float yb, out float pb);
            // yaw: shortest turn, plus the extra circle when the key asks for one; the gaze changes during the first 'turn' seconds of the segment, the position moves the whole segment
            float dy = Mathf.DeltaAngle(ya, yb); if (Mathf.Abs(b.extraYaw) > 0.5f) dy += b.extraYaw;
            float turn = b.turn < 0f ? seg : Mathf.Min(b.turn, seg); float g = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - a.t) / turn));
            float yaw = ya + dy * g, pitch = Mathf.Lerp(pa, pb, g);
            var pos = Vector3.Lerp(a.pos, b.pos, Mathf.SmoothStep(0f, 1f, u * 0.6f + u * u * (3f - 2f * u) * 0.4f));
            // walking head bob and a little natural sway
            float speed = (b.pos - a.pos).magnitude / seg;
            if (speed > 0.15f) { pos.y += Mathf.Sin(t * 10.5f) * 0.012f; pos.x += Mathf.Sin(t * 5.2f) * 0.008f; }
            yaw += Mathf.Sin(t * 0.9f) * 0.8f; pitch += Mathf.Sin(t * 1.3f + 1f) * 0.5f;
            return (pos, yaw, pitch);
        }

        private static void Aim(Vector3 from, Vector3 to, out float yaw, out float pitch)
        {
            var d = to - from; yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        }

        [UnityTest, Explicit("review walkthrough"), Timeout(14400000)]
        public IEnumerator Walkthrough()
        {
            string dir = Environment.GetEnvironmentVariable("BENCH_CLIP_DIR"); Assert.IsFalse(string.IsNullOrEmpty(dir), "set BENCH_CLIP_DIR");
            string cfgs = Environment.GetEnvironmentVariable("BENCH_CONFIGS") ?? "B17";
            int W = int.TryParse(Environment.GetEnvironmentVariable("BENCH_W"), out var w) ? w : 1920, H = int.TryParse(Environment.GetEnvironmentVariable("BENCH_H"), out var h) ? h : 1080;
            float fps = float.TryParse(Environment.GetEnvironmentVariable("BENCH_FPS"), out var fp) ? fp : 30f;
            int maxFrames = int.TryParse(Environment.GetEnvironmentVariable("BENCH_FRAMES_MAX"), out var mf) ? mf : int.MaxValue;
            bool diff = Environment.GetEnvironmentVariable("BENCH_DIFF") == "1", jpg = Environment.GetEnvironmentVariable("BENCH_JPG") != "0";
            var segFilter = (Environment.GetEnvironmentVariable("BENCH_SEG") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            int authoredQuality = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;

            var go = new GameObject("WalkCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 78f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None; cam.enabled = false;
            const int SW = 480, SH = 270;
            var small = new RenderTexture(SW, SH, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var toggles = new PerfToggles(); var keys = Path(); float total = keys[keys.Count - 1].t; int frames = Mathf.Min(maxFrames, Mathf.CeilToInt(total * fps));
            var doorsDone = new HashSet<string>();
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGB24, false, false), st = new Texture2D(SW, SH, TextureFormat.RGB24, false, false);

            Color32[] Grab(RenderTexture src)
            {
                Graphics.Blit(src, small); var prev = RenderTexture.active; RenderTexture.active = small; st.ReadPixels(new Rect(0, 0, SW, SH), 0, 0); st.Apply(); RenderTexture.active = prev; return st.GetPixels32();
            }
            (double mean, double pct) Compare(Color32[] a, Color32[] b)
            {
                long sum = 0; int over = 0; for (int i = 0; i < a.Length; i++) { int d = Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); sum += d; if (d > 60) over++; }
                return (sum / (3.0 * a.Length), 100.0 * over / a.Length);
            }

            using (var tris = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count"))
            using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count"))
            {
                foreach (var id in cfgs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var preset = PerfPresets.All.First(p => p.Id == id.Trim());
                    string outDir = Path_Combine(dir, preset.Id); Directory.CreateDirectory(outDir); Directory.CreateDirectory(Path_Combine(dir, "worst"));
                    var csv = new StringBuilder("frame,t,seg,room,x,y,z,yaw,pitch,triangles,setpass,cpu_ms,done_ms,chunks,decision,diff_cull_mean,diff_cull_pct,diff_orig_mean,diff_orig_pct\n");
                    string room = ""; string seg = ""; int n = 0; double worstCull = 0, worstOrig = 0; int worstCullF = -1, worstOrigF = -1;
                    doorsDone.Clear();
                    for (int f = 0; f < frames; f++)
                    {
                        float t = f / fps; var (pos, yaw, pitch) = Sample(keys, t);
                        var ks = keys.FindLast(k => k.t <= t); if (ks.seg != null) seg = ks.seg;
                        foreach (var k in keys) if (k.door != null) SetDoorAngle(k.door, DoorAngleAt(t - (k.t - 3f)));   // a pure function of the path time: identical door phase in every configuration and run, whatever the render speed
                        if (segFilter.Length > 0 && !segFilter.Contains(seg)) continue;
                        cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pitch, yaw, 0f));
                        string r = RoomAt(pos); if (r != room || f == 0) { room = r; toggles.Apply(preset, cam, room); }
                        var sw = System.Diagnostics.Stopwatch.StartNew(); cam.Render(); double cpu = sw.Elapsed.TotalMilliseconds;
                        AsyncGPUReadback.Request(rt, 0, 0, 1, 0, 1, 0, 1, TextureFormat.RGBA32, null).WaitForCompletion(); double done = sw.Elapsed.TotalMilliseconds; sw.Stop();
                        yield return null;
                        double tr = tris.LastValue, sp = setPass.LastValue; string decision = ExteriorVisibility.LastDecision; int chunks = ExteriorVisibility.ChunksDrawn;
                        cam.Render(); yield return null; cam.Render();
                        double dc = double.NaN, dp = double.NaN, oc = double.NaN, op = double.NaN;
                        if (jpg || diff)
                        {
                            if (jpg) { var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev; File.WriteAllBytes(Path_Combine(outDir, "f" + n.ToString("00000") + ".jpg"), tex.EncodeToJPG(93)); }
                            if (diff)
                            {
                                var mine = Grab(rt);   // rt still holds the production render of this frame
                                // reference 1: the same exterior with every visibility cull off (room-aware, portal, occlusion) - anything different is a culling error
                                bool ve = ExteriorVisibility.Enabled, vp = ExteriorVisibility.PortalCullEnabled; var qv = QuestExterior.Current; bool occ = cam.useOcclusionCulling;
                                ExteriorVisibility.Enabled = false; cam.useOcclusionCulling = false; cam.Render(); cam.Render();   // back to back in the same frame: animation, doors and dust are in the same state as the picture compared
                                var refCull = Grab(rt); (dc, dp) = Compare(mine, refCull);
                                // reference 2: the untouched original exterior with every cull off - the approved look
                                QuestExterior.Current = QuestExterior.Variant.Original; cam.Render(); cam.Render();
                                var refOrig = Grab(rt); (oc, op) = Compare(mine, refOrig);
                                ExteriorVisibility.Enabled = ve; ExteriorVisibility.PortalCullEnabled = vp; QuestExterior.Current = qv; cam.useOcclusionCulling = occ;
                                if (dp > worstCull) { worstCull = dp; worstCullF = n; SaveTriple(Path_Combine(dir, "worst"), preset.Id + "_cull_f" + n, rt, W, H, mine, refCull, SW, SH); }
                                if (op > worstOrig) { worstOrig = op; worstOrigF = n; }
                            }
                        }
                        csv.Append(n).Append(',').Append(t.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(seg).Append(',').Append(room).Append(',').Append(pos.x.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(pos.y.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(pos.z.ToString("0.00", CultureInfo.InvariantCulture))
                           .Append(',').Append(yaw.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(pitch.ToString("0.0", CultureInfo.InvariantCulture)).Append(',').Append(tr).Append(',').Append(sp).Append(',').Append(cpu.ToString("0.00", CultureInfo.InvariantCulture)).Append(',').Append(done.ToString("0.00", CultureInfo.InvariantCulture))
                           .Append(',').Append(chunks).Append(',').Append(decision?.Replace(',', ';')).Append(',').Append(double.IsNaN(dc) ? "" : dc.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(double.IsNaN(dp) ? "" : dp.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(double.IsNaN(oc) ? "" : oc.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(double.IsNaN(op) ? "" : op.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
                        n++;
                    }
                    File.WriteAllText(Path_Combine(dir, preset.Id + "_walk.csv"), csv.ToString());
                    Debug.Log($"WALK {preset.Id} frames {n} worst culling difference {worstCull:0.000}% at frame {worstCullF}; worst difference from the original exterior {worstOrig:0.000}% at frame {worstOrigF}");
                }
            }
            toggles.Apply(PerfPresets.All[0], cam, "PatientBay"); QuestExterior.ResetToDefault();
            cam.targetTexture = null; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(small); UnityEngine.Object.Destroy(go);
            QualitySettings.SetQualityLevel(authoredQuality, true);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }


        /// <summary>
        /// Visibility sweep: renders a grid of positions x 12 headings x 3 pitches in every room, with the doors closed and again open, once as the production configuration and once with every cull
        /// off (same exterior variant), and counts the poses whose pictures differ. A culling rule that hides something it should draw shows up here at any pose, not only on the review path.
        /// Environment: BENCH_CLIP_DIR, BENCH_CONFIGS (default B17).
        /// </summary>
        [UnityTest, Explicit("visibility sweep"), Timeout(14400000)]
        public IEnumerator Sweep_poses()
        {
            string dir = Environment.GetEnvironmentVariable("BENCH_CLIP_DIR"); Assert.IsFalse(string.IsNullOrEmpty(dir));
            var preset = PerfPresets.All.First(p => p.Id == (Environment.GetEnvironmentVariable("BENCH_CONFIGS") ?? "B17"));
            int authoredQuality = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true; ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            const int SW = 480, SH = 270;
            var go = new GameObject("SweepCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 78f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(SW, SH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None; cam.enabled = false;
            var st = new Texture2D(SW, SH, TextureFormat.RGB24, false, false); var toggles = new PerfToggles();
            Color32[] Grab() { var prev = RenderTexture.active; RenderTexture.active = rt; st.ReadPixels(new Rect(0, 0, SW, SH), 0, 0); st.Apply(); RenderTexture.active = prev; return st.GetPixels32(); }
            double Pct(Color32[] a, Color32[] b) { int over = 0; for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b) > 60) over++; return 100.0 * over / a.Length; }
            var spots = new List<Vector3>();
            foreach (var x in new[] { -2f, -1f, 0f, 1f, 2f }) foreach (var z in new[] { -1.5f, 0f, 1.5f }) spots.Add(new Vector3(x, 1.62f, z));
            foreach (var x in new[] { 3.2f, 4.5f, 5.8f }) for (float z = -9f; z <= 13.6f; z += 1.5f) spots.Add(new Vector3(x, 1.62f, z));
            foreach (var x in new[] { 7.5f, 9.5f, 11.5f }) spots.Add(new Vector3(x, 1.62f, 5.2f));
            var only = Environment.GetEnvironmentVariable("BENCH_SPOTS");   // "x,z;x,z" replaces the grid (quick re-checks, e.g. the theatre east end after the city shell fix)
            if (!string.IsNullOrEmpty(only)) { spots.Clear(); foreach (var pair in only.Split(';')) { var xz = pair.Split(','); spots.Add(new Vector3(float.Parse(xz[0], CultureInfo.InvariantCulture), 1.62f, float.Parse(xz[1], CultureInfo.InvariantCulture))); } }
            var csv = new StringBuilder("doors,room,x,z,yaw,pitch,diff_pct,vs_exterior_cull_off_pct,vs_occlusion_off_pct\n"); int poses = 0, bad = 0, saved = 0; double worst = 0; string worstName = ""; Directory.CreateDirectory(dir); Directory.CreateDirectory(Path_Combine(dir, "sweep_worst"));
            foreach (bool open in new[] { false, true })
            {
                if (open) { SetDoorAngle("PatientBay", DoorOpenAngle); SetDoorAngle("OT", DoorOpenAngle); for (int i = 0; i < 10; i++) yield return null; }
                toggles.Apply(preset, cam, "Corridor");
                foreach (var sp in spots)
                    foreach (float yaw in new[] { 0f, 30f, 60f, 90f, 120f, 150f, 180f, 210f, 240f, 270f, 300f, 330f })
                        foreach (float pitch in new[] { 0f, -25f, 12f })
                        {
                            cam.transform.SetPositionAndRotation(sp, Quaternion.Euler(pitch, yaw, 0f)); string room = RoomAt(sp);
                            cam.useOcclusionCulling = !preset.OcclusionOff; ExteriorVisibility.Enabled = true; cam.Render(); cam.Render(); var mine = Grab();
                            ExteriorVisibility.Enabled = false; cam.useOcclusionCulling = false; cam.Render(); cam.Render(); var reference = Grab(); ExteriorVisibility.Enabled = true; cam.useOcclusionCulling = true;
                            double pct = Pct(mine, reference); poses++; double cityOnly = double.NaN, occOnly = double.NaN;
                            if (pct > 0.05)
                            {   // which cull is responsible: the exterior culling alone, or the baked occlusion alone
                                ExteriorVisibility.Enabled = false; cam.useOcclusionCulling = true; cam.Render(); cam.Render(); cityOnly = Pct(mine, Grab());
                                ExteriorVisibility.Enabled = true; cam.useOcclusionCulling = false; cam.Render(); cam.Render(); occOnly = Pct(mine, Grab());
                                ExteriorVisibility.Enabled = true; cam.useOcclusionCulling = true;
                            }
                             csv.Append(open ? "open" : "closed").Append(',').Append(room).Append(',').Append(sp.x).Append(',').Append(sp.z).Append(',').Append(yaw).Append(',').Append(pitch).Append(',').Append(pct.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(double.IsNaN(cityOnly) ? "" : cityOnly.ToString("0.000", CultureInfo.InvariantCulture)).Append(',').Append(double.IsNaN(occOnly) ? "" : occOnly.ToString("0.000", CultureInfo.InvariantCulture)).Append('\n');
                            if (Environment.GetEnvironmentVariable("BENCH_SAVE") == "1" && pitch == 0f && yaw % 90f == 0f && !open) { Directory.CreateDirectory(Path_Combine(dir, "sweep_saved")); SaveTriple(Path_Combine(dir, "sweep_saved"), $"pose_{room}_{sp.x}_{sp.z}_{yaw}", rt, SW, SH, mine, reference, SW, SH); }   // left: production, right: every cull off
                            if (pct > 0.05) { bad++; if (pct > 0.2 && saved++ < 60) SaveTriple(Path_Combine(dir, "sweep_worst"), $"pose_{(open ? "open" : "closed")}_{room}_{sp.x}_{sp.z}_{yaw}_{pitch}", rt, SW, SH, mine, reference, SW, SH); if (pct > worst) { worst = pct; worstName = $"{(open ? "open" : "closed")}_{room}_{sp.x}_{sp.z}_{yaw}_{pitch}"; SaveTriple(Path_Combine(dir, "sweep_worst"), "worst_" + worstName, rt, SW, SH, mine, reference, SW, SH); } }
                            if (poses % 40 == 0) yield return null;
                        }
            }
            File.WriteAllText(Path_Combine(dir, preset.Id + "_sweep.csv"), csv.ToString());
            Debug.Log($"SWEEP {preset.Id} poses {poses}; poses differing by more than 0.05% of pixels: {bad}; worst {worst:0.000}% at {worstName}");
            toggles.Apply(PerfPresets.All[0], cam, "PatientBay"); QuestExterior.ResetToDefault(); cam.targetTexture = null; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
            QualitySettings.SetQualityLevel(authoredQuality, true); ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }


        /// <summary>Finds what a small occlusion-culling difference depends on: renders one pose (BENCH_POSE "x,z,yaw,pitch") with occlusion on and off under each preset in BENCH_CONFIGS and prints the differing share of pixels.</summary>
        [UnityTest, Explicit("occlusion probe"), Timeout(3600000)]
        public IEnumerator Occlusion_probe()
        {
            var pv = (Environment.GetEnvironmentVariable("BENCH_POSE") ?? "5.8,6,330,-25").Split(',').Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
            int authoredQuality = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true; ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            const int SW = 480, SH = 270;
            var go = new GameObject("ProbeCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 78f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(SW, SH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
            go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None; cam.enabled = false;
            var st = new Texture2D(SW, SH, TextureFormat.RGB24, false, false); var toggles = new PerfToggles();
            Color32[] Grab() { var prev = RenderTexture.active; RenderTexture.active = rt; st.ReadPixels(new Rect(0, 0, SW, SH), 0, 0); st.Apply(); RenderTexture.active = prev; return st.GetPixels32(); }
            var pos = new Vector3(pv[0], 1.62f, pv[1]); cam.transform.SetPositionAndRotation(pos, Quaternion.Euler(pv[3], pv[2], 0f));
            foreach (var id in (Environment.GetEnvironmentVariable("BENCH_CONFIGS") ?? "B17").Split(','))
            {
                var preset = PerfPresets.All.First(p => p.Id == id.Trim()); toggles.Apply(preset, cam, "Corridor");
                cam.useOcclusionCulling = true; cam.Render(); cam.Render(); var on = Grab(); cam.useOcclusionCulling = false; cam.Render(); cam.Render(); var off = Grab();
                int over = 0; for (int i = 0; i < on.Length; i++) if (Math.Abs(on[i].r - off[i].r) + Math.Abs(on[i].g - off[i].g) + Math.Abs(on[i].b - off[i].b) > 60) over++;
                Debug.Log($"PROBE {preset.Id} {preset.Label}: occlusion on vs off differ in {100.0 * over / on.Length:0.000}% of pixels");
                cam.useOcclusionCulling = true;
            }
            // which light is responsible: switch off only the realtime point fills, then only the realtime directional key
            toggles.Apply(PerfPresets.All.First(p => p.Id == "B17"), cam, "Corridor");
            var rl = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(l => l.enabled && l.bakingOutput.lightmapBakeType == LightmapBakeType.Realtime).ToList();
            foreach (var (label, pick) in new (string, Func<Light, bool>)[] { ("realtime points only off", l => l.type == LightType.Point), ("realtime directionals only off", l => l.type == LightType.Directional) })
            {
                var sel = rl.Where(pick).ToList(); foreach (var l in sel) l.enabled = false;
                cam.useOcclusionCulling = true; cam.Render(); cam.Render(); var on = Grab(); cam.useOcclusionCulling = false; cam.Render(); cam.Render(); var off = Grab();
                int over = 0; for (int i2 = 0; i2 < on.Length; i2++) if (Math.Abs(on[i2].r - off[i2].r) + Math.Abs(on[i2].g - off[i2].g) + Math.Abs(on[i2].b - off[i2].b) > 60) over++;
                Debug.Log($"PROBE {label} ({sel.Count} lights: {string.Join(", ", sel.Select(l => l.name))}): occlusion on vs off differ in {100.0 * over / on.Length:0.000}% of pixels");
                foreach (var l in sel) l.enabled = true; cam.useOcclusionCulling = true;
            }
            toggles.Apply(PerfPresets.All[0], cam, "PatientBay"); QuestExterior.ResetToDefault(); cam.targetTexture = null; UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
            QualitySettings.SetQualityLevel(authoredQuality, true); ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }


        /// <summary>Lists every realtime light with the renderers whose bounds contain its position (and whether they are baked as occluders): a light inside an occluder is dropped by occlusion culling.</summary>
        [UnityTest, Explicit("light occlusion probe"), Timeout(3600000)]
        public IEnumerator Light_occluder_probe()
        {
            string outPath = Environment.GetEnvironmentVariable("BENCH_OUT"); Assert.IsFalse(string.IsNullOrEmpty(outPath));
            LogAssert.ignoreFailingMessages = true; ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            var sb = new StringBuilder(); var rends = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (l.type == LightType.Directional || l.bakingOutput.lightmapBakeType != LightmapBakeType.Realtime) continue;
                sb.AppendLine($"{l.gameObject.scene.name}/{l.name} {l.type} pos {l.transform.position} range {l.range} intensity {l.intensity} enabled {l.enabled}");
                foreach (var r in rends)
                    if (r.bounds.size.magnitude < 30f && r.bounds.Contains(l.transform.position))
                    {
                        var f = UnityEditor.GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                        sb.AppendLine($"    inside {r.name} ({(f.HasFlag(UnityEditor.StaticEditorFlags.OccluderStatic) ? "OCCLUDER" : "not occluder")}) bounds {r.bounds.min} .. {r.bounds.max}");
                    }
            }
            Directory.CreateDirectory(Path_Combine(Path_Combine(outPath, ".."), "")); File.WriteAllText(outPath, sb.ToString());
        }

        private static string Path_Combine(string a, string b) => System.IO.Path.Combine(a, b);

        private const float DoorOpenAngle = -100f, DoorSweepSpeed = 160f;   // the angle and speed SwingDoor itself uses (Toggle target -100, MoveTowards 160 degrees per second)

        /// <summary>Door angle in degrees after the given time since the door was told to open: closed before, then the same linear sweep SwingDoor makes, then open.</summary>
        private static float DoorAngleAt(float secondsSinceOpen) => secondsSinceOpen <= 0f ? 0f : Mathf.Max(DoorOpenAngle, -DoorSweepSpeed * secondsSinceOpen);

        /// <summary>Sets the door between the hospital and toRoom to exactly this angle now (no physics step, no timing): the angle, the swing target and the transform, so SwingDoor.FixedUpdate has nothing left to move.</summary>
        private static void SetDoorAngle(string toRoom, float angle)
        {
            foreach (var d in UnityEngine.Object.FindObjectsByType<SwingDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (d.RoomA != toRoom && d.RoomB != toRoom) continue;
                var t = typeof(SwingDoor); const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                t.GetProperty("Angle", F).SetValue(d, angle); t.GetField("_target", F).SetValue(d, angle);
                var closed = (Quaternion)t.GetField("_closed", F).GetValue(d);
                var rot = (d.transform.parent != null ? d.transform.parent.rotation : Quaternion.identity) * closed * Quaternion.Euler(0, angle, 0);
                var body = d.GetComponent<Rigidbody>(); if (body != null) body.position = body.position; d.transform.rotation = rot; if (body != null) body.rotation = rot;
            }
        }

        private static void SaveTriple(string dir, string name, RenderTexture rt, int W, int H, Color32[] mine, Color32[] other, int SW, int SH)
        {
            var t = new Texture2D(SW * 2, SH, TextureFormat.RGB24, false); var all = new Color32[SW * 2 * SH];
            for (int y = 0; y < SH; y++) { Array.Copy(mine, y * SW, all, y * SW * 2, SW); Array.Copy(other, y * SW, all, y * SW * 2 + SW, SW); }
            t.SetPixels32(all); t.Apply(); File.WriteAllBytes(System.IO.Path.Combine(dir, name + ".jpg"), t.EncodeToJPG(90)); UnityEngine.Object.Destroy(t);
        }
    }
}
#endif
