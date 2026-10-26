using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The spoken medical orders on the Unity side (ADR-0028, 27 Oct 2026): the client sends the sentence and an identifier-free scene to the right route and reads the route's own answers (the files of tests/orders/unity_contract are what the route returns, checked by
    /// the server tests); the flow shows a proposal and sends nothing to the engine before the trainee's yes; a yes applies exactly the action the signed ticket names with <c>SimulationHost.Apply</c>, so the engine validates it; a no, a question, an unsupported order, a refused
    /// ticket, an offline service and a duplicate release do nothing. A fake transport stands in for the service; the engine session is the real one. The headset check (a real microphone, the panel in the room) is Sannan's (DC-08, DC-11).
    /// </summary>
    public class SpokenOrderTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Contract(string name) => File.ReadAllText(Path.Combine(Repo, "tests", "orders", "unity_contract", name + ".json"));

        private sealed class FakeHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            /// <summary>The answer for a route; a later call for the same route replaces it.</summary>
            public FakeHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public int Count(string suffix) => Urls.Count(u => u.EndsWith(suffix));
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url); Bodies.Add(jsonBody);
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
            public IEnumerator SendBytes(string method, string url, byte[] body, string contentType, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
                done(new HttpResult(0, "", true));
                yield break;
            }
        }

        private static void Run(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) stack.Push(inner);
            }
        }

        private static AuthService SignedIn(FakeHttp http)
        {
            http.Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"u\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            var service = new AuthService(http, Base);
            SignInResult result = null;
            Run(service.SignIn("jane@example.org", "pw", r => result = r));
            Assert.IsTrue(result.Ok, result.Message);
            return service;
        }

        private static SimulationHost NewHost()
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice, new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))));
            return host;
        }

        private static void Identify(SimulationHost host) { foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) host.Apply(new SimAction(v)); }

        private sealed class Rig : IDisposable
        {
            public readonly FakeHttp Http = new FakeHttp();
            public readonly AuthService Auth;
            public readonly SimulationHost Host;
            public readonly SpokenOrderFlow Flow;
            private readonly GameObject _go;
            public Rig(bool identify = true)
            {
                Auth = SignedIn(Http);
                Host = NewHost();
                if (identify) Identify(Host);
                _go = new GameObject("flow");
                Flow = _go.AddComponent<SpokenOrderFlow>();
                Flow.Auth = () => Auth;
                Flow.Host = () => Host;
            }
            public string Url(string tail) => Base + "/orders/organisations/" + Org + "/" + tail;
            public bool Handled(string text) { bool h = false; Run(Flow.Offer(text, x => h = x)); return h; }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(_go); UnityEngine.Object.DestroyImmediate(Host.gameObject); }
        }

        // ------------------------------------------------------------------------------------------------------------- the client

        [Test]
        public void The_sentence_goes_to_the_hear_route_with_a_scene_that_holds_no_identifier()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_question"));
                Assert.IsFalse(rig.Handled("What does a CRP tell us?"));                                              // a question is not an order: the ordinary speech path answers
                Assert.AreEqual(1, rig.Http.Count("/orders/organisations/" + Org + "/hear"));
                string body = rig.Http.Bodies.Last();
                StringAssert.Contains("\"text\":\"What does a CRP tell us?\"", body);
                StringAssert.Contains("Practice session.", body);
                foreach (var secret in new[] { "token", "email", "pseudonym", "user_id", "organisation", "T-ab12cd34", "jane" }) StringAssert.DoesNotContain(secret, body);
                Assert.AreEqual(0, rig.Http.Count("/confirm"));
            }
        }

        [Test]
        public void The_answers_of_the_route_are_read_as_the_route_gives_them()
        {
            var p = SpokenOrderClient.ParseHearing(Contract("hear_proposal_gemma"));
            Assert.IsTrue(p.IsProposal && p.ByGemma);
            Assert.AreEqual("TICKET-CRP.sig", p.Ticket);
            Assert.AreEqual("Read by Gemma Cloud", p.InterpreterText);
            Assert.AreEqual("gemma4:31b", p.ReportedModel);
            var f = SpokenOrderClient.ParseHearing(Contract("hear_proposal_grammar_fallback"));
            Assert.IsTrue(f.IsProposal && !f.ByGemma);
            Assert.AreEqual("Read by the offline grammar (CLOUD_UNREACHABLE)", f.InterpreterText);
            var o = SpokenOrderClient.ParseHearing(Contract("hear_needs_oxygen"));
            Assert.AreEqual("device", o.Needs[0].Param);
            CollectionAssert.AreEqual(new[] { "nasal_cannula", "simple_mask", "non_rebreather_mask" }, o.Needs[0].Options.Select(x => x.Key).ToArray());
            var c = SpokenOrderClient.ParseHearing(Contract("hear_clarify"));
            Assert.IsTrue(c.IsClarification && !c.IsProposal);
            CollectionAssert.AreEqual(new[] { "CT", "MRI" }, c.Options);
            Assert.IsTrue(SpokenOrderClient.ParseHearing(Contract("hear_unsupported")).IsUnsupported);
            Assert.IsFalse(SpokenOrderClient.ParseHearing(Contract("hear_question")).IsProposal);
            Assert.IsFalse(SpokenOrderClient.ParseHearing("not json").FromService);
            var r = SpokenOrderClient.ParseConfirmation(Contract("confirm_released_oxygen"));
            Assert.IsTrue(r.Released);
            Assert.AreEqual("request_oxygen", r.Verb);
            Assert.AreEqual("simple_mask", r.Params["device"]);
            Assert.IsTrue(SpokenOrderClient.ParseConfirmation(Contract("confirm_released_no_verb")).NoEngineVerb);
            Assert.IsTrue(SpokenOrderClient.ParseConfirmation(Contract("confirm_cancelled")).Cancelled);
            Assert.IsTrue(SpokenOrderClient.ParseConfirmation(Contract("confirm_open")).Open);
            Assert.IsFalse(SpokenOrderClient.ParseConfirmation(Contract("confirm_refused")).Released);
            Assert.IsFalse(SpokenOrderClient.ParseConfirmation("").FromService);
        }

        [Test]
        public void The_bodies_sent_are_byte_for_byte_the_ones_the_route_is_tested_with()
        {
            string Request(string name) => File.ReadAllText(Path.Combine(Repo, "tests", "orders", "unity_contract", "requests", name + ".json")).Trim();
            Assert.AreEqual(Request("hear"), SpokenOrderClient.HearBody("Can I get a CRP on her?", "Practice session. Location: Ward bay. Identity confirmed. Allergies known."));
            Assert.AreEqual(Request("confirm"), SpokenOrderClient.ConfirmBody("TICKET-CRP.sig", "yes"));
            Assert.AreEqual(Request("details"), SpokenOrderClient.DetailsBody("TICKET-O2.sig", "device", "simple_mask"));
        }

        // ------------------------------------------------------------------------------------------------------------- the flow and the engine

        [Test]
        public void A_proposal_sends_nothing_to_the_engine_until_the_trainees_yes_and_then_exactly_the_ticketed_action()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_test"));
                var applied = new List<SimAction>();
                rig.Host.Applied += (a, o) => applied.Add(a);
                Assert.IsTrue(rig.Handled("Can I get a CRP on her?"));
                Assert.AreEqual(OrderPhase.Proposed, rig.Flow.Phase);
                Assert.AreEqual("Confirm: order crp?", rig.Flow.Message);
                Assert.AreEqual("Read by Gemma Cloud", rig.Flow.InterpreterText);
                Assert.AreEqual(0, applied.Count);                                                                      // a hearing changes nothing
                Assert.AreEqual(0, rig.Http.Count("/confirm"));
                Run(rig.Flow.Confirm());
                Assert.AreEqual(1, rig.Http.Count("/confirm"));
                StringAssert.Contains("\"ticket\":\"TICKET-CRP.sig\"", rig.Http.Bodies.Last());
                StringAssert.Contains("\"spoken\":\"yes\"", rig.Http.Bodies.Last());
                Assert.AreEqual(1, applied.Count);
                Assert.AreEqual("order_investigation", applied[0].Verb);
                Assert.AreEqual("crp", applied[0].Params["test"]);
                Assert.AreEqual(Channel.Vr, applied[0].Channel);
                Assert.IsTrue(rig.Host.Session.Observable.Investigations.Any(i => i.Test == "crp"), "the engine took the order");
                StringAssert.StartsWith("Sent to the team", rig.Flow.Message);
            }
        }

        [Test]
        public void A_no_cancels_the_ticket_and_nothing_reaches_the_engine()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_cancelled"));
                var applied = new List<SimAction>();
                rig.Host.Applied += (a, o) => applied.Add(a);
                Assert.IsTrue(rig.Handled("order a crp"));
                Run(rig.Flow.Reject());
                StringAssert.Contains("\"spoken\":\"no\"", rig.Http.Bodies.Last());
                Assert.AreEqual(0, applied.Count);
                Assert.AreEqual(OrderPhase.Notice, rig.Flow.Phase);
                StringAssert.Contains("Nothing was ordered", rig.Flow.Message);
                Assert.IsFalse(rig.Flow.Pending);
            }
        }

        [Test]
        public void The_engine_can_refuse_a_confirmed_order_and_the_trainee_sees_why()
        {
            using (var rig = new Rig(identify: false))                                                                     // identity is not confirmed: the engine refuses the order
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_test"));
                Assert.IsTrue(rig.Handled("order a crp"));
                Run(rig.Flow.Confirm());
                Assert.AreEqual(0, rig.Flow.Applied);
                StringAssert.Contains("Identity not confirmed", rig.Flow.Message);
                Assert.IsFalse(rig.Host.Session.Observable.Investigations.Any(i => i.Test == "crp"));
            }
        }

        [Test]
        public void An_order_is_not_sent_twice_within_a_moment()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_test"));
                Assert.IsTrue(rig.Handled("order a crp"));
                Run(rig.Flow.Confirm());
                Assert.AreEqual(1, rig.Flow.Applied);
                Assert.IsTrue(rig.Handled("order a crp"));                                                                // a repeated recognition proposes it again
                Run(rig.Flow.Confirm());
                Assert.AreEqual(1, rig.Flow.Applied);
                StringAssert.Contains("Already sent", rig.Flow.Message);
            }
        }

        [Test]
        public void A_missing_detail_is_chosen_from_the_closed_list_and_the_completed_order_is_confirmed_with_its_new_ticket()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_needs_oxygen")).Answer("POST", rig.Url("details"), 200, Contract("details_done")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_oxygen"));
                var applied = new List<SimAction>();
                rig.Host.Applied += (a, o) => applied.Add(a);
                Assert.IsTrue(rig.Handled("put oxygen on her"));
                Assert.AreEqual(OrderPhase.NeedsDetail, rig.Flow.Phase);
                Assert.AreEqual("Which oxygen device?", rig.Flow.Message);
                Run(rig.Flow.Confirm());                                                                               // a yes before the detail confirms nothing
                Assert.AreEqual(0, rig.Http.Count("/confirm"));
                Run(rig.Flow.Choose("device", "simple_mask"));
                StringAssert.Contains("\"ticket\":\"TICKET-O2.sig\"", rig.Http.Bodies.Last());
                StringAssert.Contains("\"choices\":{\"device\":\"simple_mask\"}", rig.Http.Bodies.Last());
                Assert.AreEqual(OrderPhase.Proposed, rig.Flow.Phase);
                Assert.AreEqual("Confirm: request oxygen (simple face mask)?", rig.Flow.Message);
                Assert.AreEqual("Read by Gemma Cloud", rig.Flow.InterpreterText);                                      // the details step asked no model: the reader of the sentence is unchanged
                Assert.AreEqual(0, applied.Count);
                Run(rig.Flow.Confirm());
                StringAssert.Contains("\"ticket\":\"TICKET-O2-SM.sig\"", rig.Http.Bodies.Last());                       // the NEW single-use ticket
                Assert.AreEqual(1, applied.Count);
                Assert.AreEqual("request_oxygen", applied[0].Verb);
                Assert.AreEqual("simple_mask", applied[0].Params["device"]);
            }
        }

        [Test]
        public void A_question_to_clarify_has_no_ticket_and_cannot_be_confirmed()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_clarify"));
                Assert.IsTrue(rig.Handled("Get a scan of her tummy."));
                Assert.AreEqual(OrderPhase.Clarifying, rig.Flow.Phase);
                Assert.AreEqual("CT or MRI?", rig.Flow.Message);
                Assert.IsFalse(rig.Flow.Pending);
                Run(rig.Flow.Confirm());
                Run(rig.Flow.Reject());
                Assert.AreEqual(0, rig.Http.Count("/confirm"));                                                         // an ambiguous request never executes
                Assert.AreEqual(0, rig.Flow.Applied);
                bool picked = false;
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma"));
                Run(rig.Flow.PickOption("CT", h => picked = h));                                                        // a chosen alternative is a new sentence for the same route, the same read-back and the same yes
                Assert.IsTrue(picked);
                Assert.AreEqual(OrderPhase.Proposed, rig.Flow.Phase);
                StringAssert.Contains("\"text\":\"CT\"", rig.Http.Bodies.Last());
            }
        }

        [Test]
        public void An_unsupported_order_is_said_plainly_and_a_non_order_leaves_the_ordinary_path_alone()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_unsupported"));
                Assert.IsTrue(rig.Handled("Intubate her now."));
                Assert.AreEqual(OrderPhase.Notice, rig.Flow.Phase);
                StringAssert.Contains("no such order", rig.Flow.Message);
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_question"));
                rig.Flow.Clear();
                Assert.IsFalse(rig.Handled("What does a CRP tell us?"));
                Assert.AreEqual(OrderPhase.Idle, rig.Flow.Phase);
            }
        }

        [Test]
        public void The_offline_grammar_fallback_is_shown_as_such_and_still_needs_the_yes()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_grammar_fallback")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_test"));
                Assert.IsTrue(rig.Handled("order a crp"));
                Assert.AreEqual("Read by the offline grammar (CLOUD_UNREACHABLE)", rig.Flow.InterpreterText);
                Assert.AreEqual(OrderPhase.Proposed, rig.Flow.Phase);
                Assert.AreEqual(0, rig.Flow.Applied);
                Run(rig.Flow.Confirm());
                Assert.AreEqual(1, rig.Flow.Applied);
            }
        }

        [Test]
        public void An_unreachable_service_is_no_order_and_says_so_without_throwing()
        {
            using (var rig = new Rig())
            {
                Assert.IsFalse(rig.Handled("order a crp"));                                                            // the fake host has no answer: a network error
                StringAssert.Contains("Orders unavailable", rig.Flow.InterpreterText);
                Assert.AreEqual(OrderPhase.Idle, rig.Flow.Phase);
            }
            Run(SpokenOrderClient.Hear(null, "order a crp", "", h => Assert.AreEqual("NOT_SIGNED_IN", h.Reason)));
        }

        [Test]
        public void A_spoken_yes_answers_a_pending_proposal_and_any_other_sentence_does_not()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_open"));
                Assert.IsTrue(rig.Handled("order a crp"));
                bool answered = true;
                Run(rig.Flow.AnswerBySpeech("what is her potassium", a => answered = a));
                Assert.IsFalse(answered);                                                                              // not a yes or a no: the route leaves the ticket open
                Assert.IsTrue(rig.Flow.Pending);
                rig.Http.Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_test"));
                Run(rig.Flow.AnswerBySpeech("yes please", a => answered = a));
                Assert.IsTrue(answered);
                Assert.AreEqual(1, rig.Flow.Applied);
            }
        }

        [Test]
        public void A_refused_ticket_releases_nothing_and_a_hint_is_a_plain_sentence()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_refused"));
                Assert.IsTrue(rig.Handled("order a crp"));
                Run(rig.Flow.Confirm());
                Assert.AreEqual(0, rig.Flow.Applied);
                StringAssert.Contains("no longer open", rig.Flow.Message);
            }
            Assert.AreEqual("Choose how the patient is moved", HintTexts.For("MISSING_MODE"));
        }

        [Test]
        public void A_release_with_no_engine_verb_is_noted_and_not_applied()
        {
            using (var rig = new Rig())
            {
                rig.Http.Answer("POST", rig.Url("hear"), 200, Contract("hear_proposal_gemma")).Answer("POST", rig.Url("confirm"), 200, Contract("confirm_released_no_verb"));
                Assert.IsTrue(rig.Handled("call the surgeon"));
                Run(rig.Flow.Confirm());
                Assert.AreEqual(0, rig.Flow.Applied);
                StringAssert.Contains("no action for this one", rig.Flow.Message);
            }
        }
    }
}
