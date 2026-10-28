using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    public sealed class TransportPresentationTests
    {
        private SimulationHost _host;
        private TransportPresentation _view;
        private static SimAction Action(string verb, params (string key, string value)[] parameters) =>
            new SimAction(verb, "patient", Channel.Vr, parameters.ToDictionary(p => p.key, p => p.value));

        [SetUp]
        public void SetUp()
        {
            if (SimulationHost.Instance != null)
            {
                SimulationHost.Instance.SaveLogOnDestroy = false;
                Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            }
            _host = new GameObject("transport presentation test host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _view = new TransportPresentation();
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" })
                Assert.IsTrue(_host.Apply(Action(verb)).Accepted);
        }

        [TearDown]
        public void TearDown() { if (_host != null) Object.DestroyImmediate(_host.gameObject); }

        private void Observe()
        {
            string log = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            _view.Observe(_host.Observable, _host.Session);
            Assert.AreEqual(log, _host.Session.Log.Hash(), "Projection must not emit actions");
            Assert.AreEqual(state, _host.Observable.Hash(), "Projection must not change clinical state");
        }

        private void Until(string stage)
        {
            for (int i = 0; i < 12000 && _host.Observable.Transport?.State != stage; i++) _host.Tick(.5);
            Assert.AreEqual(stage, _host.Observable.Transport?.State);
            Observe();
        }

        [TestCase("walk")]
        [TestCase("wheelchair")]
        [TestCase("trolley")]
        public void Every_journey_stage_uses_core_and_duplicate_observations_cannot_advance_it(string mode)
        {
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", mode),
                ("escort", "porter,nurse"), ("monitoring", "portable"), ("oxygen", "true"))).Accepted);
            var stages = new[] { "REQUESTED", "PREPARING", "IN_TRANSIT", "ARRIVED", "DONE", "RETURNING", "BACK" };
            var phases = new[] { TransportVisualPhase.Bedside, TransportVisualPhase.Bedside, TransportVisualPhase.Outbound,
                TransportVisualPhase.Inside, TransportVisualPhase.Inside, TransportVisualPhase.Returning, TransportVisualPhase.Bedside };
            for (int i = 0; i < stages.Length; i++)
            {
                Until(stages[i]);
                for (int repeat = 0; repeat < 20; repeat++) Observe();
                Assert.AreEqual(phases[i], _view.Phase);
                Assert.AreEqual(mode, _view.Mode); Assert.AreEqual("ct", _view.Destination);
                Assert.AreEqual(stages[i], _view.Stage);
                Assert.IsTrue(_view.Porter); Assert.IsTrue(_view.Nurse); Assert.IsTrue(_view.Monitor); Assert.IsTrue(_view.Oxygen);
                Assert.AreEqual(i < 2 || i == 6, _view.BedsideVisible);
                Assert.AreEqual(i == 2 || i == 5, _view.ActorVisible);
                Assert.AreEqual(_host.Observable.TraineeLocation, _view.TraineeLocation);
                Assert.AreEqual(_host.Observable.BehaviourMode ?? "", _view.Behaviour);
            }
        }

        [Test]
        public void Cancel_before_departure_keeps_bedside_and_restart_replaces_all_mode_kit()
        {
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "trolley"),
                ("escort", "porter,nurse"), ("monitoring", "portable"), ("oxygen", "true"))).Accepted);
            Observe();
            Assert.IsTrue(_host.Apply(Action("cancel_transport")).Accepted);
            Observe(); Assert.AreEqual("CANCELLED", _view.Stage); Assert.IsTrue(_view.BedsideVisible); Assert.IsFalse(_view.ActorVisible);
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "xray"), ("mode", "walk"),
                ("escort", ""), ("monitoring", "none"), ("oxygen", "false"))).Accepted);
            Observe(); Assert.AreEqual("xray", _view.Destination); Assert.AreEqual("walk", _view.Mode);
            Assert.IsFalse(_view.Porter); Assert.IsFalse(_view.Nurse); Assert.IsFalse(_view.Monitor); Assert.IsFalse(_view.Oxygen);
        }

        [Test]
        public void Corridor_cancellation_and_large_tick_restore_the_authoritative_bedside_state()
        {
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "wheelchair"))).Accepted);
            Until("IN_TRANSIT");
            Assert.IsTrue(_host.Apply(Action("cancel_transport")).Accepted); Observe();
            Assert.AreEqual(TransportVisualPhase.Returning, _view.Phase); Assert.IsFalse(_view.BedsideVisible);
            _host.Tick(7200); Observe();
            Assert.AreEqual(TransportVisualPhase.Bedside, _view.Phase); Assert.IsTrue(_view.BedsideVisible); Assert.IsFalse(_view.ActorVisible);
        }

        [Test]
        public void New_session_and_missing_session_clear_the_previous_party()
        {
            Assert.IsTrue(_host.Apply(Action("request_transport", ("to", "ct"), ("mode", "trolley"),
                ("escort", "porter,nurse"), ("monitoring", "portable"), ("oxygen", "true"))).Accepted);
            Until("IN_TRANSIT");
            _host.StartSession("VAR-S1-A", 43, SessionMode.Practice); Observe();
            Assert.IsTrue(_view.BedsideVisible); Assert.IsFalse(_view.ActorVisible); Assert.IsEmpty(_view.Mode);
            Assert.IsFalse(_view.Porter); Assert.IsFalse(_view.Monitor); Assert.IsFalse(_view.Oxygen);
            _view.Observe(null, null);
            Assert.IsEmpty(_view.Stage); Assert.IsEmpty(_view.Destination); Assert.IsEmpty(_view.TraineeLocation);
        }

        [Test]
        public void Held_lab_sample_and_rejected_destination_never_create_a_patient_journey()
        {
            Assert.IsFalse(_host.Apply(Action("request_transport", ("to", "missing"), ("mode", "walk"))).Accepted);
            Assert.IsTrue(_host.Apply(Action("order_investigation", ("test", "lft"))).Accepted);
            Assert.IsTrue(_host.Apply(Action("take_sample", ("test", "lft"), ("tube", "serum_gold"), ("label", "bedside"), ("send", "later"))).Accepted);
            _host.Tick(1200); Observe();
            Assert.IsTrue(_view.BedsideVisible); Assert.IsFalse(_view.ActorVisible); Assert.IsEmpty(_view.Stage);
        }

        [TestCase("IN_TRANSIT", "hovercraft")]
        [TestCase("FUTURE_STAGE", "walk")]
        public void Unknown_contract_values_do_not_start_a_motion(string stage, string mode)
        {
            // Deliberately synthetic contract-drift fixture; never applied to the Session.
            var fixture = _host.Observable with
            { Transport = new ObservedTransport(stage, "ct", mode, new[] { "porter" }, "none", false, "", "") };
            string log = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            _view.Observe(fixture, new object());
            Assert.AreEqual(TransportVisualPhase.Unavailable, _view.Phase); Assert.IsFalse(_view.ActorVisible);
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
        }
    }
}
