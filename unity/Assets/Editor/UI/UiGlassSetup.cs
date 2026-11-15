using System.IO;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ProcSense.EditorTools.UI
{
    /// <summary>
    /// P3-UIX-03 editor steps. <see cref="Build"/> writes the two shared glass materials (Assets/UI/Theme/Resources/UiGlass.mat and UiGlassLight.mat) from the UiTheme tokens; a material in
    /// Resources also keeps the shader in every player build. <see cref="Capture"/> renders a sample panel (title, body, buttons with icons) inside the patient bay to
    /// artifacts/captures/uix03_glass.png for the desktop review the task asks for; the headset check in both eyes is DC-13/DC-14 with the diagnostic APK.
    /// </summary>
    public static class UiGlassSetup
    {
        public const string Folder = "Assets/UI/Theme/Resources/";
        /// <summary>How much of the blurred room shows through the dark glass: low, so body text keeps its contrast over a bright room.</summary>
        public const float DarkFrost = .2f;

        [MenuItem("ProcSense/UI/Build glass materials")]
        public static void Build()
        {
            var shader = Shader.Find(GlassPanel.ShaderName);
            if (shader == null) throw new FileNotFoundException(GlassPanel.ShaderName);
            Directory.CreateDirectory(Folder);
            var t = UiTheme.Current;
            Write(GlassPanel.DarkMaterial, shader, t.GlassTint, DarkFrost, t.Edge);
            Write(GlassPanel.LightMaterial, shader, t.GlassTintLight, .55f, new Color(1, 1, 1, .5f));
            AssetDatabase.SaveAssets();
            Debug.Log("[ProcSense] glass materials written");
        }

        private static void Write(string name, Shader shader, Color tint, float frost, Color edge)
        {
            string path = Folder + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetColor("_Color", tint);
            m.SetColor("_EdgeColor", edge);
            m.SetFloat("_EdgeWidth", UiTheme.Current.EdgeWidth);
            m.SetFloat("_Frost", frost);
            m.SetFloat("_FrostMip", 6f);
            m.SetFloat("_Streak", .04f);
            m.SetFloat("_Noise", .012f);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
        }

        /// <summary>Batch entry point: the materials, then the capture.</summary>
        public static void BuildAndCapture() { Build(); Capture(); }

        [MenuItem("ProcSense/UI/Capture glass sample")]
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var t = UiTheme.Current;
            Vector3 eye = new Vector3(0.9f, 1.65f, -0.6f), look = new Vector3(-0.3f, 1.2f, 1.4f);
            var forward = (look - eye).normalized;

            var root = new GameObject("Glass sample");
            float distance = 1.2f, mpu = t.MetresPerUnit(distance);
            root.transform.position = eye + forward * distance;
            root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(root.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 8f;
            var rect = (RectTransform)canvasGo.transform;
            rect.sizeDelta = new Vector2(900, 560);
            rect.localScale = Vector3.one * mpu;

            GlassPanel.Add(canvasGo.transform, "Panel", Vector2.zero, new Vector2(900, 560));
            Text(canvasGo.transform, "Choose a mode", new Vector2(48, 40), t.Title, FontStyles.Bold, t.TextPrimary);
            Text(canvasGo.transform, "Training shows the goals, hints and how-to for each step. The examination hides them and charges for hints.", new Vector2(48, 112), t.Body, FontStyles.Normal, t.TextSecondary, 800);
            Button(canvasGo.transform, UiIcons.Training, "Training", new Vector2(48, 250), t.Action, t.TextPrimary);
            Button(canvasGo.transform, UiIcons.Test, "Examination", new Vector2(48, 346), new Color(1, 1, 1, .12f), t.TextPrimary);
            Button(canvasGo.transform, UiIcons.Settings, "Settings", new Vector2(48, 442), new Color(1, 1, 1, .12f), t.TextPrimary);
            var card = GlassPanel.Add(canvasGo.transform, "Hint card", new Vector2(480, 250), new Vector2(372, 270), GlassPanel.Style.Light);
            Icon(card.transform, UiIcons.Hint, new Vector2(28, 28), 56, t.Hint);
            Text(card.transform, "Hint", new Vector2(100, 34), t.Heading, FontStyles.Bold, t.TextPrimary);
            Text(card.transform, "Confirm the patient's identity before anything else.", new Vector2(28, 110), t.Body, FontStyles.Normal, t.TextPrimary, 320);
            Canvas.ForceUpdateCanvases();

            var camGo = new GameObject("Capture camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = .05f;
            var rt = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string out_ = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "captures", "uix03_glass.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(out_));
            File.WriteAllBytes(out_, tex.EncodeToPNG());
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(rt);
            Debug.Log("[ProcSense] glass capture written to " + out_);
        }

        private static void Text(Transform parent, string s, Vector2 at, float size, FontStyles style, Color colour, float width = 600)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = s;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.textWrappingMode = TextWrappingModes.Normal;
            Place((RectTransform)go.transform, at, new Vector2(width, size * 3));
        }

        private static void Button(Transform parent, string icon, string label, Vector2 at, Color fill, Color text)
        {
            var b = GlassPanel.Add(parent, "Button " + label, at, new Vector2(400, 80), GlassPanel.Style.Light);
            b.color = fill.a >= 1 ? new Color(fill.r * 3f, fill.g * 3f, fill.b * 3f, 1f) : Color.white;   // the primary button is glass tinted with the accent (the kit, P3-UIX-04, owns real button styles)
            Icon(b.transform, icon, new Vector2(24, 16), 48, text);
            Text(b.transform, label, new Vector2(92, 20), UiTheme.Current.Body + 4, FontStyles.Bold, text, 290);
        }

        private static void Icon(Transform parent, string name, Vector2 at, float size, Color colour)
        {
            var go = new GameObject("Icon " + name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = UiIcons.Get(name);
            img.color = colour;
            Place((RectTransform)go.transform, at, new Vector2(size, size));
        }

        private static void Place(RectTransform r, Vector2 at, Vector2 size)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(at.x, -at.y);
            r.sizeDelta = size;
        }
    }
}
