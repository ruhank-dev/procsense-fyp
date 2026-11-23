using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool: a top-down picture of the patient bay with markers (doctor red, bed blue, charting counter green, the player's camera yellow) and the positions in the log, so "where is the doctor from where I start" has an answer.
    /// Run (graphics, Unity closed): -executeMethod ProcSense.EditorTools.BayMap.Run -> Logs/bay_map.png. The scene is not saved.
    /// </summary>
    public static class BayMap
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform Find(string n) => all.FirstOrDefault(t => t.name == n);
            var doc = Find("Doctor at the bay"); var counter = Find("charting_counter"); var bed = Find("Bed");
            var rig = all.FirstOrDefault(t => t.name.Contains("XR Origin") || t.name.Contains("XR Rig")); var cam = UnityEngine.Camera.main;
            foreach (var t in new[] { doc, counter, bed, rig }) if (t != null) Debug.Log($"[BayMap] {t.name} at {t.position:F2} active={t.gameObject.activeInHierarchy}");
            Debug.Log($"[BayMap] main camera {(cam != null ? cam.transform.position.ToString("F2") : "none")}");
            if (doc != null) foreach (var r in doc.GetComponentsInChildren<Renderer>(true)) Debug.Log($"[BayMap] doctor renderer {r.name} enabled={r.enabled} bounds {r.bounds.center:F2}");

            var renderers = all.Select(t => t.GetComponent<Renderer>()).Where(r => r != null && r.enabled && r.bounds.size.magnitude < 40f).ToList();
            var b = renderers.Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            Debug.Log($"[BayMap] room bounds {b.min:F2} to {b.max:F2}");
            void Marker(Vector3 p, Color c, float size) { var m = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(m.GetComponent<Collider>()); m.transform.position = new Vector3(p.x, 2.4f, p.z); m.transform.localScale = Vector3.one * size; var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")); mat.color = c; m.GetComponent<Renderer>().sharedMaterial = mat; }
            if (doc != null) Marker(doc.position, Color.red, .5f);
            if (counter != null) Marker(counter.position, Color.green, .4f);
            if (bed != null) Marker(bed.GetComponentsInChildren<Renderer>().Select(r => r.bounds.center).Aggregate((x, y) => (x + y) / 2), Color.blue, .5f);
            if (cam != null) Marker(cam.transform.position, Color.yellow, .35f);

            var go = new GameObject("map camera"); var c2 = go.AddComponent<Camera>(); c2.orthographic = true; c2.clearFlags = CameraClearFlags.SolidColor; c2.backgroundColor = new Color(.1f, .1f, .12f);
            c2.transform.position = new Vector3(b.center.x, 8f, b.center.z); c2.transform.rotation = Quaternion.Euler(90, 0, 0); c2.nearClipPlane = 5.2f; c2.farClipPlane = 12f;   // starts below 2.8 m: the ceiling is left out
            c2.orthographicSize = Mathf.Max(b.extents.x, b.extents.z) * 1.05f;
            var light = new GameObject("map light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(80, 0, 0);
            var rt = new RenderTexture(1024, 1024, 24); c2.targetTexture = rt; c2.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1024, 1024, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0); tex.Apply();
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/bay_map.png", tex.EncodeToPNG());
            Debug.Log("[BayMap] written Logs/bay_map.png (top-down: +x right, +z up)");
        }
    }
}
