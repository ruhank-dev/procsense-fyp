using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool for the skin signs: Place adds PatientSkin to the patient in the patient bay and saves (idempotent); Run renders Logs/patient_skin.png (calm, then each sign at full strength, face and chest) without saving the scene.
    /// Run: Unity -batchmode -quit -projectPath unity -executeMethod ProcSense.EditorTools.PatientSkinSheet.Place (or .Run, which needs graphics so no -nographics).
    /// </summary>
    public static class PatientSkinSheet
    {
        private const int Size = 512;
        private static readonly (string name, float[] signs)[] Cases =
        {
            ("calm", new float[] { 0, 0, 0, 0, 0 }), ("pallor", new float[] { 1, 0, 0, 0, 0 }), ("flush", new float[] { 0, 1, 0, 0, 0 }),
            ("jaundice", new float[] { 0, 0, 1, 0, 0 }), ("cyanosis", new float[] { 0, 0, 0, 1, 0 }), ("sweat", new float[] { 0, 0, 0, 0, 1 }),
        };

        public static void Place()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            if (patient == null) { Debug.LogWarning("[Skin] no Rocketbox patient in the patient bay"); return; }
            if (patient.GetComponent<PatientSkin>() == null) patient.AddComponent<PatientSkin>();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log("[Skin] PatientSkin is on the patient");
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            var motion = patient.GetComponent<PatientMotion>(); motion.autoMotion = false; motion.Prepare(); motion.Step(0f, .05f);
            var skin = patient.GetComponent<PatientSkin>() ?? patient.AddComponent<PatientSkin>(); skin.blendSeconds = .1f;
            var head = motion.Head; var pelvis = patient.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Pelvis"); var up = (head.position - pelvis.position).normalized;
            var camGo = new GameObject("review camera"); var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f);
            var light = new GameObject("review light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt;
            const int cols = 3; int rows = (Cases.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGB24, false);
            for (int i = 0; i < Cases.Length; i++)
            {
                var s = Cases[i].signs; skin.SetSigns(s[0], s[1], s[2], s[3], s[4]); for (int k = 0; k < 10; k++) skin.Step(.05f);
                cam.fieldOfView = 24f; var focus = head.position + Vector3.up * .02f; camGo.transform.position = focus + Vector3.up * 1.1f; camGo.transform.LookAt(focus, up);
                cam.Render(); cam.Render(); RenderTexture.active = rt;
                var tile = new Texture2D(Size, Size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tile.Apply();
                sheet.SetPixels((i % cols) * Size, (rows - 1 - i / cols) * Size, Size, Size, tile.GetPixels());
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/patient_skin.png", sheet.EncodeToPNG());
            Debug.Log("[Skin] written Logs/patient_skin.png (order: " + string.Join(", ", Cases.Select(c => c.name)) + ")");
        }
    }
}
