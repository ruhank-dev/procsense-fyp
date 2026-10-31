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
        private static InputSettings.BackgroundBehavior _originalBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode _originalEditorInput;

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
            // Interactive automation can lose Game-view focus too; keep test input deterministic.
            if (Application.isEditor || Application.isBatchMode)
            {
                if (_originalSettings == null)
                {
                    _originalSettings = InputSystem.settings;
                    // Keep the original object alive: the Editor restores that exact
                    // reference on Play exit. Replacing a transient default destroys it.
                    _originalBackground = _originalSettings.backgroundBehavior;
                    _originalEditorInput = _originalSettings.editorInputBehaviorInPlayMode;
                    _originalSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    _originalSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
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
            if (_keyboard == null && _mouse == null && _originalSettings == null) yield break;
            var scene = SceneManager.GetSceneByName("DevOnly");
            // Unsubscribe the sample overlay before removing its binding devices.
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null) InputSystem.RemoveDevice(_mouse);
            _keyboard = null; _mouse = null;
            if (_originalSettings != null)
            {
                _originalSettings.backgroundBehavior = _originalBackground;
                _originalSettings.editorInputBehaviorInPlayMode = _originalEditorInput;
                Assert.AreSame(_originalSettings, InputSystem.settings, "Restore the pre-test input configuration");
                _originalSettings = null;
            }
        }
    }
}
