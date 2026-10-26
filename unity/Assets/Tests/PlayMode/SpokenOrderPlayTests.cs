using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using ProcSense.VR.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The spoken medical orders in the room (ADR-0028, 27 Oct 2026): the ordinary spoken-action path and the order route work side by side without a second model call for an ordinary sentence; a staff-directed sentence that started no tablet action is offered to the order
    /// route; a proposal waits on the panel for the trainee's Yes (a button or the spoken word) before the engine sees anything; a question to clarify, the closed list of a missing detail and the interpreter badge (Gemma Cloud or the offline grammar) are on the panel. The
    /// services are replaced by scripted answers; the engine, the tablet and the panel are real. The headset check (the panel in the room, a real microphone) is Sannan's.
    /// </summary>
    public class SpokenOrderPlayTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;
        private TabletView _tablet;
        private SpokenActionRouter _router;
        private SpokenOrderFlow _flow;
        private TeamConversation _talk;
        private OrderPanelView _panel;
        private int _actionAsks, _hears, _confirms, _details;
        private OrderHearing _hearing;
        private OrderConfirmation _confirmation = new OrderConfirmation { FromService = true, Outcome = "open" };

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        private static OrderHearing Proposal(string label, string readBack, string ticket, bool gemma = true) => new OrderHearing
        {
            FromService = true, Status = "confirm", Intent = "ORDER_TEST", Label = label, ReadBack = readBack, Ticket = ticket, Interpretation = "order", Source = gemma ? "model" : "grammar",
            Interpreter = gemma ? "gemma_cloud" : "offline_grammar", FallbackReason = gemma ? "" : "CLOUD_UNREACHABLE",
        };

        private void Rig()
        {
            _actionAsks = _hears = _confirms = _details = 0;                                              // NUnit keeps one instance of the fixture for all its tests
            _hearing = null;
            _confirmation = new OrderConfirmation { FromService = true, Outcome = "open" };
            TabletFallbackRegistry.Clear();
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) _host.Apply(new SimAction(v));
            _tablet = Make<TabletView>("tablet");
            _tablet.RenderNow();
            var go = new GameObject("voices");
            _made.Add(go);
            go.SetActive(false);
            var voices = go.AddComponent<TeamVoicesView>();
            _talk = go.AddComponent<TeamConversation>();
            _talk.Voices = voices; _talk.SpeakAloud = false; _talk.Auth = () => null; _talk.Host = () => _host;
            _router = go.AddComponent<SpokenActionRouter>();
            _router.Tablet = _tablet; _router.Talk = _talk; _router.Host = () => _host; _router.Auth = () => null; _router.EvaluateSpeech = false;
            _flow = go.AddComponent<SpokenOrderFlow>();
            _flow.Talk = _talk; _flow.Host = () => _host; _flow.Auth = () => null;
            _flow.Hearer = (auth, text, context, done) => Hear(done);
            _flow.Confirmer = (auth, ticket, spoken, done) => Confirm(done);
            _flow.Detailer = (auth, ticket, param, value, done) => Detail(param, value, done);
            _router.Orders = _flow;
            go.SetActive(true);
            _router.Interpreter = (auth, text, ids, turns, done) => ActionAnswer(done, new string[0], "nurse");
            var pgo = new GameObject("panel");
            _made.Add(pgo);
            pgo.SetActive(false);
            _panel = pgo.AddComponent<OrderPanelView>();
            _panel.Flow = _flow;
            pgo.SetActive(true);
        }

        private IEnumerator ActionAnswer(Action<SpokenActionResult> done, string[] ids, string addressee)
        {
            _actionAsks++;
            done(new SpokenActionResult { ActionIds = ids.ToList(), Addressee = addressee, FromService = true, Cloud = true });
            yield break;
        }

        private IEnumerator Hear(Action<OrderHearing> done) { _hears++; done(_hearing); yield break; }
        private IEnumerator Confirm(Action<OrderConfirmation> done) { _confirms++; done(_confirmation); yield break; }
        private IEnumerator Detail(string param, string value, Action<OrderHearing> done)
        {
            _details++;
            done(Proposal("Oxygen (" + value + ")", "Confirm: request oxygen (" + value + ")?", "TICKET-2"));
            yield break;
        }

        [TearDown]
        public void TearDown() { foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go); _made.Clear(); TabletFallbackRegistry.Clear(); }

        [UnityTest]
        public IEnumerator A_sentence_for_the_team_that_started_no_tablet_action_is_offered_to_the_order_route_and_the_staff_dialogue_stays_quiet()
        {
            Rig();
            _hearing = Proposal("CRP", "Confirm: order crp?", "TICKET-1");
            yield return _router.Route("Can I get a CRP on her?");
            Assert.AreEqual(1, _actionAsks);
            Assert.AreEqual(1, _hears);
            Assert.AreEqual(OrderPhase.Proposed, _flow.Phase);
            Assert.AreEqual(0, _talk.RequestsSent);                                                      // the order panel answers; no staff reply is generated for a proposal
            Assert.IsFalse(_host.Session.Observable.Investigations.Any(i => i.Test == "crp"));          // nothing reached the engine
        }

        [UnityTest]
        public IEnumerator An_ordinary_sentence_costs_no_second_model_call()
        {
            Rig();
            _hearing = Proposal("CRP", "Confirm: order crp?", "TICKET-1");
            _router.Interpreter = (auth, text, ids, turns, done) => ActionAnswer(done, new[] { "ask:smoking" }, "patient");
            yield return _router.Route("Do you smoke?");                                                // a tablet action started: the order route is not asked
            Assert.AreEqual(0, _hears);
            _router.Interpreter = (auth, text, ids, turns, done) => ActionAnswer(done, new string[0], "patient");
            yield return _router.Route("You will be fine.");                                            // spoken to the patient with no action: not asked either
            Assert.AreEqual(0, _hears);
            Assert.AreEqual(OrderPhase.Idle, _flow.Phase);
        }

        [UnityTest]
        public IEnumerator A_sentence_that_is_not_an_order_goes_on_to_the_staff_dialogue_and_only_the_badge_changes()
        {
            Rig();
            _hearing = new OrderHearing { FromService = true, Status = "menu", Interpretation = "question", Interpreter = "gemma_cloud", ReadBack = "That was a question." };
            yield return _router.Route("What does a CRP tell us?");
            Assert.AreEqual(1, _hears);
            Assert.AreEqual(OrderPhase.Idle, _flow.Phase);
            Assert.AreEqual(1, _talk.RequestsSent);                                                      // the staff dialogue answered as before
            Assert.AreEqual("Read by Gemma Cloud", _flow.InterpreterText);
        }

        [UnityTest]
        public IEnumerator A_pending_proposal_is_answered_by_the_spoken_yes_without_a_second_model_call()
        {
            Rig();
            _hearing = Proposal("CRP", "Confirm: order crp?", "TICKET-1");
            yield return _router.Route("Order a CRP.");
            int asks = _actionAsks;
            _confirmation = new OrderConfirmation { FromService = true, Outcome = "released", Verb = "order_investigation", Params = new Dictionary<string, string> { ["test"] = "crp" } };
            yield return _router.Route("Yes.");
            Assert.AreEqual(asks, _actionAsks);                                                          // the answer to a proposal asks no model
            Assert.AreEqual(1, _confirms);
            Assert.IsTrue(_host.Session.Observable.Investigations.Any(i => i.Test == "crp"));
            Assert.IsFalse(_flow.Pending);
        }

        [UnityTest]
        public IEnumerator The_panel_shows_the_reader_the_read_back_and_a_yes_that_only_the_trainee_can_press()
        {
            Rig();
            _hearing = Proposal("CRP", "Confirm: order crp?", "TICKET-1");
            yield return _router.Route("Order a CRP.");
            Assert.IsTrue(_panel.Canvas.gameObject.activeSelf);
            Assert.AreEqual("Read by Gemma Cloud", _panel.BadgeText);
            Assert.AreEqual("Confirm: order crp?", _panel.MessageText);
            Assert.IsTrue(_panel.YesButton.gameObject.activeSelf && _panel.NoButton.gameObject.activeSelf);
            Assert.AreEqual(0, _confirms);
            _confirmation = new OrderConfirmation { FromService = true, Outcome = "released", Verb = "order_investigation", Params = new Dictionary<string, string> { ["test"] = "crp" } };
            _panel.YesButton.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _confirms);
            Assert.IsTrue(_host.Session.Observable.Investigations.Any(i => i.Test == "crp"));
            Assert.IsFalse(_panel.YesButton.gameObject.activeSelf);
            StringAssert.StartsWith("Sent to the team", _panel.MessageText);
        }

        [UnityTest]
        public IEnumerator The_panel_marks_the_offline_grammar_and_offers_the_closed_list_of_a_missing_detail()
        {
            Rig();
            var h = Proposal("Oxygen", "Confirm: request oxygen?", "TICKET-O2", gemma: false);
            h.Needs.Add(new OrderNeed { Param = "device", Question = "Which oxygen device?", Options = { new KeyValuePair<string, string>("nasal_cannula", "Nasal cannula"), new KeyValuePair<string, string>("simple_mask", "Simple face mask") } });
            _hearing = h;
            yield return _router.Route("Put oxygen on her.");
            Assert.AreEqual("Read by the offline grammar (CLOUD_UNREACHABLE)", _panel.BadgeText);
            Assert.AreEqual("Which oxygen device?", _panel.MessageText);
            Assert.IsFalse(_panel.YesButton.gameObject.activeSelf);                                      // nothing to confirm before the detail is chosen
            Assert.IsTrue(_panel.OptionButtons[0].gameObject.activeSelf && _panel.OptionButtons[1].gameObject.activeSelf);
            Assert.IsFalse(_panel.OptionButtons[2].gameObject.activeSelf);
            _panel.OptionButtons[1].onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual(1, _details);
            Assert.AreEqual(OrderPhase.Proposed, _flow.Phase);
            Assert.IsTrue(_panel.YesButton.gameObject.activeSelf);
            Assert.AreEqual(0, _confirms);
        }

        [UnityTest]
        public IEnumerator A_clarifying_question_shows_its_options_and_no_yes()
        {
            Rig();
            _hearing = new OrderHearing { FromService = true, Status = "clarify", ReadBack = "CT or MRI?", Interpretation = "ambiguous", Interpreter = "gemma_cloud", Options = { "CT", "MRI" } };
            yield return _router.Route("Get a scan of her tummy.");
            Assert.AreEqual(OrderPhase.Clarifying, _flow.Phase);
            Assert.AreEqual("CT or MRI?", _panel.MessageText);
            Assert.IsFalse(_panel.YesButton.gameObject.activeSelf);
            Assert.IsTrue(_panel.OptionButtons[0].gameObject.activeSelf && _panel.OptionButtons[1].gameObject.activeSelf);
            Assert.AreEqual(0, _confirms);
            Assert.IsFalse(_host.Session.Observable.Investigations.Any(i => i.Test == "ct" || i.Test == "mri"));
        }
    }
}
