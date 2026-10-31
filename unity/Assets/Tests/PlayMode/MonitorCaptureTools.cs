using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Capture tool, not a behaviour test (Explicit: never part of a normal run). Renders the PatientBay monitor from the matched cameras in a
    /// real play session, where reflection probes are registered and updated like on a device. Environment variables:
    /// MONITOR_CAPTURE_DIR (output), MONITOR_LOCAL_PROBE (0 turns the monitor's local probe off).
    /// </summary>
    public class MonitorCaptureTools
    {
        const int W = 1600, H = 1200;
        static RenderPipelineAsset _previousPipeline;

        [TearDown]
        public void RestorePipeline() { if (_previousPipeline != null) { QualitySettings.renderPipeline = _previousPipeline; _previousPipeline = null; } }

        [UnityTest, Explicit("capture tool")]
        public IEnumerator Capture_monitor_views()
        {
            var dir = System.Environment.GetEnvironmentVariable("MONITOR_CAPTURE_DIR");
            Assert.IsNotNull(dir, "set MONITOR_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            yield return SceneManager.LoadSceneAsync("PatientBay");
            for (var i = 0; i < 10; i++) yield return null;
            var fx = System.Environment.GetEnvironmentVariable("MONITOR_POSTFX") ?? "off";
#if UNITY_EDITOR
            if (fx != "off")
            {
                // test-only pipeline with post-processing enabled (URP_Quest has none); restored at the end of the test
                _previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PostFxTest/URP_PostFxTest.asset");
                Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Settings/PostFxTest/Vol_{fx}.prefab"));
                for (var i = 0; i < 4; i++) yield return null;
            }
#endif
            var local = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).FirstOrDefault(p => p.name == "Monitor local reflections");
            if (System.Environment.GetEnvironmentVariable("MONITOR_LOCAL_PROBE") == "0" && local != null) local.enabled = false;
            Debug.Log($"[Capture] probes: {string.Join(", ", Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include).Select(p => p.name + ":" + p.enabled))}");
            for (var i = 0; i < 6; i++) yield return null;

            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var screen = all.First(t => t.name == "SCREEN_Main");
            var ecg = all.First(t => t.name == "ATTACH_ECG");
            var monitor = screen.GetComponentsInParent<Transform>().First(t => t.GetComponent<Rigidbody>() != null || t.name.StartsWith("vital_signs_monitor"));
            var body = monitor.GetComponentsInChildren<MeshRenderer>().First(r => r.name.EndsWith("_Body")).bounds.center;
            var front = Vector3.ProjectOnPlane(screen.position - body, Vector3.up).normalized;
            var left = Vector3.Cross(Vector3.up, front).normalized;
            if (Vector3.Dot((ecg.position - body).normalized, left) < 0) left = -left;
            Vector3 P(float f, float l, float up) => body + front * f + left * l + Vector3.up * (up - 1.29f);
            const float Fov = 18.06f;
            var views = new (string n, Vector3 pos, Vector3 look, float fov, bool clear)[]
            {
                ("front", P(1.6f, 0f, 1.30f), P(0, 0, 1.29f), Fov, false),
                ("threequarter", P(1.25f, .95f, 1.55f), P(0, 0, 1.30f), Fov, false),
                ("bezel_closeup", P(.55f, .12f, 1.34f), P(.09f, .01f, 1.26f), Fov, false),
                ("keys_closeup", P(.40f, -.08f, 1.28f), P(.095f, 0f, 1.18f), Fov, false),
                ("glass_closeup", P(.50f, .50f, 1.50f), P(.10f, .008f, 1.316f), Fov, false),
                ("housing_closeup", P(.45f, .45f, 1.55f), P(.05f, .16f, 1.40f), Fov, false),
                ("pole_closeup", P(.55f, .55f, .75f), P(-.045f, 0, .70f), Fov, true),
                ("stand", P(3.0f, 1.8f, 1.35f), P(0, 0, .75f), 25.36f, true),
                ("base_closeup", P(1.25f, .85f, .55f), P(-.045f, 0, .09f), 28f, true),
                ("room_medium", P(2.6f, 1.4f, 1.55f), P(0, .4f, 1.0f), 50f, false),
                ("room_reference", new Vector3(2.38f, 1.62f, -1.84f), new Vector3(-0.62f, 0.95f, 1.15f), 80f, false),
            };
            // the live cameras of the scene would render too; disable them so only the capture camera draws
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) c.enabled = false;
            foreach (var v in views)
            {
                var hidden = new List<Renderer>(); var hiddenCanvases = new List<Canvas>();
                if (v.clear)
                {
                    var d = v.look - v.pos; var ray = new Ray(v.pos, d.normalized);
                    foreach (var cv in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
                        if (!cv.transform.IsChildOf(monitor) && cv.enabled && cv.renderMode == RenderMode.WorldSpace) { cv.enabled = false; hiddenCanvases.Add(cv); }
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    {
                        if (!r.enabled || r.transform.IsChildOf(monitor)) continue;
                        var b = r.bounds;
                        if (b.Contains(v.pos) || (b.IntersectRay(ray, out var dist) && dist < d.magnitude)) { r.enabled = false; hidden.Add(r); }
                    }
                }
                var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>();
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov; cam.nearClipPlane = .02f; cam.farClipPlane = 40f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .12f, .14f);
                int w = v.n == "room_reference" ? 1672 : W, h = v.n == "room_reference" ? 941 : H;
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                cam.targetTexture = rt;
                var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = fx != "off"; cd.antialiasing = AntialiasingMode.None;
                yield return null; cam.Render(); yield return null; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, v.n + ".png"), tex.EncodeToPNG());
                RenderTexture.active = prev; cam.targetTexture = null;
                Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go);
                foreach (var r in hidden) r.enabled = true;
                foreach (var cv in hiddenCanvases) cv.enabled = true;
            }
        }
    }
}
