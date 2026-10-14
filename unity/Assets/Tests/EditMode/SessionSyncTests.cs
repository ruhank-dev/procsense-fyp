using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Sync;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The upload queue and the sender, without a network (p1-backend-s03): a scripted service stands in for the real one.</summary>
    public class SessionSyncTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private const string User = "11111111-1111-1111-1111-111111111111";
        private const string Config = "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"sb_publishable_test\"}";
        private const string Token = "{\"access_token\":\"AT1\",\"refresh_token\":\"RT1\",\"expires_in\":3600}";

        private sealed class Request { public string Method, Url, Body; public Dictionary<string, string> Headers; }

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<Request> Sent = new List<Request>();
            private readonly Dictionary<string, Queue<HttpResult>> _answers = new Dictionary<string, Queue<HttpResult>>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "")
            {
                var key = method + " " + url;
                if (!_answers.ContainsKey(key)) _answers[key] = new Queue<HttpResult>();
                _answers[key].Enqueue(new HttpResult(status, body));
                return this;
            }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Sent.Add(new Request { Method = method, Url = url, Body = jsonBody, Headers = headers == null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers) });
                var key = method + " " + url;
                if (!_answers.TryGetValue(key, out var queue) || queue.Count == 0) { done(new HttpResult(0, "", true)); yield break; }
                done(queue.Count > 1 ? queue.Dequeue() : queue.Peek());
            }
            public IEnumerable<Request> To(string path) => Sent.Where(r => r.Url == Base + path);
        }

        private static string SessionUrl => "/organisations/" + Org + "/sessions";

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

        private string _dir;
        private long _now;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "procsense-sync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _now = 1_000_000;
            AuthService.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            AuthService.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private AuthService SignedIn(ScriptedHttp http, string userId = User)
        {
            var me = "{\"user_id\":\"" + userId + "\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}";
            http.Answer("GET", Base + "/config.json", 200, Config).Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, Token)
                .Answer("GET", Base + "/me", 200, me).Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            var service = new AuthService(http, Base);
            SignInResult result = null;
            Run(service.SignIn("jane@example.org", "pw", r => result = r));
            Assert.IsTrue(result.Ok, result.Message);
            return service;
        }

        private (UploadQueue queue, SessionSync sync, UploadEntry entry, string file) Setup(AuthService auth, string text = "{\"header\":{}}\n{\"seq\":0}\n", string sessionId = "s1")
        {
            var queue = new UploadQueue(_dir, () => _now);
            string file = Path.Combine(_dir, sessionId + ".jsonl");
            File.WriteAllText(file, text);
            queue.Enqueue(sessionId, file, User, Org);
            return (queue, new SessionSync(queue, () => auth, () => _now), queue.Entries[0], file);
        }

        // ---- the queue -------------------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void The_same_session_is_queued_once_and_the_queue_survives_a_restart()
        {
            var queue = new UploadQueue(_dir, () => _now);
            Assert.IsTrue(queue.Enqueue("s1", "a.jsonl", User, Org));
            Assert.IsFalse(queue.Enqueue("s1", "a.jsonl", User, Org));
            queue.Enqueue("s2", "b.jsonl", User, Org);
            queue.Retry(queue.Entries[0], "no_connection");
            var restarted = new UploadQueue(_dir, () => _now);
            CollectionAssert.AreEqual(new[] { "s1", "s2" }, restarted.Entries.Select(e => e.sessionId).ToArray());
            Assert.AreEqual(1, restarted.Entries[0].attempts);
            Assert.AreEqual("no_connection", restarted.Entries[0].reason);
            Assert.AreEqual(User, restarted.Entries[1].userId);
        }

        [Test]
        public void A_damaged_queue_file_starts_an_empty_queue_instead_of_stopping_the_app()
        {
            File.WriteAllText(Path.Combine(_dir, UploadQueue.FileName), "{ not json");
            Assert.AreEqual(0, new UploadQueue(_dir, () => _now).Entries.Count);
        }

        [Test]
        public void The_backoff_doubles_from_15_seconds_to_a_ten_minute_ceiling()
        {
            CollectionAssert.AreEqual(new long[] { 15, 30, 60, 120, 240, 480, 600, 600 }, new[] { 1, 2, 3, 4, 5, 6, 7, 50 }.Select(UploadQueue.BackoffSeconds).ToArray());
        }

        // ---- sending ---------------------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void A_stored_session_is_sent_as_the_file_is_confirmed_by_the_service_and_only_then_removed()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 202, "{\"outcome\":\"accepted\"}").Answer("GET", Base + SessionUrl + "/s1", 200, "{\"status\":\"pending\"}");
            var (queue, sync, entry, file) = Setup(auth, "HEADER\nLOG\n");
            Run(sync.Process());
            var post = http.To(SessionUrl).Single();
            Assert.AreEqual("POST", post.Method);
            Assert.AreEqual("HEADER\nLOG\n", post.Body, "the file goes up exactly as it was saved");
            Assert.AreEqual("application/x-ndjson", post.Headers["Content-Type"]);
            Assert.AreEqual("Bearer AT1", post.Headers["Authorization"]);
            Assert.AreEqual(1, http.To(SessionUrl + "/s1").Count(), "the service is asked whether it really has the session");
            Assert.AreEqual(0, queue.Entries.Count);
            Assert.AreEqual(_now, sync.LastUploadUnix);
            Assert.IsTrue(File.Exists(file), "the session file stays on the device");
            StringAssert.Contains("saved on the service", sync.Describe());
        }

        [Test]
        public void A_session_the_service_already_has_is_not_an_error()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 200, "{\"outcome\":\"already_stored\"}").Answer("GET", Base + SessionUrl + "/s1", 200, "{}");
            var (queue, sync, _, _) = Setup(auth);
            Run(sync.Process());
            Assert.AreEqual(0, queue.Entries.Count);
        }

        [Test]
        public void A_busy_or_broken_service_is_retried_later_with_a_longer_wait_each_time_and_not_before()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 500).Answer("POST", Base + SessionUrl, 429).Answer("POST", Base + SessionUrl, 503);
            var (queue, sync, entry, _) = Setup(auth);
            Run(sync.Process());
            Assert.AreEqual(1, entry.attempts); Assert.AreEqual(_now + 15, entry.nextAtUnix); Assert.AreEqual(UploadEntry.Waiting, entry.state);
            Run(sync.Process());
            Assert.AreEqual(1, http.To(SessionUrl).Count(), "not tried again before it is due");
            _now += 15;
            Run(sync.Process());
            Assert.AreEqual(2, entry.attempts); Assert.AreEqual(_now + 30, entry.nextAtUnix);
            _now += 30;
            Run(sync.Process());
            Assert.AreEqual(3, entry.attempts); Assert.AreEqual(_now + 60, entry.nextAtUnix);
            Assert.AreEqual(1, queue.Entries.Count, "a failing service never loses the session");
        }

        [Test]
        public void No_connection_stops_the_pass_and_keeps_every_session()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);   // nothing answers the upload address: no network
            var (queue, sync, first, _) = Setup(auth, sessionId: "s1");
            queue.Enqueue("s2", Path.Combine(_dir, "s1.jsonl"), User, Org);
            Run(sync.Process());
            Assert.AreEqual(1, http.To(SessionUrl).Count(), "the second session is not tried when the first found no network");
            Assert.AreEqual(2, queue.Entries.Count);
            Assert.AreEqual("no_connection", first.reason);
            Assert.AreEqual(false, sync.ServiceReachable);
            StringAssert.Contains("cannot be reached", sync.Describe());
        }

        [Test]
        public void A_refused_sign_in_waits_for_the_person_without_counting_a_failure()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 401).Answer("POST", Sb + "/auth/v1/token?grant_type=refresh_token", 400, "{}");
            var (queue, sync, entry, _) = Setup(auth);
            Run(sync.Process());
            Assert.IsFalse(auth.IsSignedIn);
            Assert.AreEqual(0, entry.attempts);
            Assert.AreEqual(1, queue.Entries.Count);
        }

        [Test]
        public void A_person_who_has_not_agreed_yet_is_retried_later_not_given_up_on()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 403, "{\"detail\":{\"code\":\"consent_required\"}}");
            var (queue, sync, entry, _) = Setup(auth);
            Run(sync.Process());
            Assert.AreEqual(UploadEntry.Waiting, entry.state); Assert.AreEqual("consent_required", entry.reason); Assert.AreEqual(1, entry.attempts);
        }

        [TestCase(409)] [TestCase(413)] [TestCase(422)] [TestCase(404)]
        public void A_session_the_service_refuses_for_good_is_set_aside_with_the_reason_and_not_retried(int status)
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, status, "{\"detail\":\"no\"}");
            var (queue, sync, entry, _) = Setup(auth);
            Run(sync.Process());
            Assert.AreEqual(UploadEntry.NeedsAttention, entry.state);
            Assert.AreEqual("refused_" + status, entry.reason);
            _now += 100000;
            Run(sync.Process());
            Assert.AreEqual(1, http.To(SessionUrl).Count(), "never retried");
            StringAssert.Contains("need attention", sync.Describe());
        }

        [Test]
        public void A_session_is_kept_when_the_service_cannot_confirm_it_has_it()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + SessionUrl, 202).Answer("GET", Base + SessionUrl + "/s1", 404);
            var (queue, sync, entry, _) = Setup(auth);
            Run(sync.Process());
            Assert.AreEqual(1, queue.Entries.Count);
            Assert.AreEqual("not_confirmed", entry.reason);
        }

        [Test]
        public void A_missing_session_file_is_set_aside()
        {
            var http = new ScriptedHttp();
            var auth = SignedIn(http);
            var (queue, sync, entry, file) = Setup(auth);
            File.Delete(file);
            Run(sync.Process());
            Assert.AreEqual("file_missing", entry.reason); Assert.AreEqual(UploadEntry.NeedsAttention, entry.state);
            Assert.AreEqual(0, http.To(SessionUrl).Count());
        }

        // ---- whose session it is ---------------------------------------------------------------------------------------------------------------------------------------------------

        [Test]
        public void A_session_is_only_ever_sent_as_the_person_it_was_played_under()
        {
            var http = new ScriptedHttp();
            var other = SignedIn(http, userId: "99999999-9999-9999-9999-999999999999");   // someone else signed in on this headset afterwards
            http.Answer("POST", Base + SessionUrl, 202).Answer("GET", Base + SessionUrl + "/s1", 200, "{}");
            var queue = new UploadQueue(_dir, () => _now);
            string file = Path.Combine(_dir, "s1.jsonl");
            File.WriteAllText(file, "x");
            queue.Enqueue("s1", file, User, Org);
            var sync = new SessionSync(queue, () => other, () => _now);
            Run(sync.Process());
            Assert.AreEqual(0, http.To(SessionUrl).Count(), "another person's session must not be sent under this sign-in");
            StringAssert.Contains("Not signed in", new SessionSync(queue, () => null, () => _now).Describe());
            Assert.AreEqual(1, queue.NotUploadable("99999999-9999-9999-9999-999999999999"));
        }

        [Test]
        public void Nothing_is_sent_when_nobody_is_signed_in_and_the_session_waits_for_its_person()
        {
            var http = new ScriptedHttp();
            var queue = new UploadQueue(_dir, () => _now);
            queue.Enqueue("s1", "x.jsonl", User, Org);
            var sync = new SessionSync(queue, () => null, () => _now);
            Run(sync.Process());
            Assert.AreEqual(0, http.Sent.Count);
            Assert.AreEqual(1, queue.Entries.Count);
        }

        [Test]
        public void Without_a_signed_in_person_a_finished_session_is_not_queued_at_all()
        {
            Assert.IsFalse(SessionUploader.Enqueue("s1", "x.jsonl"), "it has nobody to belong to: it stays on the device");
        }
    }
}
