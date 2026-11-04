#if UNITY_EDITOR
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
    /// Capture tool, not a behaviour test (Explicit: never part of a normal run). Renders the PatientBay floor from fixed cameras in a real play
    /// session. Environment: FLOOR_CAPTURE_DIR (output), FLOOR_VARIANT = wood | vinyl | original (swaps only the Floor renderer's material for the run).
    /// </summary>
    public class FloorCaptureTools
    {
        const int W = 1600, H = 900;

        [UnityTest, Explicit("capture tool")]
        public IEnumerator Capture_floor_views()
        {
            var dir = System.Environment.GetEnvironmentVariable("FLOOR_CAPTURE_DIR");
            Assert.IsNotNull(dir, "set FLOOR_CAPTURE_DIR");
            Directory.CreateDirectory(dir);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("PatientBay");
            for (var i = 0; i < 12; i++) yield return null;
            var variant = System.Environment.GetEnvironmentVariable("FLOOR_VARIANT") ?? "wood";
            var floor = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).First(r => r.name == "Floor");
#if UNITY_EDITOR
            if (variant == "vinyl") floor.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay vinyl floor.mat");
            if (variant == "original") floor.sharedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/ClinicalDetails/Bay muted wood flooring.mat");
#endif
            Debug.Log($"[FloorCapture] variant {variant}, floor material {floor.sharedMaterial.name}");
            for (var i = 0; i < 6; i++) yield return null;

            var bed = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).First(t => t.name == "hospital_bed");
            var b = bed.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            var monitorBase = new Vector3(1.1f, 0, 1.6f);
            Debug.Log($"[FloorCapture] bed bounds {b.min:F2}..{b.max:F2}");
            var views = new (string n, Vector3 pos, Vector3 look, float fov)[]
            {
                ("doorway", new Vector3(2.38f, 1.62f, -1.84f), new Vector3(-0.62f, 0.95f, 1.15f), 80f),
                ("closeup", new Vector3(b.center.x + 1.0f, .85f, b.min.z - .55f), new Vector3(b.center.x - .1f, 0f, b.min.z - 1.25f), 55f),
                ("low_reflection", new Vector3(1.95f, .32f, -1.6f), new Vector3(-1.2f, .0f, 1.0f), 62f),
                ("under_bed", new Vector3(b.center.x, .14f, b.min.z - .55f), new Vector3(b.center.x, .11f, b.center.z), 72f),
                ("monitor_stand", new Vector3(2.05f, .75f, .55f), monitorBase + new Vector3(0, .06f, 0), 50f),
                ("overview", new Vector3(2.3f, 2.2f, -1.7f), new Vector3(-.3f, 0f, .4f), 70f),
            };
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) c.enabled = false;
            foreach (var v in views)
            {
                var go = new GameObject("CaptureCamera"); var cam = go.AddComponent<Camera>();
                cam.transform.position = v.pos; cam.transform.LookAt(v.look); cam.fieldOfView = v.fov; cam.nearClipPlane = .02f; cam.farClipPlane = 40f;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .12f, .14f);
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
                cam.targetTexture = rt;
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
