using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-SES-03: the action gateway answers accepted, rejected, duplicate (by request id and within 8 s by content), practice-only, no session and paused, and raises one outcome per request.</summary>
    public class ActionGatewayTests
    {
        private sealed class FakeTarget : IActionTarget
        {
            public readonly List<SimAction> Applied = new List<SimAction>();
            public string RejectWith = "";
            public long Step { get; private set; }
            public ActionOutcome Apply(SimAction action)
            {
                Applied.Add(action);
                var o = new ActionOutcome();
                if (!string.IsNullOrEmpty(RejectWith)) o.Reject(RejectWith); else Step++;
                return o;
            }
        }

        private FakeTarget _target;
        private float _now;
        private bool _practice;
        private ActionGateway _gateway;
        private readonly List<GatewayOutcome> _raised = new List<GatewayOutcome>();

        [SetUp]
        public void SetUp()
        {
            _target = new FakeTarget();
            _now = 100f;
            _practice = true;
            _raised.Clear();
            _gateway = new ActionGateway(_target, () => _now, () => _practice);
            _gateway.Outcome += (r, o) => _raised.Add(o);
        }

        private static SimAction Bp(Channel c = Channel.Tablet) => new SimAction("measure", "bp", c);

        [Test]
        public void An_accepted_action_reaches_the_engine_once_and_reports_the_step()
        {
            var o = _gateway.Submit(new ActionRequest(Bp(), ActionSource.Tablet));
            Assert.IsTrue(o.Accepted);
            Assert.IsFalse(o.Duplicate);
            Assert.AreEqual(1, o.EngineStep);
            Assert.AreEqual(1, _target.Applied.Count);
            Assert.AreEqual(1, _raised.Count);
            Assert.AreEqual(Channel.Tablet, _target.Applied[0].Channel, "the action keeps its own channel (engine credit)");
        }

        [Test]
        public void A_rejected_action_carries_its_reason_and_the_trainee_text()
        {
            _target.RejectWith = "SESSION_PAUSED";
            var o = _gateway.Submit(new ActionRequest(Bp(), ActionSource.Menu));
            Assert.IsFalse(o.Accepted);
            Assert.AreEqual("SESSION_PAUSED", o.ReasonCode);
            Assert.IsNotEmpty(o.HintText);
            _target.RejectWith = "";
            Assert.IsTrue(_gateway.Submit(new ActionRequest(Bp(), ActionSource.Menu)).Accepted, "a refused action does not start the duplicate window");
        }

        [Test]
        public void The_same_order_from_any_source_within_eight_seconds_is_a_duplicate()
        {
            Assert.IsTrue(_gateway.Submit(new ActionRequest(Bp(Channel.Tablet), ActionSource.Tablet)).Accepted);
            _now += 3f;
            var again = _gateway.Submit(new ActionRequest(Bp(Channel.Vr), ActionSource.Voice));
            Assert.IsFalse(again.Accepted);
            Assert.IsTrue(again.Duplicate);
            Assert.AreEqual(ActionGateway.DuplicateCode, again.ReasonCode);
            Assert.AreEqual(1, _target.Applied.Count, "the duplicate never reached the engine");
            _now += ActionGateway.DuplicateWindowSeconds;
            Assert.IsTrue(_gateway.Submit(new ActionRequest(Bp(), ActionSource.Tablet)).Accepted, "after the window it is a new order");
            Assert.IsTrue(_gateway.Submit(new ActionRequest(new SimAction("measure", "temperature", Channel.Tablet), ActionSource.Tablet)).Accepted, "a different order is not a duplicate");
        }

        [Test]
        public void The_same_request_id_gets_its_first_answer_back()
        {
            var first = _gateway.Submit(new ActionRequest(Bp(), ActionSource.Voice, requestId: "r-1"));
            _now += 20f;
            var again = _gateway.Submit(new ActionRequest(Bp(), ActionSource.Voice, requestId: "r-1"));
            Assert.IsTrue(first.Accepted);
            Assert.IsTrue(again.Accepted, "the first answer, not a new action");
            Assert.IsTrue(again.Duplicate);
            Assert.AreEqual(1, _target.Applied.Count);
        }

        [Test]
        public void A_practice_only_aid_is_refused_in_the_examination()
        {
            _practice = false;
            var o = _gateway.Submit(new ActionRequest(new SimAction("show_how_to", "consent"), ActionSource.Menu, practiceOnly: true));
            Assert.AreEqual(ActionGateway.PracticeOnlyCode, o.ReasonCode);
            Assert.AreEqual(0, _target.Applied.Count);
            _practice = true;
            Assert.IsTrue(_gateway.Submit(new ActionRequest(new SimAction("show_how_to", "consent"), ActionSource.Menu, practiceOnly: true)).Accepted);
        }

        [Test]
        public void Without_a_session_the_app_gateway_answers_no_session()
        {
            Assert.Throws<System.ArgumentNullException>(() => new ActionGateway(null, () => 0f), "a gateway needs a target");
            var app = ActionGateway.Default;
            app.Reset();
            var o = app.Submit(new ActionRequest(Bp(), ActionSource.Menu));
            Assert.IsFalse(o.Accepted);
            Assert.AreEqual("NO_SESSION", o.ReasonCode);
        }

        [Test]
        public void An_empty_request_is_invalid_and_never_reaches_the_engine()
        {
            Assert.AreEqual(ActionGateway.InvalidCode, _gateway.Submit(new ActionRequest(new SimAction(""), ActionSource.Voice)).ReasonCode);
            Assert.AreEqual(ActionGateway.InvalidCode, _gateway.Submit(null).ReasonCode);
            Assert.AreEqual(0, _target.Applied.Count);
            Assert.AreEqual(2, _raised.Count, "every answer is raised");
        }

        [Test]
        public void The_duplicate_key_ignores_the_source_and_orders_parameters()
        {
            var a = new SimAction("give", "drug", Channel.Vr, new Dictionary<string, string> { ["dose"] = "1", ["route"] = "iv" });
            var b = new SimAction("give", "drug", Channel.Tablet, new Dictionary<string, string> { ["route"] = "iv", ["dose"] = "1" });
            Assert.AreEqual(ActionGateway.Key(a), ActionGateway.Key(b));
            Assert.AreNotEqual(ActionGateway.Key(a), ActionGateway.Key(new SimAction("give", "drug", Channel.Vr, new Dictionary<string, string> { ["dose"] = "2", ["route"] = "iv" })));
        }
    }
}
