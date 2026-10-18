using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    public class RoomTransitionTests
    {
        [SetUp] public void BypassLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        [UnityTearDown] public IEnumerator Cleanup() => EditorXRTestEnvironment.Cleanup();

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            SimulationHost.Instance.AutoTick = false;
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        private static RoomLayout Layout(string room) => Object.FindObjectsByType<RoomLayout>().Single(l => l.gameObject.scene.name == room);
        private static SwingDoor Door(string key) => Object.FindObjectsByType<SwingDoor>().Single(d => d.Connection == key);

        [UnityTest]
        public IEnumerator Simultaneously_started_room_bootstraps_load_services_only_once()
        {
            void Hold(Scene scene, LoadSceneMode mode)
            {
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var bootstrap in root.GetComponentsInChildren<RoomBootstrap>()) bootstrap.enabled = false;
            }
            SceneManager.sceneLoaded += Hold;
            try
            {
                yield return SceneManager.LoadSceneAsync("PatientBay");
                yield return SceneManager.LoadSceneAsync("Corridor", LoadSceneMode.Additive);
                yield return SceneManager.LoadSceneAsync("OT", LoadSceneMode.Additive);
            }
            finally { SceneManager.sceneLoaded -= Hold; }
            SceneManager.SetActiveScene(SceneManager.GetSceneByName("Corridor"));
            foreach (var bootstrap in Object.FindObjectsByType<RoomBootstrap>(FindObjectsInactive.Include)) bootstrap.enabled = true;
            yield return EditorXRTestEnvironment.EnsureSimulator();
            Assert.AreEqual(1, Enumerable.Range(0, SceneManager.sceneCount).Count(i => SceneManager.GetSceneAt(i).name == "Sys_Core"));
            Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<SimulationHost>().Length);
            Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
            Assert.AreEqual("Corridor", Object.FindAnyObjectByType<RoomLoader>().CurrentRoom);
        }

        [UnityTest]
        public IEnumerator Direct_room_play_preloads_connected_world_with_one_rig_and_session()
        {
            foreach (string room in new[] { "PatientBay", "Corridor", "OT" })
            {
                yield return SceneManager.LoadSceneAsync(room);
                yield return EditorXRTestEnvironment.EnsureSimulator();
                Assert.AreEqual(room, Object.FindAnyObjectByType<RoomLoader>().CurrentRoom);
                Assert.AreEqual(3, Object.FindObjectsByType<RoomLayout>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<SimulationHost>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
                Assert.AreEqual(1, Camera.allCamerasCount);
            }
        }

        [UnityTest]
        public IEnumerator Both_sides_share_one_solid_grabbable_leaf_with_clear_aligned_openings()
        {
            yield return Boot();
            foreach (var door in Object.FindObjectsByType<RoomDoor>())
            {
                Assert.IsEmpty(door.GetComponentsInChildren<UnityEngine.UI.Button>());
                if (door.Restricted)
                {
                    door.Enter();
                    Assert.IsFalse(Object.FindAnyObjectByType<RoomLoader>().Busy);
                    Assert.IsTrue(Physics.Raycast(door.transform.position - door.transform.forward * .3f,
                        door.transform.forward, 1, ~0, QueryTriggerInteraction.Ignore));
                }
            }
            foreach (var hinge in Object.FindObjectsByType<SwingDoor>())
            {
                var grab = hinge.GetComponent<XRGrabInteractable>();
                Assert.AreEqual(2, grab.colliders.Count, "Handles on both sides");
                Assert.IsFalse(grab.trackPosition); Assert.IsFalse(grab.trackRotation);
                Assert.IsTrue(hinge.Leaf.enabled); Assert.IsFalse(hinge.Leaf.isTrigger);
                Assert.IsTrue(hinge.Ready);
                Assert.IsTrue(hinge.Toggle());
            }
            yield return new WaitForSeconds(1);
            Assert.That(Mathf.Abs(Door("BayCorridor").Angle), Is.GreaterThan(90));
            // Eye-height line goes through both cut walls and the open leaf, into actual corridor space.
            Assert.IsFalse(Physics.Raycast(new Vector3(2.1f, 1.5f, -.8f), Vector3.right, .9f, ~0, QueryTriggerInteraction.Ignore));
            Assert.IsFalse(Physics.Raycast(new Vector3(6.1f, 1.5f, 5.2f), Vector3.right, .9f, ~0, QueryTriggerInteraction.Ignore));
            Assert.Less(Vector3.Distance(Layout("Corridor").transform.TransformPoint(new Vector3(-2, 0, -3)), new Vector3(2.5f, 0, -.8f)), .001f);
            Assert.Less(Vector3.Distance(Layout("OT").transform.TransformPoint(new Vector3(0, 0, -3.5f)), new Vector3(6.5f, 0, 5.2f)), .001f);
        }

        [UnityTest]
        public IEnumerator Hand_drag_rotates_hinge_without_translation_or_drop_and_stops_at_limits()
        {
            yield return Boot();
            var hinge = Door("BayCorridor"); var grab = hinge.GetComponent<XRGrabInteractable>();
            var handObject = new GameObject("Door grip test hand");
            handObject.transform.position = grab.colliders[0].bounds.center;
            var hand = handObject.AddComponent<XRDirectInteractor>(); hand.interactionManager = grab.interactionManager;
            var origin = hinge.transform.position;
            try
            {
                grab.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return null;
                var direction = hand.transform.position - origin;
                for (int i = 1; i <= 30; i++)
                {
                    hand.transform.position = origin + Quaternion.Euler(0, -3 * i, 0) * direction;
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(hinge.Angle, Is.LessThan(-55));
                grab.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                yield return new WaitForFixedUpdate();
                float released = hinge.Angle;
                yield return new WaitForSeconds(.3f);
                Assert.That(hinge.Angle, Is.EqualTo(released).Within(.1f));
                Assert.That(hinge.Angle, Is.InRange(-105, 105));
                Assert.Less(Vector3.Distance(origin, hinge.transform.position), .001f);
                Assert.IsTrue(hinge.GetComponent<Rigidbody>().isKinematic);
                Assert.IsFalse(hinge.GetComponent<Rigidbody>().useGravity);
            }
            finally { Object.Destroy(handObject); }
        }

        private static IEnumerator Walk(string room, Vector3 from, Vector3 to)
        {
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var capsule = rig.GetComponent<CharacterController>();
            capsule.enabled = false;
            rig.transform.position += from - Vector3.ProjectOnPlane(rig.Camera.transform.position, Vector3.up);
            capsule.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 45; i++)
            {
                capsule.Move((to - from) / 45 + Vector3.down * .015f);
                yield return null;
                Assert.AreEqual(0, rig.GetComponent<ProcSense.VR.XR.LocomotionFade>().Alpha, "Walking must never fade");
            }
            Assert.AreEqual(room, Object.FindAnyObjectByType<RoomLoader>().CurrentRoom);
            Assert.Less(Vector3.Distance(Vector3.ProjectOnPlane(rig.Camera.transform.position, Vector3.up), to), .15f);
        }

        [UnityTest]
        public IEnumerator Walk_both_doorways_and_return_preserving_live_patient_equipment_session_and_actions()
        {
            yield return Boot();
            var host = SimulationHost.Instance; var session = host.Session;
            var tablet = Object.FindAnyObjectByType<TabletView>();
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(tablet.Press(verb));
            Assert.IsTrue(tablet.Press("attach_sensor", "sensor", "spo2"));
            var sensor = Object.FindObjectsByType<SensorAttachment>().Single(s => s.BuildAction().Params["sensor"] == "spo2");
            var patient = Object.FindAnyObjectByType<PatientIdleView>();
            Assert.IsNotNull(patient);
            var hash = host.Observable.Hash(); var log = session.Log.Hash();
            Door("BayCorridor").Toggle(); Door("CorridorOT").Toggle();
            yield return new WaitForSeconds(1);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                yield return Walk("Corridor", new Vector3(1.8f, 0, -.8f), new Vector3(3.2f, 0, -.8f));
                yield return Walk("OT", new Vector3(5.8f, 0, 5.2f), new Vector3(7.2f, 0, 5.2f));
                yield return Walk("Corridor", new Vector3(7.2f, 0, 5.2f), new Vector3(5.8f, 0, 5.2f));
                yield return Walk("PatientBay", new Vector3(3.2f, 0, -.8f), new Vector3(1.8f, 0, -.8f));
                Assert.AreSame(session, host.Session); Assert.AreEqual(hash, host.Observable.Hash()); Assert.AreEqual(log, session.Log.Hash());
                Assert.IsTrue(sensor.Attached); Assert.AreSame(sensor, Object.FindObjectsByType<SensorAttachment>().Single(s => s.BuildAction().Params["sensor"] == "spo2"));
                Assert.AreSame(patient, Object.FindAnyObjectByType<PatientIdleView>());
                Assert.AreEqual(3, Object.FindObjectsByType<RoomLayout>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<XROrigin>().Length);
                Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>().Length);
            }
        }

        [UnityTest]
        public IEnumerator Closed_door_blocks_walking_and_activation_does_not_sweep_through_player()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>(); var capsule = rig.GetComponent<CharacterController>();
            capsule.enabled = false; rig.transform.position = new Vector3(1.8f, 0, -.8f); capsule.enabled = true; Physics.SyncTransforms();
            for (int i = 0; i < 50; i++) { capsule.Move(Vector3.right * .04f); yield return null; }
            Assert.That(rig.Camera.transform.position.x, Is.LessThan(2.35f));
            Assert.AreEqual("PatientBay", Object.FindAnyObjectByType<RoomLoader>().CurrentRoom);
            var door = Door("BayCorridor");
            door.Toggle(); yield return new WaitForSeconds(1);
            Assert.That(Mathf.Abs(door.Angle), Is.GreaterThan(80));
            // A player occupying the swing arc must stop closure before the leaf passes through.
            capsule.enabled = false; rig.transform.position = new Vector3(2.8f, 0, -.6f); capsule.enabled = true; Physics.SyncTransforms();
            door.Toggle(); yield return new WaitForSeconds(1);
            Assert.That(Mathf.Abs(door.Angle), Is.GreaterThan(10));
        }
    }
}
