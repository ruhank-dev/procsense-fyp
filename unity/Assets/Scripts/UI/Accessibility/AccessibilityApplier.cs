using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;

namespace ProcSense.VR.UI.Accessibility
{
    /// <summary>
    /// Applies the hand choice to the rig (p0-vr-interaction-ui-s10): when the viewer changes hands, every rig is rewired so the other stick walks. One for the whole run, made at start.
    /// Only a change of hand rewires (rewiring re-enables movement, which a room change may have frozen).
    /// </summary>
    public sealed class AccessibilityApplier : MonoBehaviour
    {
        private bool _leftHanded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Make()
        {
            if (FindAnyObjectByType<AccessibilityApplier>() != null) return;
            var go = new GameObject("Accessibility");
            DontDestroyOnLoad(go);
            go.AddComponent<AccessibilityApplier>();
        }

        private void OnEnable() { _leftHanded = AccessibilitySettings.LeftHanded; AccessibilitySettings.Changed += OnChanged; }
        private void OnDisable() => AccessibilitySettings.Changed -= OnChanged;

        private void OnChanged()
        {
            if (AccessibilitySettings.LeftHanded == _leftHanded) return;
            _leftHanded = AccessibilitySettings.LeftHanded;
            Rewire();
        }

        /// <summary>Applies the hand choice to every rig in the scene.</summary>
        public static void Rewire()
        {
            foreach (var rig in FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
                RigLocomotionSettings.Apply(rig, EditorInputSettings.ThumbstickMovement, rig.GetComponentInParent<ProcSense.VR.Boot.BootLoader>() != null);
        }
    }
}
