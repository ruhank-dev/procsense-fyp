using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace ProcSense.VR.XR
{
    /// <summary>
    /// The Quest render tier's run-time settings (P3-PRF-05/06/13, owner decision D7, 7 November 2026 UTC).
    /// The tier itself is the Android quality level's own pipeline asset (Assets/Settings/URP_Quest_Android.asset: renderer with intermediate texture Auto, so URP renders straight into the
    /// OpenXR eye buffer and fixed foveated rendering can apply); desktop keeps URP_Quest unchanged. This class owns the two values a build may set at run time:
    /// the foveation level (0 = off, 1 = strongest; the OpenXR Foveated Rendering feature is enabled for Android) and the eye-texture scale (1.0 = the runtime default, 1440 x 1584 per eye on Quest 2).
    /// Since 14 Nov 2026 the Quest profile (QuestPerformanceProfile) is the production default and the single source of both values (15 Nov 2026 sharpness revision: foveation Medium,
    /// eye buffer 1.0 drawn at 0.9 and adjusted by dynamic resolution between 0.85 and 1.0).
    /// The diagnostic build (PROCSENSE_PERF_DIAG) changes both while running.
    /// </summary>
    public static class QuestRenderTier
    {
        /// <summary>Production foveation level (the profile's value; one source, so the two can never disagree).</summary>
        public const float DefaultFoveation = ProcSense.VR.Views.QuestPerformanceProfile.Foveation;

        /// <summary>Production eye-texture allocation scale (the profile's value; 1.0 = the runtime default resolution).</summary>
        public const float DefaultEyeScale = ProcSense.VR.Views.QuestPerformanceProfile.EyeBufferScale;

        private static readonly List<XRDisplaySubsystem> Displays = new List<XRDisplaySubsystem>();

        /// <summary>Foveation level actually requested last (for the diagnostic readout).</summary>
        public static float Foveation { get; private set; } = DefaultFoveation;

        /// <summary>Clamps a foveation request to the range the XR display accepts.</summary>
        public static float ClampFoveation(float level) => Mathf.Clamp01(level);

        /// <summary>Clamps an eye-texture scale to a range that keeps clinical text readable and memory bounded.</summary>
        public static float ClampEyeScale(float scale) => Mathf.Clamp(scale, 0.7f, 1.3f);

        /// <summary>Sets the fixed foveation level on every running XR display. Returns false when no XR display runs (Editor without a headset, desktop window).</summary>
        public static bool SetFoveation(float level)
        {
            Foveation = ClampFoveation(level);
            SubsystemManager.GetSubsystems(Displays);
            bool any = false;
            foreach (var d in Displays)
            {
                if (!d.running) continue;
                d.foveatedRenderingLevel = Foveation;
                d.foveatedRenderingFlags = XRDisplaySubsystem.FoveatedRenderingFlags.None;   // fixed foveation: Quest 2 has no eye tracking
                any = true;
            }
            return any;
        }

        /// <summary>Sets the eye-texture scale (re-allocates the eye buffers; call between benchmark runs, not every frame).</summary>
        public static void SetEyeScale(float scale) => XRSettings.eyeTextureResolutionScale = ClampEyeScale(scale);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyDefaults()
        {
            if (Application.platform != RuntimePlatform.Android) return;
            SetFoveation(DefaultFoveation);
        }
    }
}
