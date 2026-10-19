using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Fetching a pre-generated scenario package from the service (plan p1-live-scenario-generation-s10, DT-199) without a network: a scripted service stands in for it. The headset check is Sannan's (DC-02).</summary>
    public class PackageClientTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Fixture => Path.Combine(Repo, "tests", "fixtures", "generated", "GEN-S1-0003-001.package.json");

        private sealed class ScriptedHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
                Bodies.Add(jsonBody);
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

        private static string Offer(string hash) => "{\"package_hash\":\"" + hash + "\",\"scenario\":\"S1\",\"level\":\"beginner\",\"case_id\":\"GEN-S1-0003-001\",\"spine\":\"VAR-S1-A\"}";

        private static FetchedPackage Fetch(ScriptedHttp http, AuthService auth)
        {
            FetchedPackage got = null;
            Run(PackageClient.FetchNext(auth, "S1", "beginner", r => got = r));
            return got;
        }

        [TearDown] public void TearDown() => AuthService.Clear();

        [Test]
        public void A_package_that_matches_its_hash_is_handed_over_and_the_engine_agrees_on_the_hash()
        {
            var text = File.ReadAllText(Fixture);
            var hash = PackageClient.Sha256Hex(text);
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 200, text);
            var got = Fetch(http, SignedIn(http));
            Assert.IsTrue(got.Ok, got.Error);
            Assert.AreEqual(hash, got.Hash);
            Assert.AreEqual(new ScenarioPackageReader(got.Text).PackageHash, got.Hash);
            CollectionAssert.Contains(http.Bodies, "{\"scenario\":\"S1\",\"level\":\"beginner\"}");
        }

        [Test]
        public void A_package_that_does_not_match_its_hash_is_refused_and_is_a_fault_not_an_offline_case()
        {
            var text = File.ReadAllText(Fixture);
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(PackageClient.Sha256Hex(text))).Answer("GET", Base + "/scenario-packages/" + PackageClient.Sha256Hex(text), 200, text + " ");
            var got = Fetch(http, SignedIn(http));
            Assert.IsFalse(got.Ok);
            Assert.IsFalse(got.PlayAuthoredInstead);
            StringAssert.Contains("does not match", got.Error);
        }

        [Test]
        public void No_pool_an_empty_pool_no_network_and_no_sign_in_all_leave_the_authored_cases_to_play()
        {
            foreach (var status in new long[] { 503, 404 })
            {
                var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", status, "{\"detail\":{\"code\":\"x\"}}");
                var got = Fetch(http, SignedIn(http));
                Assert.IsFalse(got.Ok);
                Assert.IsTrue(got.PlayAuthoredInstead, status.ToString());
            }
            var offline = new ScriptedHttp();
            var auth = SignedIn(offline);
            Assert.IsTrue(Fetch(offline, auth).PlayAuthoredInstead);                       // the scripted service has no answer for the request: no network
            FetchedPackage anonymous = null;
            Run(PackageClient.FetchNext(null, "S1", "beginner", r => anonymous = r));
            Assert.IsTrue(anonymous.PlayAuthoredInstead);
        }

        [Test]
        public void A_garbled_offer_or_a_missing_package_is_not_played()
        {
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, "not json");
            Assert.IsFalse(Fetch(http, SignedIn(http)).Ok);
            var hash = new string('a', 64);
            var http2 = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 404, "");
            var got = Fetch(http2, SignedIn(http2));
            Assert.IsFalse(got.Ok);
            Assert.IsTrue(got.PlayAuthoredInstead);
        }

        private static SimulationHost NewHost()
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            return host;
        }

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        [Test]
        public void The_starter_fetches_and_plays_a_package_in_practice_and_never_fetches_for_assessment()
        {
            var text = File.ReadAllText(Fixture);
            var hash = PackageClient.Sha256Hex(text);
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 200, text);
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                PackageStart got = null;
                Run(PackageSessionStarter.Run(auth, host, "S1", "beginner", 7, SessionMode.Practice, r => got = r, Real()));
                Assert.IsTrue(got.Started, got.Message);
                Assert.AreEqual(hash, host.PackageHash);
                Assert.AreEqual("GEN-S1-0003-001", host.Session.Case.Id);
                int before = http.Urls.Count;
                PackageStart assessment = null;
                Run(PackageSessionStarter.Run(auth, host, "S1", "beginner", 7, SessionMode.Assessment, r => assessment = r, Real()));
                Assert.IsFalse(assessment.Started);
                Assert.IsTrue(assessment.PlayAuthoredInstead);
                Assert.AreEqual(before, http.Urls.Count, "assessment must not even ask the service");
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_failed_fetch_starts_nothing_and_leaves_the_host_as_it_was()
        {
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 503, "{}");
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                PackageStart got = null;
                Run(PackageSessionStarter.Run(auth, host, "S1", "beginner", 7, SessionMode.Practice, r => got = r, Real()));
                Assert.IsFalse(got.Started);
                Assert.IsTrue(got.PlayAuthoredInstead);
                Assert.IsNull(host.Session);
                Assert.AreEqual("", host.PackageHash);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_package_the_engine_refuses_is_reported_and_starts_nothing()
        {
            var text = File.ReadAllText(Fixture).Replace("GENERATED_PRACTICE_ONLY", "APPROVED_FOR_ASSESSMENT");
            var hash = PackageClient.Sha256Hex(text);
            var http = new ScriptedHttp().Answer("POST", Base + "/scenario-packages/next", 200, Offer(hash)).Answer("GET", Base + "/scenario-packages/" + hash, 200, text);
            var auth = SignedIn(http);
            var host = NewHost();
            try
            {
                PackageStart got = null;
                Run(PackageSessionStarter.Run(auth, host, "S1", "beginner", 7, SessionMode.Practice, r => got = r, Real()));
                Assert.IsFalse(got.Started);
                Assert.IsFalse(got.PlayAuthoredInstead);
                StringAssert.Contains("refused", got.Message);
                Assert.IsNull(host.Session);
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }
    }
}
