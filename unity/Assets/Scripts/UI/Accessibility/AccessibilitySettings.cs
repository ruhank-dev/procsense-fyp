using System;
using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// The viewer's readability choices (p0-vr-interaction-ui-s10): the size of every panel, the size of the subtitles, and which hand walks and which turns.
    /// Kept on this headset with PlayerPrefs (a convenience for whoever wears it, nothing to do with the simulation or the score) and applied at once.
    /// Seated play is not kept: the rig detects it from the head height each time (ComfortControls), so a saved choice could only be stale.
    /// </summary>
    public static class AccessibilitySettings
    {
        public static readonly float[] UiScales = { 1f, 1.25f, 1.5f };
        public static readonly float[] SubtitleScales = { 1f, 1.3f, 1.6f };
        public const string UiScaleKey = "procsense.ui_scale", SubtitleScaleKey = "procsense.subtitle_scale", LeftHandedKey = "procsense.left_handed";

        /// <summary>Raised after any change (panels rescale, the rig rewires its hands).</summary>
        public static event Action Changed;

        public static float UiScale => UiScales[Index(UiScaleKey, UiScales.Length)];
        public static float SubtitleScale => SubtitleScales[Index(SubtitleScaleKey, SubtitleScales.Length)];
        public static bool LeftHanded => PlayerPrefs.GetInt(LeftHandedKey, 0) == 1;

        /// <summary>Steps the panel size through 100 %, 125 % and 150 %.</summary>
        public static void NextUiScale() => Set(UiScaleKey, (Index(UiScaleKey, UiScales.Length) + 1) % UiScales.Length);
        public static void NextSubtitleScale() => Set(SubtitleScaleKey, (Index(SubtitleScaleKey, SubtitleScales.Length) + 1) % SubtitleScales.Length);
        public static void SetLeftHanded(bool left) => Set(LeftHandedKey, left ? 1 : 0);

        /// <summary>Back to the defaults (tests, and a shared headset handed to the next person).</summary>
        public static void Reset()
        {
            foreach (var key in new[] { UiScaleKey, SubtitleScaleKey, LeftHandedKey }) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static string Describe() =>
            "Panel size " + Mathf.RoundToInt(UiScale * 100) + " %  |  Subtitles " + Mathf.RoundToInt(SubtitleScale * 100) + " %  |  " + (LeftHanded ? "Right stick walks, left stick turns" : "Left stick walks, right stick turns");

        private static int Index(string key, int count) => Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, count - 1);

        private static void Set(string key, int value)
        {
            if (PlayerPrefs.HasKey(key) && PlayerPrefs.GetInt(key) == value) return;
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
