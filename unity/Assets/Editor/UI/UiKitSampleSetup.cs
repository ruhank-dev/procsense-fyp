using System.IO;
using ProcSense.VR.UI.Kit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// P3-UIX-04 editor steps: <see cref="SaveScene"/> writes Assets/Scenes/UiKitSample.unity (a camera and the KitSample panel, for looking at the kit on the desktop) and
    /// <see cref="Capture"/> renders the sample in the patient bay to artifacts/captures/uix04_kit.png.
    /// </summary>
    public static class UiKitSampleSetup
    {
        public const string ScenePath = "Assets/Scenes/UiKitSample.unity";

        public static void SaveAndCapture() { UiGlassSetup.Build(); SaveScene(); Capture(); }

        [MenuItem("ProcSense/UI/Save kit sample scene")]
        public static void SaveScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0, 1.6f, 0);
            var c = cam.GetComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(.32f, .36f, .40f);
            var light = new GameObject("Light", typeof(Light));
            light.GetComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            var sample = new GameObject("Kit sample");
            sample.transform.position = new Vector3(0, 1.6f, KitSample.Distance);
            sample.AddComponent<KitSample>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[ProcSense] kit sample scene saved");
        }

        [MenuItem("ProcSense/UI/Capture kit sample")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            Vector3 eye = new Vector3(0.9f, 1.65f, -0.6f), look = new Vector3(-0.3f, 1.55f, 1.4f);
            var forward = (look - eye).normalized;
            var root = new GameObject("Kit sample");
            root.transform.position = eye + forward * KitSample.Distance;
            root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            KitSample.Build(root.transform);
            Canvas.ForceUpdateCanvases();

            var camGo = new GameObject("Capture camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var cam = camGo.GetComponent<Camera>();
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            cam.fieldOfView = 70f;
            cam.nearClipPlane = .05f;
            var rt = new RenderTexture(1800, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "captures", "uix04_kit.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rt);
            Debug.Log("[ProcSense] kit capture written to " + path);
        }
    }
}
