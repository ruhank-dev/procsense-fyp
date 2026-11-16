using System;
using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// The viewer's readability choices (p0-vr-interaction-ui-s10): the size of every panel, the size of the subtitles, and which hand walks and which turns.
    /// Since P3-SET-01 they are fields of the one settings record (<see cref="GameSettings"/>); this class keeps the names the panels and the rig already use.
    /// Seated play is not kept: the rig detects it from the head height each time (ComfortControls), so a saved choice could only be stale.
    /// </summary>
    public static class AccessibilitySettings
    {
        public static float[] UiScales => SettingsData.PanelScales;
        public static float[] SubtitleScales => SettingsData.SubtitleScales;

        /// <summary>Raised after any settings change (panels rescale, the rig rewires its hands).</summary>
        public static event Action Changed
        {
            add { if (_listeners == null) GameSettings.Changed += Forward; _listeners += value; }
            remove { _listeners -= value; if (_listeners == null) GameSettings.Changed -= Forward; }
        }
        private static Action _listeners;
        private static void Forward(SettingsData _) => _listeners?.Invoke();

        public static float UiScale => GameSettings.PanelScale;
        public static float SubtitleScale => GameSettings.SubtitleScale;
        public static bool LeftHanded => GameSettings.Current.leftHanded;

        /// <summary>Steps the panel size through 100 %, 125 % and 150 %.</summary>
        public static void NextUiScale() => GameSettings.Update(s => s.panelSize = (s.panelSize + 1) % SettingsData.PanelScales.Length);
        public static void NextSubtitleScale() => GameSettings.Update(s => s.subtitleSize = (s.subtitleSize + 1) % SettingsData.SubtitleScales.Length);
        public static void SetLeftHanded(bool left) => GameSettings.Update(s => s.leftHanded = left);

        /// <summary>Back to the defaults (tests, and a shared headset handed to the next person).</summary>
        public static void Reset() => GameSettings.Reset();

        public static string Describe() =>
            "Panel size " + Mathf.RoundToInt(UiScale * 100) + " %  |  Subtitles " + Mathf.RoundToInt(SubtitleScale * 100) + " %  |  " + (LeftHanded ? "Right stick walks, left stick turns" : "Left stick walks, right stick turns");
    }
}
