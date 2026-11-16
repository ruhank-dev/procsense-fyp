using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProcSense.VR.UI.Theme
{
    /// <summary>
    /// P3-UIX-09, desktop only: real blur of the room behind the glass panels. Off the headset the glass shader's PS_GLASS_BLUR variant is turned on and the main camera asks URP
    /// for its opaque texture (a per-camera override, so the shared pipeline assets and the Quest settings are untouched). On Android it is never on: this class refuses, and the
    /// Android build strips the variant altogether (GlassBlurStripper in the editor), so the headset never pays for the opaque copy. High contrast turns it off (solid plates).
    /// </summary>
    public static class GlassBlur
    {
        public const string Keyword = "PS_GLASS_BLUR";

        /// <summary>True where the blur may run: never on Android (the Quest), and never on a phone.</summary>
        public static bool Supported
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return false;
#else
                return Application.platform != RuntimePlatform.Android && !Application.isMobilePlatform;
#endif
            }
        }

        /// <summary>True when the blur is wanted now: supported, and high contrast is off.</summary>
        public static bool Wanted => Supported && !ProcSense.VR.Settings.GameSettings.Current.highContrast;

        public static bool On { get; private set; }
        private static Camera _camera;

        /// <summary>Turns the blur on or off for the current settings and main camera (the glass panels call it as they appear; cheap when nothing changed).</summary>
        public static void Apply() => Apply(Camera.main);

        public static void Apply(Camera cam)
        {
            bool want = Wanted;
            if (want == On && (cam == _camera || !want)) return;
            if (want) Shader.EnableKeyword(Keyword); else Shader.DisableKeyword(Keyword);
            if (_camera != null && _camera != cam) Request(_camera, false);
            if (cam != null) Request(cam, want);
            _camera = want ? cam : null;
            On = want;
        }

        private static void Request(Camera cam, bool on)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) { if (!on) return; data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>(); }
            data.requiresColorOption = on ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
        }

        /// <summary>Back to off (tests).</summary>
        public static void Reset()
        {
            Shader.DisableKeyword(Keyword);
            if (_camera != null) Request(_camera, false);
            _camera = null;
            On = false;
        }
    }
}
