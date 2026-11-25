using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ProcSense.VR.XR
{
    /// <summary>Shared runtime/Editor wiring; room and teleport services still own movement freezes.</summary>
    public static class RigLocomotionSettings
    {
        public static void Apply(XROrigin rig, bool sticks, bool login)
        {
            sticks &= !login;
            // the walking stick is the left one unless the viewer chose left-handed play (p0-vr-interaction-ui-s10)
            bool leftHanded = ProcSense.VR.UI.Accessibility.AccessibilitySettings.LeftHanded;
            foreach (var manager in rig.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                bool left = manager.name == (leftHanded ? "Right Controller" : "Left Controller");
                manager.smoothMotionEnabled = sticks && left;
                manager.smoothTurnEnabled = false;
                // Keep left-stick walking available while aiming at a screen;
                // the right controller retains UI scrolling mediation.
                manager.uiScrollingEnabled = !sticks || !left;
            }
            foreach (var move in rig.GetComponentsInChildren<DynamicMoveProvider>(true))
            {
                move.moveSpeed = 1.5f;
                move.enableFly = false;
                move.enableStrafe = true;
                move.leftHandMovementDirection = move.rightHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
                (leftHanded ? move.rightHandMoveInput : move.leftHandMoveInput).inputSourceMode = XRInputValueReader.InputSourceMode.InputActionReference;
                (leftHanded ? move.leftHandMoveInput : move.rightHandMoveInput).inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
                move.enabled = !login;
            }
            foreach (var turn in rig.GetComponentsInChildren<ContinuousTurnProvider>(true)) turn.enabled = false;
            foreach (var turn in rig.GetComponentsInChildren<SnapTurnProvider>(true))
            {
                // Reapplying controller wiring must preserve a trainee's comfort choice.
                if (turn.turnAmount != 30 && turn.turnAmount != 45) turn.turnAmount = 45;
                turn.enableTurnAround = false;
                turn.enabled = !login;
            }
            if (!login) ApplyComfort(rig, ProcSense.VR.Settings.GameSettings.Current, false);
        }

        /// <summary>
        /// The trainee's saved comfort choices (P3-SET-05) on the rig: smooth turning at the chosen speed instead of snap turning, continuous walking in addition to teleport,
        /// the snap angle (only when <paramref name="withAngle"/>, so a rig re-wire keeps an angle picked on the rig), and the vignette. Defaults change nothing.
        /// </summary>
        public static void ApplyComfort(XROrigin rig, ProcSense.VR.Settings.SettingsData s, bool withAngle)
        {
            bool smooth = s.turnStyle == ProcSense.VR.Settings.TurnStyle.Smooth;
            bool leftHanded = ProcSense.VR.UI.Accessibility.AccessibilitySettings.LeftHanded;
            foreach (var manager in rig.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                manager.smoothTurnEnabled = smooth;
                if (s.moveStyle == ProcSense.VR.Settings.MoveStyle.Continuous && manager.name == (leftHanded ? "Right Controller" : "Left Controller")) manager.smoothMotionEnabled = true;
            }
            foreach (var turn in rig.GetComponentsInChildren<ContinuousTurnProvider>(true)) { turn.turnSpeed = s.turnSpeed; turn.enabled = smooth; }
            foreach (var turn in rig.GetComponentsInChildren<SnapTurnProvider>(true)) { if (withAngle) turn.turnAmount = s.snapAngle; turn.enabled = !smooth; }
            foreach (var vignette in rig.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController>(true)) vignette.gameObject.SetActive(s.comfortVignette);
        }
    }
}
