using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The networked headset's request rules (P3-NET-07, 09, 10): time budgets, request ids, dropping late answers, headers, retry, pairing and the version check.</summary>
    public sealed class LanHttpTests
    {
        private sealed class Scripted : IHttp
        {
            public readonly List<(string Method, string Url, IDictionary<string, string> Headers, int Timeout)> Sent = new List<(string, string, IDictionary<string, string>, int)>();
            public readonly Queue<HttpResult> Answers = new Queue<HttpResult>();
            public Action DuringSend;
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Sent.Add((method, url, headers, timeoutSeconds));
                DuringSend?.Invoke();
                done(Answers.Count > 0 ? Answers.Dequeue() : new HttpResult(200, "{}"));
                yield break;
            }
        }

        private sealed class Memory : DeviceTokenStore.IStorage
        {
            private readonly Dictionary<string, string> _v = new Dictionary<string, string>();
            public string Get(string key) => _v.TryGetValue(key, out var s) ? s : "";
            public void Set(string key, string value) => _v[key] = value;
            public void Delete(string key) => _v.Remove(key);
        }

        private DeviceTokenStore.IStorage _before;
        [SetUp] public void SetUp() { _before = DeviceTokenStore.Storage; DeviceTokenStore.Storage = new Memory(); }
        [TearDown] public void TearDown() { DeviceTokenStore.Storage = _before; }

        private static void Run(IEnumerator routine) { while (routine.MoveNext()) if (routine.Current is IEnumerator inner) Run(inner); }

        [Test]
        public void Routes_are_classified_and_get_their_time_budget_but_never_more_than_the_caller_allows()
        {
            Assert.AreEqual(RequestKind.Health, RequestBudgets.Classify("http://192.168.1.20:8000/health"));
            Assert.AreEqual(RequestKind.Asr, RequestBudgets.Classify("http://192.168.1.20:8000/asr/organisations/o/transcribe"));
            Assert.AreEqual(RequestKind.Action, RequestBudgets.Classify("http://h/orders/organisations/o/hear"));
            Assert.AreEqual(RequestKind.Action, RequestBudgets.Classify("http://h/speech/organisations/o/action"));
            Assert.AreEqual(RequestKind.Dialogue, RequestBudgets.Classify("http://h/dialogue/x"));
            Assert.AreEqual(RequestKind.Tts, RequestBudgets.Classify("http://h/tts/organisations/o/speak"));
            Assert.AreEqual(RequestKind.Other, RequestBudgets.Classify("http://h/me"));
            Assert.AreEqual(6, RequestBudgets.SecondsFor(RequestKind.Asr, 100));
            Assert.AreEqual(3, RequestBudgets.SecondsFor(RequestKind.Asr, 3), "the caller's shorter wait wins");
            Assert.AreEqual(100, RequestBudgets.SecondsFor(RequestKind.Other, 100));
        }

        [Test]
        public void Every_request_carries_the_device_token_the_version_headers_and_a_rising_request_id()
        {
            DeviceTokenStore.Set("tok");
            var inner = new Scripted(); var http = new LanHttp(inner);
            Run(http.Send("POST", "http://h/orders/o/hear", "{}", new Dictionary<string, string> { { "Authorization", "Bearer user" } }, 100, _ => { }));
            Run(http.Send("POST", "http://h/dialogue/x", "{}", null, 100, _ => { }));
            var first = inner.Sent[0].Headers;
            Assert.AreEqual("tok", first["X-ProcSense-Device"]);
            Assert.AreEqual("Bearer user", first["Authorization"], "the signed-in user's header is left alone");
            Assert.AreEqual(ProcSense.Core.CoreInfo.Version, first["X-ProcSense-Engine"]);
            Assert.AreEqual("1", first["X-Request-Id"]); Assert.AreEqual("2", inner.Sent[1].Headers["X-Request-Id"]);
            Assert.AreEqual(8, inner.Sent[0].Timeout); Assert.AreEqual(10, inner.Sent[1].Timeout);
        }

        [Test]
        public void No_token_means_no_device_header()
        {
            var inner = new Scripted();
            Run(new LanHttp(inner).Send("GET", "http://h/health", null, null, 100, _ => { }));
            Assert.IsFalse(inner.Sent[0].Headers.ContainsKey("X-ProcSense-Device"));
        }

        [Test]
        public void An_answer_that_arrives_after_a_newer_request_of_the_same_kind_started_is_dropped()
        {
            var inner = new Scripted(); var http = new LanHttp(inner);
            HttpResult older = default, newer = default;
            inner.DuringSend = () => { inner.DuringSend = null; Run(http.Send("POST", "http://h/dialogue/b", "{}", null, 100, r => newer = r)); };
            Run(http.Send("POST", "http://h/dialogue/a", "{}", null, 100, r => older = r));
            Assert.IsTrue(older.Superseded, "the old answer must not be acted on");
            Assert.IsTrue(newer.Ok);
            Assert.AreEqual(1, http.Dropped);
        }

        [Test]
        public void A_read_is_retried_once_after_a_network_failure_but_a_post_never_is()
        {
            var inner = new Scripted(); var http = new LanHttp(inner);
            inner.Answers.Enqueue(new HttpResult(0, "", true)); inner.Answers.Enqueue(new HttpResult(200, "ok"));
            HttpResult got = default;
            Run(http.Send("GET", "http://h/health", null, null, 100, r => got = r));
            Assert.IsTrue(got.Ok); Assert.AreEqual(2, inner.Sent.Count);
            inner.Sent.Clear(); inner.Answers.Enqueue(new HttpResult(0, "", true));
            Run(http.Send("POST", "http://h/orders/o/hear", "{}", null, 100, r => got = r));
            Assert.IsTrue(got.NetworkError); Assert.AreEqual(1, inner.Sent.Count, "an action proposal is not repeated automatically");
        }

        [Test]
        public void Pairing_stores_the_token_and_reports_the_outcome_of_a_wrong_code_a_lock_and_no_laptop()
        {
            var inner = new Scripted();
            inner.Answers.Enqueue(new HttpResult(200, "{\"device_id\":\"ab12\",\"device_token\":\"secret\"}"));
            PairOutcome outcome = PairOutcome.Failed;
            Run(PairingClient.Exchange(inner, "http://h:8000", "123456", "quest", o => outcome = o));
            Assert.AreEqual(PairOutcome.Paired, outcome); Assert.AreEqual("secret", DeviceTokenStore.Get()); Assert.IsTrue(DeviceTokenStore.IsPaired);
            DeviceTokenStore.Clear();
            foreach (var (status, expected) in new[] { (403, PairOutcome.WrongCode), (429, PairOutcome.Locked), (404, PairOutcome.NotRequired), (500, PairOutcome.Failed) })
            {
                inner.Answers.Enqueue(new HttpResult(status, "{}"));
                Run(PairingClient.Exchange(inner, "http://h:8000/", "123456", "quest", o => outcome = o));
                Assert.AreEqual(expected, outcome, status.ToString()); Assert.IsFalse(DeviceTokenStore.IsPaired);
            }
            inner.Answers.Enqueue(new HttpResult(0, "", true));
            Run(PairingClient.Exchange(inner, "http://h:8000", "123456", "quest", o => outcome = o));
            Assert.AreEqual(PairOutcome.Offline, outcome);
        }

        [Test]
        public void The_version_check_says_when_the_laptop_does_not_know_this_engine_or_speaks_another_api()
        {
            var engine = ProcSense.Core.CoreInfo.Version;
            Assert.AreEqual(CompatState.Ok, PairingClient.Evaluate("{\"api_version\":1,\"engine_versions\":[\"" + engine + "\"]}"));
            Assert.AreEqual(CompatState.Ok, PairingClient.Evaluate("{\"status\":\"ok\"}"), "an older server that reports nothing is not refused");
            Assert.AreEqual(CompatState.EngineMismatch, PairingClient.Evaluate("{\"api_version\":1,\"engine_versions\":[\"9.9.9\"]}"));
            Assert.AreEqual(CompatState.ApiMismatch, PairingClient.Evaluate("{\"api_version\":2,\"engine_versions\":[\"" + engine + "\"]}"));
            Assert.AreEqual(CompatState.Unknown, PairingClient.Evaluate("not json"));
            Assert.AreNotEqual("", PairingClient.Banner(CompatState.EngineMismatch)); Assert.AreEqual("", PairingClient.Banner(CompatState.Ok));
        }

        [Test]
        public void The_keypad_takes_six_digits_and_no_more_and_can_go_back()
        {
            var entry = new PairingEntry();
            for (int d = 1; d <= 8; d++) entry.Press(d);
            Assert.AreEqual("123456", entry.Code); Assert.IsTrue(entry.Complete);
            entry.Back(); Assert.AreEqual("12345_", entry.Display); Assert.IsFalse(entry.Complete);
            entry.Clear(); Assert.AreEqual("______", entry.Display);
        }
    }
}
