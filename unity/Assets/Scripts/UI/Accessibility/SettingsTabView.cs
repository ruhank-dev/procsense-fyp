using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// The tablet's "Settings" tab (p0-vr-interaction-ui-s10): panel size, subtitle size, which hand walks, seated or standing, and a reset for the next person.
    /// Sizes and hand apply at once and are kept on this headset; seated play goes to the rig's own calibration. Nothing here touches the simulation.
    /// </summary>
    public sealed class SettingsTabView : MonoBehaviour
    {
        private TMP_Text _summary;
        private Button _size, _subtitles, _hand, _seated, _reset;

        public string SummaryText => _summary != null ? _summary.text : "";
        public Button SizeButton => _size;
        public Button SubtitleButton => _subtitles;
        public Button HandButton => _hand;
        public Button SeatedButton => _seated;
        public Button ResetButton => _reset;

        /// <summary>Draws the tab under <paramref name="root"/> (the tablet's tab area starts 190 px from the top).</summary>
        public void Build(Transform root)
        {
            UiKit.Panel(root, "Settings background", new Vector2(0, 190), new Vector2(900, 410), new Color(0.08f, 0.10f, 0.15f, 1f));
            UiKit.Label(root, "Settings title", "Readability and comfort (kept on this headset)", new Vector2(14, 194), new Vector2(872, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _size = UiKit.MakeButton(root, "", new Vector2(10, 232), new Vector2(430, 56), 22);
            _subtitles = UiKit.MakeButton(root, "", new Vector2(460, 232), new Vector2(430, 56), 22);
            _hand = UiKit.MakeButton(root, "", new Vector2(10, 300), new Vector2(430, 56), 22);
            _seated = UiKit.MakeButton(root, "", new Vector2(460, 300), new Vector2(430, 56), 22);
            _reset = UiKit.MakeButton(root, "Reset for the next person", new Vector2(10, 368), new Vector2(430, 50), 22);
            _summary = UiKit.Label(root, "Settings summary", "", new Vector2(14, 430), new Vector2(872, 90), 22, TextAlignmentOptions.TopLeft);
            UiKit.Label(root, "Settings note", "Status is always shown in words and symbols as well as colour.", new Vector2(14, 530), new Vector2(872, 30), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            _size.onClick.AddListener(AccessibilitySettings.NextUiScale);
            _subtitles.onClick.AddListener(AccessibilitySettings.NextSubtitleScale);
            _hand.onClick.AddListener(() => AccessibilitySettings.SetLeftHanded(!AccessibilitySettings.LeftHanded));
            _seated.onClick.AddListener(() =>
            {
                var controls = Controls;
                if (controls != null) controls.SetSeated(!controls.Seated);   // refused while a room change or teleport is running
                Refresh();
            });
            _reset.onClick.AddListener(AccessibilitySettings.Reset);
            Refresh();
        }

        private void OnEnable() { AccessibilitySettings.Changed += Refresh; Refresh(); }
        private void OnDisable() => AccessibilitySettings.Changed -= Refresh;

        private float _next;
        private void Update()
        {
            if (Time.unscaledTime < _next) return;   // seated can change from the comfort panel or the height button
            _next = Time.unscaledTime + .5f;
            Refresh();
        }

        public void Refresh()
        {
            if (_summary == null) return;
            Label(_size, "Panel size: " + Mathf.RoundToInt(AccessibilitySettings.UiScale * 100) + " %");
            Label(_subtitles, "Subtitle size: " + Mathf.RoundToInt(AccessibilitySettings.SubtitleScale * 100) + " %");
            Label(_hand, AccessibilitySettings.LeftHanded ? "Left-handed: right stick walks" : "Right-handed: left stick walks");
            var controls = Controls;
            Label(_seated, controls == null ? "Seated play: no headset rig" : controls.Seated ? "Seated (press for standing)" : "Standing (press for seated)");
            _seated.interactable = controls != null;
            string summary = AccessibilitySettings.Describe() + "  |  " + (controls == null ? "no rig" : controls.Seated ? "Seated" : "Standing");
            if (_summary.text != summary) _summary.text = summary;
        }

        private static ProcSense.VR.XR.ComfortControls Controls => FindAnyObjectByType<ProcSense.VR.XR.ComfortControls>();

        private static void Label(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label.text != text) label.text = text;
        }
    }
}
