using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    public class EquipmentDropTests
    {
        [UnitySetUp]
        public IEnumerator OpenBay()
        {
            yield return SceneManager.LoadSceneAsync("PatientBay");
            float end = Time.realtimeSinceStartup + 10;
            while ((SimulationHost.Instance == null || Object.FindFirstObjectByType<RoomLoader>() == null
                || Object.FindFirstObjectByType<RoomLoader>().Busy) && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.AutoTick = false;
            SimulationHost.Instance.SaveLogOnDestroy = false;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Released_equipment_falls_and_is_stopped_by_the_floor()
        {
            int actions = SimulationHost.Instance.Session.Log.Count;
            foreach (string name in new[] { "bp_cuff", "spo2_probe", "Physical notes folder", "vital_signs_monitor" })
            {
                var grab = Object.FindObjectsByType<XRGrabInteractable>().Single(g => g.name == name);
                var body = grab.GetComponent<Rigidbody>();
                var original = new Pose(body.position, body.rotation);
                var handObject = new GameObject("Gravity test hand");
                var hand = handObject.AddComponent<XRDirectInteractor>();
                hand.interactionManager = grab.interactionManager;
                try
                {
                    grab.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                    Assert.IsTrue(body.isKinematic, name + " should track the hand stably");
                    grab.trackPosition = false;
                    grab.trackRotation = false;
                    var drop = new Pose(new Vector3(1.6f, 1.1f, -.85f), Quaternion.identity);
                    grab.SetTargetPose(drop);
                    body.position = drop.position;
                    body.rotation = drop.rotation;
                    grab.transform.SetPositionAndRotation(drop.position, drop.rotation);
                    grab.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                    yield return new WaitForFixedUpdate();
                    Assert.IsFalse(body.isKinematic, name);
                    Assert.IsTrue(body.useGravity, name);
                    yield return new WaitForSeconds(.25f);
                    Assert.Less(body.position.y, .99f, name + " did not fall after release");
                    yield return new WaitForSeconds(1.1f);
                    float bottom = grab.colliders.Where(c => c != null && c.enabled && !c.isTrigger).Min(c => c.bounds.min.y);
                    Assert.That(bottom, Is.InRange(-.025f, .08f), name + " should land on the floor, not tunnel through it");
                }
                finally
                {
                    if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
                    body.position = original.position;
                    body.rotation = original.rotation;
                    grab.SetTargetPose(original);
                    grab.trackPosition = true;
                    grab.trackRotation = true;
                    Object.DestroyImmediate(handObject);
                }
            }
            Assert.AreEqual(actions, SimulationHost.Instance.Session.Log.Count, "Dropping items must not emit clinical actions");
        }

        [UnityTest]
        public IEnumerator Gravity_does_not_pull_attached_sensors_off_and_tablet_returns_settle_on_tray()
        {
            var tablet = Object.FindFirstObjectByType<TabletView>();
            foreach (var sensor in Object.FindObjectsByType<SensorAttachment>())
            {
                string id = sensor.BuildAction().Params["sensor"];
                Assert.IsTrue(tablet.Press("attach_sensor", "sensor", id));
                for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(sensor.Attached);
                Assert.IsTrue(sensor.GetComponent<Rigidbody>().isKinematic);
                Vector3 mounted = sensor.transform.position;
                for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
                Assert.Less(Vector3.Distance(mounted, sensor.transform.position), .005f);
                int before = SimulationHost.Instance.Session.Log.Count;
                Assert.IsTrue(tablet.Press("detach_sensor", "sensor", id));
                for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
                Assert.IsFalse(sensor.Attached);
                Assert.IsFalse(sensor.GetComponent<Rigidbody>().isKinematic);
                var home = GameObject.Find(sensor.name + " tray pose").transform;
                Assert.Less(Vector3.Distance(home.position, sensor.transform.position), .03f, sensor.name);
                Assert.AreEqual(before + 1, SimulationHost.Instance.Session.Log.Count, "No accidental reattach after removal");
            }
        }
    }
}
