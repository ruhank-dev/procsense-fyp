using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Sync;

namespace ProcSense.Tests.EditMode
{
    internal static class LanRun
    {
        public static void Run(IEnumerator routine)
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

        public sealed class MemoryStorage : DeviceTokenStore.IStorage
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key) => _values.TryGetValue(key, out var v) ? v : "";
            public void Set(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        public sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public Action<string> OnSend;
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
                OnSend?.Invoke(url);
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
        }
    }

    /// <summary>P3-NET-14 without a network: the last good package is cached on the device and used when the laptop cannot supply one.</summary>
    public class PackageCacheLanTests
    {
        private const string Base = "http://192.168.1.20:8000";
        private sealed class MemoryPackages : IPackageStorage
        {
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>();
            public string Read(string key) => Files.TryGetValue(key, out var v) ? v : "";
            public void Write(string key, string text) => Files[key] = text;
        }

        private DeviceTokenStore.IStorage _storage; private bool _privateBefore;
        [SetUp] public void SetUp() { _storage = DeviceTokenStore.Storage; DeviceTokenStore.Storage = new LanRun.MemoryStorage(); _privateBefore = ServiceSettings.AllowPrivateHttp; ServiceSettings.AllowPrivateHttp = true; AuthService.Clear(); }
        [TearDown] public void TearDown() { DeviceTokenStore.Storage = _storage; ServiceSettings.AllowPrivateHttp = _privateBefore; AuthService.Clear(); }

        private const string Identity = "{\"user_id\":\"u\",\"pseudonym\":\"T-demo0001\",\"display_name\":\"D\",\"organisation_id\":\"o\",\"organisation_name\":\"Demo\",\"role\":\"trainee\"}";
        private static string Offer(string hash) => "{\"package_hash\":\"" + hash + "\",\"scenario\":\"S1\",\"level\":\"beginner\",\"case_id\":\"GEN-1\",\"spine\":\"V\"}";

        private static AuthService Paired(LanRun.ScriptedHttp http)
        {
            DeviceTokenStore.Set("DEVTOKEN");
            http.Answer("GET", Base + AuthService.DemoIdentityPath, 200, Identity);
            var s = new AuthService(http, Base);
            SignInResult r = null; LanRun.Run(s.SignInPairedDevice(x => r = x));
            Assert.IsTrue(r.Ok, r.Message);
            return s;
        }

        private static FetchedPackage Fetch(AuthService a, PackageCache c) { FetchedPackage got = null; LanRun.Run(PackageClient.FetchNextCached(a, c, "S1", "beginner", r => got = r)); return got; }

        [Test]
        public void A_good_package_is_cached_and_used_when_the_laptop_is_unreachable_later()
        {
            const string text = "{\"case\":{\"id\":\"GEN-1\"}}";
            var hash = PackageClient.Sha256Hex(text);
            var cache = new PackageCache(new MemoryPackages());
            var http = new LanRun.ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 200, text);
            var first = Fetch(Paired(http), cache);
            Assert.IsTrue(first.Ok); Assert.IsFalse(first.Cached);

            var offline = new LanRun.ScriptedHttp();   // the package routes give no answer
            var second = Fetch(Paired(offline), cache);
            Assert.IsTrue(second.Ok); Assert.IsTrue(second.Cached);
            Assert.AreEqual(text, second.Text); Assert.AreEqual(hash, second.Hash);
            StringAssert.Contains("saved on this headset", second.Error);
        }

        [Test]
        public void With_nothing_cached_the_authored_pool_is_played_and_a_damaged_cache_is_never_used()
        {
            var store = new MemoryPackages();
            var cache = new PackageCache(store);
            var none = Fetch(Paired(new LanRun.ScriptedHttp()), cache);
            Assert.IsFalse(none.Ok); Assert.IsTrue(none.PlayAuthoredInstead);

            store.Files["S1_beginner"] = PackageClient.Sha256Hex("good") + "\n" + "tampered";
            var bad = Fetch(Paired(new LanRun.ScriptedHttp()), cache);
            Assert.IsFalse(bad.Ok); Assert.IsTrue(bad.PlayAuthoredInstead);
        }

        [Test]
        public void A_hash_mismatch_is_a_fault_and_is_not_replaced_by_the_cache()
        {
            const string text = "{\"a\":1}";
            var cache = new PackageCache(new MemoryPackages());
            cache.Save("S1", "beginner", PackageClient.Sha256Hex("old"), "old");
            var hash = PackageClient.Sha256Hex(text);
            var http = new LanRun.ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 200, text + " ");
            var got = Fetch(Paired(http), cache);
            Assert.IsFalse(got.Ok); Assert.IsFalse(got.PlayAuthoredInstead); Assert.IsFalse(got.Cached);
        }
    }

    /// <summary>P3-NET-15: the certificate fingerprint pinned at pairing; a different certificate is refused.</summary>
    public class TlsPinTests
    {
        private const string Https = "https://192.168.1.20:8000";
        private static readonly string Fp = new string('a', 64);
        private static readonly string Other = new string('b', 64);
        private DeviceTokenStore.IStorage _storage;
        [SetUp] public void SetUp() { _storage = DeviceTokenStore.Storage; DeviceTokenStore.Storage = new LanRun.MemoryStorage(); TlsPin.Storage = null; TlsPin.ResetState(); }
        [TearDown] public void TearDown() { TlsPin.ResetState(); DeviceTokenStore.Storage = _storage; }

        [Test]
        public void A_pinned_certificate_is_accepted_and_a_different_one_is_refused_with_a_banner()
        {
            TlsPin.Set(Fp.ToUpperInvariant());
            Assert.AreEqual(PinDecision.Accept, TlsPin.Decide(Fp));
            Assert.AreEqual("", TlsPin.Banner());
            Assert.AreEqual(PinDecision.Refuse, TlsPin.Decide(Other));
            Assert.IsTrue(TlsPin.Mismatch);
            Assert.AreNotEqual("", TlsPin.Banner());
        }

        [Test]
        public void Without_a_pin_a_certificate_is_refused_outside_the_pairing_window()
        {
            Assert.AreEqual(PinDecision.Refuse, TlsPin.Decide(Fp));
            TlsPin.PairingWindow = true;
            Assert.AreEqual(PinDecision.Accept, TlsPin.Decide(Fp));
        }

        [Test]
        public void Pinning_applies_only_to_https_on_the_private_network()
        {
            Assert.IsTrue(TlsPin.AppliesTo(Https));
            Assert.IsTrue(TlsPin.AppliesTo("https://127.0.0.1:8000"));
            Assert.IsFalse(TlsPin.AppliesTo("http://192.168.1.20:8000"));
            Assert.IsFalse(TlsPin.AppliesTo("https://service.example.org"));
            Assert.IsFalse(TlsPin.AppliesTo("https://8.8.8.8"));
        }

        [Test]
        public void Pairing_over_https_pins_the_fingerprint_the_connection_showed()
        {
            var http = new LanRun.ScriptedHttp().Answer("POST", Https + "/pairing/exchange", 200, "{\"device_id\":\"d\",\"device_token\":\"T\",\"tls_fingerprint\":\"" + Fp + "\"}");
            http.OnSend = _ => TlsPin.Decide(Fp);           // what the certificate handler does during the connection
            PairOutcome outcome = PairOutcome.Failed;
            LanRun.Run(PairingClient.Exchange(http, Https, "123456", "q", o => outcome = o));
            Assert.AreEqual(PairOutcome.Paired, outcome);
            Assert.AreEqual(Fp, TlsPin.Get());
            Assert.AreEqual(PinDecision.Refuse, TlsPin.Decide(Other));
        }

        [Test]
        public void Pairing_fails_when_the_reported_fingerprint_is_not_the_one_the_connection_showed()
        {
            var http = new LanRun.ScriptedHttp().Answer("POST", Https + "/pairing/exchange", 200, "{\"device_id\":\"d\",\"device_token\":\"T\",\"tls_fingerprint\":\"" + Other + "\"}");
            http.OnSend = _ => TlsPin.Decide(Fp);
            PairOutcome outcome = PairOutcome.Paired;
            LanRun.Run(PairingClient.Exchange(http, Https, "123456", "q", o => outcome = o));
            Assert.AreEqual(PairOutcome.Failed, outcome);
            Assert.IsFalse(TlsPin.IsPinned); Assert.AreEqual("", DeviceTokenStore.Get());
        }

        [Test]
        public void Pairing_over_plain_http_is_unchanged_and_pins_nothing()
        {
            const string plain = "http://192.168.1.20:8000";
            var http = new LanRun.ScriptedHttp().Answer("POST", plain + "/pairing/exchange", 200, "{\"device_id\":\"d\",\"device_token\":\"T\"}");
            PairOutcome outcome = PairOutcome.Failed;
            LanRun.Run(PairingClient.Exchange(http, plain, "123456", "q", o => outcome = o));
            Assert.AreEqual(PairOutcome.Paired, outcome); Assert.IsFalse(TlsPin.IsPinned);
        }
    }

    /// <summary>P3-NET-12: the optional notice socket, over a scripted transport. Without it everything else is untouched.</summary>
    public class HeadsetNoticesTests
    {
        private sealed class FakeTransport : INoticeTransport
        {
            public readonly Queue<string> Inbox = new Queue<string>();
            public string OpenedUrl, OpenedToken; public bool ThrowOnOpen;
            public bool Closed { get; set; }
            public void Open(string url, string token) { if (ThrowOnOpen) throw new InvalidOperationException("no sockets here"); OpenedUrl = url; OpenedToken = token; }
            public bool TryReceive(out string m) { if (Inbox.Count > 0) { m = Inbox.Dequeue(); return true; } m = null; return false; }
            public void Close() { Closed = true; }
        }

        private DeviceTokenStore.IStorage _storage; private bool _privateBefore;
        [SetUp] public void SetUp() { _storage = DeviceTokenStore.Storage; DeviceTokenStore.Storage = new LanRun.MemoryStorage(); _privateBefore = ServiceSettings.AllowPrivateHttp; ServiceSettings.AllowPrivateHttp = true; }
        [TearDown] public void TearDown() { DeviceTokenStore.Storage = _storage; ServiceSettings.AllowPrivateHttp = _privateBefore; }

        [Test]
        public void The_socket_address_follows_the_https_and_private_network_rules()
        {
            Assert.AreEqual("ws://192.168.1.20:8000/ws/headset/s-1", HeadsetNotices.SocketUrl("http://192.168.1.20:8000/", "s-1"));
            Assert.AreEqual("wss://192.168.1.20:8000/ws/headset/s1", HeadsetNotices.SocketUrl("https://192.168.1.20:8000", "s1"));
            Assert.AreEqual("", HeadsetNotices.SocketUrl("http://example.org", "s1"));
            Assert.AreEqual("", HeadsetNotices.SocketUrl("http://192.168.1.20:8000", "a/b"));
            Assert.AreEqual("", HeadsetNotices.SocketUrl("http://192.168.1.20:8000", new string('x', 65)));
        }

        [Test]
        public void Notices_update_progress_quota_and_shutdown_and_the_token_is_not_in_the_address()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            var t = new FakeTransport();
            var n = new HeadsetNotices(t);
            Assert.IsTrue(n.Start("http://192.168.1.20:8000", "s1"));
            Assert.AreEqual("DEVTOKEN", t.OpenedToken); StringAssert.DoesNotContain("DEVTOKEN", t.OpenedUrl);
            t.Inbox.Enqueue("{\"kind\":\"hello\",\"session_id\":\"s1\",\"detail\":\"\"}");
            t.Inbox.Enqueue("{\"kind\":\"generation_progress\",\"session_id\":\"s1\",\"progress\":0.5,\"detail\":\"\"}");
            Assert.AreEqual(2, n.Poll());
            Assert.IsTrue(n.Connected); Assert.AreEqual(0.5f, n.Progress, 0.001f);
            StringAssert.Contains("50%", n.Status());
            t.Inbox.Enqueue("{\"kind\":\"quota\",\"session_id\":\"s1\",\"detail\":\"cloud\"}");
            n.Poll(); Assert.IsTrue(n.QuotaExhausted);
            t.Inbox.Enqueue("{\"kind\":\"shutdown\",\"session_id\":\"s1\"}");
            n.Poll(); Assert.IsTrue(n.Shutdown); StringAssert.Contains("shutting down", n.Status());
        }

        [Test]
        public void Garbage_unknown_kinds_and_a_dropped_socket_change_nothing_and_never_throw()
        {
            DeviceTokenStore.Set("T");
            var t = new FakeTransport(); var n = new HeadsetNotices(t);
            n.Start("http://192.168.1.20:8000", "s1");
            t.Inbox.Enqueue("not json"); t.Inbox.Enqueue("{\"kind\":\"mystery\"}");
            Assert.DoesNotThrow(() => n.Poll());
            Assert.IsFalse(n.Connected); Assert.IsFalse(n.Shutdown); Assert.IsFalse(n.QuotaExhausted); Assert.AreEqual("", n.Status());
            t.Closed = true; Assert.DoesNotThrow(() => n.Poll()); Assert.IsFalse(n.Connected);
        }

        [Test]
        public void An_unpaired_headset_a_bad_address_or_a_failing_transport_simply_does_not_connect()
        {
            var t = new FakeTransport();
            Assert.IsFalse(new HeadsetNotices(t).Start("http://192.168.1.20:8000", "s1"), "not paired");
            DeviceTokenStore.Set("T");
            Assert.IsFalse(new HeadsetNotices(t).Start("http://example.org", "s1"), "public http");
            Assert.IsNull(t.OpenedUrl);
            Assert.IsFalse(new HeadsetNotices(new FakeTransport { ThrowOnOpen = true }).Start("http://192.168.1.20:8000", "s1"));
            Assert.IsFalse(new HeadsetNotices(null).Start("http://192.168.1.20:8000", "s1"));
            Assert.AreEqual(0, new HeadsetNotices(null).Poll());
        }
    }
}
