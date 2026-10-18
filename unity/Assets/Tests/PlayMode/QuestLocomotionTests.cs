#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Login;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

namespace ProcSense.Tests.PlayMode
{
    public class QuestLocomotionTests
    {
        [SetUp] public void BypassLogin() => DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => DemoLoginSettings.LoginRequiredOverride = null;

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 40; i++) yield return null;
            SimulationHost.Instance.AutoTick = false;
        }

        [UnityTest]
        public IEnumerator Shared_runtime_setup_maps_left_to_walk_and_right_to_snap_without_editor_preferences()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            RigLocomotionSettings.Apply(rig, true, false);
            var managers = rig.GetComponentsInChildren<ControllerInputActionManager>();
            Assert.IsTrue(managers.Single(m => m.name == "Left Controller").smoothMotionEnabled);
            Assert.IsFalse(managers.Single(m => m.name == "Left Controller").uiScrollingEnabled);
            Assert.IsFalse(managers.Single(m => m.name == "Right Controller").smoothMotionEnabled);
            Assert.IsTrue(managers.Single(m => m.name == "Right Controller").uiScrollingEnabled);
            Assert.IsTrue(managers.All(m => !m.smoothTurnEnabled));
            var move = rig.GetComponentInChildren<DynamicMoveProvider>();
            Assert.IsTrue(move.enabled && move.enableStrafe && !move.enableFly);
            Assert.AreEqual(1.5f, move.moveSpeed);
            Assert.AreEqual(XRInputValueReader.InputSourceMode.Unused, move.rightHandMoveInput.inputSourceMode);
            Assert.AreEqual(45, rig.GetComponentInChildren<SnapTurnProvider>().turnAmount);
            Assert.IsTrue(rig.GetComponentsInChildren<ContinuousTurnProvider>(true).All(p => !p.enabled));
        }

        [UnityTest]
        public IEnumerator Left_stick_moves_on_the_floor_and_cannot_pass_through_a_solid_obstruction()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            RigLocomotionSettings.Apply(rig, true, false);
            var move = rig.GetComponentInChildren<DynamicMoveProvider>();
            var reader = move.leftHandMoveInput; var mode = reader.inputSourceMode;
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Movement regression obstruction";
            var forward = Vector3.ProjectOnPlane(rig.Camera.transform.forward, Vector3.up).normalized;
            Vector3 start = rig.Camera.transform.position;
            blocker.transform.position = Vector3.ProjectOnPlane(start + forward * .85f, Vector3.up) + Vector3.up;
            blocker.transform.rotation = Quaternion.LookRotation(forward);
            blocker.transform.localScale = new Vector3(3, 2, .1f);
            Physics.SyncTransforms();
            string hash = SimulationHost.Instance.Observable.Hash(); int actions = SimulationHost.Instance.Session.Log.Count;
            try
            {
                reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
                reader.manualValue = Vector2.up;
                yield return new WaitForSeconds(1);
                float distance = Vector3.Dot(rig.Camera.transform.position - start, forward);
                Assert.That(distance, Is.InRange(.05f, .70f), "Move toward the obstruction, then stop at its collider");
                Assert.That(rig.transform.position.y, Is.InRange(-.06f, .1f));
                reader.manualValue = Vector2.zero;
                Assert.AreEqual(hash, SimulationHost.Instance.Observable.Hash());
                Assert.AreEqual(actions, SimulationHost.Instance.Session.Log.Count);
            }
            finally { reader.manualValue = Vector2.zero; reader.inputSourceMode = mode; Object.Destroy(blocker); }
        }

        [UnityTest]
        public IEnumerator Seated_startup_is_lifted_once_and_Y_button_toggles_only_on_a_fresh_press()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>(); var controls = rig.GetComponent<ComfortControls>();
            controls.SetSeated(false);
            var camera = rig.Camera.transform;
            var driver = camera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>(); bool tracking = driver.enabled; driver.enabled = false;
            Vector3 original = camera.localPosition;
            float floorOffset = rig.CameraFloorOffsetObject.transform.localPosition.y;
            string hash = SimulationHost.Instance.Observable.Hash();
            try
            {
                camera.localPosition += Vector3.up * (1.1f - rig.CameraInOriginSpaceHeight);
                controls.CalibrateInitialHeight();
                Assert.IsTrue(controls.Seated);
                Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(1.6f).Within(.01f));
                controls.CalibrateInitialHeight();
                Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(1.6f).Within(.01f));
                Assert.IsTrue(controls.ProcessHeightButton(true)); Assert.IsFalse(controls.Seated);
                Assert.IsFalse(controls.ProcessHeightButton(true)); Assert.IsFalse(controls.Seated);
                Assert.IsFalse(controls.ProcessHeightButton(false));
                Assert.IsTrue(controls.ProcessHeightButton(true)); Assert.IsTrue(controls.Seated);
                controls.ProcessHeightButton(false); controls.SetSeated(false);
                Assert.AreEqual(floorOffset, rig.CameraFloorOffsetObject.transform.localPosition.y);
                camera.localPosition += Vector3.up * (1.75f - rig.CameraInOriginSpaceHeight);
                controls.CalibrateInitialHeight(); Assert.IsFalse(controls.Seated, "Standing tracking must not be shortened");
                Assert.AreEqual(hash, SimulationHost.Instance.Observable.Hash());
            }
            finally { controls.ProcessHeightButton(false); controls.SetSeated(false); camera.localPosition = original; driver.enabled = tracking; }
        }

        [UnityTest]
        public IEnumerator Room_transitions_freeze_stick_movement_and_height_changes_then_restore_controls()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>(); RigLocomotionSettings.Apply(rig, true, false);
            var controls = rig.GetComponent<ComfortControls>(); var rooms = Object.FindAnyObjectByType<RoomLoader>();
            var move = rig.GetComponentInChildren<DynamicMoveProvider>();
            Assert.IsTrue(rooms.RequestRoom("Corridor")); yield return null;
            Assert.IsFalse(move.enabled);
            bool seated = controls.Seated;
            Assert.IsFalse(controls.ProcessHeightButton(true)); Assert.AreEqual(seated, controls.Seated);
            float until = Time.realtimeSinceStartup + 10;
            while (rooms.Busy && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(rooms.Busy); Assert.IsTrue(move.enabled);
            Assert.IsFalse(controls.ProcessHeightButton(true), "A held button cannot fire after transition");
            controls.ProcessHeightButton(false);
            Assert.IsTrue(controls.ProcessHeightButton(true)); Assert.AreNotEqual(seated, controls.Seated);
            controls.ProcessHeightButton(false);
        }
    }
}
#endif
