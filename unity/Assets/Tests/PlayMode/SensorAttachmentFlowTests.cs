using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ISubsystem = ProcSense.Core.Pipeline.ISubsystem;
using ProcSense.Core.Workflow;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Patient equipment in the shipped game flow (Boot, menu-started session), driven through XRI the way a controller does: a hand interactor selects the sensor, carries it to the patient
    /// target slightly off centre, releases it, the patient socket takes it and Core logs attach_sensor; it stays on through idle motion, animator culling and LOD changes; a hand takes it
    /// off (detach_sensor) and it can be placed again. Before the menu has started a session the target is shown as unavailable and nothing snaps (15 Nov 2026 headset regression).
    /// </summary>
    public class SensorAttachmentFlowTests
    {
        private static readonly string[] Sensors = { "spo2", "nibp_cuff" };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ProcSense.VR.Boot.BootLoader.MenuStartsSession = !Application.isEditor;
            SimulationHost.AutoStartFromConfig = true;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            SimulationHost.SessionsDirectoryOverride = null;
            CharacterPerfCuller.VisibilityProbe = r => r.isVisible;
            ModeChoice.Reset();
            yield return TestHostHygiene.ResetToEmptyScene();
        }

        private static IEnumerator BootPlayer()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            var dir = Path.Combine(Application.temporaryCachePath, "sensor_flow_tests");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);   // no interrupted session from an earlier run is offered
            SimulationHost.SessionsDirectoryOverride = dir;
            ProcSense.VR.Boot.BootLoader.MenuStartsSession = true;          // the player configuration: no session until the menu starts one
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return EditorXRTestEnvironment.EnsureSimulator();
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            for (int i = 0; i < 20 && MenuFlow.Current == null; i++) yield return null;
            Assert.IsNotNull(MenuFlow.Current);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
        }

        private static void StartScenario(string modeButton)
        {
            var installer = Object.FindFirstObjectByType<MenuFlowInstaller>();
            Assert.IsTrue(installer.Panel.Click(modeButton));
            Assert.IsTrue(installer.Panel.Click(installer.Panel.VisibleButtons().First(b => b.StartsWith("Gallbladder removal"))));
            Assert.IsTrue(installer.Panel.Click("Begin"));
            Assert.AreEqual(MenuPhase.Playing, installer.Flow.Phase, installer.Flow.Message);
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions);
        }

        private static SensorAttachment Binding(string sensor) =>
            Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None).Single(s => s.BuildAction().Params["sensor"] == sensor);

        /// <summary>A test hand: an XR direct interactor registered with the sensor's own interaction manager (the controller path).</summary>
        private static XRDirectInteractor Hand(XRGrabInteractable grab)
        {
            var hand = new GameObject("Test hand").AddComponent<XRDirectInteractor>();
            hand.interactionManager = grab.interactionManager;
            return hand;
        }

        /// <summary>Selects the sensor with the hand and carries it so its fitted anchor sits <paramref name="offset"/> from the patient target (the hand's attach point is where the anchor goes).</summary>
        private static IEnumerator Carry(XRDirectInteractor hand, SensorAttachment binding, Vector3 offset)
        {
            var grab = binding.GetComponent<XRGrabInteractable>();
            var target = binding.Destination.attachTransform;
            if (!grab.interactorsSelecting.Contains(hand)) grab.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            Assert.IsTrue(grab.interactorsSelecting.Contains(hand), "the hand selects the sensor");
            for (int i = 0; i < 12; i++)
            {
                hand.transform.SetPositionAndRotation(target.position + offset, target.rotation);
                yield return new WaitForFixedUpdate();
            }
        }

        private static IEnumerator Release(XRDirectInteractor hand, SensorAttachment binding)
        {
            var grab = binding.GetComponent<XRGrabInteractable>();
            grab.interactionManager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
        }

        private static int Accepted(string verb, string sensor) =>
            SimulationHost.Instance.Session.Log.Entries.Count(e => e.Accepted && e.Verb == verb && e.Params.TryGetValue("sensor", out var s) && s == sensor);

        private static void AssertSnapped(SensorAttachment binding, float metres, string because)
        {
            var grab = binding.GetComponent<XRGrabInteractable>();
            var actual = grab.GetAttachTransform(binding.Destination);
            var target = binding.Destination.attachTransform;
            Assert.IsTrue(binding.Attached, because);
            Assert.IsTrue(binding.Destination.interactablesSelected.Contains(grab), because + ": the patient socket holds the sensor");
            Assert.That(Vector3.Distance(actual.position, target.position), Is.LessThan(metres), because + ": anchor on the patient target");
            Assert.That(Quaternion.Angle(actual.rotation, target.rotation), Is.LessThan(1f), because + ": fitted orientation");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Before_the_menu_starts_a_session_the_target_is_unavailable_and_nothing_snaps()
        {
            yield return BootPlayer();
            Assert.IsNull(SimulationHost.Instance.Session, "the player starts no fixed case");
            Assert.IsFalse(SensorAttachment.SessionAcceptsActions);
            foreach (var sensor in Sensors)
            {
                var binding = Binding(sensor);
                var hand = Hand(binding.GetComponent<XRGrabInteractable>());
                try
                {
                    yield return Carry(hand, binding, new Vector3(.01f, 0, 0));
                    yield return null;
                    Assert.IsTrue(binding.PlacementGuidesVisible, sensor + ": the target is still shown while held");
                    Assert.AreEqual(SensorPlacementGuide.TargetState.Unavailable, binding.PatientTargetState, sensor + ": never shown ready when nothing can record the placement");
                    Assert.AreEqual(EquipmentRefusalText.NoSession, binding.NoticeText, sensor + ": the label says why, not only the amber target");
                    yield return Release(hand, binding);
                    Assert.IsFalse(binding.Attached, sensor);
                    Assert.IsFalse(binding.Destination.hasSelection, sensor + ": the socket must not take it without a session");
                    Assert.AreEqual(EquipmentRefusalText.NoSession, binding.NoticeText, sensor + ": still explained after release");
                }
                finally { Object.Destroy(hand.gameObject); }
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Training_select_attach_stay_on_and_remove_through_the_hand() => Cycle("TRAINING MODE");

        [UnityTest, Timeout(300000)]
        public IEnumerator Examination_select_attach_stay_on_and_remove_through_the_hand() => Cycle("TEST / EXAMINATION MODE");

        private IEnumerator Cycle(string modeButton)
        {
            yield return BootPlayer();
            StartScenario(modeButton);
            yield return null;
            foreach (var sensor in Sensors)
            {
                var binding = Binding(sensor);
                var grab = binding.GetComponent<XRGrabInteractable>();
                var hand = Hand(grab);
                try
                {
                    // select and carry to the arm/finger target, 1 cm off centre: the guide reads ready
                    yield return Carry(hand, binding, new Vector3(.01f, 0, 0));
                    yield return null;
                    Assert.AreEqual(SensorPlacementGuide.TargetState.Ready, binding.PatientTargetState, sensor);
                    // release: the socket takes it, snaps it to the fitted pose and Core logs exactly one accepted attach
                    yield return Release(hand, binding);
                    AssertSnapped(binding, .001f, sensor + " after release");
                    Assert.AreEqual(1, Accepted("attach_sensor", sensor), sensor + ": one accepted attach_sensor");

                    // stays on: idle arm motion, the character culler's worst case (nothing reported visible) and every LOD level
                    bool cullerWas = CharacterPerfCuller.Enabled;
                    CharacterPerfCuller.Enabled = true;
                    CharacterPerfCuller.VisibilityProbe = r => false;
                    var culler = new GameObject("Test culler").AddComponent<CharacterPerfCuller>();
                    culler.CullDelaySeconds = 0f;
                    var patientAnimator = binding.Destination.GetComponentInParent<Animator>();
                    try
                    {
                        culler.Rescan();
                        if (patientAnimator != null) Assert.IsFalse(culler.IsAnimatorEligible(patientAnimator), "the animator carrying the patient attach anchors is never culled");
                        var lod = binding.Destination.GetComponentInParent<LODGroup>();
                        for (int level = -1; level < 3; level++)
                        {
                            if (lod != null) lod.ForceLOD(level);
                            for (int f = 0; f < 20; f++)
                            {
                                yield return null;
                                AssertSnapped(binding, .002f, sensor + " frame " + f + " LOD " + level);
                            }
                        }
                        if (lod != null) lod.ForceLOD(-1);
                    }
                    finally { Object.Destroy(culler.gameObject); CharacterPerfCuller.VisibilityProbe = r => r.isVisible; CharacterPerfCuller.Enabled = cullerWas; }
                    Assert.AreEqual(1, Accepted("attach_sensor", sensor), sensor + ": staying on emits nothing");

                    // remove: the hand takes it off the patient and carries it away to the bedside (not straight above the target: dropped there it would land back on it and attach again, legitimately);
                    // Core logs detach_sensor and the socket does not take it back
                    yield return Carry(hand, binding, new Vector3(.45f, .25f, 0));
                    Assert.IsFalse(binding.Attached, sensor + " after taking it off");
                    Assert.AreEqual(1, Accepted("detach_sensor", sensor), sensor + ": one accepted detach_sensor");
                    yield return Release(hand, binding);
                    Assert.IsFalse(binding.Attached, sensor + " must not jump back on");

                    // reposition: it can be placed again
                    yield return Carry(hand, binding, new Vector3(0, 0, .008f));
                    yield return Release(hand, binding);
                    AssertSnapped(binding, .001f, sensor + " placed again");
                    Assert.AreEqual(2, Accepted("attach_sensor", sensor), sensor);
                }
                finally { Object.Destroy(hand.gameObject); }
            }
        }
        /// <summary>Attaches one sensor through the hand and checks the engine took it.</summary>
        private static IEnumerator AttachWithHand(string sensor, string because)
        {
            var binding = Binding(sensor);
            var hand = Hand(binding.GetComponent<XRGrabInteractable>());
            int before = Accepted("attach_sensor", sensor);
            try
            {
                yield return Carry(hand, binding, new Vector3(.006f, 0, 0));
                yield return Release(hand, binding);
            }
            finally { Object.Destroy(hand.gameObject); }
            AssertSnapped(binding, .001f, sensor + " " + because);
            Assert.AreEqual(before + 1, Accepted("attach_sensor", sensor), sensor + " " + because);
            Assert.AreEqual("", binding.NoticeText, sensor + " " + because + ": no refusal shown after an accepted attach");
        }

        /// <summary>A Core subsystem that refuses attach_sensor with a real engine reason (the patient is elsewhere), first in the pipeline so nothing else changes.</summary>
        private sealed class RefuseAttach : ISubsystem
        {
            public int Order => int.MinValue;
            public void Init(Session session) { }
            public void OnAction(SimAction action, ActionOutcome outcome) { if (action.Verb == "attach_sensor") outcome.Reject("PATIENT_ELSEWHERE"); }
            public void OnTick() { }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Training_shows_the_engine_reason_and_the_wrong_target() => Refusals("TRAINING MODE", "Not attached: the patient is not here.", EquipmentRefusalText.WrongTarget);

        [UnityTest, Timeout(300000)]
        public IEnumerator Examination_shows_a_neutral_reason_without_hints() => Refusals("TEST / EXAMINATION MODE", EquipmentRefusalText.ExamNotAttached, EquipmentRefusalText.ExamWrongTarget);

        private IEnumerator Refusals(string modeButton, string refusal, string wrongTarget)
        {
            yield return BootPlayer();
            // wrong target first, with the normal engine: the cuff released on the finger target is not attached and the label says so
            StartScenario(modeButton);
            for (int i = 0; i < 3; i++) yield return null;   // a new session sends every loose sensor home on the next LateUpdate (also out of a hand): let that happen first
            var cuff = Binding("nibp_cuff");
            var spo2 = Binding("spo2");
            var hand = Hand(cuff.GetComponent<XRGrabInteractable>());
            try
            {
                var grab = cuff.GetComponent<XRGrabInteractable>();
                grab.interactionManager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                for (int i = 0; i < 12; i++) { hand.transform.SetPositionAndRotation(spo2.Destination.attachTransform.position, spo2.Destination.attachTransform.rotation); yield return new WaitForFixedUpdate(); }
                yield return Release(hand, cuff);
                Assert.IsFalse(cuff.Attached);
                Assert.IsFalse(spo2.Destination.hasSelection, "the finger target never takes the cuff");
                Assert.AreEqual(wrongTarget, cuff.NoticeText);
            }
            finally { Object.Destroy(hand.gameObject); }

            // an engine refusal in a running scenario: start the same mode again with a pipeline that refuses attach_sensor (a real Core rejection through Fire)
            var flow = Object.FindFirstObjectByType<MenuFlowInstaller>().Flow;
            flow.BackToMenu();
            SimulationHost.Instance.SubsystemFactory = c => new ISubsystem[] { new RefuseAttach() }.Concat(Subsystems.Default(c));
            try
            {
                StartScenario(modeButton);
                yield return null;
                hand = Hand(spo2.GetComponent<XRGrabInteractable>());
                try
                {
                    yield return Carry(hand, spo2, new Vector3(.006f, 0, 0));
                    yield return Release(hand, spo2);
                }
                finally { Object.Destroy(hand.gameObject); }
                Assert.IsFalse(spo2.Attached, "a refused attach does not stay on");
                Assert.AreEqual(0, Accepted("attach_sensor", "spo2"));
                Assert.AreEqual("PATIENT_ELSEWHERE", SimulationHost.Instance.Session.LastRejection);
                Assert.AreEqual(refusal, spo2.NoticeText, "the refusal is said, not silent");
                if (modeButton != "TRAINING MODE")
                    StringAssert.DoesNotContain("patient is not here", spo2.NoticeText, "the examination gives no hint");
            }
            finally { SimulationHost.Instance.SubsystemFactory = null; }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator Running_scenarios_accept_equipment_through_menu_retry_new_scenario_and_resume()
        {
            yield return BootPlayer();
            var installer = Object.FindFirstObjectByType<MenuFlowInstaller>();
            var flow = installer.Flow; var panel = installer.Panel;
            var host = SimulationHost.Instance;
            Assert.IsFalse(SensorAttachment.SessionAcceptsActions);
            Assert.AreEqual(EquipmentRefusalText.NoSessionCode, SensorAttachment.UnavailableReason);

            Assert.IsTrue(panel.Click("TRAINING MODE"));
            Assert.IsFalse(SensorAttachment.SessionAcceptsActions, "choosing a mode starts nothing");
            Assert.IsTrue(panel.Click(panel.VisibleButtons().First(b => b.StartsWith("Gallbladder removal"))));
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions, "the briefing already has its session");
            Assert.IsTrue(panel.Click("Begin"));
            for (int i = 0; i < 3; i++) yield return null;
            yield return AttachWithHand("spo2", "while playing");

            // back to the menu with the scenario still running: not blocked by the menu phase
            flow.BackToMenu();
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase);
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions, "a begun scenario is not blocked by the menu");
            yield return AttachWithHand("nibp_cuff", "with the menu open");
            Assert.IsNotNull(host.SaveSessionLog());
            flow.FindInterrupted();
            Assert.IsNotNull(flow.Interrupted, "the saved, unsubmitted session is offered");

            // resume the interrupted session: equipment comes back on from the log and the hand still works
            var oldSession = host.Session;
            Assert.IsTrue(flow.Resume());
            Assert.AreNotSame(oldSession, host.Session);
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions, "after resume");
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(Binding("spo2").Attached && Binding("nibp_cuff").Attached, "resumed equipment is back on the patient");

            // submit: the scenario is finished and says so
            Assert.IsTrue(panel.Click("Submit patient to OT"));
            Assert.IsTrue(panel.Click("Confirm: submit patient to OT"));
            Assert.AreEqual(MenuPhase.Results, flow.Phase);
            Assert.IsFalse(SensorAttachment.SessionAcceptsActions);
            Assert.AreEqual(EquipmentRefusalText.FinishedCode, SensorAttachment.UnavailableReason);

            // retry and new scenario: each new session takes equipment again
            Assert.IsTrue(panel.Click("Retry"));
            Assert.IsTrue(panel.Click("Begin"));
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions, "after Retry");
            // the new session took the sensors off; the XRI socket waits its recycle delay (1 s) before it takes the same sensor again
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(Binding("spo2").Attached, "a new session starts without equipment on");
            yield return AttachWithHand("spo2", "after Retry");
            Assert.IsTrue(panel.Click("Submit patient to OT"));
            Assert.IsTrue(panel.Click("Confirm: submit patient to OT"));
            Assert.IsTrue(panel.Click("New scenario"));
            Assert.IsTrue(panel.Click("Begin"));
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions, "after New scenario");
            yield return new WaitForSeconds(1.2f);
            yield return AttachWithHand("nibp_cuff", "after New scenario");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator With_the_fixed_start_up_case_the_hand_attaches_too()
        {
            SimulationHost.AutoStartFromConfig = true;     // the Editor and test configuration: the scene's SessionConfig starts at once
            yield return SceneManager.LoadSceneAsync("PatientBay");
            yield return TestHostHygiene.WaitForHost(50);
            var host = SimulationHost.Instance; host.AutoTick = false; host.SaveLogOnDestroy = false;
            Assert.IsNotNull(host.Session);
            Assert.IsTrue(SensorAttachment.SessionAcceptsActions);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            yield return AttachWithHand("spo2", "with the start-up case");
        }
    }
}
