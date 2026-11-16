using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Sync;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The optional AI wording of the results (P3-RES-07) through a fake transport: validated wording replaces only the recommendation lines and is labelled; anything else keeps the engine's lines.</summary>
    public class DebriefRewriteClientTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private const string Report = "{\"schema\":\"debrief_facts.v1\",\"case\":\"VAR-S1-A\",\"mode\":\"practice\",\"end_state\":\"SAFETY_FAILURE\",\"result\":\"FAIL\",\"items\":[{\"id\":\"safety.missed_identity\",\"status\":\"penalty\",\"points\":-2}]}";
        private static readonly IReadOnlyList<string> Engine = new[] { "Confirm the patient's identity before anything else." };

        private const string AiAnswer = "{\"template\":[{\"title\":\"What to improve\",\"text\":\"missed identity (penalty).\",\"fact_ids\":[\"f1\"]}],"
            + "\"ai\":[{\"title\":\"What to improve\",\"text\":\"Next time, check who the patient is first.\",\"fact_ids\":[\"f1\"]},{\"title\":\"How the session ended\",\"text\":\"The session ended in a safety failure.\",\"fact_ids\":[\"f2\"]}],"
            + "\"used\":\"ai\",\"reasons\":[],\"latency_ms\":812}";
        private const string TemplateAnswer = "{\"template\":[{\"title\":\"What to improve\",\"text\":\"missed identity (penalty).\",\"fact_ids\":[\"f1\"]}],\"ai\":null,\"used\":\"template\",\"reasons\":[\"FEATURE_OFF\"],\"latency_ms\":0}";

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Calls = new List<string>();
            public string LastBody = "";
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Calls.Add(method + " " + url);
                if (url.Contains("/debrief/")) LastBody = jsonBody;
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

        private static RewordedRecommendations Fetch(AuthService auth, string report = Report)
        {
            RewordedRecommendations got = null;
            Run(DebriefRewriteClient.Fetch(auth, report, Engine, r => got = r));
            Assert.IsNotNull(got);
            return got;
        }

        [TearDown] public void TearDown() => AuthService.Clear();

        [Test]
        public void Validated_ai_wording_is_used_and_marked_reworded()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + DebriefRewriteClient.PathFor(Org), 200, AiAnswer);
            var got = Fetch(auth);
            Assert.IsTrue(got.Reworded);
            CollectionAssert.AreEqual(new[] { "What to improve: Next time, check who the patient is first.", "How the session ended: The session ended in a safety failure." }, got.Lines);
            CollectionAssert.Contains(http.Calls, "POST " + Base + "/debrief/organisations/" + Org + "/rewrite");
            Assert.AreEqual("{\"report\":" + Report + "}", http.LastBody, "the engine's debrief facts are sent unchanged");
            StringAssert.Contains(DebriefRewriteClient.Label, DebriefRewriteClient.LabelLine);
        }

        [Test]
        public void The_service_template_answer_keeps_the_engine_lines_with_the_reason()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + DebriefRewriteClient.PathFor(Org), 200, TemplateAnswer);
            var got = Fetch(auth);
            Assert.IsFalse(got.Reworded);
            Assert.AreEqual("FEATURE_OFF", got.Reason);
            CollectionAssert.AreEqual(Engine, got.Lines);
        }

        [Test]
        public void Offline_refused_and_not_signed_in_all_fall_back_to_the_deterministic_text()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);                                                           // no answer scripted for the route: the transport reports a network error
            var offline = Fetch(auth);
            Assert.AreEqual((false, "OFFLINE"), (offline.Reworded, offline.Reason));
            CollectionAssert.AreEqual(Engine, offline.Lines);

            http.Answer("POST", Base + DebriefRewriteClient.PathFor(Org), 429, "{\"detail\":\"too_many_ai_calls\"}");
            var limited = Fetch(auth);
            Assert.AreEqual((false, "HTTP_429"), (limited.Reworded, limited.Reason));
            CollectionAssert.AreEqual(Engine, limited.Lines);

            var nobody = Fetch(null);
            Assert.AreEqual((false, "NOT_SIGNED_IN"), (nobody.Reworded, nobody.Reason));
            CollectionAssert.AreEqual(Engine, nobody.Lines);

            var noReport = Fetch(auth, "");
            Assert.AreEqual((false, "NO_REPORT"), (noReport.Reworded, noReport.Reason));
        }

        [Test]
        public void An_answer_that_is_not_understood_or_empty_keeps_the_engine_lines()
        {
            foreach (var body in new[] { "", "not json", "{}", "{\"used\":\"ai\",\"ai\":[]}", "{\"used\":\"ai\",\"ai\":[{\"title\":\"What went well\",\"text\":\"  \",\"fact_ids\":[\"f1\"]}]}" })
            {
                var got = DebriefRewriteClient.Parse(body, Engine);
                Assert.IsFalse(got.Reworded, body);
                CollectionAssert.AreEqual(Engine, got.Lines, body);
            }
        }
    }
}
