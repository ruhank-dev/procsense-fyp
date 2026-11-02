#if UNITY_EDITOR
using System.Collections;
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
    /// Capture tool, not a behaviour test (Explicit). Renders PatientBay from the reference viewpoint and a fixed set of close-ups in a real play session, shipping pipeline,
    /// nothing swapped. Environment: LIGHT_CAPTURE_DIR (output). LIGHT_FLOOR=wood|vinyl is NOT honoured here: the lighting task never changes the floor.
    /// </summary>
    public class LightingCaptureTools
    {
        const int W = 1600, H = 900;
        static RenderPipelineAsset _previousPipeline;

        [TearDown]
        public void RestorePipeline() { if (_previousPipeline != null) { QualitySettings.renderPipeline = _previousPipeline; _previousPipeline = null; } }

        [UnityTest, Explicit("capture tool")]
        public IEnumerator Capture_lighting_views()
        {
            var dir = System.Environment.GetEnvironmentVariable("LIGHT_CAPTURE_DIR");
            Assert.IsNotNull(dir, "set LIGHT_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("PatientBay");
            for (var i = 0; i < 14; i++) yield return null;
            var fx = System.Environment.GetEnvironmentVariable("LIGHT_POSTFX") ?? "off";
            if (fx != "off")
            {
                // test-only pipeline with HDR + post-processing (the shipping Quest asset has neither); restored at the end
                _previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PostFxTest/URP_PostFxTest.asset");
                Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Settings/PostFxTest/Vol_{fx}.prefab"));
                for (var i = 0; i < 4; i++) yield return null;
            }

            Bounds Of(string name)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(x => x.name == name);
                var rs = t == null ? new Renderer[0] : t.GetComponentsInChildren<Renderer>();
                return rs.Length == 0 ? new Bounds(Vector3.zero, Vector3.one) : rs.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            }
            var win = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude).Where(r => r.name.ToLowerInvariant().Contains("window") && !r.name.ToLowerInvariant().Contains("exterior") && r.bounds.size.y > .5f).Select(r => r.bounds).DefaultIfEmpty(new Bounds(new Vector3(-1f, 1.5f, 1.95f), Vector3.one)).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            var bed = Of("hospital_bed"); var mon = new Vector3(1.1f, 0, 1.6f);
            Debug.Log($"[LightingCapture] window bounds {win.min:F2}..{win.max:F2}; bed {bed.min:F2}..{bed.max:F2}");
            var views = new (string n, Vector3 pos, Vector3 look, float fov)[]
            {
                ("reference", new Vector3(2.38f, 1.62f, -1.84f), new Vector3(-0.62f, 0.95f, 1.15f), 80f),
                ("floor_sun", new Vector3(1.9f, 1.05f, -1.35f), new Vector3(-.4f, 0f, .3f), 62f),
                ("window_exterior", new Vector3(1.2f, 1.55f, -1.4f), win.center, 48f),
                ("sun_wall_shadows", new Vector3(2.0f, 1.45f, 1.2f), new Vector3(-1.2f, 1.0f, -1.9f), 70f),
                ("metal_monitor_stand", new Vector3(1.95f, 1.15f, .55f), mon + new Vector3(0, .85f, 0), 42f),
                ("metal_bed_rail", new Vector3(1.05f, 1.0f, -.75f), new Vector3(bed.center.x, .72f, bed.min.z + .5f), 50f),
                ("shadows_equipment", new Vector3(2.05f, .75f, .55f), mon + new Vector3(0, .06f, 0), 52f),
                ("indirect_corner", new Vector3(1.9f, 1.5f, -1.6f), new Vector3(-2.3f, 1.2f, 1.8f), 62f),
                ("overview", new Vector3(2.3f, 2.2f, -1.7f), new Vector3(-.3f, 0f, .4f), 70f),
                ("patient_face", new Vector3(.62f, 1.18f, 1.28f), new Vector3(0f, .9f, 1.72f), 34f),
                ("patient_torso", new Vector3(.95f, 1.45f, .85f), new Vector3(0f, .85f, 1.2f), 46f),
                ("patient_legs", new Vector3(.95f, .95f, -.35f), new Vector3(0f, .72f, .55f), 52f),
                ("patient_hands", new Vector3(-.62f, 1.05f, .55f), new Vector3(.2f, .74f, .95f), 44f),
                ("patient_bed_contact", new Vector3(1.0f, .62f, .25f), new Vector3(0f, .62f, .85f), 50f),
                ("room_sofa_window", new Vector3(1.3f, 1.5f, -1.2f), new Vector3(-2.2f, 1.0f, .1f), 70f),
                ("room_prints", new Vector3(0f, 1.5f, -.5f), new Vector3(0f, 2.1f, 1.9f), 62f),
                ("room_wall_tv", new Vector3(-.3f, 1.4f, 1.2f), new Vector3(0f, 1.7f, -1.95f), 50f),
                ("bay_doorway", new Vector3(2.36f, 1.60f, -.85f), new Vector3(-.6f, 1.0f, .9f), 78f),
                ("bay_tv_from_bed", new Vector3(0f, 1.05f, 1.35f), new Vector3(0f, 1.7f, -1.95f), 52f),
                ("bay_tv_wall", new Vector3(.2f, 1.45f, .2f), new Vector3(0f, 1.45f, -1.95f), 74f),
                ("bay_sink_access", new Vector3(-.35f, 1.55f, -.25f), new Vector3(-1.45f, .95f, -1.8f), 62f),
                ("bay_sofa", new Vector3(-.55f, 1.45f, -1.15f), new Vector3(-2.1f, .6f, .45f), 66f),
                ("bay_floor", new Vector3(2.2f, 1.9f, -1.5f), new Vector3(.4f, 0f, .1f), 78f),
                ("bay_rail_open", new Vector3(2.1f, 1.65f, -1.5f), new Vector3(.1f, 2.45f, .5f), 74f),
                ("gown_front", new Vector3(0f, 1.15f, -.55f), new Vector3(0f, .85f, .85f), 40f),
                ("gown_front_low", new Vector3(.05f, .88f, -.45f), new Vector3(0f, .80f, .75f), 44f),
                ("gown_top", new Vector3(0f, 2.1f, .55f), new Vector3(0f, .75f, .75f), 44f),
                ("gown_side_left", new Vector3(-.95f, .95f, .75f), new Vector3(0f, .78f, .75f), 46f),
                ("closed_privacy", new Vector3(2.15f, 1.75f, -1.55f), new Vector3(0f, 1.0f, .9f), 74f),
                ("closed_privacy_top", new Vector3(.1f, 2.65f, -1.7f), new Vector3(.1f, 1.7f, .6f), 82f),
            };
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) c.enabled = false;
            foreach (var v in views)
            {
                foreach (var curtain in Object.FindObjectsByType<ProcSense.VR.Views.CurtainCloth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.UsesPath)) curtain.Open = v.n.StartsWith("closed_") ? 0f : 1f;   // privacy curtain: open by default, drawn shut for the closed_ views
                for (int settle = 0; settle < 150; settle++) yield return null;
                var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>();
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov; cam.nearClipPlane = .02f; cam.farClipPlane = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .12f, .14f);
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                cam.targetTexture = rt;
                var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = fx != "off"; cd.antialiasing = AntialiasingMode.None;
                yield return null; cam.Render(); yield return null; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, v.n + ".png"), tex.EncodeToPNG());
                RenderTexture.active = prev; cam.targetTexture = null;
                Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go);
            }
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }
    }
}
#endif
