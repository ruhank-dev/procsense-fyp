using UnityEngine;

namespace ProcSense.VR.XR
{
    /// <summary>Local Editor preferences only; standalone players keep their normal XR setup.</summary>
    public static class EditorInputSettings
    {
        public enum InputMode { Automatic, Headset, KeyboardMouse }
        public static string PreferenceKey => "ProcSense.PlayInput." + Application.dataPath;
        public static InputMode Mode
        {
            get
            {
#if UNITY_EDITOR
                return (InputMode)UnityEditor.EditorPrefs.GetInt(PreferenceKey, 0);
#else
                return InputMode.Headset;
#endif
            }
        }
        public static bool ThumbstickMovement
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorPrefs.GetBool(PreferenceKey + ".Thumbsticks", true);
#else
                return false;
#endif
            }
        }
        public static bool ShouldUseSimulator => Application.isEditor &&
            (Mode == InputMode.KeyboardMouse || (Mode == InputMode.Automatic && !UnityEngine.XR.XRSettings.isDeviceActive));
    }
}
