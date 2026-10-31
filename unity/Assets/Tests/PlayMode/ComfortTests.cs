using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.XR;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    public class ComfortTests
    {
        [SetUp] public void BypassLoginForClinicalChecks() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [TearDown] public void RestoreLogin() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
        [UnityTest]
        public IEnumerator Rig_waits_for_floor_during_delayed_room_load()
        {
            yield return SceneManager.LoadSceneAsync("Sys_Core", LoadSceneMode.Single);
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            Assert.IsTrue(rooms.Busy);
            Assert.IsFalse(rooms.RequestRoom("Corridor"));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(rig.transform.position.y, Is.EqualTo(0).Within(0.01f), "no floor exists yet; input/gravity must remain held");
            yield return SceneManager.LoadSceneAsync("PatientBay", LoadSceneMode.Additive);
            float ready = Time.realtimeSinceStartup + 15;
            while (rooms.Busy && Time.realtimeSinceStartup < ready) yield return null;
            Assert.IsFalse(rooms.Busy);
            Assert.That(rig.transform.position.y, Is.InRange(-0.05f, 0.1f));
        }

        [UnityTest]
        public IEnumerator Snap_provider_and_comfort_controls_work_without_room_panels()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var snap = rig.GetComponentInChildren<SnapTurnProvider>();
            var reader = snap.rightHandTurnInput;
            var originalMode = reader.inputSourceMode;
            float before = rig.transform.eulerAngles.y;
            try
            {
                reader.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
                reader.manualValue = Vector2.right;
                yield return null;
                yield return null;
                Assert.That(Mathf.DeltaAngle(before, rig.transform.eulerAngles.y), Is.EqualTo(45).Within(0.01f));
                reader.manualValue = Vector2.zero;
            }
            finally { reader.inputSourceMode = originalMode; }
            Assert.IsNull(Object.FindAnyObjectByType<ComfortPanel>());
            rig.GetComponent<ComfortControls>().SetSeated(true);
            Assert.IsTrue(rig.GetComponent<ComfortControls>().Seated);
            rig.GetComponent<ComfortControls>().SetTurnAngle(30);
            Assert.AreEqual(30, rig.GetComponent<ComfortControls>().TurnAngle);
        }

        [UnityTest]
        public IEnumerator Teleport_targets_match_controller_rays_and_reject_blocked_selection()
        {
            yield return Boot();
            var rays = Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include).Where(r => r.name == "Teleport Interactor").ToArray();
            Assert.AreEqual(2, rays.Length);
            var anchor = Object.FindObjectsByType<SafeTeleportAnchor>().First(a => a.name == "TELEPORT_Bedside");
            foreach (var ray in rays)
            {
                Assert.AreNotEqual(0, ray.interactionLayers.value & anchor.interactionLayers.value);
                Assert.AreEqual(QueryTriggerInteraction.Collide, ray.raycastTriggerInteraction);
            }
            Assert.IsTrue(anchor.GetComponent<Collider>().isTrigger);
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstruction.transform.position = anchor.teleportAnchorTransform.position + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            foreach (var ray in rays) Assert.IsFalse(anchor.IsSelectableBy((IXRSelectInteractor)ray));
            Object.Destroy(obstruction);
            yield return null;
            Assert.IsTrue(Object.FindAnyObjectByType<SafeTeleportProvider>().CanTeleport(anchor.teleportAnchorTransform.position));
        }

        [UnityTest]
        public IEnumerator Direct_room_play_binds_teleport_targets_after_services_load()
        {
            foreach (string room in new[] { "PatientBay", "Corridor", "OT" })
            {
                yield return SceneManager.LoadSceneAsync(room, LoadSceneMode.Single);
                float end = Time.realtimeSinceStartup + 10;
                while (!SceneManager.GetSceneByName("DevOnly").isLoaded && Time.realtimeSinceStartup < end) yield return null;
                yield return null;
                SimulationHost.Instance.AutoTick = false;
                var provider = Object.FindAnyObjectByType<SafeTeleportProvider>();
                var ray = Object.FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include).First(r => r.name == "Teleport Interactor");
                end = Time.realtimeSinceStartup + 15;
                while (Object.FindAnyObjectByType<RoomLoader>().Busy && Time.realtimeSinceStartup < end) yield return null;
                var anchor = Object.FindObjectsByType<SafeTeleportAnchor>().First(a => a.gameObject.scene.name == room);
                anchor.IsSelectableBy((IXRSelectInteractor)ray); // Must bind before a selection request can be generated.
                Assert.AreSame(provider, anchor.teleportationProvider);
                anchor.RequestTeleport();
                Assert.IsTrue(provider.Busy, room);
                yield return Idle(provider);
            }
        }

        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 30; i++) yield return null;
            // The room loads additively and teleport is refused while it is Busy: wait for it instead of racing a fixed frame budget.
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            int extra = 0;
            float end = Time.realtimeSinceStartup + 20;
            while (rooms != null && rooms.Busy && Time.realtimeSinceStartup < end) { extra++; yield return null; }
            if (extra > 0) Debug.Log($"[ComfortTests] room needed {extra} frames beyond the fixed 30 to finish loading");
            SimulationHost.Instance.AutoTick = false;
        }
        private static IEnumerator Idle(SafeTeleportProvider provider)
        {
            float end = Time.realtimeSinceStartup + 5;
            while (provider.Busy && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsFalse(provider.Busy);
        }

        [UnityTest]
        public IEnumerator Station_teleport_moves_under_fade_without_changing_clinical_state()
        {
            yield return Boot();
            var provider = Object.FindAnyObjectByType<SafeTeleportProvider>();
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            string state = SimulationHost.Instance.Observable.Hash();
            string log = SimulationHost.Instance.Session.Log.Hash();
            foreach (var anchor in Object.FindObjectsByType<SafeTeleportAnchor>().Where(a => a.gameObject.scene.name == rooms.CurrentRoom).OrderBy(a => a.name))
            {
                var destination = anchor.teleportAnchorTransform.position;
                Assert.IsTrue(provider.CanTeleport(destination), anchor.name);
                anchor.RequestTeleport();
                Assert.IsTrue(provider.Busy);
                Assert.IsFalse(rooms.RequestRoom("Corridor"));
                Assert.IsFalse(provider.QueueTeleportRequest(new TeleportRequest { destinationPosition = destination }));
                yield return Idle(provider);
                Assert.Less(Vector3.Distance(Vector3.ProjectOnPlane(rig.Camera.transform.position, Vector3.up), Vector3.ProjectOnPlane(destination, Vector3.up)), 0.06f);
                Assert.AreEqual(0, rig.GetComponent<LocomotionFade>().Alpha);
                Assert.AreEqual(state, SimulationHost.Instance.Observable.Hash());
                Assert.AreEqual(log, SimulationHost.Instance.Session.Log.Hash());
            }
        }

        [UnityTest]
        public IEnumerator Invalid_floor_wall_bed_and_dynamic_obstruction_requests_are_rejected()
        {
            yield return Boot();
            var provider = Object.FindAnyObjectByType<SafeTeleportProvider>();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var start = rig.transform.position;
            foreach (var point in new[] { new Vector3(0, 0, 1), new Vector3(2.49f, 0, -1), new Vector3(-3, 0, 0), new Vector3(0, -5, 0), new Vector3(float.NaN, 0, 0) })
                Assert.IsFalse(provider.QueueTeleportRequest(new TeleportRequest { destinationPosition = point }), point.ToString());
            Assert.AreEqual(start, rig.transform.position);
            var destination = Object.FindObjectsByType<SafeTeleportAnchor>().First(a => a.name == "TELEPORT_Bedside").teleportAnchorTransform.position;
            Assert.IsTrue(provider.QueueTeleportRequest(new TeleportRequest { destinationPosition = destination }));
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.transform.position = destination + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            yield return Idle(provider);
            Assert.AreEqual(start, rig.transform.position);
            Object.Destroy(obstacle);
            yield return null;
            Assert.IsTrue(provider.CanTeleport(destination));
        }

        [UnityTest]
        public IEnumerator Seated_calibration_is_reversible_and_snap_turn_is_45_or_30_degrees()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var controls = rig.GetComponent<ComfortControls>();
            var snap = rig.GetComponentInChildren<SnapTurnProvider>(true);
            Assert.AreEqual(45, snap.turnAmount);
            Assert.IsFalse(snap.enableTurnAround);
            float original = rig.CameraFloorOffsetObject.transform.localPosition.y;
            controls.SetSeated(true);
            Assert.IsTrue(controls.Seated);
            Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(1.6f).Within(0.01f));
            controls.SetSeated(true);
            Assert.That(rig.CameraInOriginSpaceHeight, Is.EqualTo(1.6f).Within(0.01f));
            float yaw = rig.transform.eulerAngles.y;
            Assert.IsTrue(controls.Turn(1));
            yield return null;
            Assert.That(Mathf.DeltaAngle(yaw, rig.transform.eulerAngles.y), Is.EqualTo(45).Within(0.01f));
            controls.SetTurnAngle(30);
            RigLocomotionSettings.Apply(rig, true, false);
            Assert.AreEqual(30, snap.turnAmount, "Reapplying input setup must preserve the selected comfort angle");
            yaw = rig.transform.eulerAngles.y;
            Assert.IsTrue(controls.Turn(-1));
            yield return null;
            Assert.That(Mathf.DeltaAngle(yaw, rig.transform.eulerAngles.y), Is.EqualTo(-30).Within(0.01f));
            controls.SetTurnAngle(90);
            Assert.AreEqual(30, controls.TurnAngle);
            controls.SetSeated(false);
            Assert.AreEqual(original, rig.CameraFloorOffsetObject.transform.localPosition.y);
        }

        [UnityTest]
        public IEnumerator Corridor_and_OT_targets_work_after_scene_changes_and_seated_setting_persists()
        {
            yield return Boot();
            var rig = Object.FindAnyObjectByType<XROrigin>();
            var controls = rig.GetComponent<ComfortControls>();
            controls.SetSeated(true);
            var rooms = Object.FindAnyObjectByType<RoomLoader>();
            var provider = rig.GetComponentInChildren<SafeTeleportProvider>();
            foreach (string room in new[] { "Corridor", "OT", "Corridor", "PatientBay" })
            {
                Assert.IsTrue(rooms.RequestRoom(room));
                Assert.IsFalse(provider.QueueTeleportRequest(new TeleportRequest { destinationPosition = Vector3.zero }));
                float end = Time.realtimeSinceStartup + 10;
                while (rooms.Busy && Time.realtimeSinceStartup < end) yield return null;
                Assert.AreEqual(room, rooms.CurrentRoom, rooms.LastError);
                Assert.IsNull(Object.FindAnyObjectByType<ComfortPanel>(), room);
                Assert.IsTrue(controls.Seated);
                if (room == "Corridor")
                {
                    Assert.IsNotNull(Object.FindAnyObjectByType<SafeTeleportArea>());
                    var layout = Object.FindObjectsByType<RoomLayout>().Single(l => l.gameObject.scene.name == "Corridor");
                    Assert.IsFalse(provider.CanTeleport(layout.transform.TransformPoint(new Vector3(-2.1f, 0, -1.5f))), "behind diagnostic wall");
                    Assert.IsTrue(provider.QueueTeleportRequest(new TeleportRequest { destinationPosition = layout.transform.TransformPoint(Vector3.zero), matchOrientation = MatchOrientation.WorldSpaceUp }));
                }
                else
                {
                    Object.FindObjectsByType<SafeTeleportAnchor>().First(a => a.gameObject.scene.name == room).RequestTeleport();
                    Assert.IsTrue(provider.Busy, room);
                }
                yield return Idle(provider);
                Assert.AreEqual(0, rig.GetComponent<LocomotionFade>().Alpha);
            }
        }
    }
}
