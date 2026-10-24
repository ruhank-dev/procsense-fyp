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
    /// <summary>The Unity half of the speech evaluation hook (p1-ai-language-s22, ADR-0025) without a network: a scripted service stands in. The scene wiring and the headset run are Sannan's (DC-09); nothing here was run on a headset.</summary>
    public class SpeechEvaluationTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private const string Url = Base + "/evaluate/organisations/" + Org;

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

        private static string Act(string objective, string assertion = "performed", string actor = "trainee", int r = 3, int c = 3, int k = 3) =>
            "{\"objective\":\"" + objective + "\",\"assertion\":\"" + assertion + "\",\"actor\":\"" + actor + "\",\"recipient\":\"patient\",\"form\":\"question\",\"performed_now\":" + (assertion == "performed" && actor == "trainee" ? "true" : "false") +
            ",\"relevance\":" + r + ",\"correctness\":" + c + ",\"completeness\":" + k + ",\"evidence\":\"Do you sleep well at night?\",\"rationale\":\"asks a history question\"}";

        private static string Evaluated(string utteranceId, params string[] acts) =>
            "{\"decision\":\"EVALUATED\",\"utterance_id\":\"" + utteranceId + "\",\"acts\":[" + string.Join(",", acts) + "],\"claims_physical_action\":false,\"physical_action\":\"\",\"critical_topic\":false,\"menu\":[],\"reasons\":[],\"source\":\"evaluate\"}";

        [TearDown] public void TearDown() => AuthService.Clear();

        [Test]
        public void The_service_answers_are_read_and_anything_unreadable_is_the_safe_answer()
        {
            var ok = SpeechEvaluation.Parse(Evaluated("u-1", Act("ASK_TOPIC")));
            Assert.AreEqual(EvaluateDecision.Evaluated, ok.Decision);
            Assert.AreEqual("ASK_TOPIC", ok.Acts[0].objective);
            Assert.IsTrue(ok.Acts[0].performed_now);
            Assert.AreEqual(3, ok.Acts[0].completeness);
            Assert.IsTrue(ok.FromService);
            Assert.AreEqual(0, SpeechEvaluation.Parse(Evaluated("u-2")).Acts.Length);                                                             // nothing addressed
            var down = SpeechEvaluation.Parse("{\"decision\":\"UNAVAILABLE\",\"menu\":[{\"behaviour\":\"b01\",\"label\":\"Asks relevant questions\",\"actions\":[\"ask\"]}],\"reasons\":[\"EVAL_INVALID\"]}");
            Assert.AreEqual(EvaluateDecision.Unavailable, down.Decision);
            Assert.AreEqual("ask", down.Menu[0].actions[0]);
            foreach (var bad in new[] { "", "not json", "{}", "{\"decision\":\"CREDIT_EVERYTHING\"}", "{\"decision\":\"EVALUATED\",\"acts\":[{\"objective\":\"\"}]}", "{\"decision\":\"OFFER_EVIDENCE\"}" })
            {
                var r = SpeechEvaluation.Parse(bad);
                Assert.AreEqual(EvaluateDecision.Unavailable, r.Decision, bad);
                Assert.AreEqual(0, r.Acts.Length, bad);
            }
        }

        [Test]
        public void The_request_carries_the_engines_own_context_and_the_words()
        {
            var host = NewHost();
            try
            {
                var ctx = InterpretationContextBuilder.Build(host.Session, host.Content, new[] { new ContextTurn("patient", "Will it hurt?") }, "patient");
                var body = SpeechEvaluation.RequestBody(ctx, "She said \"yes\".", "u-9");
                StringAssert.StartsWith("{\"context\":{\"case\":\"VAR-S1-A\"", body);
                StringAssert.Contains("\"verified_actions\":[", body);
                StringAssert.Contains("\"utterance\":\"She said \\\"yes\\\".\"", body);
                StringAssert.EndsWith("\"utterance_id\":\"u-9\"}", body);
                Assert.IsNotNull(JsonUtility.FromJson<EvaluateDto>("{\"decision\":\"UNAVAILABLE\"}"));
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Every_judged_objective_is_applied_once_as_a_logged_speech_evidence_action_and_never_twice()
        {
            var http = new ScriptedHttp().Answer("POST", Url, 200, Evaluated("u-1", Act("ASK_TOPIC"), Act("CLARIFY", "intended_later")));
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                var se = new SpeechEvaluation();
                EvaluateResult got = null;
                Run(se.Evaluate(auth, host, "Do you sleep well at night?", "u-1", new List<ContextTurn>(), "patient", new EvaluateCancellation(), r => got = r));
                Assert.AreEqual("POST " + Url, http.Urls.Last());
                StringAssert.Contains("\"steps\":", http.Bodies.Last());
                Assert.AreEqual(EvaluateDecision.Evaluated, got.Decision);
                int before = host.Session.Log.Count;
                Assert.AreEqual(2, se.Apply(host, got));
                Assert.AreEqual(0, se.Apply(host, got), "a repeated answer for the same utterance must not be applied twice");
                Assert.AreEqual(before + 2, host.Session.Log.Count);
                var entry = host.Session.Log.Entries[host.Session.Log.Count - 2];
                Assert.AreEqual(SpeechEvidenceSubsystem.Verb, entry.Verb);
                Assert.IsTrue(entry.Accepted, entry.Reject);
                Assert.AreEqual("ASK_TOPIC", entry.Params[SpeechEvidenceSubsystem.ActParam]);
                Assert.AreEqual("3", entry.Params[SpeechEvidenceSubsystem.CompletenessParam]);
                Assert.IsTrue(entry.Effects.Any(e => e.StartsWith(SpeechEvidenceSubsystem.StepsEffect)));
                var store = new ProcSense.Core.Scoring.EvidenceStore(host.Content.Scoring);
                store.FromLog(host.Session.Log);
                Assert.IsFalse(store.Records.Single(r => r.Act == "CLARIFY").Accepted);                           // a planned objective earns nothing
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Only_an_evaluation_with_objectives_changes_the_session()
        {
            var host = NewHost();
            try
            {
                var se = new SpeechEvaluation();
                int before = host.Session.Log.Count;
                foreach (var body in new[] { Evaluated("u-1"), "{\"decision\":\"UNAVAILABLE\"}" })
                    Assert.AreEqual(0, se.Apply(host, SpeechEvaluation.Parse(body)), body);
                Assert.AreEqual(0, se.Apply(host, null));
                Assert.AreEqual(0, se.Apply(null, SpeechEvaluation.Parse(Evaluated("u-2", Act("ASK_TOPIC")))));
                Assert.AreEqual(before, host.Session.Log.Count);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void No_sign_in_no_network_a_server_error_and_a_garbled_answer_are_each_the_safe_answer_and_the_callback_runs_once()
        {
            var host = NewHost();
            try
            {
                int calls = 0;
                EvaluateResult got = null;
                Run(new SpeechEvaluation().Evaluate(null, host, "x", "u-1", null, "patient", null, r => { got = r; calls++; }));
                Assert.AreEqual((EvaluateDecision.Unavailable, 1), (got.Decision, calls));
                Assert.IsFalse(got.FromService);
                foreach (var answer in new[] { new HttpResult(0, "", true), new HttpResult(503, "{}"), new HttpResult(200, "garbage"), new HttpResult(500, "") })
                {
                    var http = new ScriptedHttp();
                    http.Answer("POST", Url, answer.Status, answer.Body);
                    var auth = SignedIn(http);
                    calls = 0;
                    var se = new SpeechEvaluation();
                    Run(se.Evaluate(auth, host, "x", "u-2", null, "patient", null, r => { got = r; calls++; }));
                    Assert.AreEqual(1, calls);
                    Assert.AreEqual(EvaluateDecision.Unavailable, got.Decision);
                    AuthService.Clear();
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_answer_that_arrives_after_the_request_was_cancelled_is_dropped()
        {
            var cancel = new EvaluateCancellation();
            var http = new ScriptedHttp { BeforeAnswer = cancel.Cancel }.Answer("POST", Url, 200, Evaluated("u-1", Act("ASK_TOPIC")));
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                int calls = 0;
                var se = new SpeechEvaluation();
                Run(se.Evaluate(auth, host, "x", "u-1", null, "patient", cancel, r => calls++));
                Assert.AreEqual(0, calls);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_engine_never_lets_a_judged_critical_objective_or_a_physical_claim_become_evidence_or_a_physical_completion()
        {
            var host = NewHost();
            try
            {
                var se = new SpeechEvaluation();
                var critical = SpeechEvaluation.Parse(Evaluated("u-3", Act("STATE_ALLERGY")).Replace("\"claims_physical_action\":false", "\"claims_physical_action\":true"));     // even a perfect claim of the allergy check
                Assert.AreEqual(1, se.Apply(host, critical));                                                                                                             // the action is logged ...
                var store = new ProcSense.Core.Scoring.EvidenceStore(host.Content.Scoring);
                store.FromLog(host.Session.Log);
                Assert.IsFalse(store.Records.Single().Accepted);                                                                                                          // ... and the engine does not accept it as evidence
                var report = ProcSense.Core.Scoring.Scorer.Score(host.Session, host.Content);
                Assert.IsTrue(report.Communication.All(c => !c.PhysicalVerified));                                                                                         // a spoken claim never makes the physical part true
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }
    }
}
