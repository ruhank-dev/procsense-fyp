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
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Desktop Unity render of the updated project for the overhaul walkthrough video (not Quest footage). Explicit, never a regression test.
    /// Camera moves come from a JSON plan (VID_PLAN) so shots can be re-captured alone (VID_SHOTS). Frames go to VID_DIR/&lt;shot&gt;/f00000.jpg at 1920x1080, 30 fps path rate.
    /// VID_RECON=1 writes VID_DIR/recon.txt (canvases, people, equipment positions) instead of shots. VID_PRESET defaults to B87 (Impostor exterior, the Android default).
    /// </summary>
    public class OverhaulVideoCapture
    {
        [Serializable] public class KeyJ { public float t; public float[] pos; public float[] look; public float fov; public float roll; }
        [Serializable] public class ShotJ { public string name; public string phase; public string doors; public float doorsOpenAt; public string canvas; public bool hideMenu; public KeyJ[] keys; }
        [Serializable] public class PlanJ { public ShotJ[] shots; }

        private static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);

        private static string Env(string k, string d = null) { var v = Environment.GetEnvironmentVariable(k); return string.IsNullOrEmpty(v) ? d : v; }

        private const float DoorOpenAngle = -100f, DoorSweepSpeed = 160f;
        private static float DoorAngleAt(float s) => s <= 0f ? 0f : Mathf.Max(DoorOpenAngle, -DoorSweepSpeed * s);
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

        private static string RoomAt(Vector3 p) => p.x < 2.5f ? "PatientBay" : (p.x > 6.5f && p.z > 4.2f && p.z < 6.3f ? "OT" : "Corridor");

        private static string Hier(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }

        [UnityTest, Explicit("overhaul video capture"), Timeout(14400000)]
        public IEnumerator Capture()
        {
            string dir = Env("VID_DIR"); Assert.IsFalse(string.IsNullOrEmpty(dir), "set VID_DIR");
            Directory.CreateDirectory(dir);
            bool recon = Env("VID_RECON") == "1";
            int W = int.Parse(Env("VID_W", "1920")), H = int.Parse(Env("VID_H", "1080")); float fps = 30f;
            var preset = PerfPresets.All.First(p => p.Id == Env("VID_PRESET", "B87"));
            var only = new HashSet<string>((Env("VID_SHOTS") ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            PlanJ plan = recon ? new PlanJ { shots = new ShotJ[0] } : JsonUtility.FromJson<PlanJ>(File.ReadAllText(Env("VID_PLAN")));
            int authored = QualitySettings.GetQualityLevel(); QualitySettings.SetQualityLevel(2, true);
            LogAssert.ignoreFailingMessages = true;
            Time.captureFramerate = 30;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
            var toggles = new PerfToggles();
            var log = new StringBuilder();
            try
            {
                foreach (var phase in new[] { "login", "world" })
                {
                    var shots = plan.shots.Where(s => (s.phase ?? "world") == phase && (only.Count == 0 || only.Contains(s.name))).ToList();
                    if (!recon && shots.Count == 0) continue;
                    TestHostHygiene.DestroyRoomLeftovers();
                    ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = phase == "login";
                    UnityEngine.Random.InitState(20261114);
                    yield return SceneManager.LoadSceneAsync("Boot");
                    if (phase == "world")
                    {
                        yield return EditorXRTestEnvironment.EnsureSimulator();
                        for (int i = 0; i < 150; i++) yield return null;
                    }
                    else for (int i = 0; i < 90; i++) yield return null;
                    foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) { log.AppendLine($"existing camera {Hier(c.transform)} pos {c.transform.position} rot {c.transform.eulerAngles} enabled {c.enabled}"); c.enabled = false; }
                    var go = new GameObject("VideoCamera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f; cam.fieldOfView = 70f; cam.clearFlags = CameraClearFlags.Skybox; cam.aspect = (float)W / H;
                    cam.targetTexture = rt; var ud = go.AddComponent<UniversalAdditionalCameraData>(); ud.antialiasing = AntialiasingMode.None; cam.enabled = false;
                    if (phase == "world") toggles.Apply(preset, cam, "PatientBay");
                    if (Env("VID_NOLM") == "1") { log.AppendLine("lightmaps: " + LightmapSettings.lightmaps.Length); LightmapSettings.lightmaps = new LightmapData[0]; }
                    if (Env("VID_NOPROBES") == "1") { var pr = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None); foreach (var rp in pr) rp.enabled = false; log.AppendLine("probes disabled: " + pr.Length + " probes: " + string.Join(";", pr.Select(x => Hier(x.transform) + " mode " + x.mode + " tex " + (x.bakedTexture != null ? x.bakedTexture.name : "null")))); }

                    if (recon)
                    {
                        log.AppendLine("=== phase " + phase);
                        foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            var rtf = c.GetComponent<RectTransform>();
                            log.AppendLine($"canvas {Hier(c.transform)} mode {c.renderMode} active {c.gameObject.activeInHierarchy} pos {c.transform.position} fwd {c.transform.forward} size {rtf.rect.size} scale {c.transform.lossyScale.x:0.#####}");
                        }
                        if (phase == "world")
                        {
                            var rx = new System.Text.RegularExpressions.Regex("patient|nurse|reception|monitor|tablet|debrief|curtain|window|bed|ventilator|trolley|ceiling|light|staff|civil|doctor|surgeon|theatre|table|chair|desk|sofa|plant|clock|poster|tv", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                                if (t.parent != null && t.parent.parent == null && rx.IsMatch(t.name) || (t.parent != null && t.parent.parent != null && t.parent.parent.parent == null && rx.IsMatch(t.name)))
                                    log.AppendLine($"obj {Hier(t)} pos {t.position:0.00}");
                        }
                        File.WriteAllText(Path.Combine(dir, "recon.txt"), log.ToString());
                        for (int k = 0; k < 2; k++) { cam.transform.SetPositionAndRotation(k == 0 ? new Vector3(1.8f, 1.6f, -1.5f) : new Vector3(4.5f, 1.6f, -1f), Quaternion.LookRotation(k == 0 ? new Vector3(-1.8f, -0.5f, 2.5f) : new Vector3(0, -0.1f, -1f))); for (int i = 0; i < 3; i++) { cam.Render(); yield return null; } SaveJpg(rt, tex, W, H, Path.Combine(dir, $"recon_{phase}_{k}.jpg")); }
                    }

                    if (Env("VID_MATS") == "1")
                    {
                        var seen = new HashSet<string>(); var sb = new StringBuilder();
                        foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                            foreach (var m in r.sharedMaterials)
                            {
                                if (m == null || m.shader == null) { sb.AppendLine("NULLMAT " + Hier(r.transform)); continue; }
                                string key = m.name + "|" + m.shader.name; if (!seen.Add(key)) continue;
                                string tn = m.HasProperty("_BaseMap") ? (m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : "NO_BASEMAP") : (m.HasProperty("_MainTex") ? (m.mainTexture != null ? m.mainTexture.name : "NO_MAINTEX") : "-");
                                sb.AppendLine($"{key} supported={m.shader.isSupported} tex={tn} at {Hier(r.transform)}");
                            }
                        File.WriteAllText(Path.Combine(dir, "materials.txt"), sb.ToString());
                    }
                    foreach (var shot in shots)
                    {
                        string outDir = Path.Combine(dir, shot.name); if (Directory.Exists(outDir)) Directory.Delete(outDir, true); Directory.CreateDirectory(outDir);
                        var keys = shot.keys; float total = keys[keys.Length - 1].t; int frames = Mathf.CeilToInt(total * fps);
                        Canvas movedCanvas = null; CanvasSave saved = default;
                        if (!string.IsNullOrEmpty(shot.canvas))
                        {
                            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (c.isRootCanvas && Hier(c.transform).Contains(shot.canvas)) { movedCanvas = c; break; }
                            if (movedCanvas != null && movedCanvas.renderMode == RenderMode.ScreenSpaceOverlay) { saved = new CanvasSave(movedCanvas); movedCanvas.renderMode = RenderMode.ScreenSpaceCamera; movedCanvas.worldCamera = cam; movedCanvas.planeDistance = 1f; }
                            else if (movedCanvas != null && movedCanvas.renderMode == RenderMode.ScreenSpaceCamera) movedCanvas.worldCamera = cam;
                            log.AppendLine($"shot {shot.name}: canvas {(movedCanvas != null ? Hier(movedCanvas.transform) + " " + movedCanvas.renderMode : "NOT FOUND")}");
                        }
                        var gf = GameObject.Find("Game flow panel"); if (gf != null) gf.SetActive(!shot.hideMenu);
                        for (int i = 0; i < 20; i++) yield return null;
                        var doorList = (shot.doors ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int f = 0; f < frames; f++)
                        {
                            float t = f / fps;
                            int ki = 0; while (ki < keys.Length - 2 && keys[ki + 1].t <= t) ki++;
                            var a = keys[ki]; var b = keys[ki + 1]; float u = Mathf.Clamp01((t - a.t) / Mathf.Max(0.001f, b.t - a.t));
                            float s = Mathf.SmoothStep(0, 1, u);
                            // ease only at the shot ends so the move reads as one continuous glide through interior keys
                            float e = (keys.Length == 2) ? s : u;
                            Vector3 pos = Vector3.Lerp(V(a.pos), V(b.pos), e), look = Vector3.Lerp(V(a.look), V(b.look), e);
                            float fov = Mathf.Lerp(a.fov > 0 ? a.fov : 70f, b.fov > 0 ? b.fov : 70f, e);
                            if (shot.canvas != null && movedCanvas != null && movedCanvas.renderMode == RenderMode.ScreenSpaceCamera) { /* camera stays at plan pose; overlay canvas follows the camera */ }
                            foreach (var d in doorList) SetDoorAngle(d, DoorAngleAt(t - shot.doorsOpenAt));
                            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((look - pos).normalized, Vector3.up) * Quaternion.Euler(0, 0, Mathf.Lerp(a.roll, b.roll, e)));
                            cam.fieldOfView = fov;
                            if (phase == "world") { string room = RoomAt(pos); toggles.Apply(preset, cam, room); }
                            cam.Render(); yield return null; cam.Render();
                            SaveJpg(rt, tex, W, H, Path.Combine(outDir, "f" + f.ToString("00000") + ".jpg"));
                        }
                        if (movedCanvas != null && saved.set) saved.Restore(movedCanvas);
                        log.AppendLine($"shot {shot.name}: {frames} frames");
                    }
                    cam.targetTexture = null; UnityEngine.Object.Destroy(go);
                }
            }
            finally
            {
                File.AppendAllText(Path.Combine(dir, "capture.log"), log.ToString());
                Time.captureFramerate = 0;
                UnityEngine.Object.Destroy(rt);
                QualitySettings.SetQualityLevel(authored, true);
                ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            }
        }

        private struct CanvasSave
        {
            public bool set; private RenderMode m; private Camera c; private float d;
            public CanvasSave(Canvas cv) { set = true; m = cv.renderMode; c = cv.worldCamera; d = cv.planeDistance; }
            public void Restore(Canvas cv) { cv.renderMode = m; cv.worldCamera = c; cv.planeDistance = d; }
        }

        private static void SaveJpg(RenderTexture rt, Texture2D tex, int W, int H, string path)
        {
            var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToJPG(95));
        }
    }
}
#endif
