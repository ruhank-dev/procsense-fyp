using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public sealed class EnvironmentIntegrationTests
    {
        [SetUp] public void Setup() => ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }

        [UnityTest]
        public IEnumerator Visible_patient_owns_band_sensors_nurse_contact_and_speech()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            var patient = Object.FindAnyObjectByType<PatientMotion>(); Assert.IsNotNull(patient);
            var nurse = Object.FindAnyObjectByType<NurseWristbandController>(); Assert.IsNotNull(nurse);
            Assert.IsTrue(nurse.Wrist.IsChildOf(patient.transform), "Nurse must reach the visible wrist");
            var sensors = Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None);
            Assert.AreEqual(2, sensors.Length);
            foreach (var sensor in sensors) Assert.IsTrue(sensor.Destination.transform.IsChildOf(patient.transform), sensor.name);
            var presentation = Object.FindAnyObjectByType<SessionPresentation>(); presentation.Sync();
            Assert.AreSame(patient.Head, presentation.Voice.Heads["patient"]);
            var station = Object.FindObjectsByType<ProcSense.VR.UI.ClinicalPanelStation>(FindObjectsSortMode.None).Single(s => s.WardAnchor != null);
            var television = GameObject.Find("hospital_tv"); Assert.IsNotNull(television);
            Assert.Greater(station.WardAnchor.position.z, television.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.z) + .005f,
                "The checklist must be in front of the television, not hidden inside it");
            var tablet = Object.FindAnyObjectByType<TabletView>();
            Assert.IsTrue(tablet.Press("attach_sensor", "sensor", "spo2"));
            Assert.IsTrue(tablet.Press("attach_sensor", "sensor", "nibp_cuff"));
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            var hand = nurse.Wrist.parent; var original = hand.localPosition;
            var log = host.Session.Log.Hash(); var state = host.Observable.Hash();
            try
            {
                hand.localPosition += hand.InverseTransformVector(Vector3.up * .025f);
                for (int i = 0; i < 3; i++) yield return null;
                foreach (var sensor in sensors)
                {
                    Assert.IsTrue(sensor.Attached);
                    var grab = sensor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                    Assert.Less(Vector3.Distance(grab.GetAttachTransform(sensor.Destination).position, sensor.Destination.attachTransform.position), .0002f);
                }
                Assert.AreEqual(log, host.Session.Log.Hash(), "Cosmetic bone motion must not emit actions");
                Assert.AreEqual(state, host.Observable.Hash());
            }
            finally { hand.localPosition = original; }
        }
    }
}
