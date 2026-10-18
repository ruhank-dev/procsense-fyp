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
            foreach (var manager in rig.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                bool left = manager.name == "Left Controller";
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
                move.leftHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
                move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
                move.enabled = !login;
            }
            foreach (var turn in rig.GetComponentsInChildren<ContinuousTurnProvider>(true)) turn.enabled = false;
            foreach (var turn in rig.GetComponentsInChildren<SnapTurnProvider>(true))
            {
                turn.turnAmount = 45;
                turn.enableTurnAround = false;
                turn.enabled = !login;
            }
        }
    }
}
