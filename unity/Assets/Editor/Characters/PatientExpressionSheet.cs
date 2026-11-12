using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool: opens the patient bay, shows the Rocketbox patient's face in each behaviour mode (and calm) and writes one contact sheet to Logs/patient_expressions.png.
    /// Run: Unity -projectPath unity -executeMethod ProcSense.EditorTools.PatientExpressionSheet.Run (needs graphics, so no -nographics). Changes nothing in the project.
    /// </summary>
    public static class PatientExpressionSheet
    {
        private const int Size = 512;

        private static int Shape(SkinnedMeshRenderer r, string ending) { for (int i = 0; i < r.sharedMesh.blendShapeCount; i++) if (r.sharedMesh.GetBlendShapeName(i).EndsWith(ending)) return i; return -1; }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            var face = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && Shape(r, "EyeBlinkLeft") >= 0);
            var head = patient.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Head");

            var camGo = new GameObject("expression camera"); var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 22; cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f);
            var focus = head.position + Vector3.up * .02f;
            camGo.transform.position = focus + Vector3.up * 1.1f; camGo.transform.LookAt(focus, (head.position - head.parent.parent.position).normalized);
            var light = new GameObject("expression light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);

            var modes = new[] { "" }.Concat(PatientExpression.Modes).ToArray();
            var cols = 4; var rows = (modes.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGB24, false);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt;
            var all = Enumerable.Range(0, face.sharedMesh.blendShapeCount).ToList();
            for (int m = 0; m < modes.Length; m++)
            {
                foreach (var i in all) face.SetBlendShapeWeight(i, 0);
                foreach (var kv in PatientExpression.For(modes[m]))
                {
                    string ending = kv.Key == PatientExpression.Droop ? "EyeBlinkLeft" : kv.Key;
                    int i = Shape(face, ending); if (i >= 0) face.SetBlendShapeWeight(i, kv.Value * 100f);
                    if (kv.Key == PatientExpression.Droop) { int r = Shape(face, "EyeBlinkRight"); if (r >= 0) face.SetBlendShapeWeight(r, kv.Value * 100f); }
                }
                face.enabled = false; face.enabled = true; face.updateWhenOffscreen = true; Canvas.ForceUpdateCanvases(); cam.Render(); cam.Render();
                Debug.Log("[ExpressionSheet] weights set: " + Enumerable.Range(0, face.sharedMesh.blendShapeCount).Count(k => face.GetBlendShapeWeight(k) > 0f) + " on " + face.name);
                RenderTexture.active = rt;
                var tile = new Texture2D(Size, Size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tile.Apply();
                sheet.SetPixels((m % cols) * Size, (rows - 1 - m / cols) * Size, Size, Size, tile.GetPixels());
                Debug.Log("[ExpressionSheet] tile " + m + " = " + (modes[m] == "" ? "CALM" : modes[m]));
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/patient_expressions.png", sheet.EncodeToPNG());
            Debug.Log("[ExpressionSheet] written Logs/patient_expressions.png");
        }
    }
}
