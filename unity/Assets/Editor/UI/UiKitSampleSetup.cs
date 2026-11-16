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

        /// <summary>P3-SET-06 captures: the settings panel's Display tab in the patient bay, normal and high contrast (artifacts/captures/set06_normal.png, set06_high_contrast.png).</summary>
        [MenuItem("ProcSense/UI/Capture settings panel")]
        public static void CaptureSettings()
        {
            var store = ProcSense.VR.Settings.GameSettings.Store;
            ProcSense.VR.Settings.GameSettings.Store = new ScratchStore();   // never touch the developer's own saved settings
            try
            {
                foreach (var high in new[] { false, true })
                {
                    ProcSense.VR.Settings.GameSettings.Update(s => s.highContrast = high);
                    EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
                    Vector3 eye = new Vector3(0.9f, 1.65f, -0.6f), look = new Vector3(-0.3f, 1.55f, 1.4f);
                    var forward = (look - eye).normalized;
                    var go = new GameObject("Settings capture");
                    go.transform.SetPositionAndRotation(eye + forward * ProcSense.VR.UI.SettingsUi.SettingsPanel.Distance, Quaternion.LookRotation(forward, Vector3.up));
                    go.AddComponent<ProcSense.VR.UI.SettingsUi.SettingsPanel>().Build(ProcSense.VR.UI.SettingsUi.SettingsPanel.Tab.Display);
                    Canvas.ForceUpdateCanvases();
                    Render(eye, forward, high ? "set06_high_contrast.png" : "set06_normal.png");
                    Object.DestroyImmediate(go);
                }
            }
            finally { ProcSense.VR.Settings.GameSettings.Store = store; }
        }

        /// <summary>P3-MNU-03 capture: the game-flow main menu in the kit's glass style, in the patient bay (artifacts/captures/mnu03_main_menu.png).</summary>
        [MenuItem("ProcSense/UI/Capture main menu")]
        public static void CaptureMenu()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            Vector3 eye = new Vector3(0.9f, 1.65f, -0.6f), look = new Vector3(-0.3f, 1.55f, 1.4f);
            var forward = (look - eye).normalized;
            var hostGo = new GameObject("capture host");
            var host = hostGo.AddComponent<ProcSense.VR.Bridge.SimulationHost>();
            var flow = new ProcSense.VR.Flow.MenuFlow(host, ProcSense.VR.Content.UnityContentReader.ForThisPlatform(), () => 1UL);
            flow.SignedIn();
            var go = new GameObject("Menu capture");
            go.transform.SetPositionAndRotation(eye + forward * ProcSense.VR.Flow.MenuFlowInstaller.Distance, Quaternion.LookRotation(forward, Vector3.up));
            go.AddComponent<ProcSense.VR.Flow.FlowPanel>().Build(flow, go.transform);
            Canvas.ForceUpdateCanvases();
            Render(eye, forward, "mnu03_main_menu.png");
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(hostGo);
        }

        /// <summary>
        /// P3-UIX-10: review captures of every flow screen from one fixed camera in the patient bay (artifacts/captures/review/NN_name.png): main menu, procedure cards, briefing,
        /// playing with the hint panel, the examination hint confirmation, the pause menu, the results, and each settings tab. For the owner's approval.
        /// </summary>
        [MenuItem("ProcSense/UI/Capture review screens")]
        public static void CaptureReview()
        {
            var store = ProcSense.VR.Settings.GameSettings.Store;
            ProcSense.VR.Settings.GameSettings.Store = new ScratchStore();
            try
            {
                EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
                Vector3 eye = new Vector3(0.9f, 1.65f, -0.6f), look = new Vector3(-0.3f, 1.55f, 1.4f);
                var forward = (look - eye).normalized;
                var head = new GameObject("Review head", typeof(Camera)).transform;
                head.SetPositionAndRotation(eye, Quaternion.LookRotation(forward, Vector3.up));
                var hostGo = new GameObject("review host");
                var host = hostGo.AddComponent<ProcSense.VR.Bridge.SimulationHost>();
                host.AutoTick = false;
                host.SaveLogOnDestroy = false;
                var reader = ProcSense.VR.Content.UnityContentReader.ForThisPlatform();
                var flow = new ProcSense.VR.Flow.MenuFlow(host, reader, () => 42UL) { EndSessionOnSubmit = false };
                var root = new GameObject("Review menu");
                root.transform.SetPositionAndRotation(eye + forward * ProcSense.VR.Flow.MenuFlowInstaller.Distance, Quaternion.LookRotation(forward, Vector3.up));
                var panel = root.AddComponent<ProcSense.VR.Flow.FlowPanel>();
                panel.Build(flow, root.transform);
                var hintGo = new GameObject("Hint panel");
                hintGo.transform.SetParent(root.transform, false);
                hintGo.transform.localPosition = new Vector3(-.95f, 0f, .05f);
                hintGo.transform.localRotation = Quaternion.Euler(0f, -15f, 0f);
                var hint = hintGo.AddComponent<ProcSense.VR.Flow.HintPanel>();
                hint.Build(flow, ProcSense.VR.Flow.HintPanel.CostFrom(reader));
                int n = 0;
                void Shot(string name) { ProcSense.VR.UI.Theme.UiMotion.Tick(1f); Canvas.ForceUpdateCanvases(); Render(eye, forward, "review/" + (++n).ToString("00") + "_" + name + ".png"); }

                flow.SignedIn(); Shot("main_menu");
                flow.ChooseMode(ProcSense.Core.SessionMode.Assessment); Shot("procedure_cards_examination");
                flow.ChooseSurgery("cholecystectomy", ProcSense.Core.TrainingLevel.Beginner); Shot("briefing");
                flow.Begin(); Shot("playing_with_hint_panel");
                hint.Press(); Shot("examination_hint_confirmation");
                hint.Dialog.Confirm.onClick.Invoke(); Shot("hint_card");
                var pause = root.AddComponent<ProcSense.VR.Flow.PauseMenu>();
                pause.Flow = flow; pause.Head = head;
                pause.Pause(); Shot("pause_menu");
                pause.Resume();
                flow.Submit(); flow.Submit(); Shot("results");
                foreach (ProcSense.VR.UI.SettingsUi.SettingsPanel.Tab tab in System.Enum.GetValues(typeof(ProcSense.VR.UI.SettingsUi.SettingsPanel.Tab)))
                {
                    var s = new GameObject("Review settings");
                    s.transform.SetPositionAndRotation(eye + forward * ProcSense.VR.UI.SettingsUi.SettingsPanel.Distance, Quaternion.LookRotation(forward, Vector3.up));
                    s.AddComponent<ProcSense.VR.UI.SettingsUi.SettingsPanel>().Build(tab);
                    root.SetActive(false);
                    Shot("settings_" + tab.ToString().ToLowerInvariant());
                    Object.DestroyImmediate(s);
                    root.SetActive(true);
                }
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(hostGo);
                Object.DestroyImmediate(head.gameObject);
            }
            finally { ProcSense.VR.Settings.GameSettings.Store = store; }
        }

        private sealed class ScratchStore : ProcSense.VR.Settings.ISettingsStore
        {
            private readonly System.Collections.Generic.Dictionary<string, string> _v = new System.Collections.Generic.Dictionary<string, string>();
            public string GetString(string key) => _v.TryGetValue(key, out var s) ? s : "";
            public bool HasKey(string key) => _v.ContainsKey(key);
            public int GetInt(string key, int fallback) => fallback;
            public void SetString(string key, string value) => _v[key] = value;
            public void DeleteKey(string key) => _v.Remove(key);
            public void Save() { }
        }

        private static void Render(Vector3 eye, Vector3 forward, string file)
        {
            var camGo = new GameObject("Capture camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
            var cam = camGo.GetComponent<Camera>();
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(forward, Vector3.up));
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
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "captures", file));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(rt);
            Debug.Log("[ProcSense] capture written to " + path);
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
