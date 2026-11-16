using ProcSense.VR.XR;
using UnityEngine;
using UnityEngine.XR;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The one place that defines the Quest 2 performance profile (14 Nov 2026, sharpness revision 15 Nov 2026 UTC). The headset measured about 34 FPS, 96% GPU and 26.47 ms GPU time (72 Hz, foveation off,
    /// 4x MSAA, eye scale 1.0) before the overhaul. The first overhaul profile (eye buffer 0.9 drawn at 0.8, dynamic resolution 0.7-0.9, foveation High) was faster but the owner's headset footage looked grainy,
    /// blurry and jagged: 0.8 of the runtime's 1440 x 1584 is 1152 x 1267 per eye (40% of the 1832 x 1920 panel's pixels), without MSAA, with High foveation in the periphery, and in the release APK the scale
    /// never rose because Frame Timing Stats were off (no GPU time). The sharpness revision keeps the baked lighting, no realtime shadows, culling, ASTC, LODs and Vulkan/OpenXR, and changes only:
    /// eye buffer allocated at 1.0 (the runtime default), drawn at 0.9 to start, dynamic resolution between 0.85 and 1.0, fixed foveation Medium (0.5) with Subsampled Layout kept.
    /// This class is the single source of the run-time scale and foveation (QuestRenderTier's defaults are these constants; URP renderScale stays 1.0 so nothing multiplies twice).
    /// Static values (asset fields, not code): URP_Quest_Android MSAA 1, OpenXR Foveated Rendering with Subsampled Layout, Symmetric Projection, Multiview Render Regions (final pass), Optimize Buffer Discards.
    /// Dynamic resolution moves <c>XRSettings.renderViewportScale</c> (no buffer re-allocation) from the GPU frame time with hysteresis. FPS, GPU ms and whether the Vulkan compositor honours the viewport
    /// scale are headset checks (DC-13); nothing here is measured on a headset.
    /// </summary>
    public static class QuestPerformanceProfile
    {
        /// <summary>Eye texture allocation scale (the largest size dynamic resolution may use; 1.0 = the runtime's recommended 1440 x 1584 per eye on Quest 2).</summary>
        public const float EyeBufferScale = 1f;
        /// <summary>Effective render scale at start, as a fraction of the runtime's recommended eye buffer.</summary>
        public const float StartScale = 0.9f;
        /// <summary>The initial drawn scale (alias of <see cref="StartScale"/> under the name the smoke test and the diagnostics look for).</summary>
        public const float RenderScale = StartScale;
        public const float MinScale = 0.85f;
        public const float MaxScale = EyeBufferScale;
        /// <summary>Foveation level (0 off .. 1 high). Medium (0.5) since 15 Nov 2026; Quest 2 has no eye tracking, so this is fixed foveation.</summary>
        public const float Foveation = 0.5f;
        /// <summary>MSAA samples the Android URP asset is authored with (documentation only: the value lives in URP_Quest_Android.asset; the diagnostic build can switch 2x at run time).</summary>
        public const int Msaa = 1;

        /// <summary>GPU frame time above which the scale is lowered, below which it may be raised (ms). 72 Hz = 13.89 ms.</summary>
        public const float HighGpuMs = 12.5f, LowGpuMs = 10.0f;

        /// <summary>Range dynamic resolution may use now. Production keeps <see cref="MinScale"/>..<see cref="MaxScale"/>; only the diagnostic presets change it (and put it back).</summary>
        public static float RangeMin = MinScale, RangeMax = MaxScale;

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

        /// <summary>Sets the effective scale (clamped to the current range) through the viewport scale of the eye buffer that is actually allocated.</summary>
        public static void SetScale(float effective)
        {
            CurrentScale = Mathf.Clamp(effective, RangeMin, RangeMax);
            float alloc = XRSettings.eyeTextureResolutionScale;
            XRSettings.renderViewportScale = alloc > 0f ? Mathf.Clamp(CurrentScale / alloc, 0.1f, 1f) : ViewportFor(CurrentScale);
        }

        /// <summary>Pixels per eye actually drawn: the allocated eye texture times the viewport scale (what the runtime reports, not the constants). Zero without an XR display.</summary>
        public static Vector2Int EffectiveEyePixels() => EffectiveEyePixels(XRSettings.eyeTextureWidth, XRSettings.eyeTextureHeight, XRSettings.renderViewportScale);

        public static Vector2Int EffectiveEyePixels(int allocatedWidth, int allocatedHeight, float viewportScale)
            => new Vector2Int(Mathf.RoundToInt(allocatedWidth * viewportScale), Mathf.RoundToInt(allocatedHeight * viewportScale));

        /// <summary>One line with the effective (run-time) values, for the device log and the diagnostic readout.</summary>
        public static string Describe()
        {
            var px = EffectiveEyePixels();
            return "eye texture " + XRSettings.eyeTextureWidth + "x" + XRSettings.eyeTextureHeight + " (scale " + F(XRSettings.eyeTextureResolutionScale) + "), viewport " + XRSettings.renderViewportScale.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)
                + ", drawn " + px.x + "x" + px.y + " per eye, effective scale " + F(CurrentScale) + " (range " + F(RangeMin) + "-" + F(RangeMax) + ", dynamic " + (DynamicResolution ? "on" : "off")
                + "), foveation " + F(QuestRenderTier.Foveation) + ", MSAA " + PipelineMsaa() + ", frame timing " + FrameTimingManager.IsFeatureEnabled();
        }

        private static string F(float v) => v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        private static int PipelineMsaa() => UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset u ? u.msaaSampleCount : QualitySettings.antiAliasing;

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
                float max = RangeMax;
                floor = Mathf.Clamp(floor, RangeMin, max);
                if (scale < floor) { _over = 0; _under = 0; return floor; }
                if (_cool > 0) { _cool--; return scale; }
                if (gpuMs > HighGpuMs) { _over++; _under = 0; }
                else if (gpuMs < LowGpuMs && gpuMs > 0f) { _under++; _over = 0; }
                else { _over = 0; _under = 0; }
                if (_over >= OverSamples) { _over = 0; _cool = Cooldown; return Mathf.Max(floor, scale - StepDown); }
                if (_under >= UnderSamples) { _under = 0; _cool = Cooldown; return Mathf.Min(max, scale + StepUp); }
                return scale;
            }
        }

        /// <summary>Android-only driver: applies the profile once the XR display runs, then runs dynamic resolution from the GPU time.</summary>
        public sealed class Driver : MonoBehaviour
        {
            private readonly Controller _ctl = new Controller();
            private bool _applied, _noGpuLogged; private float _next, _noGpuSince = -1f, _lastLog = -100f; private int _tries;
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
                    if (Apply()) { _applied = true; Debug.Log(Stamp() + " QuestPerformanceProfile applied (read back from XRSettings): " + Describe()); }
                    else if (_tries >= 30) { _applied = true; Debug.LogWarning(Stamp() + " QuestPerformanceProfile: XR display never ran in 30 s; run-time scale and foveation not applied."); }
                    return;
                }
                if (!DynamicResolution) return;
                float floor = TextFloor(Camera.main);
                float gpu = GpuMs();
                if (gpu <= 0f) NoGpuTime(); else _noGpuSince = -1f;
                if (gpu <= 0f && CurrentScale >= floor) return;
                float next = _ctl.Step(gpu, CurrentScale, floor);
                if (Mathf.Approximately(next, CurrentScale)) return;
                SetScale(next);
                if (Time.unscaledTime - _lastLog > 10f) { _lastLog = Time.unscaledTime; Debug.Log(Stamp() + " QuestPerformanceProfile dynamic resolution (GPU " + gpu.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms): " + Describe()); }
            }

            /// <summary>Logs once when FrameTimingManager gives no GPU time for 5 s (Frame Timing Stats off or unsupported): the scale then holds where it is.</summary>
            private void NoGpuTime()
            {
                if (_noGpuLogged) return;
                if (_noGpuSince < 0f) { _noGpuSince = Time.unscaledTime; return; }
                if (Time.unscaledTime - _noGpuSince < 5f) return;
                _noGpuLogged = true;
                Debug.LogWarning(Stamp() + " QuestPerformanceProfile: FrameTimingManager returned no GPU time for 5 s; dynamic resolution holds: " + Describe());
            }

            /// <summary>The lowest scale allowed this frame: the clinical text floor while such a panel is in front of the head, else the current range minimum.</summary>
            internal static float TextFloor(Camera cam)
            {
                TextFloorActive = cam != null && ProcSense.VR.UI.Accessibility.ClinicalText.Reading(cam.transform.position, cam.transform.forward);
                return TextFloorActive ? ProcSense.VR.UI.Accessibility.ClinicalText.FloorScale : RangeMin;
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
