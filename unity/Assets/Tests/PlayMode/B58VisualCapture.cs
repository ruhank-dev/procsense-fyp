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
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Visual verification of the B58 look-preserving candidate (audit lane 3): did two additional lights per object (instead of four) and hard realtime shadows (instead of soft) change anything a clinician would notice?
    /// Renders the SAME list of camera poses once per preset, at a Quest eye size (1440 x 1584, 4x MSAA, 96 degree vertical field of view), and writes lossless PNGs for offline comparison
    /// (tools/unity/b58_visual_report.py). The poses cover: patient monitor UI and labels, posters and signs, the TV screen, doors and door glass, window glass, patient and staff faces and bodies, floor contact shadows,
    /// the corridor ceiling and its light fixtures, metallic and glossy equipment, the operating theatre and the patient bay.
    ///
    /// Determinism (so that a pixel difference is a rendering difference and not time): Boot is reloaded for every preset, Time.captureFramerate is 30, Random is seeded, every particle system (dust) gets a fixed seed
    /// derived from its hierarchy path and is restarted, and each shot is taken with Time.timeScale = 0 after the same number of frames. Compare presets that share their culling setup (B20 with B58, B61 with B62,
    /// B29 with B56, B15-based B63 B64 B65 with B15 or B60): the character culler and room culling legitimately change which characters have animated by the time a pose is reached.
    ///
    /// Environment: B58V_DIR (output root, default %PROCSENSE_WORK%/reports/b58_visual, else D:/ProcSenseWork/reports/b58_visual), B58V_CONFIGS (comma list of preset ids, default B20,B58; an entry like B20@r2 renders preset B20 again into the folder B20_r2, which measures the capture noise floor), B58V_POSES (optional comma list of pose ids).
    /// Output: <root>/<preset>/<pose id>.png, <root>/poses.csv. Test filter: ProcSense.Tests.PlayMode.B58VisualCapture. Not a regression test: it asserts only that it produced pictures.
    /// </summary>
    public sealed class B58VisualCapture
    {
        private const int W = 1440, H = 1584;

        private readonly struct P
        {
            public readonly string Id, Category; public readonly Vector3 Pos, Look;
            public P(string id, string category, Vector3 pos, Vector3 look) { Id = id; Category = category; Pos = pos; Look = look; }
        }

        private static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // Fixed poses (world metres). Text poses come from the text-compression probe (one metre in front of the surface, looking straight at it).
        private static readonly P[] Fixed =
        {
            new P("mon_ui",        "monitors",  V(1.092f, 1.316f, 0.701f), V(1.092f, 1.316f, 1.701f)),
            new P("mon_close",     "monitors",  V(1.092f, 1.316f, 1.25f),  V(1.092f, 1.316f, 1.701f)),
            new P("sign_poster02", "signs",     V(12.04f, 1.8f, 2.563f),   V(12.04f, 1.8f, 1.563f)),
            new P("sign_poster05", "signs",     V(12.637f, 1.8f, 5.15f),   V(13.637f, 1.8f, 5.15f)),
            new P("sign_poster11", "signs",     V(5.636f, 1.9f, -0.88f),   V(6.636f, 1.9f, -0.88f)),
            new P("sign_poster12", "signs",     V(7.364f, 1.8f, 4.15f),    V(6.364f, 1.8f, 4.15f)),
            new P("sign_tv",       "signs",     V(4.5f, 1.794f, -7.482f),  V(4.5f, 1.794f, -8.482f)),
            new P("door_ot",       "doors",     V(4.5f, 1.6f, 5.2f),       V(8f, 1.5f, 5.2f)),
            new P("door_bay_in",   "doors",     V(0.6f, 1.6f, -0.8f),      V(2.5f, 1.5f, -0.8f)),
            new P("door_bay_out",  "doors",     V(4.4f, 1.6f, -0.8f),      V(2.5f, 1.5f, -0.8f)),
            new P("win_bay",       "windows",   V(1.8f, 1.5f, -0.6f),      V(-2.45f, 1.6f, 0f)),
            new P("win_corr_ext",  "windows",   V(4.5f, 1.6f, 9f),         V(4.5f, 1.5f, 14.2f)),
            new P("win_glass_obl", "windows",   V(4.5f, 1.6f, 13.4f),      V(30f, -10f, 40f)),
            new P("win_down",      "windows",   V(-1.6f, 1.5f, 0f),        V(-25f, -12f, 5f)),
            new P("light_bay_low", "lighting",  V(2.2f, 0.9f, -1.4f),      V(-1.3f, 0.2f, 0.9f)),
            new P("light_bay",     "lighting",  V(2.2f, 1.65f, -1.8f),     V(-1.3f, 1.0f, 0.9f)),
            new P("light_ceiling", "lighting",  V(4.5f, 1.6f, 3f),         V(4.5f, 3.2f, 6.5f)),
            new P("light_corr",    "lighting",  V(3.2f, 1.55f, 5.6f),      V(5.45f, 1.2f, 7.9f)),
            new P("light_corr_long","lighting", V(4.5f, 1.7f, 13.6f),      V(4.5f, 1.5f, -9f)),
            new P("refl_theatre",  "reflections", V(9.5f, 1.7f, 5.2f),     V(13f, 1.3f, 5.4f)),
            new P("refl_bench",    "reflections", V(3.2f, 1.2f, 5.6f),     V(5.45f, 0.7f, 7.9f)),
            new P("ot_view",       "theatre",   V(7f, 1.7f, 5.2f),         V(11f, 1.2f, 5.4f)),
            new P("ot_low",        "theatre",   V(8.0f, 1.25f, 5.2f),      V(10.5f, 0.9f, 5.4f)),
            new P("bay_near",      "bay",       V(0f, 1.6f, -1.4f),        V(0f, 1.6f, -1.95f)),
            new P("bay_reception", "bay",       V(4.5f, 1.6f, -1f),        V(4.5f, 1.4f, -9f)),
        };

        [UnityTest, Timeout(3600000)]
        public IEnumerator Capture_b58_visual()
        {
            string work = Environment.GetEnvironmentVariable("PROCSENSE_WORK"); if (string.IsNullOrEmpty(work)) work = "D:/ProcSenseWork";
            string root = Environment.GetEnvironmentVariable("B58V_DIR"); if (string.IsNullOrEmpty(root)) root = Path.Combine(work, "reports/b58_visual");
            string cfgs = Environment.GetEnvironmentVariable("B58V_CONFIGS") ?? "B20,B58";
            var only = new HashSet<string>((Environment.GetEnvironmentVariable("B58V_POSES") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            Directory.CreateDirectory(root);
            var ids = cfgs.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
            foreach (var id in ids) Assert.IsTrue(PerfPresets.All.Any(p => p.Id == id.Split('@')[0]), "unknown preset " + id);

            int authored = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            var poses = new List<P>(Fixed); bool peopleAdded = false;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
            int shots = 0;
            Time.captureFramerate = 30;
            try
            {
                foreach (var id in ids)
                {
                    var preset = PerfPresets.All.First(p => p.Id == id.Split('@')[0]);
                    string outDir = Path.Combine(root, id.Replace('@', '_')); Directory.CreateDirectory(outDir);
                    Time.timeScale = 1f; UnityEngine.Random.InitState(20261110);
                    yield return SceneManager.LoadSceneAsync("Boot");
                    yield return EditorXRTestEnvironment.EnsureSimulator();
                    for (int i = 0; i < 120; i++) yield return null;
                    foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
                    var go = new GameObject("B58Camera"); var cam = go.AddComponent<Camera>();
                    cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 96f; cam.clearFlags = CameraClearFlags.Skybox; cam.aspect = (float)W / H;
                    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt;
                    go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None; cam.enabled = false;
                    var toggles = new PerfToggles();
                    SeedParticles();
                    for (int i = 0; i < 60; i++) yield return null;

                    if (!peopleAdded)
                    {
                        // People poses are derived once (from the first preset) and reused for every preset, so all presets see exactly the same camera positions.
                        var people = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                            .Where(a => a.isHuman && a.GetBoneTransform(HumanBodyBones.Head) != null && a.gameObject.activeInHierarchy).OrderBy(a => a.name, StringComparer.Ordinal).Take(6).ToList();
                        foreach (var a in people)
                        {
                            var head = a.GetBoneTransform(HumanBodyBones.Head).position; var fwd = a.transform.forward; fwd.y = 0f; fwd.Normalize();
                            string n = a.name.Replace(' ', '_');
                            poses.Add(new P("face_" + n, "people", head + fwd * 1.1f, head));
                            poses.Add(new P("body_" + n, "people", a.transform.position + fwd * 2.4f + Vector3.up * 1.4f, a.transform.position + Vector3.up * 0.95f));
                        }
                        peopleAdded = true;
                        var csv = new StringBuilder("id,category,x,y,z,look_x,look_y,look_z\n");
                        foreach (var p in poses) csv.AppendLine(string.Join(",", p.Id, p.Category, F(p.Pos.x), F(p.Pos.y), F(p.Pos.z), F(p.Look.x), F(p.Look.y), F(p.Look.z)));
                        File.WriteAllText(Path.Combine(root, "poses.csv"), csv.ToString());
                    }

                    string room = "";
                    foreach (var p in poses)
                    {
                        if (only.Count > 0 && !only.Contains(p.Id)) continue;
                        Time.timeScale = 1f;
                        cam.transform.SetPositionAndRotation(p.Pos, Quaternion.LookRotation((p.Look - p.Pos).normalized, Vector3.up));
                        string r = RoomAt(p.Pos); if (r != room) { room = r; toggles.Apply(preset, cam, room); }
                        for (int i = 0; i < 8; i++) { cam.Render(); yield return null; }   // the same eight frames before every shot: culling, light selection and animation settle identically
                        Time.timeScale = 0f; yield return null; yield return null;
                        cam.Render(); yield return null;
                        var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(outDir, p.Id + ".png"), tex.EncodeToPNG());
                        shots++;
                    }
                    Time.timeScale = 1f;
                    toggles.Apply(PerfPresets.All[0], cam, "PatientBay"); QuestExterior.ResetToDefault();
                    cam.targetTexture = null; rt.Release(); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(go);
                    yield return null;
                }
            }
            finally
            {
                Time.timeScale = 1f; Time.captureFramerate = 0; QualitySettings.SetQualityLevel(authored, true);
            }
            Assert.Greater(shots, 0, "no picture was produced");
        }

        private static string RoomAt(Vector3 p) => p.x < 2.5f ? "PatientBay" : (p.x > 6.5f && p.z > 4.2f && p.z < 6.3f ? "OT" : "Corridor");
        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        /// <summary>Gives every particle system (dust motes) a fixed random seed derived from its hierarchy path, then restarts it, so two launches draw the same motes.</summary>
        private static void SeedParticles()
        {
            foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                string path = ps.name; for (var t = ps.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
                uint h = 2166136261u; foreach (char ch in path) { h ^= ch; h *= 16777619u; }
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.useAutoRandomSeed = false; ps.randomSeed = h;
                ps.Play(true);
            }
        }
    }
}
