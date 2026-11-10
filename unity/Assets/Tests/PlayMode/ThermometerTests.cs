using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Workflow;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ProcSense.Tests.PlayMode
{
    public sealed class ThermometerTests
    {
        private ContactActionEmitter _tool;
        private XRGrabInteractable _grab;
        private XRDirectInteractor _hand;
        private SimulationHost _host;
        private EquipmentSurfaceView Display => _tool.GetComponentInChildren<EquipmentSurfaceView>();
        [UnitySetUp] public IEnumerator Open()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            _host = SimulationHost.Instance; _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _tool = Object.FindAnyObjectByType<ContactActionEmitter>(); Assert.IsNotNull(_tool);
            _grab = _tool.GetComponent<XRGrabInteractable>();
            _hand = new GameObject("Thermometer test hand").AddComponent<XRDirectInteractor>();
            _hand.interactionManager = _grab.interactionManager;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_hand != null) Object.DestroyImmediate(_hand.gameObject);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            yield return EditorXRTestEnvironment.Cleanup();
        }
        private void Hold() => _grab.interactionManager.SelectEnter((IXRSelectInteractor)_hand, (IXRSelectInteractable)_grab);
        private void Activate() => _grab.activated.Invoke(new ActivateEventArgs { interactorObject = _hand, interactableObject = _grab });
        private void AtEar(float offset = 0)
        {
            // Pose fixture: contact validation uses the actual fitted probe tip and moving head target.
            var shift = _tool.ContactTarget.position + Vector3.right * offset - _tool.ContactPoint.position;
            _tool.transform.position += shift;
            _tool.GetComponent<Rigidbody>().position = _tool.transform.position;
            Physics.SyncTransforms();
        }

        [UnityTest] public IEnumerator Held_ear_activation_matches_Core_and_tablet_without_pickup_or_duplicate_actions()
        {
            Display.RenderNow(); Assert.AreEqual("--.- C", Display.DisplayText);
            Hold(); Assert.AreEqual(0, _host.Session.Log.Count);
            AtEar(); Activate(); Activate();
            Assert.AreEqual(1, _host.Session.Log.Count);
            Assert.IsNotNull(_host.Observable.Vitals.SingleOrDefault(v => v.Name == "temperature_c"));
            Display.RenderNow(); Assert.AreEqual(_host.Observable.Vitals.Single(v => v.Name == "temperature_c").Value + " C", Display.DisplayText);
            var reference = new Session(_host.Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(_host.Content));
            reference.Apply(_tool.BuildAction());
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
            var log = _host.Session.Log.Hash(); var state = _host.Observable.Hash();
            _grab.interactionManager.SelectExit((IXRSelectInteractor)_hand, (IXRSelectInteractable)_grab);
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Display.RenderNow(); Assert.AreEqual("--.- C", Display.DisplayText);
            Assert.IsTrue(TabletFallbackRegistry.Fire(_tool.BuildAction()).Accepted);
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            Display.RenderNow(); Assert.That(Display.DisplayText, Does.Not.Contain("--"));
            yield return null;
        }

        [UnityTest] public IEnumerator Unheld_distant_and_stale_hand_activation_send_nothing_and_Core_rejection_fabricates_no_reading()
        {
            AtEar(); Activate(); Assert.AreEqual(0, _host.Session.Log.Count);
            Hold(); AtEar(_tool.ContactRadius + .02f); Activate(); Assert.AreEqual(0, _host.Session.Log.Count);
            AtEar();
            _grab.activated.Invoke(new ActivateEventArgs()); Assert.AreEqual(0, _host.Session.Log.Count);
            // Reusable contact emitter pilot: Core refuses NIBP without its cuff, leaving the display unchanged.
            _tool.Configure("measure_nibp");
            bool? accepted = null; _tool.Fired += answer => accepted = answer.Accepted;
            Activate(); Assert.AreEqual(false, accepted); Assert.AreEqual(1, _host.Session.Log.Count);
            Display.RenderNow(); Assert.AreEqual("--.- C", Display.DisplayText);
            yield return null;
        }

        [UnityTest] public IEnumerator Disable_and_end_remove_physical_input_and_fallback_while_ear_follows_the_visible_head()
        {
            var log = _host.Session.Log.Hash(); var target = _tool.ContactTarget;
            var local = target.localPosition; var parent = target.parent; var rotation = parent.localRotation;
            parent.localRotation *= Quaternion.Euler(0, 3, 0);
            Assert.AreEqual(local, target.localPosition); Assert.AreEqual(log, _host.Session.Log.Hash());
            parent.localRotation = rotation;
            Hold(); AtEar(); _tool.enabled = false; Activate();
            Assert.IsFalse(TabletFallbackRegistry.All.Any(e => e.Owner == _tool));
            Assert.AreEqual(0, _host.Session.Log.Count);
            _tool.enabled = true; Assert.AreEqual(1, TabletFallbackRegistry.All.Count(e => e.Owner == _tool));
            // Finished-host fixture: exercise the input guard without saving/queuing a real user's session.
            typeof(SimulationHost).GetField("_ended", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(_host, true);
            Activate(); Assert.AreEqual(0, _host.Session.Log.Count);
            Assert.IsFalse(_tool.PatientAvailable);
            yield return null;
        }
    }
}
