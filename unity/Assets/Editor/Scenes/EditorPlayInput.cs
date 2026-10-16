using System.Collections.Generic;
using ProcSense.VR.Boot;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ProcSense.EditorTools
{
    /// <summary>Local Play choices, without saving XR project settings or editing the rig prefab.</summary>
    [InitializeOnLoad]
    public static class EditorPlayInput
    {
        private const string InputMenu = "ProcSense/Development/Play input/";
        private const string MovementMenu = "ProcSense/Development/Movement/";
        private static string RestoreKey => EditorInputSettings.PreferenceKey + ".RestoreXR";
        private static readonly HashSet<XROrigin> ConfiguredRigs = new HashSet<XROrigin>();
        private static double _nextScan;

        static EditorPlayInput()
        {
            EditorApplication.playModeStateChanged += StateChanged;
            EditorApplication.update += Update;
            // A domain reload occurs between ExitingEditMode and XR runtime initialization.
            if (SessionState.GetBool(RestoreKey, false)) ApplyStartupChoice();
        }

        [MenuItem(InputMenu + "Quest headset and controllers")]
        public static void UseHeadset() => SetMode(EditorInputSettings.InputMode.Headset);
        [MenuItem(InputMenu + "Keyboard and mouse")]
        public static void UseKeyboardMouse() => SetMode(EditorInputSettings.InputMode.KeyboardMouse);
        [MenuItem(InputMenu + "Automatic detection")]
        public static void UseAutomatic() => SetMode(EditorInputSettings.InputMode.Automatic);
        [MenuItem(MovementMenu + "Thumbsticks - left move, right snap turn")]
        public static void UseThumbsticks() => SetMovement(true);
        [MenuItem(MovementMenu + "Room-scale and teleport")]
        public static void UseRoomScale() => SetMovement(false);

        public static void SetMode(EditorInputSettings.InputMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetInt(EditorInputSettings.PreferenceKey, (int)mode);
        }
        private static void SetMovement(bool enabled)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorPrefs.SetBool(EditorInputSettings.PreferenceKey + ".Thumbsticks", enabled);
        }

        [MenuItem(InputMenu + "Quest headset and controllers", true)]
        private static bool HeadsetChecked() => ValidateInput("Quest headset and controllers", EditorInputSettings.InputMode.Headset);
        [MenuItem(InputMenu + "Keyboard and mouse", true)]
        private static bool KeyboardChecked() => ValidateInput("Keyboard and mouse", EditorInputSettings.InputMode.KeyboardMouse);
        [MenuItem(InputMenu + "Automatic detection", true)]
        private static bool AutomaticChecked() => ValidateInput("Automatic detection", EditorInputSettings.InputMode.Automatic);
        private static bool ValidateInput(string label, EditorInputSettings.InputMode mode)
        {
            Menu.SetChecked(InputMenu + label, EditorInputSettings.Mode == mode);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
        [MenuItem(MovementMenu + "Thumbsticks - left move, right snap turn", true)]
        private static bool ThumbsticksChecked() => ValidateMovement("Thumbsticks - left move, right snap turn", true);
        [MenuItem(MovementMenu + "Room-scale and teleport", true)]
        private static bool RoomScaleChecked() => ValidateMovement("Room-scale and teleport", false);
        private static bool ValidateMovement(string label, bool enabled)
        {
            Menu.SetChecked(MovementMenu + label, EditorInputSettings.ThumbstickMovement == enabled);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private static void StateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (settings != null)
                {
                    SessionState.SetBool(RestoreKey + ".Original", settings.InitManagerOnStart);
                    SessionState.SetBool(RestoreKey, true);
                    ApplyStartupChoice();
                }
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
                if (settings != null && SessionState.GetBool(RestoreKey, false))
                    settings.InitManagerOnStart = SessionState.GetBool(RestoreKey + ".Original", true);
                SessionState.EraseBool(RestoreKey);
                ConfiguredRigs.Clear();
            }
        }
        private static void ApplyStartupChoice()
        {
            var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
            if (settings != null)
                settings.InitManagerOnStart = EditorInputSettings.Mode != EditorInputSettings.InputMode.KeyboardMouse;
            // No SetDirty/SaveAssets: this flag is restored when Play ends.
        }

        private static void Update()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < _nextScan) return;
            _nextScan = EditorApplication.timeSinceStartup + .2;
            foreach (var rig in Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None))
            {
                var rooms = Object.FindFirstObjectByType<RoomLoader>();
                if (rig.GetComponentInParent<BootLoader>() == null && (rooms == null || rooms.Busy)) continue;
                if (ConfiguredRigs.Add(rig)) ApplyLocomotion(rig);
            }
        }
        public static void ApplyLocomotion(XROrigin rig)
        {
            bool login = rig.GetComponentInParent<BootLoader>() != null;
            bool sticks = EditorInputSettings.ThumbstickMovement && !login;
            foreach (var manager in rig.GetComponentsInChildren<ControllerInputActionManager>(true))
            {
                manager.smoothMotionEnabled = sticks && manager.name == "Left Controller";
                manager.smoothTurnEnabled = false;
            }
            foreach (var move in rig.GetComponentsInChildren<DynamicMoveProvider>(true))
            {
                move.moveSpeed = 1.5f;
                move.enableFly = false;
                move.enableStrafe = true;
                move.leftHandMovementDirection = DynamicMoveProvider.MovementDirection.HeadRelative;
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
