using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Bridge;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

namespace ProcSense.Tests.PlayMode
{
    public class FoundationTests
    {
        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 30; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Duplicate_host_cannot_replace_or_restart_the_session()
        {
            yield return Boot();
            var host = SimulationHost.Instance;
            var session = host.Session;
            host.AutoTick = false;
            var duplicate = new GameObject("duplicate host");
            duplicate.AddComponent<SimulationHost>();
            Assert.AreSame(host, SimulationHost.Instance);
            yield return null;
            Assert.IsNull(duplicate.GetComponent<SimulationHost>());
            Assert.AreSame(session, host.Session);
            Object.Destroy(duplicate);
        }

        [UnityTest]
        public IEnumerator Additive_room_unload_keeps_one_session_rig_camera_and_event_system()
        {
            yield return Boot();
            var host = SimulationHost.Instance;
            host.AutoTick = false;
            var session = host.Session;
            var hash = host.Observable.Hash();
            for (int i = 0; i < 3; i++)
            {
                var room = SceneManager.CreateScene("Foundation test room");
                yield return SceneManager.UnloadSceneAsync(room);
                Assert.AreSame(host, SimulationHost.Instance);
                Assert.AreSame(session, host.Session);
                Assert.AreEqual(hash, host.Observable.Hash());
                Assert.AreEqual(1, Object.FindObjectsByType<SimulationHost>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(1, Camera.allCamerasCount);
            }
        }

        [UnityTest]
        public IEnumerator Constrained_locomotion_stops_at_every_wall_and_floor()
        {
            yield return Boot();
            Assert.IsNotNull(Object.FindFirstObjectByType<SimulatorLocomotion>());
            var rig = Object.FindFirstObjectByType<XROrigin>();
            var controller = rig.GetComponent<CharacterController>();
            var body = rig.GetComponentInChildren<XRBodyTransformer>();
            Assert.IsTrue(controller.enabled);
            Assert.IsTrue(body.useCharacterControllerIfExists);
            var starts = new[] { new Vector3(0, 0, -1.4f), new Vector3(1.8f, 0, 1.4f), new Vector3(-2, 0, -1.4f), new Vector3(2, 0, -1.4f) };
            var directions = new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right };
            for (int wall = 0; wall < starts.Length; wall++)
            {
                controller.enabled = false;
                rig.transform.position = starts[wall];
                controller.enabled = true;
                Physics.SyncTransforms();
                for (int frame = 0; frame < 25; frame++)
                {
                    body.QueueTransformation(new XROriginMovement { motion = directions[wall] * 0.1f + Vector3.down * 0.05f });
                    yield return null;
                }
                var p = rig.transform.position;
                Assert.That(p.x, Is.InRange(-2.3f, 2.3f), "side wall " + wall);
                Assert.That(p.z, Is.InRange(-1.8f, 1.8f), "front/back wall " + wall);
                Assert.That(p.y, Is.InRange(-0.05f, 0.1f), "floor " + wall);
                Assert.IsTrue(controller.isGrounded);
            }
            // Diagonal motion must slide safely at the corner instead of crossing either wall.
            for (int frame = 0; frame < 30; frame++)
            {
                body.QueueTransformation(new XROriginMovement { motion = new Vector3(0.1f, -0.05f, -0.1f) });
                yield return null;
            }
            Assert.Less(rig.transform.position.x, 2.3f);
            Assert.Greater(rig.transform.position.z, -1.8f);
        }

        [UnityTest]
        public IEnumerator Constrained_locomotion_allows_clear_floor_movement()
        {
            yield return Boot();
            var rig = Object.FindFirstObjectByType<XROrigin>();
            var start = rig.transform.position;
            var body = rig.GetComponentInChildren<XRBodyTransformer>();
            for (int i = 0; i < 5; i++)
            {
                body.QueueTransformation(new XROriginMovement { motion = new Vector3(0.05f, -0.02f, 0) });
                yield return null;
            }
            Assert.Greater(rig.transform.position.x, start.x + 0.15f);
        }

        [UnityTest]
        public IEnumerator Constrained_locomotion_cannot_walk_through_the_bed()
        {
            yield return Boot();
            var rig = Object.FindFirstObjectByType<XROrigin>();
            var body = rig.GetComponentInChildren<XRBodyTransformer>();
            for (int i = 0; i < 35; i++)
            {
                body.QueueTransformation(new XROriginMovement { motion = new Vector3(0, -0.02f, 0.1f) });
                yield return null;
            }
            Assert.Less(rig.transform.position.z, -0.2f, "bed starts at z=0; capsule must stop in front");
            Assert.That(rig.transform.position.y, Is.InRange(-0.05f, 0.1f));
        }

        [UnityTest]
        public IEnumerator Simulator_FPS_input_moves_the_rig_without_moving_the_head_through_walls()
        {
            yield return Boot();
            while (Time.time < 1.1f) yield return null;
            var simulator = Object.FindFirstObjectByType<XRDeviceSimulator>();
            var rig = Object.FindFirstObjectByType<XROrigin>();
            Assert.IsTrue(simulator.manipulatingFPS);
            var original = simulator.keyboardXTranslateAction;
            var device = InputSystem.AddDevice<Gamepad>();
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Foundation test");
            asset.AddActionMap(map);
            var action = map.AddAction("test navigation", InputActionType.Value, device.path + "/leftStick/x");
            var reference = InputActionReference.Create(action);
            try
            {
                simulator.keyboardXTranslateAction = reference;
                action.Enable();
                var headStart = rig.transform.InverseTransformPoint(rig.Camera.transform.position);
                InputSystem.QueueStateEvent(device, new GamepadState { leftStick = Vector2.left });
                float finish = Time.time + 2.5f;
                while (Time.time < finish) yield return null;
                Assert.That(rig.transform.position.x, Is.InRange(-2.3f, -1.5f));
                Assert.That(rig.transform.position.y, Is.InRange(-0.05f, 0.1f));
                var headEnd = rig.transform.InverseTransformPoint(rig.Camera.transform.position);
                Assert.That(headEnd.x, Is.EqualTo(headStart.x).Within(0.02f), "FPS walking must move the rig, not tracked head position");
            }
            finally
            {
                simulator.keyboardXTranslateAction = original;
                asset.Disable();
                Object.Destroy(reference);
                Object.Destroy(asset);
                InputSystem.RemoveDevice(device);
            }
        }
    }
}
