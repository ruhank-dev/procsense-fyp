using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Boot;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ProcSense.Tests.PlayMode
{
    internal static class EditorXRTestEnvironment
    {
        private static Keyboard _keyboard;
        private static Mouse _mouse;
        private static InputSettings _originalSettings;
        private static InputSettings _testSettings;

        // Application startup deliberately excludes developer simulation from batch runs.
        // Tests that exercise simulated input must request their own fixture instead.
        public static IEnumerator EnsureSimulator()
        {
            float end = Time.realtimeSinceStartup + 10;
            RoomLoader rooms;
            while (((rooms = Object.FindFirstObjectByType<RoomLoader>()) == null || rooms.Busy) && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.IsNotNull(rooms, "The room-loading service must exist before loading simulated input");
            Assert.IsFalse(rooms.Busy, "The initial room did not finish loading");
            if (Application.isBatchMode)
            {
                if (_testSettings == null)
                {
                    _originalSettings = InputSystem.settings;
                    // Input System destroys its transient default when settings are replaced.
                    // Keep a value-identical backup in that case; persistent assets retain identity.
                    if (_originalSettings.hideFlags == HideFlags.HideAndDontSave)
                        _originalSettings = Object.Instantiate(_originalSettings);
                    _testSettings = Object.Instantiate(_originalSettings);
                    _testSettings.hideFlags = HideFlags.HideAndDontSave;
                    _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings = _testSettings;
                }
                // The sample overlay reads its keyboard/mouse bindings in Start, even in batch.
                // Supply test-owned devices before enabling the simulator; never depend on hardware.
                if (Keyboard.current == null) _keyboard = InputSystem.AddDevice<Keyboard>();
                if (Mouse.current == null) _mouse = InputSystem.AddDevice<Mouse>();
            }
            if (Application.isBatchMode && !SceneManager.GetSceneByName("DevOnly").isLoaded)
                yield return SceneManager.LoadSceneAsync("DevOnly", LoadSceneMode.Additive);
            end = Time.realtimeSinceStartup + 5;
            while (Object.FindFirstObjectByType<XRDeviceSimulator>() == null && Time.realtimeSinceStartup < end)
                yield return null;
            Assert.IsNotNull(Object.FindFirstObjectByType<XRDeviceSimulator>(), "The test's DevOnly simulator fixture did not become ready");
            yield return null;
        }

        public static IEnumerator Cleanup()
        {
            if (_keyboard == null && _mouse == null && _testSettings == null) yield break;
            var scene = SceneManager.GetSceneByName("DevOnly");
            // Unsubscribe the sample overlay before removing its binding devices.
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _keyboard = null; _mouse = null;
            if (_testSettings != null)
            {
                InputSystem.settings = _originalSettings;
                Assert.AreSame(_originalSettings, InputSystem.settings, "Restore the pre-test input configuration");
                // Input System owns/destroys the HideAndDontSave test clone on replacement.
                _originalSettings = null; _testSettings = null;
            }
        }
    }
}
