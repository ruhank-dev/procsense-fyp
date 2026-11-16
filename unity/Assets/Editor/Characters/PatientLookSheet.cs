using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool for the look-at: Logs/patient_look.png shows the patient with no target, then looking at a marker to the patient's right, to the left, above and well off to one side (past the limits). The scene is not saved.
    /// Run: Unity -projectPath unity -executeMethod ProcSense.EditorTools.PatientLookSheet.Run (graphics needed, so no -nographics).
    /// </summary>
    public static class PatientLookSheet
    {
        private const int Size = 512;

        /// <summary>Logs/patient_gesture.png: top view at rest and at the peak of a head shake, side view at rest and at the peak of a nod (the chin should dip).</summary>
        public static void Gestures()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient"); var motion = patient.GetComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare(); motion.Step(0f, .05f);
            var head = motion.Head; var all = patient.GetComponentsInChildren<Transform>(true);
            var side = (all.First(t => t.name == "Bip01 R Thigh").position - all.First(t => t.name == "Bip01 L Thigh").position).normalized; var along = (head.position - all.First(t => t.name == "Bip01 Pelvis").position).normalized;
            var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f); cam.fieldOfView = 30f;
            var light = new GameObject("light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt; var sheet = new Texture2D(4 * Size, Size, TextureFormat.RGB24, false);
            var face = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);
            var frames = new (HeadGestureKind kind, float at, bool fromSide)[] { (HeadGestureKind.None, 0f, false), (HeadGestureKind.Shake, .25f, false), (HeadGestureKind.None, 0f, true), (HeadGestureKind.Nod, .2f, true) };
            float clock = 10f;
            for (int i = 0; i < frames.Length; i++)
            {
                clock += 5f; motion.Step(clock, .05f); motion.Gesture(frames[i].kind); clock += .01f; motion.Step(clock, .01f); clock += frames[i].at; motion.Step(clock, .01f);
                face.enabled = false; face.enabled = true; face.updateWhenOffscreen = true;
                var focus = head.position + along * .03f;
                camGo.transform.position = focus + (frames[i].fromSide ? side * .55f + Vector3.up * .55f : Vector3.up * 1.0f); camGo.transform.LookAt(focus, frames[i].fromSide ? Vector3.up : along);
                cam.Render(); cam.Render(); RenderTexture.active = rt;
                var tile = new Texture2D(Size, Size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tile.Apply(); sheet.SetPixels(i * Size, 0, Size, Size, tile.GetPixels());
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/patient_gesture.png", sheet.EncodeToPNG());
            Debug.Log("[Look] written Logs/patient_gesture.png (top rest | shake peak | side rest | nod peak)");
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient"); var motion = patient.GetComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare(); motion.Step(0f, .05f);
            var head = motion.Head; var all = patient.GetComponentsInChildren<Transform>(true);
            var pelvis = all.First(t => t.name == "Bip01 Pelvis"); var side = (all.First(t => t.name == "Bip01 R Thigh").position - all.First(t => t.name == "Bip01 L Thigh").position).normalized;
            var along = (head.position - pelvis.position).normalized;
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere); marker.transform.localScale = Vector3.one * .12f; Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = Color.red };
            var cases = new (string name, Vector3? offset)[] { ("none", null), ("patient right 0.35m", side * .35f + Vector3.up * .5f), ("patient left 0.35m", -side * .35f + Vector3.up * .5f), ("above head end", along * .5f + Vector3.up * .5f), ("far right (past limits)", side * .9f + Vector3.up * .3f) };
            var camGo = new GameObject("cam"); var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f); cam.fieldOfView = 80f;
            var light = new GameObject("light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt; const int cols = 3; int rows = (cases.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGB24, false);
            var face = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);
            float clock = 0f;
            for (int i = 0; i < cases.Length; i++)
            {
                motion.lookAt = cases[i].offset.HasValue ? marker.transform : null; marker.SetActive(cases[i].offset.HasValue);
                if (cases[i].offset.HasValue) marker.transform.position = head.position + cases[i].offset.Value;
                for (int k = 0; k < 40; k++) { clock += .05f; motion.Step(clock, .05f); }
                face.enabled = false; face.enabled = true; face.updateWhenOffscreen = true;
                var focus = head.position + along * .05f + side * (cases[i].offset.HasValue ? cases[i].offset.Value.x * 0f : 0f);
                camGo.transform.position = focus + Vector3.up * 1.1f; camGo.transform.LookAt(focus, along);
                cam.Render(); cam.Render(); RenderTexture.active = rt;
                var tile = new Texture2D(Size, Size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tile.Apply();
                sheet.SetPixels((i % cols) * Size, (rows - 1 - i / cols) * Size, Size, Size, tile.GetPixels());
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/patient_look.png", sheet.EncodeToPNG());
            Debug.Log("[Look] written Logs/patient_look.png (order: " + string.Join(" | ", cases.Select(c => c.name)) + ")");
        }
    }
}
