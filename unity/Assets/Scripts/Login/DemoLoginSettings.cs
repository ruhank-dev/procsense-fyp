using UnityEngine;

namespace ProcSense.VR.Login
{
    [CreateAssetMenu(menuName = "ProcSense/Demo login settings")]
    public sealed class DemoLoginSettings : ScriptableObject
    {
        public bool ShowOnLaunch = true;
        public bool BypassInDevelopmentBuild;
        public const string DemoEmail = "demo@procsense.app";
        public const string DemoPassword = "ProcSenseDemo!";
        public const string BypassPreference = "ProcSense.Login.BypassInEditor";
        public const string PreviewPreference = "ProcSense.Login.PreviewOnce";
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        public static bool? LoginRequiredOverride;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOverride() => LoginRequiredOverride = null;
#endif
        public bool ShouldShow()
        {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
            if (LoginRequiredOverride.HasValue) return LoginRequiredOverride.Value;
#endif
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(PreviewPreference, false))
            {
                UnityEditor.EditorPrefs.DeleteKey(PreviewPreference);
                return true;
            }
            if (UnityEditor.EditorPrefs.GetBool(BypassPreference, false)) return false;
#endif
            return ShowOnLaunch && !(Debug.isDebugBuild && BypassInDevelopmentBuild);
        }
    }
}
