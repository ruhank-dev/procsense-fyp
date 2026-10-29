using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// On every world canvas UiKit makes: keeps the canvas at its designed size times the viewer's panel size (and the subtitle size for a subtitle), changing at once
    /// when the setting changes. It also carries how far away the panel is meant to be read, so the readability check can work out the size of its text in the headset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccessibleCanvas : MonoBehaviour
    {
        /// <summary>Metres per canvas pixel as designed (before the viewer's size).</summary>
        public float BaseScale = .001f;
        /// <summary>How far the panel is meant to be read from: 0.5 m held in the hand, 0.6 m at a desk, 1 m at the bedside, more on a wall.</summary>
        public float ViewingDistance = 1f;
        public bool Subtitle;

        /// <summary>Reading distances by canvas name (a panel not named here is read from 1 m), so the panels need not say it themselves.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, float> Distances = new System.Collections.Generic.Dictionary<string, float>
        {
            // held in the hand
            ["Tablet"] = .5f, ["Phone"] = .5f, ["Folder document page"] = .45f, ["Page buttons"] = .45f, ["Printout text"] = .45f, ["Specimen label"] = .3f,
            ["Wristband card"] = .5f, ["Notes"] = .5f,
            // at a desk or a trolley
            ["EMR shell"] = .6f, ["EMR"] = .6f, ["Results"] = .6f, ["Diagnostics preview"] = .6f, ["Folder page"] = .6f, ["Spoken order"] = .6f, ["Push to talk"] = .6f,
            ["Main menu"] = .6f, ["ModeChooser"] = .6f, ["Diagnostic wait status"] = .6f,
            // on a wall
            ["WHO board"] = 1.5f, ["Debrief"] = 1f, ["Door panel"] = 1.2f, ["Diagnostic information"] = 1.5f,
        };

        /// <summary>Makes the component for a canvas UiKit has just made.</summary>
        public static AccessibleCanvas Attach(Canvas canvas, float metresPerPixel)
        {
            var a = canvas.gameObject.AddComponent<AccessibleCanvas>();
            a.BaseScale = metresPerPixel;
            a.ViewingDistance = Distances.TryGetValue(canvas.name, out var d) ? d : 1f;
            a.Subtitle = canvas.name.EndsWith("ubtitles") || canvas.name.EndsWith(" subtitle");
            a.Apply();
            return a;
        }

        /// <summary>The smallest font size that is 24 headset pixels tall at this panel's reading distance at its designed scale (UiKit raises smaller labels to it).</summary>
        public float MinFontSize => Mathf.Ceil(Readability.MinHeadsetPixels / (Mathf.Atan2(BaseScale, Mathf.Max(.05f, ViewingDistance)) * Mathf.Rad2Deg * Readability.PixelsPerDegree));

        public float Scale => BaseScale * AccessibilitySettings.UiScale * (Subtitle ? AccessibilitySettings.SubtitleScale : 1f);

        // subscribed for the canvas's whole life, so a hidden panel (subtitles between lines) is the right size when it next shows
        private void Awake() => AccessibilitySettings.Changed += Apply;
        private void OnDestroy() => AccessibilitySettings.Changed -= Apply;
        private void OnEnable() => Apply();

        public void Apply()
        {
            if (this == null) return;
            transform.localScale = Vector3.one * Scale;
        }
    }
}
