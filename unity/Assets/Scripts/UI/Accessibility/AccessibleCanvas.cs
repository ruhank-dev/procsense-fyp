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
        /// <summary>A panel with clinical text (<see cref="ClinicalText"/>): sharp SDF material, and dynamic resolution held at 0.8 while it is read.</summary>
        public bool Clinical;

        /// <summary>Reading distances by canvas name (a panel not named here is read from 1 m), so the panels need not say it themselves.</summary>
        public static readonly System.Collections.Generic.Dictionary<string, float> Distances = new System.Collections.Generic.Dictionary<string, float>
        {
            // held in the hand
            ["Tablet"] = .5f, ["Phone"] = .5f, ["Folder document page"] = .45f, ["Page buttons"] = .45f, ["Printout text"] = .45f, ["Specimen label"] = .3f,
            ["Wristband card"] = .5f, ["Notes"] = .5f,
            // at a desk or a trolley
            ["EMR shell"] = .6f, ["EMR"] = .6f, ["Results"] = .6f, ["Diagnostics preview"] = .6f, ["Folder page"] = .6f, ["Spoken order"] = .6f, ["Push to talk"] = .6f,
            ["Main menu"] = .6f, ["ModeChooser"] = .6f, ["Diagnostic wait status"] = .6f,
            // the game-flow menu floats MenuFlowInstaller.Distance (1.5 m) in front of the head (was read as 1 m before 14 Nov 2026 UTC)
            ["Game flow"] = 1.5f,
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
            a.Clinical = ClinicalText.IsClinical(canvas.name);
            if (a.Clinical && a.isActiveAndEnabled) ClinicalText.Register(a);
            a.Apply();
            return a;
        }

        /// <summary>The smallest font size that is 24 headset pixels tall at this panel's reading distance at its designed scale (UiKit raises smaller labels to it).</summary>
        public float MinFontSize => Mathf.Ceil(Readability.MinHeadsetPixels / (Mathf.Atan2(BaseScale, Mathf.Max(.05f, ViewingDistance)) * Mathf.Rad2Deg * Readability.PixelsPerDegree));

        public float Scale => BaseScale * AccessibilitySettings.UiScale * (Subtitle ? AccessibilitySettings.SubtitleScale : 1f);

        // subscribed for the canvas's whole life, so a hidden panel (subtitles between lines) is the right size when it next shows
        private void Awake() => AccessibilitySettings.Changed += Apply;
        private void OnDestroy() { AccessibilitySettings.Changed -= Apply; ClinicalText.Unregister(this); }
        private void OnEnable() { if (Clinical) ClinicalText.Register(this); Apply(); }
        private void OnDisable() => ClinicalText.Unregister(this);

        public void Apply()
        {
            if (this == null) return;
            transform.localScale = Vector3.one * Scale;
            if (Subtitle) ApplySubtitleChoices();
        }

        private float _plateAlpha = -1f;

        /// <summary>P3-SET-06: a subtitle canvas shows only with subtitles on, and its dark plate (the "Background" panel UiKit makes) only with the plate on. Failure phrases of voice
        /// mode are not subtitles (they show on the push-to-talk panel), so they stay visible either way.</summary>
        private void ApplySubtitleChoices()
        {
            var s = ProcSense.VR.Settings.GameSettings.Current;
            var canvas = GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = s.subtitles;
            var plate = transform.Find("Background");
            var image = plate != null ? plate.GetComponent<UnityEngine.UI.Image>() : null;
            if (image == null) return;
            if (_plateAlpha < 0) _plateAlpha = image.color.a;
            var c = image.color;
            c.a = s.subtitleBackground ? _plateAlpha : 0f;
            image.color = c;
        }
    }
}
