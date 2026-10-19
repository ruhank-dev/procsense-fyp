using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Speech;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The Unity half of the speech interpretation hook (p1-ai-language-s12, ADR-0022) without a network: a scripted service stands in. The scene wiring and the headset run are Sannan's (DC-09); nothing here was run on a headset.</summary>
    public class SpeechInterpretationTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private const string Url = Base + "/interpret/organisations/" + Org;

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            public Action BeforeAnswer;
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
                Bodies.Add(jsonBody);
                if (url == Url) BeforeAnswer?.Invoke();
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
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

        private static AuthService SignedIn(ScriptedHttp http)
        {
            http.Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"u\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}],\"is_platform_admin\":false}")
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

        private static string Offer(string act, string behaviour, string utterance) =>
            "{\"decision\":\"OFFER_EVIDENCE\",\"evidence\":{\"act\":\"" + act + "\",\"behaviour\":\"" + behaviour + "\",\"span\":\"Do you sleep well at night?\",\"status\":\"question\",\"confidence\":0.93,\"utterance_id\":\"" + utterance + "\"},\"clarify\":\"\",\"options\":[],\"menu\":[],\"points_to\":[],\"reasons\":[]}";

        [TearDown] public void TearDown() => AuthService.Clear();

        [Test]
        public void The_service_answers_are_read_and_anything_unreadable_is_the_menu()
        {
            var offer = SpeechInterpretation.Parse(Offer("ASK_TOPIC", "b01", "u-1"));
            Assert.AreEqual(InterpretDecision.OfferEvidence, offer.Decision);
            Assert.AreEqual("ASK_TOPIC", offer.Evidence.act);
            Assert.IsTrue(offer.FromService);
            var clarify = SpeechInterpretation.Parse("{\"decision\":\"CLARIFY\",\"clarify\":\"Did you mean A, or B?\",\"options\":[\"A\",\"B\"],\"reasons\":[\"AMBIGUOUS\"]}");
            Assert.AreEqual(InterpretDecision.Clarify, clarify.Decision);
            CollectionAssert.AreEqual(new[] { "A", "B" }, clarify.Options);
            Assert.AreEqual(InterpretDecision.Abstain, SpeechInterpretation.Parse("{\"decision\":\"ABSTAIN\",\"reasons\":[\"NOT_ELIGIBLE\"]}").Decision);
            var menu = SpeechInterpretation.Parse("{\"decision\":\"MENU\",\"menu\":[{\"behaviour\":\"b01\",\"label\":\"Asks relevant questions\",\"actions\":[\"ask\"]}],\"points_to\":[\"read_wristband\"]}");
            Assert.AreEqual("ask", menu.Menu[0].actions[0]);
            Assert.AreEqual("read_wristband", menu.PointsTo[0]);
            foreach (var bad in new[] { "", "not json", "{}", "{\"decision\":\"CREDIT_EVERYTHING\"}", "{\"decision\":\"OFFER_EVIDENCE\"}", "{\"decision\":\"OFFER_EVIDENCE\",\"evidence\":{\"act\":\"\"}}", "{\"decision\":\"CLARIFY\"}" })
            {
                var r = SpeechInterpretation.Parse(bad);
                Assert.AreEqual(InterpretDecision.Menu, r.Decision, bad);
                Assert.IsNull(r.Evidence, bad);
            }
        }

        [Test]
        public void The_request_carries_the_engines_own_context_the_words_and_the_clarifications_used()
        {
            var host = NewHost();
            try
            {
                var ctx = InterpretationContextBuilder.Build(host.Session, host.Content, new[] { new ContextTurn("patient", "Will it hurt?") }, "patient");
                var body = SpeechInterpretation.RequestBody(ctx, "She said \"yes\".", "u-9", 1);
                StringAssert.StartsWith("{\"context\":{\"case\":\"VAR-S1-A\"", body);
                StringAssert.Contains("\"utterance\":\"She said \\\"yes\\\".\"", body);
                StringAssert.Contains("\"utterance_id\":\"u-9\"", body);
                StringAssert.EndsWith("\"clarifications_used\":1}", body);
                Assert.IsNotNull(JsonUtility.FromJson<InterpretDto>("{\"decision\":\"MENU\"}"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_offer_is_applied_to_the_session_once_as_a_logged_speech_evidence_action_and_never_twice()
        {
            var http = new ScriptedHttp().Answer("POST", Url, 200, Offer("ASK_TOPIC", "b01", "u-1"));
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                var si = new SpeechInterpretation();
                InterpretResult got = null;
                Run(si.Interpret(auth, host, "Do you sleep well at night?", "u-1", new List<ContextTurn>(), "patient", new InterpretCancellation(), r => got = r));
                Assert.AreEqual("POST " + Url, http.Urls.Last());
                StringAssert.Contains("\"steps\":", http.Bodies.Last());
                Assert.AreEqual(InterpretDecision.OfferEvidence, got.Decision);
                int before = host.Session.Log.Count;
                Assert.IsTrue(si.Apply(host, got));
                Assert.IsFalse(si.Apply(host, got), "a repeated answer for the same utterance must not be applied twice");
                Assert.AreEqual(before + 1, host.Session.Log.Count);
                var entry = host.Session.Log.Entries.Last();
                Assert.AreEqual(SpeechEvidenceSubsystem.Verb, entry.Verb);
                Assert.IsTrue(entry.Accepted, entry.Reject);
                Assert.AreEqual("ASK_TOPIC", entry.Params[SpeechEvidenceSubsystem.ActParam]);
                Assert.IsTrue(entry.Effects.Any(e => e.StartsWith(SpeechEvidenceSubsystem.StepsEffect)));
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Only_an_offer_changes_the_session_a_clarification_an_abstention_and_the_menu_send_nothing()
        {
            var host = NewHost();
            try
            {
                var si = new SpeechInterpretation();
                int before = host.Session.Log.Count;
                foreach (var body in new[] { "{\"decision\":\"CLARIFY\",\"clarify\":\"Did you mean A, or B?\"}", "{\"decision\":\"ABSTAIN\"}", "{\"decision\":\"MENU\"}" })
                    Assert.IsFalse(si.Apply(host, SpeechInterpretation.Parse(body)), body);
                Assert.IsFalse(si.Apply(host, null));
                Assert.IsFalse(si.Apply(null, SpeechInterpretation.Parse(Offer("ASK_TOPIC", "b01", "u-2"))));
                Assert.AreEqual(before, host.Session.Log.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void One_clarification_is_counted_and_the_next_request_says_so_until_a_new_utterance_starts()
        {
            var clarify = "{\"decision\":\"CLARIFY\",\"clarify\":\"Did you mean A, or B?\",\"options\":[\"A\",\"B\"]}";
            var http = new ScriptedHttp().Answer("POST", Url, 200, clarify);
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                var si = new SpeechInterpretation();
                InterpretResult got = null;
                Run(si.Interpret(auth, host, "x", "u-1", null, "patient", new InterpretCancellation(), r => got = r));
                Assert.AreEqual(InterpretDecision.Clarify, got.Decision);
                Assert.AreEqual(1, si.ClarificationsUsed);
                Run(si.Interpret(auth, host, "y", "u-2", null, "patient", new InterpretCancellation(), r => got = r));
                StringAssert.EndsWith("\"clarifications_used\":1}", http.Bodies.Last());
                si.NewUtterance();
                Assert.AreEqual(0, si.ClarificationsUsed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void No_sign_in_no_network_a_server_error_and_a_garbled_answer_are_each_the_menu_and_the_callback_runs_once()
        {
            var host = NewHost();
            try
            {
                int calls = 0;
                InterpretResult got = null;
                Run(new SpeechInterpretation().Interpret(null, host, "x", "u-1", null, "patient", null, r => { got = r; calls++; }));
                Assert.AreEqual((InterpretDecision.Menu, 1), (got.Decision, calls));
                Assert.IsFalse(got.FromService);
                foreach (var answer in new[] { new HttpResult(0, "", true), new HttpResult(503, "{}"), new HttpResult(200, "garbage"), new HttpResult(500, "") })
                {
                    var http = new ScriptedHttp();
                    http.Answer("POST", Url, answer.Status, answer.Body);
                    var auth = SignedIn(http);
                    calls = 0;
                    var si = new SpeechInterpretation();
                    Run(si.Interpret(auth, host, "x", "u-2", null, "patient", null, r => { got = r; calls++; }));
                    Assert.AreEqual(1, calls);
                    Assert.AreEqual(InterpretDecision.Menu, got.Decision);
                    Assert.AreEqual(0, si.ClarificationsUsed);
                    AuthService.Clear();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_answer_that_arrives_after_the_request_was_cancelled_is_dropped()
        {
            var cancel = new InterpretCancellation();
            var http = new ScriptedHttp { BeforeAnswer = cancel.Cancel }.Answer("POST", Url, 200, Offer("ASK_TOPIC", "b01", "u-1"));
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                int calls = 0;
                var si = new SpeechInterpretation();
                Run(si.Interpret(auth, host, "x", "u-1", null, "patient", cancel, r => calls++));
                Assert.AreEqual(0, calls);
                Assert.AreEqual(0, si.ClarificationsUsed);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_engine_refuses_what_the_service_should_not_have_offered_and_the_menu_path_stays_open()
        {
            var host = NewHost();
            try
            {
                var si = new SpeechInterpretation();
                var critical = SpeechInterpretation.Parse(Offer("STATE_ALLERGY", "b13", "u-3"));              // a hostile or mistaken service
                Assert.IsTrue(si.Apply(host, critical));                                                         // the action is logged ...
                var store = new ProcSense.Core.Scoring.EvidenceStore(host.Content.Scoring);
                store.FromLog(host.Session.Log);
                Assert.IsFalse(store.Records.Single().Accepted);                                                  // ... and the engine refuses it
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }
    }
}
