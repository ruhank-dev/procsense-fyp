using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Unity close-up renders of the PatientBay patient monitor from the same viewpoints as the Blender studio set (front, side, rear, three-quarter, bezel, keys, bay, stand).</summary>
    public static class MonitorHeroReview
    {
        const int W = 1600, H = 1200;
        static string Out
        {
            get
            {
                var a = System.Environment.GetCommandLineArgs(); var i = System.Array.IndexOf(a, "-dir");
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MonitorBuild", i >= 0 ? a[i + 1] : a.Contains("-after") ? "unity_after" : "unity_before"));
            }
        }

        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var screen = all.First(t => t.name == "SCREEN_Main");
            var ecg = all.First(t => t.name == "ATTACH_ECG");
            var root = screen.root;
            var monitor = screen.GetComponentsInParent<Transform>().First(t => t.GetComponent<Rigidbody>() != null || t.name.StartsWith("vital_signs_monitor"));
            var body = monitor.GetComponentsInChildren<MeshRenderer>().First(r => r.name.EndsWith("_Body")).bounds.center;
            var front = Vector3.ProjectOnPlane(screen.position - body, Vector3.up).normalized;
            var right = Vector3.Cross(Vector3.up, -front).normalized;                          // as seen by a viewer facing the screen
            var left = -right;
            Debug.Log($"[MonitorHero] body {body:F3} front {front:F3} left-bay side {(ecg.position - body).normalized:F3} (dot with left {Vector3.Dot((ecg.position - body).normalized, left):F2})");
            if (Vector3.Dot((ecg.position - body).normalized, left) < 0) { right = -right; left = -left; }
            Vector3 P(float f, float l, float up) => body + front * f + left * l + Vector3.up * (up - 1.29f);
            // (name, camera position, target, fov) mirroring render_monitor.py: 85 mm on a 36 mm sensor, 4:3 -> vertical fov ~ 18 deg
            var views = new (string n, Vector3 pos, Vector3 look, float fov)[]
            {
                ("front", P(1.6f, 0f, 1.30f), P(0, 0, 1.29f), 18.06f),
                ("side", P(.1f, 1.6f, 1.29f), P(0, 0, 1.29f), 18.06f),
                ("rear", P(-1.5f, -.5f, 1.45f), P(-.1f, 0, 1.29f), 18.06f),
                ("threequarter", P(1.25f, .95f, 1.55f), P(0, 0, 1.30f), 18.06f),
                ("bezel_closeup", P(.55f, .12f, 1.34f), P(.09f, .01f, 1.26f), 18.06f),
                ("bay_closeup", P(.30f, .55f, 1.40f), P(.02f, .165f, 1.28f), 18.06f),
                ("keys_closeup", P(.40f, -.08f, 1.28f), P(.095f, 0f, 1.18f), 18.06f),
                ("pole_closeup", P(.55f, .55f, .75f), P(-.045f, 0, .70f), 18.06f),
                ("glass_closeup", P(.50f, .50f, 1.50f), P(.10f, .008f, 1.316f), 18.06f),
                ("housing_closeup", P(.45f, .45f, 1.55f), P(.05f, .16f, 1.40f), 18.06f),
                ("stand", P(3.0f, 1.8f, 1.35f), P(0, 0, .75f), 25.36f),
            };
            for (var i = 0; i < 3; i++) Render(null, views[0].pos, views[0].look, views[0].fov);
            foreach (var v in views)
            {
                // review-only: anything standing between the camera and the monitor (walls, cabinets, curtains) is switched off for that frame
                var hidden = new System.Collections.Generic.List<Renderer>();
                if (v.n == "rear" || v.n == "stand" || v.n == "pole_closeup")
                {
                    var dir = v.look - v.pos; var ray = new Ray(v.pos, dir.normalized); var len = dir.magnitude;
                    foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                    {
                        if (!r.enabled || r.transform.IsChildOf(monitor)) continue;
                        var b = r.bounds;
                        if (b.Contains(v.pos) || (b.IntersectRay(ray, out var d) && d < len) || (r.bounds.size.y > 2.2f && Vector3.Distance(b.center, body) < 3f && !(r is SkinnedMeshRenderer) && b.size.x > 2f)) { r.enabled = false; hidden.Add(r); }
                    }
                }
                Render(Path.Combine(Out, v.n + ".png"), v.pos, v.look, v.fov);
                foreach (var r in hidden) r.enabled = true;
            }
        }

        static void Render(string file, Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("MonitorReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.fieldOfView = fov; cam.nearClipPlane = .02f; cam.farClipPlane = 40f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.12f, .12f, .14f);
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render(); cam.Render();
            if (file != null)
            {
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
                File.WriteAllBytes(file, tex.EncodeToPNG());
                RenderTexture.active = prev; Object.DestroyImmediate(tex);
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
        }
    }
}
