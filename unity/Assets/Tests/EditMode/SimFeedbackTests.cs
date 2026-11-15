using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Events;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-SES-10: on a real session (VAR-S1-A), the feedback stream reports accepted and refused actions (with the trainee text), the goals the case graph completes with their
    /// score items, clinical events as alarms, the end state after submission, the flow phase and the gateway's own refusals, and changes nothing in the session.
    /// </summary>
    public class SimFeedbackTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static IContentReader Real() => new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private SimulationHost _host;
        private SimFeedback _feedback;
        private string _phase;
        private readonly List<SimFeedbackEvent> _raised = new List<SimFeedbackEvent>();

        [SetUp]
        public void SetUp()
        {
            _raised.Clear();
            _phase = null;
            _host = new GameObject("feedback host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _feedback = new SimFeedback(() => _host.Session, () => _phase);
            _feedback.Attach(_host);
            _feedback.Raised += _raised.Add;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice, Real());
        }

        [TearDown] public void TearDown() => Object.DestroyImmediate(_host.gameObject);

        private ActionOutcome Act(string verb) => _host.Apply(new SimAction(verb, "patient", Channel.Cli));
        private IEnumerable<SimFeedbackEvent> Of(FeedbackKind k) => _raised.Where(e => e.Kind == k);

        [Test]
        public void Actions_goals_and_score_items_are_reported_and_the_session_is_unchanged()
        {
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Act(verb).Accepted, verb);
            string hash = _host.Session.Log.Hash();
            Assert.AreEqual(4, Of(FeedbackKind.ActionAccepted).Count());
            Assert.AreEqual("confirm_identity", Of(FeedbackKind.ActionAccepted).Last().Id);
            var goals = _host.Session.GraphHistory.Where(g => g.Kind == "goal_complete").ToList();
            Assert.Greater(goals.Count, 0, "confirming identity completes a goal of the case");
            CollectionAssert.AreEqual(goals.Select(g => g.Id), Of(FeedbackKind.GoalCompleted).Select(e => e.Id));
            Assert.AreEqual(goals.Sum(g => g.ScoreItems.Count), Of(FeedbackKind.ScoreItem).Count());
            _feedback.Poll();
            Assert.AreEqual(goals.Count, Of(FeedbackKind.GoalCompleted).Count(), "polling again raises nothing twice");
            Assert.AreEqual(hash, _host.Session.Log.Hash(), "the stream only reads");
        }

        [Test]
        public void A_refused_action_carries_its_reason_and_text()
        {
            var o = Act("no_such_verb_at_all");
            Assert.IsFalse(o.Accepted);
            var e = Of(FeedbackKind.ActionRejected).Single();
            Assert.AreEqual(o.RejectReason, e.Ids[2]);
            Assert.AreEqual(HintTexts.For(o.RejectReason), e.Text);
        }

        [Test]
        public void Clinical_events_phases_and_the_end_state_are_reported()
        {
            _host.Session.Events.Publish(new EventFired("E-TEST-ALARM", 7));
            Assert.AreEqual("E-TEST-ALARM", Of(FeedbackKind.Alarm).Single().Id);

            _phase = "Playing";
            _feedback.Poll();
            _feedback.Poll();
            Assert.AreEqual("Playing", Of(FeedbackKind.Phase).Single().Id, "one event per change");

            var submit = _host.Apply(new SimAction(ExamControl.SubmitPatient, "", Channel.Vr));
            Assert.IsTrue(submit.Accepted, submit.RejectReason);
            Assert.IsNotEmpty(_host.Session.EndStates.Resolved);
            Assert.AreEqual(_host.Session.EndStates.Resolved, Of(FeedbackKind.EndState).Single().Id);
        }

        [Test]
        public void The_gateway_refusals_that_never_reached_the_engine_are_reported_once()
        {
            var gateway = new ActionGateway(new HostTarget(_host), () => 0f, () => false);
            gateway.Outcome += _feedback.OnGatewayOutcome;
            gateway.Submit(new ActionRequest(new SimAction("ask_identity", "patient", Channel.Cli), ActionSource.Voice));
            gateway.Submit(new ActionRequest(new SimAction("ask_identity", "patient", Channel.Cli), ActionSource.Tablet));     // duplicate within 8 s
            gateway.Submit(new ActionRequest(new SimAction("show_how_to", "consent"), ActionSource.Menu, practiceOnly: true));  // practice-only in the examination
            Assert.AreEqual(1, Of(FeedbackKind.ActionAccepted).Count(), "reported once, by the host");
            CollectionAssert.AreEqual(new[] { ActionGateway.DuplicateCode, ActionGateway.PracticeOnlyCode }, Of(FeedbackKind.ActionRejected).Select(e => e.Ids[2]));
        }

        private sealed class HostTarget : IActionTarget
        {
            private readonly SimulationHost _h;
            public HostTarget(SimulationHost h) { _h = h; }
            public ActionOutcome Apply(SimAction a) => _h.Apply(a);
            public long Step => _h.Session.Clock.Steps;
        }
    }
}
