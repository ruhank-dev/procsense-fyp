using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The tablet's Session tab: the person, the uploads, a button that finishes and sends the session, and the latest results fetched from the service (p1-backend-s03).</summary>
    public class SessionTabTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string User = "11111111-1111-1111-1111-111111111111";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private const string ListUrl = Base + "/organisations/" + Org + "/sessions?status=any&limit=5";

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Sent = new List<string>();
            public readonly Dictionary<string, Queue<HttpResult>> Answers = new Dictionary<string, Queue<HttpResult>>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "")
            {
                var key = method + " " + url;
                if (!Answers.ContainsKey(key)) Answers[key] = new Queue<HttpResult>();
                Answers[key].Enqueue(new HttpResult(status, body));
                return this;
            }
            public readonly List<(string prefix, HttpResult result)> PrefixAnswers = new List<(string, HttpResult)>();
            /// <summary>Answers any address that starts with the given text (for a session id the host makes up).</summary>
            public ScriptedHttp AnswerPrefix(string method, string urlPrefix, long status, string body = "") { PrefixAnswers.Add((method + " " + urlPrefix, new HttpResult(status, body))); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Sent.Add(method + " " + url);
                var key = method + " " + url;
                if (Answers.TryGetValue(key, out var queue) && queue.Count > 0) { done(queue.Count > 1 ? queue.Dequeue() : queue.Peek()); yield break; }
                foreach (var (prefix, result) in PrefixAnswers) if (key.StartsWith(prefix, StringComparison.Ordinal)) { done(result); yield break; }
                done(new HttpResult(0, "", true));
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();
        private string _dir;
        private SessionSync _original;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "procsense-tab-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SimulationHost.SessionsDirectoryOverride = Path.Combine(_dir, "sessions");
            AuthService.Clear();
            _original = SessionUploader.Instance != null ? SessionUploader.Instance.Sync : null;
            if (SimulationHost.Instance != null)   // a host left behind by an earlier test would make the new one a duplicate that switches itself off
            {
                SimulationHost.Instance.SaveLogOnDestroy = false;
                UnityEngine.Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
            SimulationHost.SessionsDirectoryOverride = null;
            if (SessionUploader.Instance != null && _original != null) SessionUploader.Instance.Sync = _original;
            AuthService.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static string Row(string status, string result) => "{\"session_id\":\"s\",\"trainee\":\"T-ab12cd34\",\"case_id\":\"VAR-S1-A\",\"mode\":\"practice\",\"status\":\"" + status + "\",\"result\":" + (result == null ? "null" : "\"" + result + "\"")
            + ",\"end_state\":null,\"process_score\":" + (result == null ? "null" : "16.0") + ",\"content_version\":\"v\",\"received_at\":\"2026-10-14T10:34:39Z\"}";

        private static string Page(params string[] rows) => "{\"total\":" + rows.Length + ",\"limit\":5,\"offset\":0,\"items\":[" + string.Join(",", rows) + "]}";

        private TabletView MakeTablet()
        {
            var go = new GameObject("tablet");
            _made.Add(go);
            var tablet = go.AddComponent<TabletView>();
            tablet.Build();
            return tablet;
        }

        private SimulationHost MakeHostWithAnAction()
        {
            var go = new GameObject("host");
            _made.Add(go);
            var host = go.AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            host.Apply(new SimAction("introduce_self", "patient", Channel.Vr, new Dictionary<string, string>()));
            return host;
        }

        private static IEnumerator SignIn(ScriptedHttp http)
        {
            http.Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"" + User + "\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo Hospital\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            SignInResult result = null;
            yield return new AuthService(http, Base).SignIn("jane@example.org", "pw", r => result = r);
            Assert.IsTrue(result.Ok, result.Message);
        }

        [UnityTest]
        public IEnumerator The_tablet_has_a_Session_tab_that_shows_the_signed_in_person_and_the_results_the_service_returns()
        {
            var http = new ScriptedHttp();
            yield return SignIn(http);
            http.Answer("GET", ListUrl, 200, Page(Row("verified", "SUCCESS"), Row("pending", null)));
            var tablet = MakeTablet();
            Assert.IsNotNull(tablet.SessionTab);
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Introduce yourself"), "the four action tabs are unchanged");
            Assert.IsFalse(tablet.Buttons.Any(b => b.label.StartsWith("Finish session")), "the Session tab's buttons are not simulation buttons");
            tablet.ShowTab(4);
            yield return null;
            yield return tablet.SessionTab.RefreshResults();
            StringAssert.Contains("T-ab12cd34", tablet.SessionTab.AccountText);
            StringAssert.Contains("Demo Hospital", tablet.SessionTab.AccountText);
            StringAssert.Contains("Success, score 16 (provisional)", tablet.SessionTab.ResultsText);
            StringAssert.Contains("waiting to be checked", tablet.SessionTab.ResultsText);
            tablet.ShowTab(0);
            Assert.IsFalse(tablet.SessionTab.gameObject.activeSelf, "the other tabs hide the Session tab again");
        }

        [UnityTest]
        public IEnumerator Without_signing_in_the_tab_says_the_person_is_practising_offline_and_the_results_ask_for_a_sign_in()
        {
            var tablet = MakeTablet();
            tablet.ShowTab(4);
            yield return null;
            yield return tablet.SessionTab.RefreshResults();
            StringAssert.Contains("offline", tablet.SessionTab.AccountText);
            StringAssert.Contains("Sign in", tablet.SessionTab.ResultsText);
        }

        [UnityTest]
        public IEnumerator Finishing_the_session_saves_it_sends_it_and_the_tab_then_shows_it_saved_on_the_service()
        {
            Assert.IsNotNull(SessionUploader.Instance);
            var http = new ScriptedHttp();
            yield return SignIn(http);
            http.Answer("POST", Base + "/organisations/" + Org + "/sessions", 202, "{\"outcome\":\"accepted\"}")
                .AnswerPrefix("GET", Base + "/organisations/" + Org + "/sessions/", 200, "{}")
                .Answer("GET", ListUrl, 200, Page(Row("pending", null)));
            // the real uploader, with a queue in a temporary folder and the scripted service answering for any session id
            var auth = AuthService.Current;
            SessionUploader.Instance.Sync = new SessionSync(new UploadQueue(Path.Combine(_dir, "outbox"), SessionUploader.UnixNow), () => auth, SessionUploader.UnixNow);
            var host = MakeHostWithAnAction();
            var tablet = MakeTablet();
            tablet.ShowTab(4);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(tablet.SessionTab.FinishButton.interactable, "there is something to finish");
            tablet.SessionTab.FinishButton.onClick.Invoke();
            Assert.IsTrue(host.Ended);
            StringAssert.Contains("Session saved", tablet.SessionTab.MessageText);
            Assert.IsFalse(tablet.SessionTab.FinishButton.interactable, "a finished session cannot be finished again");
            for (int i = 0; i < 200 && SessionUploader.Instance.Sync.Queue.Entries.Count > 0; i++) yield return null;
            Assert.AreEqual(0, SessionUploader.Instance.Sync.Queue.Entries.Count, "the queue is empty once the service confirmed the session");
            Assert.IsTrue(http.Sent.Any(s => s == "POST " + Base + "/organisations/" + Org + "/sessions"));
            Assert.IsTrue(http.Sent.Any(s => s.StartsWith("GET " + Base + "/organisations/" + Org + "/sessions/")), "the service was asked to confirm it has the session");
            float limit = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < limit && !tablet.SessionTab.MessageText.Contains("Saved on the service")) yield return null;   // the tab redraws twice a second
            StringAssert.Contains("Saved on the service", tablet.SessionTab.MessageText);
        }

        [UnityTest]
        public IEnumerator Finishing_while_signed_out_keeps_the_session_on_the_device_and_says_so()
        {
            var host = MakeHostWithAnAction();
            var tablet = MakeTablet();
            tablet.ShowTab(4);
            for (int i = 0; i < 3; i++) yield return null;
            tablet.SessionTab.FinishButton.onClick.Invoke();
            Assert.IsTrue(host.Ended);
            StringAssert.Contains("on this device", tablet.SessionTab.MessageText);
        }
    }
}
