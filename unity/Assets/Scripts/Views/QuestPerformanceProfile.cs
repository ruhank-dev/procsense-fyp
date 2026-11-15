using ProcSense.VR.XR;
using UnityEngine;
using UnityEngine.XR;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The one place that defines the aggressive Quest 2 performance profile (14 Nov 2026). The headset measured about 34 FPS, 96% GPU and 26.47 ms GPU time (72 Hz, foveation off, 4x MSAA,
    /// eye scale 1.0), so every setting here cuts pixel work. The Android player applies it by itself; desktop and the Editor are untouched (<see cref="Active"/> is false there).
    /// Static values (asset fields, not code): URP_Quest_Android MSAA 1, OpenXR Foveated Rendering with Subsampled Layout, Symmetric Projection, Multiview Render Regions (final pass), Optimize Buffer Discards.
    /// Run-time values (this class): eye buffer scale 0.9 allocated, drawn at 0.8 to start, dynamic resolution between 0.7 and 0.9 of the native size, fixed foveation High.
    /// Dynamic resolution moves <c>XRSettings.renderViewportScale</c> (no buffer re-allocation) from the GPU frame time with hysteresis; the eye buffer itself is allocated once.
    /// Whether the viewport scale is honoured by the Vulkan compositor path and how sharp the monitors stay at 0.8 are headset checks (DC-13).
    /// </summary>
    public static class QuestPerformanceProfile
    {
        /// <summary>Eye texture allocation scale (the largest size dynamic resolution may use).</summary>
        public const float EyeBufferScale = 0.9f;
        /// <summary>Effective render scale at start, as a fraction of the native eye buffer (1440 x 1584 per eye on Quest 2).</summary>
        public const float StartScale = 0.8f;
        /// <summary>The initial drawn scale (alias of <see cref="StartScale"/> under the name the smoke test and the diagnostics look for).</summary>
        public const float RenderScale = StartScale;
        public const float MinScale = 0.7f;
        public const float MaxScale = EyeBufferScale;
        /// <summary>Foveation level (0 off .. 1 high). Quest 2 has no eye tracking, so this is fixed foveation.</summary>
        public const float Foveation = 1f;
        /// <summary>MSAA samples the Android URP asset is authored with (documentation only: the value lives in URP_Quest_Android.asset).</summary>
        public const int Msaa = 1;

        /// <summary>GPU frame time above which the scale is lowered, below which it may be raised (ms). 72 Hz = 13.89 ms.</summary>
        public const float HighGpuMs = 12.5f, LowGpuMs = 10.0f;

        /// <summary>Dynamic resolution switch (the diagnostic presets turn it off to hold a fixed scale).</summary>
        public static bool DynamicResolution = true;

        /// <summary>Scale currently requested (effective, fraction of native).</summary>
        public static float CurrentScale { get; private set; } = StartScale;

        /// <summary>True while a clinical text panel is being read, so dynamic resolution holds at least <see cref="UI.Accessibility.ClinicalText.FloorScale"/> (diagnostics).</summary>
        public static bool TextFloorActive { get; private set; }

        /// <summary>True only in the Android player.</summary>
        public static bool Active => Application.platform == RuntimePlatform.Android && !Application.isEditor;

        /// <summary>The profile's value for the preset system: neutral (off / 1.0) everywhere except the Android player.</summary>
        public static float ProfileFoveation => Active ? Foveation : 0f;
        public static float ProfileEyeScale => Active ? EyeBufferScale : 1f;

        /// <summary>Viewport scale (fraction of the allocated eye buffer) that gives an effective scale.</summary>
        public static float ViewportFor(float effective) => Mathf.Clamp(effective / EyeBufferScale, 0.1f, 1f);

        /// <summary>Applies the profile. Returns false while no XR display runs yet (the driver retries).</summary>
        public static bool Apply()
        {
            QuestRenderTier.SetEyeScale(EyeBufferScale);
            bool fov = QuestRenderTier.SetFoveation(Foveation);
            SetScale(StartScale);
            return fov;
        }

        /// <summary>Sets the effective scale (clamped to the profile range) through the viewport scale.</summary>
        public static void SetScale(float effective)
        {
            CurrentScale = Mathf.Clamp(effective, MinScale, MaxScale);
            XRSettings.renderViewportScale = ViewportFor(CurrentScale);
        }

        /// <summary>
        /// Application SpaceWarp on or off at run time. Returns false when the feature is not in the build (it is OFF in the production build: it needs motion vectors and costs GPU, and it is
        /// synthesised frames, not native 72 FPS) or the runtime refuses. A separate build with the OpenXR "Application SpaceWarp" feature enabled is needed to use it.
        /// </summary>
        public static bool TrySetSpaceWarp(bool enabled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { return UnityEngine.XR.OpenXR.Features.SpaceWarpFeature.SetSpaceWarp(enabled); } catch (System.Exception) { return false; }
#else
            return false;
#endif
        }

        /// <summary>Dynamic resolution rule. Pure and stateful so it can be tested without a headset.</summary>
        public sealed class Controller
        {
            public int OverSamples = 6, UnderSamples = 120, Cooldown = 30;
            public float StepDown = 0.03f, StepUp = 0.02f;
            private int _over, _under, _cool;

            /// <summary>Feeds one GPU frame time (ms) and the current effective scale; returns the scale to use next. <paramref name="floor"/> is the lowest scale allowed now
            /// (<see cref="MinScale"/>, or <see cref="UI.Accessibility.ClinicalText.FloorScale"/> while a clinical text panel is being read); a scale below it is raised at once.</summary>
            public float Step(float gpuMs, float scale, float floor = MinScale)
            {
                floor = Mathf.Clamp(floor, MinScale, MaxScale);
                if (scale < floor) { _over = 0; _under = 0; return floor; }
                if (_cool > 0) { _cool--; return scale; }
                if (gpuMs > HighGpuMs) { _over++; _under = 0; }
                else if (gpuMs < LowGpuMs && gpuMs > 0f) { _under++; _over = 0; }
                else { _over = 0; _under = 0; }
                if (_over >= OverSamples) { _over = 0; _cool = Cooldown; return Mathf.Max(floor, scale - StepDown); }
                if (_under >= UnderSamples) { _under = 0; _cool = Cooldown; return Mathf.Min(MaxScale, scale + StepUp); }
                return scale;
            }
        }

        /// <summary>Android-only driver: applies the profile once the XR display runs, then runs dynamic resolution from the GPU time.</summary>
        public sealed class Driver : MonoBehaviour
        {
            private readonly Controller _ctl = new Controller();
            private bool _applied; private float _next; private int _tries;
            private readonly FrameTiming[] _timing = new FrameTiming[1];

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            private static void Install()
            {
                if (!Active || FindAnyObjectByType<Driver>() != null) return;
                var go = new GameObject("QuestPerformanceProfile") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go); go.AddComponent<Driver>();
            }

            private void Update()
            {
                if (!_applied)
                {
                    if (Time.unscaledTime < _next) return;
                    _next = Time.unscaledTime + 1f; _tries++;
                    if (Apply()) { _applied = true; Debug.Log(Stamp() + " QuestPerformanceProfile applied: eye buffer " + EyeBufferScale + ", start scale " + StartScale + ", foveation " + Foveation + ", dynamic resolution " + (DynamicResolution ? "on" : "off")); }
                    else if (_tries >= 30) { _applied = true; Debug.LogWarning(Stamp() + " QuestPerformanceProfile: XR display never ran in 30 s; run-time scale and foveation not applied."); }
                    return;
                }
                if (!DynamicResolution) return;
                float floor = TextFloor(Camera.main);
                float gpu = GpuMs();
                if (gpu <= 0f && CurrentScale >= floor) return;
                float next = _ctl.Step(gpu, CurrentScale, floor);
                if (!Mathf.Approximately(next, CurrentScale)) SetScale(next);
            }

            /// <summary>The lowest scale allowed this frame: the clinical text floor while such a panel is in front of the head, else the profile minimum.</summary>
            internal static float TextFloor(Camera cam)
            {
                TextFloorActive = cam != null && ProcSense.VR.UI.Accessibility.ClinicalText.Reading(cam.transform.position, cam.transform.forward);
                return TextFloorActive ? ProcSense.VR.UI.Accessibility.ClinicalText.FloorScale : MinScale;
            }

            private float GpuMs()
            {
                FrameTimingManager.CaptureFrameTimings();
                return FrameTimingManager.GetLatestTimings(1, _timing) > 0 ? (float)_timing[0].gpuFrameTime : 0f;
            }

            private static string Stamp() => System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
        }
    }
}
