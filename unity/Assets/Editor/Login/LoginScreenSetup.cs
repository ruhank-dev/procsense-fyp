using System;
using System.IO;
using TMPro;
using ProcSense.VR.Boot;
using ProcSense.VR.Login;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace ProcSense.EditorTools
{
    public static class LoginScreenSetup
    {
        public const string PrefabPath = "Assets/Prefabs/UI/LoginScreen.prefab";
        public const string SettingsPath = "Assets/Settings/DemoLoginSettings.asset";
        private static readonly Color Ink = new Color(.07f, .12f, .16f);
        private static readonly Color Teal = new Color(.10f, .49f, .45f);
        private static readonly Color Muted = new Color(.39f, .46f, .48f);
        private static TMP_FontAsset _font;

        [MenuItem("ProcSense/Open Application", false, -10)]
        public static void OpenApplication()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(WorkspaceMenu.BootPath, OpenSceneMode.Single);
            EditorSceneManager.playModeStartScene = null;
        }

        [MenuItem("ProcSense/Development/Bypass login in Editor")]
        private static void ToggleBypass() => EditorPrefs.SetBool(DemoLoginSettings.BypassPreference, !EditorPrefs.GetBool(DemoLoginSettings.BypassPreference, false));
        [MenuItem("ProcSense/Development/Bypass login in Editor", true)]
        private static bool CheckBypass() { Menu.SetChecked("ProcSense/Development/Bypass login in Editor", EditorPrefs.GetBool(DemoLoginSettings.BypassPreference, false)); return true; }

        [MenuItem("ProcSense/Development/Preview login")]
        public static void Preview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(WorkspaceMenu.BootPath, OpenSceneMode.Single);
            EditorPrefs.SetBool(DemoLoginSettings.PreviewPreference, true);
            EditorApplication.isPlaying = true;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private static void Fill(RectTransform rect, float inset = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset;
        }
        private static RoundedGraphic Panel(Transform parent, string name, float x, float y, float width, float height, Color color, float radius = 16)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var graphic = rect.gameObject.AddComponent<RoundedGraphic>();
            graphic.color = color; graphic.Radius = radius; graphic.raycastTarget = false;
            return graphic;
        }
        private static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height, float size, Color color, bool bold = false)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = _font; label.text = value; label.fontSize = size; label.color = color;
            label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            label.raycastTarget = false; label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }
        private static UnityEngine.UI.Button Button(Transform parent, string name, string label, float x, float y, float width, float height, Color background, Color foreground, float size)
        {
            var graphic = Panel(parent, name, x, y, width, height, Color.white, 12);
            graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = graphic;
            var colors = button.colors;
            colors.normalColor = background; colors.highlightedColor = Color.Lerp(background, Color.white, .08f);
            colors.pressedColor = Color.Lerp(background, Ink, .12f); colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(.57f, .64f, .63f); colors.fadeDuration = .12f; button.colors = colors;
            var text = Text(graphic.transform, "Label", label, 0, 0, width, height, size, foreground, true);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
        private static TMP_InputField Field(Transform parent, string name, string hint, float y, bool secret)
        {
            var border = Panel(parent, name, 44, y, 412, 62, Color.white, 12);
            border.raycastTarget = true;
            var fill = Panel(border.transform, "Fill", 1.5f, 1.5f, 409, 59, new Color(.99f, .995f, .985f), 11);
            var viewport = Rect(border.transform, "Viewport", 19, 13, secret ? 299 : 374, 38);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var label = Text(viewport, "Value", "", 0, 0, viewport.sizeDelta.x, 38, 23, Ink);
            Fill(label.rectTransform);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholder = Text(viewport, "Placeholder", hint, 0, 0, viewport.sizeDelta.x, 38, 22, new Color(.55f, .60f, .60f));
            Fill(placeholder.rectTransform);
            var field = border.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport; field.textComponent = (TextMeshProUGUI)label; field.placeholder = placeholder;
            field.targetGraphic = border; field.contentType = secret ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.EmailAddress;
            field.characterLimit = secret ? 128 : 254; field.asteriskChar = '*'; field.caretColor = Teal; field.customCaretColor = true;
            field.caretWidth = 2; field.selectionColor = new Color(.12f, .55f, .48f, .2f);
            var colors = field.colors;
            colors.normalColor = new Color(.79f, .83f, .82f); colors.highlightedColor = new Color(.56f, .69f, .66f);
            colors.selectedColor = Teal; colors.pressedColor = Teal; colors.fadeDuration = .1f; field.colors = colors;
            return field;
        }

        public static object Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save existing scene edits first.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null) throw new InvalidOperationException("Login already exists; preserve authored edits.");
            string original = SceneManager.GetActiveScene().path;
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (_font == null) throw new InvalidOperationException("Existing TMP font is unavailable.");
            Directory.CreateDirectory("Assets/Prefabs/UI"); AssetDatabase.Refresh();
            var root = new GameObject("ProcSense Login", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(TrackedDeviceGraphicRaycaster));
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
                var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 1000); scaler.matchWidthOrHeight = .5f;
                ((RectTransform)root.transform).sizeDelta = new Vector2(1600, 1000);
                var bg = Panel(root.transform, "Background", 0, 0, 1600, 1000, new Color(.045f, .095f, .13f), 0); Fill(bg.rectTransform);
                var glow = Panel(root.transform, "Ambient teal", -130, 390, 1000, 850, new Color(.11f, .43f, .38f, .12f), 450);
                var brand = Rect(root.transform, "Brand", 112, 70, 440, 50);
                Panel(brand, "Brand mark", 0, 0, 45, 45, new Color(.37f, .80f, .73f), 12);
                Panel(brand, "Mark horizontal", 12, 20, 21, 5, Ink, 1);
                Panel(brand, "Mark vertical", 20, 12, 5, 21, Ink, 1);
                Text(brand, "Name", "ProcSense", 62, 3, 320, 44, 31, Color.white, true);
                var introduction = Rect(root.transform, "Introduction", 0, 0, 640, 610);
                introduction.anchorMin = introduction.anchorMax = new Vector2(.07f, .5f); introduction.pivot = new Vector2(0, .5f); introduction.anchoredPosition = Vector2.zero;
                var overline = Text(introduction, "Overline", "IMMERSIVE CLINICAL TRAINING", 0, 22, 620, 34, 16, new Color(.46f, .76f, .73f)); overline.characterSpacing = 2;
                Text(introduction, "Headline", "Make every\npractice count.", 0, 93, 640, 193, 65, new Color(.96f, .98f, .95f), true);
                Text(introduction, "Description", "Build confidence at the bedside,\none decision at a time.", 2, 311, 615, 90, 25, new Color(.67f, .75f, .77f));
                var art = Rect(introduction, "Signal ribbons", -40, 415, 670, 170).gameObject.AddComponent<LoginArtwork>();
                art.color = new Color(.37f, .80f, .73f, .48f); art.raycastTarget = false;
                var divider = Panel(root.transform, "Divider", 0, 0, 1, 630, new Color(.6f, .8f, .78f, .10f), 0).rectTransform;
                divider.anchorMin = divider.anchorMax = divider.pivot = new Vector2(.54f, .5f); divider.anchoredPosition = Vector2.zero;
                var card = Panel(root.transform, "Sign-in card", 0, 0, 500, 670, new Color(.97f, .98f, .955f), 24).rectTransform;
                card.anchorMin = card.anchorMax = card.pivot = new Vector2(.74f, .5f); card.pivot = new Vector2(.5f, .5f); card.anchoredPosition = Vector2.zero;
                Text(card, "Eyebrow", "YOUR WORKSPACE", 44, 39, 410, 24, 13, Teal, true).characterSpacing = 2;
                Text(card, "Heading", "Welcome back", 44, 78, 410, 49, 37, Ink, true);
                Text(card, "Subtitle", "Sign in to your training workspace.", 44, 137, 410, 32, 20, Muted);
                Text(card, "Email label", "Email address", 44, 196, 410, 26, 18, Ink, true);
                var email = Field(card, "Email", "you@example.com", 229, false);
                Text(card, "Password label", "Password", 44, 316, 410, 26, 18, Ink, true);
                var password = Field(card, "Password", "Enter your password", 349, true);
                var reveal = Button(password.transform, "Reveal password", "Show", 326, 7, 75, 48, new Color(.99f, .995f, .985f), Teal, 16);
                var status = Text(card, "Status", "", 44, 426, 412, 36, 17, new Color(.66f, .20f, .20f));
                var signIn = Button(card, "Sign in", "Sign in", 44, 471, 412, 60, Teal, Color.white, 22);
                var demo = Panel(card, "Demo account", 44, 553, 412, 85, new Color(.89f, .93f, .895f), 12);
                Text(demo.transform, "Demo heading", "DEMO ACCESS", 16, 10, 380, 21, 12, Teal, true).characterSpacing = 1;
                Text(demo.transform, "Demo credentials", DemoLoginSettings.DemoEmail + "\n" + DemoLoginSettings.DemoPassword, 16, 33, 380, 43, 16, Muted);
                var footer = Text(root.transform, "Footer", "Demo environment  /  ProcSense", 0, 0, 700, 34, 16, new Color(.52f, .65f, .67f)).rectTransform;
                footer.anchorMin = footer.anchorMax = new Vector2(.07f, .04f); footer.pivot = Vector2.zero; footer.anchoredPosition = Vector2.zero;
                root.AddComponent<DemoLoginView>().Configure(email, password, status, reveal.GetComponentInChildren<TMP_Text>(), signIn, reveal, card, introduction.gameObject, divider.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var settings = AssetDatabase.LoadAssetAtPath<DemoLoginSettings>(SettingsPath);
            if (settings == null) { settings = ScriptableObject.CreateInstance<DemoLoginSettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
            var boot = EditorSceneManager.OpenScene(WorkspaceMenu.BootPath, OpenSceneMode.Single);
            UnityEngine.Object.FindFirstObjectByType<BootLoader>().ConfigureLogin(settings, AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/XR/PlayerRig.prefab"));
            EditorSceneManager.MarkSceneDirty(boot); EditorSceneManager.SaveScene(boot); AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            return new { prefab = PrefabPath, settings = SettingsPath, bypass = "ProcSense/Development/Bypass login in Editor" };
        }
    }
}
