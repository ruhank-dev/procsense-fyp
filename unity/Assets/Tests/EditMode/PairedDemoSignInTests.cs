using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Login;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The networked demonstration sign-in (P3-NET-07, P3-NET-11) without a network: a paired headset becomes the synthetic demonstration user through the laptop; real accounts are untouched.</summary>
    public class PairedDemoSignInTests
    {
        private const string Base = "http://192.168.1.20:8000";
        private const string Identity = "{\"user_id\":\"33333333-3333-3333-3333-333333333333\",\"pseudonym\":\"T-demo0001\",\"display_name\":\"Demo trainee\",\"organisation_id\":\"44444444-4444-4444-4444-444444444444\",\"organisation_name\":\"Demo hospital\",\"role\":\"trainee\"}";

        private sealed class MemoryStorage : DeviceTokenStore.IStorage
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key) => _values.TryGetValue(key, out var v) ? v : "";
            public void Set(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        private sealed class FakeHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<Dictionary<string, string>> Headers = new List<Dictionary<string, string>>();
            private readonly long _status; private readonly string _body; private readonly bool _network;
            public FakeHttp(long status, string body = "", bool networkError = false) { _status = status; _body = body; _network = networkError; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url);
                Headers.Add(headers == null ? new Dictionary<string, string>() : new Dictionary<string, string>(headers));
                done(new HttpResult(_status, _body, _network));
                yield break;
            }
        }

        private DeviceTokenStore.IStorage _storage;
        private bool _offlineBefore, _privateBefore;

        [SetUp]
        public void SetUp()
        {
            _storage = DeviceTokenStore.Storage;
            DeviceTokenStore.Storage = new MemoryStorage();
            _offlineBefore = DemoLoginSettings.OfflineDemoAllowed;
            _privateBefore = ServiceSettings.AllowPrivateHttp;
            ServiceSettings.AllowPrivateHttp = true;   // the LAN address of the laptop
            AuthService.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            DeviceTokenStore.Storage = _storage;
            DemoLoginSettings.OfflineDemoAllowed = _offlineBefore;
            ServiceSettings.AllowPrivateHttp = _privateBefore;
            AuthService.Clear();
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

        private static SignInResult SignIn(AuthService service)
        {
            SignInResult result = null;
            Run(service.SignInPairedDevice(r => result = r));
            Assert.IsNotNull(result, "the sign-in never finished");
            return result;
        }

        [Test]
        public void A_paired_headset_signs_in_as_the_demonstration_user_with_its_device_token()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            var http = new FakeHttp(200, Identity);
            var service = new AuthService(http, Base);
            var result = SignIn(service);
            Assert.IsTrue(result.Ok, result.Message);
            Assert.AreSame(service, AuthService.Current);
            Assert.IsTrue(service.IsSignedIn); Assert.IsTrue(service.IsDemoDevice);
            Assert.AreEqual("33333333-3333-3333-3333-333333333333", service.UserId);
            Assert.AreEqual("44444444-4444-4444-4444-444444444444", service.OrganisationId);
            Assert.AreEqual("T-demo0001", service.Pseudonym);
            Assert.AreEqual("trainee", service.Role);
            CollectionAssert.AreEqual(new[] { "GET " + Base + AuthService.DemoIdentityPath }, http.Urls);
            Assert.AreEqual("DEVTOKEN", http.Headers[0]["X-ProcSense-Device"]);
        }

        [Test]
        public void Later_calls_carry_the_device_token_and_no_password_is_ever_sent()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            var http = new FakeHttp(200, Identity);
            var service = new AuthService(http, Base);
            Assert.IsTrue(SignIn(service).Ok);
            http.Urls.Clear(); http.Headers.Clear();
            HttpResult answer = default;
            Run(service.Authorized("GET", "/organisations/o/sessions", null, r => answer = r));
            Assert.AreEqual("Bearer DEVTOKEN", http.Headers[0]["Authorization"]);
            Assert.IsTrue(service.IsSignedIn, "a signed-in demonstration headset is not signed out by a normal call");
        }

        [Test]
        public void A_headset_that_is_not_paired_does_not_touch_the_network()
        {
            var http = new FakeHttp(200, Identity);
            var service = new AuthService(http, Base);
            var result = SignIn(service);
            Assert.AreEqual(SignInOutcome.NotPaired, result.Outcome);
            Assert.AreEqual(0, http.Urls.Count);
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void A_revoked_headset_forgets_its_token_and_is_told_to_pair_again()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            foreach (var status in new long[] { 401, 403 })
            {
                DeviceTokenStore.Set("DEVTOKEN");
                var service = new AuthService(new FakeHttp(status), Base);
                Assert.AreEqual(SignInOutcome.NotPaired, SignIn(service).Outcome, status.ToString());
                Assert.IsFalse(DeviceTokenStore.IsPaired, "the revoked token is dropped");
                Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
            }
        }

        [Test]
        public void An_unreachable_laptop_or_a_server_without_the_demonstration_identity_signs_nobody_in()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            Assert.AreEqual(SignInOutcome.Unreachable, SignIn(new AuthService(new FakeHttp(0, "", true), Base)).Outcome);
            Assert.IsTrue(DeviceTokenStore.IsPaired, "no network is not a revocation");
            Assert.AreEqual(SignInOutcome.Failed, SignIn(new AuthService(new FakeHttp(404), Base)).Outcome);
            Assert.AreEqual(SignInOutcome.Failed, SignIn(new AuthService(new FakeHttp(200, "not json"), Base)).Outcome);
            Assert.AreEqual(SignInOutcome.Failed, SignIn(new AuthService(new FakeHttp(200, "{\"user_id\":\"u\"}"), Base)).Outcome, "an identity with no organisation cannot upload");
            Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void An_address_that_is_not_allowed_is_refused_before_any_request()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            var http = new FakeHttp(200, Identity);
            Assert.AreEqual(SignInOutcome.ServiceNotSet, SignIn(new AuthService(http, "http://example.com")).Outcome);
            Assert.AreEqual(0, http.Urls.Count);
        }

        [Test]
        public void Signing_out_forgets_the_demonstration_identity()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            var service = new AuthService(new FakeHttp(200, Identity), Base);
            Assert.IsTrue(SignIn(service).Ok);
            service.SignOut();
            Assert.IsFalse(service.IsSignedIn); Assert.IsFalse(service.IsDemoDevice); Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void Only_the_fixed_demonstration_account_on_a_paired_headset_takes_the_laptop_road()
        {
            Assert.IsFalse(PairedDemoSignIn.Applies(DemoLoginSettings.DemoEmail), "not paired");
            DeviceTokenStore.Set("DEVTOKEN");
            Assert.IsTrue(PairedDemoSignIn.Applies(DemoLoginSettings.DemoEmail));
            Assert.IsTrue(PairedDemoSignIn.Applies("  " + DemoLoginSettings.DemoEmail.ToUpperInvariant() + " "));
            Assert.IsFalse(PairedDemoSignIn.Applies("jane@example.org"), "real accounts keep the hosted service");
            Assert.IsFalse(PairedDemoSignIn.Applies(""));
        }

        [Test]
        public void The_flow_moves_on_after_the_laptop_identity_signs_in()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            int entered = 0; var messages = new List<string>();
            Run(PairedDemoSignIn.Run(() => new AuthService(new FakeHttp(200, Identity), Base), false, messages.Add, () => entered++));   // finished stands for MenuFlow.SignedIn
            Assert.AreEqual(1, entered);
            Assert.IsNotNull(AuthService.Current);
            Assert.AreEqual("44444444-4444-4444-4444-444444444444", AuthService.Current.OrganisationId);
        }

        [Test]
        public void A_failed_laptop_sign_in_stays_on_the_login_screen_when_the_offline_demonstration_is_off()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            int entered = 0; var messages = new List<string>();
            Run(PairedDemoSignIn.Run(() => new AuthService(new FakeHttp(0, "", true), Base), false, messages.Add, () => entered++));
            Assert.AreEqual(0, entered);
            Assert.IsNull(AuthService.Current);
            Assert.AreEqual("Cannot reach the laptop.", messages[messages.Count - 1]);
        }

        [Test]
        public void A_failed_laptop_sign_in_falls_back_to_the_offline_demonstration_when_the_build_allows_it()
        {
            DeviceTokenStore.Set("DEVTOKEN");
            int entered = 0;
            Run(PairedDemoSignIn.Run(() => new AuthService(new FakeHttp(0, "", true), Base), true, _ => { }, () => entered++));
            Assert.AreEqual(1, entered);
            Assert.IsNull(AuthService.Current, "the offline demonstration has no service identity");
        }
    }
}
