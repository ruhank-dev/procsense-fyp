#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using ProcSense.VR.Diagnostics;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Audit lane 2: explains why the visible-renderer dump of the stereo benchmark showed 0 city renderers for B20 and 43 for B28 in the theatre turn cell, and guards the cause.
    /// Probe_theatre (Explicit, diagnostic): for each configuration (BENCH_PROBE_CONFIGS, default B20,B28,B15,B27) it applies the preset the way the benchmark does (PerfToggles.Apply rewrites forceRenderingOff of every
    /// renderer), then logs the culling decision and the city renderers that are drawn (enabled, not forced off, visible) right after the change, after warm-up and every 6 frames of the 360 degree turn at the theatre cell pose.
    /// Output csv: BENCH_OUT, default D:\ProcSenseWork\reports\theatre_probe.csv. Filter name: TheatreCellProbe.
    /// Stale_flags_are_reapplied (normal test): city flags rewritten by another script must be corrected on the next render (the fast path used to trust its cached hide state).
    /// </summary>
    public class TheatreCellProbe
    {
        private static readonly Vector3 P = new Vector3(8.5f, 1.65f, 5.2f), L = new Vector3(11f, 1.65f, 5.2f);

        private static bool UnderCity(Transform t) { for (; t != null; t = t.parent) if (t.name == "City exterior") return true; return false; }

        private static IEnumerator Boot(Action<Camera> got)
        {
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            for (int i = 0; i < 120; i++) yield return null;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            var go = new GameObject("ProbeEye"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 96f;
            var rt = new RenderTexture(512, 512, 24) { antiAliasing = 1 }; cam.targetTexture = rt; go.AddComponent<UniversalAdditionalCameraData>(); cam.enabled = false;
            got(cam);
        }

        private static void Render(Camera cam, Quaternion rot) { cam.transform.SetPositionAndRotation(P, rot); cam.Render(); }

        private struct CityState { public int Renderers, NotForcedOff, Visible, OptNotForcedOff, OptVisible; public long VisibleTris; }

        private static CityState Count()
        {
            var s = new CityState();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!UnderCity(r.transform) || r is ParticleSystemRenderer) continue;
                bool opt = r.name.Contains("(Quest)") || r.transform.parent != null && r.transform.parent.name.Contains("(Quest)");
                s.Renderers++;
                if (!r.enabled || r.forceRenderingOff) continue;
                s.NotForcedOff++; if (opt) s.OptNotForcedOff++;
                if (!r.isVisible) continue;
                s.Visible++; if (opt) s.OptVisible++;
                var mf = r.GetComponent<MeshFilter>(); if (mf != null && mf.sharedMesh != null) for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) s.VisibleTris += mf.sharedMesh.GetIndexCount(i) / 3;
            }
            return s;
        }

        [UnityTest, Explicit("diagnostic probe"), Timeout(1800000)]
        public IEnumerator Probe_theatre()
        {
            Camera cam = null; yield return Boot(c => cam = c);
            string outPath = Environment.GetEnvironmentVariable("BENCH_PROBE_OUT") ?? @"D:\ProcSenseWork\reports\theatre_probe.csv";   // not BENCH_OUT: StereoBenchmark writes there in the same launch
            var ids = (Environment.GetEnvironmentVariable("BENCH_PROBE_CONFIGS") ?? "B20,B28,B15,B27").Split(',').Select(s => s.Trim()).ToArray();
            var sb = new StringBuilder("config,phase,frame,yaw,room,decision,city_hidden_now,chunks_drawn,chunks_total,city_renderers,not_forced_off,visible,opt_not_forced_off,opt_visible,visible_city_tris\n");
            var toggles = new PerfToggles(); var baseRot = Quaternion.LookRotation((L - P).normalized);
            ExteriorVisibility.CollectStats = false;
            foreach (var id in ids)
            {
                var preset = PerfPresets.All.First(p => p.Id == id);
                toggles.Apply(preset, cam, "OT");
                void Log(string phase, int frame, float yaw)
                {
                    var st = Count(); var ev = ExteriorVisibility.Instance;
                    sb.Append(id).Append(',').Append(phase).Append(',').Append(frame).Append(',').Append(yaw.ToString("0")).Append(',').Append(ev != null ? ev.RoomIndexAt(P) : -9).Append(',').Append(ExteriorVisibility.LastDecision.Replace(',', ';')).Append(',')
                      .Append(ExteriorVisibility.CityHiddenNow ? 1 : 0).Append(',').Append(ExteriorVisibility.ChunksDrawn).Append(',').Append(ExteriorVisibility.ChunksTotal).Append(',').Append(st.Renderers).Append(',').Append(st.NotForcedOff).Append(',').Append(st.Visible)
                      .Append(',').Append(st.OptNotForcedOff).Append(',').Append(st.OptVisible).Append(',').Append(st.VisibleTris).Append('\n');
                }
                Render(cam, baseRot); yield return null; Render(cam, baseRot); Log("after_change", 1, 0);
                for (int k = 0; k < 30; k++) { Render(cam, baseRot); yield return null; }
                Log("warm", 30, 0);
                for (int k = 0; k < 60; k++) { var rot = baseRot * Quaternion.Euler(0, 6f * k, 0); Render(cam, rot); yield return null; if (k % 6 == 0) Log("turn", k, 6f * k); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outPath)); File.WriteAllText(outPath, sb.ToString());
            Debug.Log("[TheatreCellProbe] written " + outPath);
            toggles.Apply(PerfPresets.All[0], cam, "OT");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator Stale_flags_are_reapplied()
        {
            Camera cam = null; yield return Boot(c => cam = c);
            var toggles = new PerfToggles(); var baseRot = Quaternion.LookRotation((L - P).normalized);
            toggles.Apply(PerfPresets.All.First(p => p.Id == "B28"), cam, "OT");
            ExteriorVisibility.FastPath = true; ExteriorVisibility.CollectStats = false;
            // a pose in a room that shows no window or door aperture: the city must be hidden (find one yaw where the decision says "none")
            float hiddenYaw = -1;
            for (int k = 0; k < 60 && hiddenYaw < 0; k++) { Render(cam, baseRot * Quaternion.Euler(0, 6f * k, 0)); yield return null; if (ExteriorVisibility.CityHiddenNow) hiddenYaw = 6f * k; }
            Assume.That(hiddenYaw >= 0, "no theatre yaw hides the city in this scene set");
            var rotHidden = baseRot * Quaternion.Euler(0, hiddenYaw, 0);
            for (int k = 0; k < 3; k++) { Render(cam, rotHidden); yield return null; }
            var city = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => UnderCity(r.transform) && r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            Assume.That(city.Length > 0, "no enabled city renderers");
            Assert.IsTrue(city.All(r => r.forceRenderingOff), "the city is hidden before the external write");
            foreach (var r in city) r.forceRenderingOff = false;      // what PerfToggles.Apply does on every configuration change
            ExteriorVisibility.MarkDirty();
            Render(cam, rotHidden); yield return null;
            Assert.IsTrue(city.All(r => r.forceRenderingOff), "after the next render the city must be hidden again (the fast path trusted its cached state)");
            toggles.Apply(PerfPresets.All[0], cam, "OT");
        }
    }
}
#endif
