using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Sync;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The person's latest sessions, fetched from the service and put into words for the tablet (p1-backend-s04 route, p1-backend-s03 panel).</summary>
    public class ResultsClientTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private const string Page = "{\"total\":4,\"limit\":5,\"offset\":0,\"items\":["
            + "{\"session_id\":\"a\",\"trainee\":\"T-ab12cd34\",\"display_name\":null,\"case_id\":\"VAR-S1-E\",\"mode\":\"practice\",\"status\":\"verified\",\"result\":\"SUCCESS\",\"end_state\":\"REDIRECTED_APPROPRIATELY\",\"process_score\":2.0,\"process_score_provisional\":true,\"content_version\":\"v\",\"received_at\":\"2026-10-14T10:34:39.251238Z\"},"
            + "{\"session_id\":\"b\",\"trainee\":\"T-ab12cd34\",\"display_name\":null,\"case_id\":\"VAR-S1-A\",\"mode\":\"practice\",\"status\":\"pending\",\"result\":null,\"end_state\":null,\"process_score\":null,\"process_score_provisional\":true,\"content_version\":\"v\",\"received_at\":\"2026-10-14T10:40:00Z\"},"
            + "{\"session_id\":\"c\",\"trainee\":\"T-ab12cd34\",\"display_name\":null,\"case_id\":\"VAR-S2-B\",\"mode\":\"assessment\",\"status\":\"verified\",\"result\":\"FAIL\",\"end_state\":\"SAFETY_FAILURE\",\"process_score\":-3.5,\"process_score_provisional\":true,\"content_version\":\"v\",\"received_at\":\"2026-10-13T08:00:00Z\"},"
            + "{\"session_id\":\"d\",\"trainee\":\"T-ab12cd34\",\"display_name\":null,\"case_id\":\"VAR-S1-B\",\"mode\":\"practice\",\"status\":\"unverifiable\",\"result\":null,\"end_state\":null,\"process_score\":null,\"process_score_provisional\":true,\"content_version\":\"v\",\"received_at\":\"2026-10-12T08:00:00Z\"}]}";

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
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

        [TearDown] public void TearDown() => AuthService.Clear();

        [Test]
        public void Each_session_becomes_one_plain_line_with_what_the_check_found()
        {
            var view = ResultsClient.Parse(Page);
            Assert.IsTrue(view.Ok);
            Assert.AreEqual(4, view.Total);
            Assert.AreEqual(4, view.Lines.Count);
            StringAssert.Contains("VAR-S1-E", view.Lines[0]);
            StringAssert.Contains("Success, score 2 (provisional)", view.Lines[0]);
            StringAssert.Contains("waiting to be checked", view.Lines[1]);
            StringAssert.Contains("Fail, score -3.5 (provisional)", view.Lines[2]);
            StringAssert.Contains("could not be checked", view.Lines[3]);
            Assert.IsTrue(view.AnyPending);
            Assert.IsFalse(view.Lines.Any(l => l.Contains("T-ab12cd34")), "the line is about the person's own sessions: no ids");
        }

        [Test]
        public void A_person_with_no_sessions_and_an_answer_that_is_not_understood_are_worded_differently()
        {
            var empty = ResultsClient.Parse("{\"total\":0,\"limit\":5,\"offset\":0,\"items\":[]}");
            Assert.IsTrue(empty.Ok); Assert.AreEqual("No sessions on the service yet.", empty.Text);
            foreach (var bad in new[] { "", "not json", "{}", "[1,2]" })
            {
                var view = ResultsClient.Parse(bad);
                Assert.IsFalse(view.Ok, bad);
                StringAssert.Contains("not understood", view.Text);
            }
        }

        [Test]
        public void The_request_asks_for_the_signed_in_persons_own_organisation_and_the_latest_five()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("GET", Base + "/organisations/" + Org + "/sessions?status=any&limit=5", 200, Page);
            ResultsView view = null;
            Run(ResultsClient.Fetch(auth, 5, v => view = v));
            Assert.IsTrue(view.Ok, view.Error);
            Assert.AreEqual(4, view.Lines.Count);
            CollectionAssert.Contains(http.Urls, "GET " + Base + "/organisations/" + Org + "/sessions?status=any&limit=5");
        }

        [Test]
        public void Problems_are_worded_for_the_person_not_the_developer()
        {
            ResultsView view = null;
            Run(ResultsClient.Fetch(null, 5, v => view = v));
            StringAssert.Contains("Sign in", view.Error);
            var http = new ScriptedHttp();
            var auth = SignedIn(http);   // the sessions address does not answer: no network
            Run(ResultsClient.Fetch(auth, 5, v => view = v));
            StringAssert.Contains("Cannot reach", view.Error);
            http.Answer("GET", Base + "/organisations/" + Org + "/sessions?status=any&limit=5", 500);
            Run(ResultsClient.Fetch(auth, 5, v => view = v));
            StringAssert.Contains("500", view.Error);
        }
    }
}
