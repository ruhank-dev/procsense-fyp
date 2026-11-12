using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool: opens the patient bay, drives the Rocketbox patient's real PatientMotion into each behaviour mode (and calm) and writes two contact sheets: Logs/patient_faces.png (the face) and Logs/patient_bodies.png (head, eyes, arm).
    /// Run: Unity -projectPath unity -executeMethod ProcSense.EditorTools.PatientExpressionSheet.Run (needs graphics, so no -nographics). The scene is not saved.
    /// </summary>
    public static class PatientExpressionSheet
    {
        private const int Size = 512;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            var motion = patient.GetComponent<PatientMotion>();
            motion.autoMotion = false; motion.Prepare();
            var head = motion.Head;
            var pelvis = patient.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Pelvis");
            var headUp = (head.position - pelvis.position).normalized;

            var camGo = new GameObject("review camera"); var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f);
            var light = new GameObject("review light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);

            var modes = new[] { "" }.Concat(PatientExpression.Modes).ToArray();
            Sheet("Logs/patient_faces.png", modes, motion, cam, camGo, head.position + Vector3.up * .02f, 1.1f, 22f, headUp);
            Sheet("Logs/patient_bodies.png", modes, motion, cam, camGo, Vector3.Lerp(pelvis.position, head.position, .55f), 1.9f, 38f, headUp);
            Debug.Log("[ExpressionSheet] written Logs/patient_faces.png and Logs/patient_bodies.png");
        }

        private static void Sheet(string path, string[] modes, PatientMotion motion, Camera cam, GameObject camGo, Vector3 focus, float distance, float fov, Vector3 up)
        {
            cam.fieldOfView = fov; camGo.transform.position = focus + Vector3.up * distance; camGo.transform.LookAt(focus, up);
            const int cols = 4; int rows = (modes.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGB24, false);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt;
            var face = motion.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);
            for (int m = 0; m < modes.Length; m++)
            {
                motion.SetMood("", true); Settle(motion, 3f);
                motion.SetMood(modes[m], true); Settle(motion, 4f);
                face.enabled = false; face.enabled = true; face.updateWhenOffscreen = true;
                cam.Render(); cam.Render(); RenderTexture.active = rt;
                var tile = new Texture2D(Size, Size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tile.Apply();
                sheet.SetPixels((m % cols) * Size, (rows - 1 - m / cols) * Size, Size, Size, tile.GetPixels());
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, sheet.EncodeToPNG());
        }

        private static float clock;
        private static void Settle(PatientMotion motion, float seconds) { for (float s = 0; s < seconds; s += .05f) { clock += .05f; motion.Step(clock, .05f); } }
    }
}
