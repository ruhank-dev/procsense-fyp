using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Extensions.PerformanceSettings;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Quest 2 run-time settings that the editor cannot express (P3-17, 9 Nov 2026), installed by itself in the Android player only; a no-op in the Editor and on desktop.
    /// (1) Refresh rate: Unity 6000.5 exposes only <c>XRDisplaySubsystem.TryGetDisplayRefreshRate</c> (no request or supported-rates call, and the OpenXR 1.18 plugin does not wrap
    /// XR_FB_display_refresh_rate), so the 72 Hz Quest 2 default cannot be requested explicitly. This reads the rate the runtime actually runs at and logs it, and warns when it is not 72 Hz.
    /// (2) CPU and GPU levels: the OpenXR plugin exposes <c>XrPerformanceSettingsFeature.SetPerformanceLevelHint</c> (XR_EXT_performance_settings), which needs the "XR Performance Settings"
    /// feature enabled for Android (it is). Both domains are hinted <see cref="Level"/> (SustainedHigh: the highest level the headset can hold without throttling; Boost is never used).
    /// Whether the headset keeps that level under heat is a Quest-only check (DC-13). Results go to the log with a UTC time.
    /// </summary>
    public sealed class QuestRuntimeSettings : MonoBehaviour
    {
        public const float TargetRefreshRate = 72f;

        /// <summary>Level requested for both CPU and GPU. Changeable for the diagnostic build.</summary>
        public static PerformanceLevelHint Level = PerformanceLevelHint.SustainedHigh;

        /// <summary>Master switch for the level request (the diagnostic build can turn it off to compare).</summary>
        public static bool RequestLevels = true;

        /// <summary>Last observed refresh rate, 0 before a display reports one.</summary>
        public static float ObservedRefreshRate { get; private set; }
        public static bool LevelCpuAccepted { get; private set; }
        public static bool LevelGpuAccepted { get; private set; }
        public static bool Done { get; private set; }

        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();
        private float _next; private int _tries;

        /// <summary>True when the observed rate is the 72 Hz target (within 0.5 Hz).</summary>
        public static bool IsTargetRate(float hz) => Mathf.Abs(hz - TargetRefreshRate) < 0.5f;

        /// <summary>Only the Android player installs the component.</summary>
        public static bool ShouldInstall(RuntimePlatform platform, bool isEditor) => platform == RuntimePlatform.Android && !isEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!ShouldInstall(Application.platform, Application.isEditor)) return;
            if (FindAnyObjectByType<QuestRuntimeSettings>() != null) return;
            var go = new GameObject("QuestRuntimeSettings") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go); go.AddComponent<QuestRuntimeSettings>();
        }

        private void Update()
        {
            if (Done || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f; _tries++;
            if (TryApply()) Done = true;
            else if (_tries >= 30) { Done = true; Debug.LogWarning(Stamp() + " QuestRuntimeSettings: XR display never reported a refresh rate in 30 s; levels cpu " + LevelCpuAccepted + " gpu " + LevelGpuAccepted); }
        }

        /// <summary>One attempt: read the refresh rate and send the level hints. Returns true when the display reported a rate (the session exists).</summary>
        public static bool TryApply()
        {
            SubsystemManager.GetSubsystems(Displays);
            foreach (var d in Displays)
            {
                if (!d.running || !d.TryGetDisplayRefreshRate(out float hz)) continue;
                ObservedRefreshRate = hz;
                if (RequestLevels)
                {
                    LevelCpuAccepted = SafeHint(PerformanceDomain.Cpu);
                    LevelGpuAccepted = SafeHint(PerformanceDomain.Gpu);
                }
                var msg = Stamp() + " QuestRuntimeSettings: refresh " + hz.ToString("F1") + " Hz" + (IsTargetRate(hz) ? " (target 72)" : " (NOT the 72 Hz target)")
                    + "; level hint " + (RequestLevels ? Level.ToString() + " cpu " + LevelCpuAccepted + " gpu " + LevelGpuAccepted : "not requested");
                if (IsTargetRate(hz)) Debug.Log(msg); else Debug.LogWarning(msg);
                return true;
            }
            return false;
        }

        private static bool SafeHint(PerformanceDomain domain)
        {
            try { return XrPerformanceSettingsFeature.SetPerformanceLevelHint(domain, Level); }
            catch (System.Exception e) { Debug.LogWarning(Stamp() + " QuestRuntimeSettings: level hint failed: " + e.Message); return false; }
        }

        private static string Stamp() => System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + "Z";
    }
}
