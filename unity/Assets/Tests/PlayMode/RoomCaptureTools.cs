#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Capture tool, not a behaviour test (Explicit). Renders any room from views listed in a JSON file so viewpoints can change without recompiling.
    /// Environment: ROOM_SCENE (Corridor, OT, PatientBay, Recovery), ROOM_VIEWS (path to JSON: {"views":[{"n":"name","p":[x,y,z],"l":[x,y,z],"f":60}]}), ROOM_CAPTURE_DIR (output).
    /// </summary>
    public class RoomCaptureTools
    {
        [System.Serializable] public class View { public string n; public float[] p; public float[] l; public float f = 60f; public float open = -1f; }
        [System.Serializable] public class Views { public View[] views; }

        [UnityTest, Explicit("capture tool")]
        public IEnumerator Capture_room_views()
        {
            string scene = System.Environment.GetEnvironmentVariable("ROOM_SCENE"), json = System.Environment.GetEnvironmentVariable("ROOM_VIEWS"), dir = System.Environment.GetEnvironmentVariable("ROOM_CAPTURE_DIR");
            Assert.IsFalse(string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(json) || string.IsNullOrEmpty(dir), "set ROOM_SCENE, ROOM_VIEWS and ROOM_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            var views = JsonUtility.FromJson<Views>(File.ReadAllText(json)).views;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync(scene);
            for (int i = 0; i < 40; i++) yield return null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            int w = int.TryParse(System.Environment.GetEnvironmentVariable("ROOM_W"), out var ww) ? ww : 1600, h = w * 9 / 16;
            // diagnostics: ROOM_EXP="nosky,nosun,nolights,nolightmaps" renders the first view once per variant (each switched off cumulatively off the baseline)
            var exp = (System.Environment.GetEnvironmentVariable("ROOM_EXP") ?? "").Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (exp.Length > 0)
            {
                var v0 = views[0]; var all = new List<string> { "base" }; all.AddRange(exp);
                foreach (var name in all)
                {
                    if (name == "nosky") { RenderSettings.skybox = null; RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = null; }
                    if (name == "nosun") foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name == "City sun")) l.enabled = false;
                    if (name == "nolights") foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.type == LightType.Directional)) l.enabled = false;
                    if (name == "nolightmaps") LightmapSettings.lightmaps = new LightmapData[0];
                    if (name == "nobaysun") foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.name == "Sun through window")) l.enabled = false;
                    if (name == "noshadows") QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                    if (name == "listlights") foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Debug.Log("LIGHTDUMP " + l.gameObject.scene.name + " / " + l.name + " type=" + l.type + " mode=" + l.lightmapBakeType + " shadows=" + l.shadows + " enabled=" + l.enabled + " int=" + l.intensity);
                    var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>();
                    cam.transform.position = new Vector3(v0.p[0], v0.p[1], v0.p[2]); cam.transform.LookAt(new Vector3(v0.l[0], v0.l[1], v0.l[2])); cam.fieldOfView = v0.f; cam.nearClipPlane = .02f; cam.farClipPlane = 6000f; cam.clearFlags = CameraClearFlags.Skybox;
                    var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); cam.targetTexture = rt; go.AddComponent<UniversalAdditionalCameraData>();
                    for (int k = 0; k < 10; k++) yield return null;
                    cam.Render(); yield return null; cam.Render();
                    var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(dir, "exp_" + name + ".png"), tex.EncodeToPNG()); RenderTexture.active = prev; cam.targetTexture = null; Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go);
                }
                yield break;
            }
            foreach (var v in views)
            {
                if (v.open >= 0) foreach (var cc in Object.FindObjectsByType<ProcSense.VR.Views.CurtainCloth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(c => c.UsesPath)) cc.Open = v.open;
                if (v.n.Contains("top_down"))                                                                                   // plan view: the ceiling and everything hung from it are lifted off
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.bounds.min.y > 2.6f && r.bounds.max.y < 4.6f && Mathf.Abs(r.bounds.center.x - 4.5f) < 60f && r.bounds.center.y > -5f)) r.enabled = false;
                if (v.n.Contains("top_down")) foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(r => r.transform.parent != null && r.transform.parent.name == "Layer A near towers")) r.enabled = false;   // the hospital's own roof shell
                var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>();
                cam.transform.position = new Vector3(v.p[0], v.p[1], v.p[2]); cam.transform.LookAt(new Vector3(v.l[0], v.l[1], v.l[2])); cam.fieldOfView = v.f; cam.nearClipPlane = .02f; cam.farClipPlane = 6000f;
                cam.clearFlags = CameraClearFlags.Skybox; cam.backgroundColor = new Color(.12f, .12f, .14f);
                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 }; cam.targetTexture = rt;
                var cd = go.AddComponent<UniversalAdditionalCameraData>(); cd.antialiasing = AntialiasingMode.None;
                for (int k = 0; k < 30; k++) yield return null;
                cam.Render(); yield return null; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, v.n + ".png"), tex.EncodeToPNG());
                RenderTexture.active = prev; cam.targetTexture = null; Object.Destroy(tex); Object.Destroy(rt); Object.Destroy(go);
            }
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }

        /// <summary>
        /// Video frames: ROOM_VIEWS holds exactly two views (start and end of a slow camera move); ROOM_FRAMES frames (default 72) are written as f0000.png ... at a fixed game time step of 1/12 s, so particles and slow noise animate as in play.
        /// Join them with ffmpeg: ffmpeg -framerate 12 -i f%04d.png out.mp4.
        /// </summary>
        [UnityTest, Explicit("capture tool")]
        public IEnumerator Capture_room_video()
        {
            string scene = System.Environment.GetEnvironmentVariable("ROOM_SCENE"), json = System.Environment.GetEnvironmentVariable("ROOM_VIEWS"), dir = System.Environment.GetEnvironmentVariable("ROOM_CAPTURE_DIR");
            Assert.IsFalse(string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(json) || string.IsNullOrEmpty(dir), "set ROOM_SCENE, ROOM_VIEWS and ROOM_CAPTURE_DIR");
            Directory.CreateDirectory(dir); var views = JsonUtility.FromJson<Views>(File.ReadAllText(json)).views; Assert.AreEqual(2, views.Length, "two views: start and end");
            int frames = int.TryParse(System.Environment.GetEnvironmentVariable("ROOM_FRAMES"), out var ff) ? ff : 72;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync(scene);
            for (int i = 0; i < 40; i++) yield return null;
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) c.enabled = false;
            int w = int.TryParse(System.Environment.GetEnvironmentVariable("ROOM_W"), out var ww) ? ww : 1280, h = w * 9 / 16;
            var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = .02f; cam.farClipPlane = 6000f; cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 }; cam.targetTexture = rt; go.AddComponent<UniversalAdditionalCameraData>().antialiasing = AntialiasingMode.None;
            Time.captureDeltaTime = 1f / 12f;
            cam.transform.position = new Vector3(views[0].p[0], views[0].p[1], views[0].p[2]); cam.transform.LookAt(new Vector3(views[0].l[0], views[0].l[1], views[0].l[2])); cam.fieldOfView = views[0].f;
            for (int i = 0; i < 36; i++) { yield return null; cam.Render(); }                                             // the first ~2 s of a fresh capture camera show a start-up transient (a one-step dip in overall brightness near 1.75 s); settle it before writing frames
            for (int f = 0; f < frames; f++)
            {
                float k = Mathf.SmoothStep(0, 1, f / (float)(frames - 1)); Vector3 P(float[] a) => new Vector3(a[0], a[1], a[2]);
                cam.transform.position = Vector3.Lerp(P(views[0].p), P(views[1].p), k); cam.transform.LookAt(Vector3.Lerp(P(views[0].l), P(views[1].l), k)); cam.fieldOfView = Mathf.Lerp(views[0].f, views[1].f, k);
                yield return null; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, "f" + f.ToString("D4") + ".png"), tex.EncodeToPNG()); RenderTexture.active = prev; Object.Destroy(tex);
            }
            Time.captureDeltaTime = 0f; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(go);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        }
    }
}
#endif
