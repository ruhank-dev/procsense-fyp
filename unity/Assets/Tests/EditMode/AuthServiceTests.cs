using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Auth;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Signing in to the hosted service without a network: a scripted service stands in for it (p1-backend-s10).</summary>
    public class AuthServiceTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Config = "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"sb_publishable_test\"}";
        private const string Token = "{\"access_token\":\"AT1\",\"refresh_token\":\"RT1\",\"expires_in\":3600,\"token_type\":\"bearer\"}";
        private const string Me = "{\"user_id\":\"11111111-1111-1111-1111-111111111111\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"Jane\",\"is_platform_admin\":false,\"memberships\":["
            + "{\"organisation_id\":\"0000-old\",\"organisation_name\":\"Old\",\"slug\":\"old\",\"role\":\"trainee\",\"status\":\"deactivated\"},"
            + "{\"organisation_id\":\"22222222-2222-2222-2222-222222222222\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}";
        private const string ConsentOk = "{\"version\":\"v\",\"needs_consent\":false,\"kinds\":[]}";
        private const string ConsentNeeded = "{\"version\":\"v\",\"needs_consent\":true,\"kinds\":[]}";

        private sealed class Request { public string Method, Url, Body; public Dictionary<string, string> Headers; }

        /// <summary>Answers by method and address, in order; the last answer for an address repeats. An address with no answer behaves like no network.</summary>
        private sealed class FakeHttp : IHttp
        {
            public readonly List<Request> Sent = new List<Request>();
            private readonly Dictionary<string, Queue<HttpResult>> _answers = new Dictionary<string, Queue<HttpResult>>();
            public FakeHttp Answer(string method, string url, long status, string body = "")
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
        }

        private static FakeHttp SignedInService() => new FakeHttp()
            .Answer("GET", Base + "/config.json", 200, Config)
            .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, Token)
            .Answer("GET", Base + "/me", 200, Me)
            .Answer("GET", Base + "/privacy/consent", 200, ConsentOk);

        /// <summary>Runs a coroutine to its end the way Unity does, including coroutines started from inside coroutines.</summary>
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

        private static SignInResult SignIn(AuthService service, string email = "jane@example.org", string password = "correct horse")
        {
            SignInResult result = null;
            Run(service.SignIn(email, password, r => result = r));
            Assert.IsNotNull(result, "the sign-in never finished");
            return result;
        }

        [SetUp, TearDown] public void ClearCurrent() => AuthService.Clear();

        [Test]
        public void A_good_account_signs_in_and_the_password_goes_only_to_the_account_system()
        {
            var http = SignedInService();
            var service = new AuthService(http, Base);
            var result = SignIn(service);
            Assert.IsTrue(result.Ok, result.Message);
            Assert.AreSame(service, AuthService.Current);
            Assert.AreEqual("T-ab12cd34", service.Pseudonym);
            Assert.AreEqual("22222222-2222-2222-2222-222222222222", service.OrganisationId);   // the first ACTIVE membership, not the deactivated one before it
            Assert.AreEqual("Demo", service.OrganisationName);
            Assert.AreEqual("trainee", service.Role);
            CollectionAssert.AreEqual(new[] { Base + "/config.json", Sb + "/auth/v1/token?grant_type=password", Base + "/me", Base + "/privacy/consent" }, http.Sent.Select(r => r.Url).ToArray());
            var token = http.Sent[1];
            Assert.AreEqual("sb_publishable_test", token.Headers["apikey"]);
            Assert.IsFalse(token.Headers.ContainsKey("Authorization"));
            StringAssert.Contains("correct horse", token.Body);
            Assert.IsFalse(http.Sent.Where(r => r != token).Any(r => (r.Body ?? "").Contains("correct horse") || r.Url.Contains("correct")), "the password must not go anywhere else");
            Assert.AreEqual("Bearer AT1", http.Sent[2].Headers["Authorization"]);
            Assert.AreEqual("Bearer AT1", http.Sent[3].Headers["Authorization"]);
        }

        [Test]
        public void A_wrong_password_is_told_plainly_and_nothing_stays_signed_in()
        {
            var http = new FakeHttp().Answer("GET", Base + "/config.json", 200, Config).Answer("POST", Sb + "/auth/v1/token?grant_type=password", 400, "{\"error\":\"invalid_grant\"}");
            var service = new AuthService(http, Base);
            var result = SignIn(service);
            Assert.AreEqual(SignInOutcome.WrongCredentials, result.Outcome);
            Assert.AreEqual("Email or password is not right.", result.Message);
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
            Assert.AreEqual(2, http.Sent.Count, "no call is made with a token that does not exist");
        }

        [Test]
        public void No_network_is_reported_as_unreachable_not_as_a_wrong_password()
        {
            var result = SignIn(new AuthService(new FakeHttp(), Base));   // nothing answers
            Assert.AreEqual(SignInOutcome.Unreachable, result.Outcome);
            var afterConfig = new FakeHttp().Answer("GET", Base + "/config.json", 200, Config);   // the sign-in host does not answer
            Assert.AreEqual(SignInOutcome.Unreachable, SignIn(new AuthService(afterConfig, Base)).Outcome);
        }

        [Test]
        public void An_address_that_is_not_set_or_not_secure_is_refused_before_any_request()
        {
            foreach (var address in new[] { "", "   ", "http://example.com", "ftp://example.com", "service.test" })
            {
                var http = new FakeHttp();
                Assert.AreEqual(SignInOutcome.ServiceNotSet, SignIn(new AuthService(http, address)).Outcome, address);
                Assert.AreEqual(0, http.Sent.Count, address);
            }
            Assert.IsTrue(ServiceSettings.IsAcceptable("http://localhost:8000") && ServiceSettings.IsAcceptable("http://127.0.0.1:8000") && ServiceSettings.IsAcceptable("https://procsense.onrender.com"));
        }

        [Test]
        public void A_sign_in_address_that_is_not_secure_is_not_trusted_with_a_password()
        {
            var insecure = "{\"supabase_url\":\"http://project.example\",\"supabase_publishable_key\":\"k\"}";
            var http = new FakeHttp().Answer("GET", Base + "/config.json", 200, insecure);
            Assert.AreEqual(SignInOutcome.Failed, SignIn(new AuthService(http, Base)).Outcome);
            Assert.AreEqual(1, http.Sent.Count);
            var garbage = new FakeHttp().Answer("GET", Base + "/config.json", 200, "not json at all");
            Assert.AreEqual(SignInOutcome.Failed, SignIn(new AuthService(garbage, Base)).Outcome);
        }

        [Test]
        public void An_account_in_no_active_organisation_is_signed_out_again()
        {
            var none = "{\"user_id\":\"u\",\"pseudonym\":\"T-x\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"o\",\"organisation_name\":\"O\",\"slug\":\"o\",\"role\":\"trainee\",\"status\":\"deactivated\"}]}";
            var http = new FakeHttp().Answer("GET", Base + "/config.json", 200, Config).Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, Token).Answer("GET", Base + "/me", 200, none);
            var service = new AuthService(http, Base);
            Assert.AreEqual(SignInOutcome.NoOrganisation, SignIn(service).Outcome);
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void A_person_who_has_not_agreed_is_sent_to_the_web_page_and_signed_out_again()
        {
            var http = new FakeHttp().Answer("GET", Base + "/config.json", 200, Config).Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, Token)
                .Answer("GET", Base + "/me", 200, Me).Answer("GET", Base + "/privacy/consent", 200, ConsentNeeded);
            var service = new AuthService(http, Base);
            var result = SignIn(service);
            Assert.AreEqual(SignInOutcome.ConsentNeeded, result.Outcome);
            StringAssert.Contains(Base, result.Message);   // says where to go
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void A_401_renews_the_token_once_and_repeats_the_call_with_the_new_one()
        {
            var http = SignedInService()
                .Answer("GET", Base + "/organisations/o/progress", 401).Answer("GET", Base + "/organisations/o/progress", 200, "{\"ok\":true}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=refresh_token", 200, "{\"access_token\":\"AT2\",\"refresh_token\":\"RT2\",\"expires_in\":3600}");
            var service = new AuthService(http, Base);
            Assert.IsTrue(SignIn(service).Ok);
            http.Sent.Clear();
            HttpResult answer = default;
            Run(service.Authorized("GET", "/organisations/o/progress", null, r => answer = r));
            Assert.IsTrue(answer.Ok); StringAssert.Contains("true", answer.Body);
            CollectionAssert.AreEqual(new[] { "GET", "POST", "GET" }, http.Sent.Select(r => r.Method).ToArray());
            Assert.AreEqual("Bearer AT1", http.Sent[0].Headers["Authorization"]);
            StringAssert.Contains("RT1", http.Sent[1].Body);
            Assert.AreEqual("sb_publishable_test", http.Sent[1].Headers["apikey"]);
            Assert.AreEqual("Bearer AT2", http.Sent[2].Headers["Authorization"]);
        }

        [Test]
        public void A_refused_renewal_signs_the_person_out()
        {
            var http = SignedInService().Answer("GET", Base + "/organisations/o/progress", 401).Answer("POST", Sb + "/auth/v1/token?grant_type=refresh_token", 400, "{\"error\":\"invalid_grant\"}");
            var service = new AuthService(http, Base);
            Assert.IsTrue(SignIn(service).Ok);
            HttpResult answer = default;
            Run(service.Authorized("GET", "/organisations/o/progress", null, r => answer = r));
            Assert.AreEqual(401, answer.Status);
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
        }

        [Test]
        public void No_network_while_renewing_keeps_the_person_signed_in_for_the_next_try()
        {
            var http = SignedInService().Answer("GET", Base + "/organisations/o/progress", 401);   // the refresh address has no answer: no network
            var service = new AuthService(http, Base);
            Assert.IsTrue(SignIn(service).Ok);
            HttpResult answer = default;
            Run(service.Authorized("GET", "/organisations/o/progress", null, r => answer = r));
            Assert.IsTrue(service.IsSignedIn, "a lost connection is not a sign-out");
        }

        [Test]
        public void A_call_when_nobody_is_signed_in_is_a_401_without_touching_the_network()
        {
            var http = new FakeHttp();
            var service = new AuthService(http, Base);
            HttpResult answer = default;
            Run(service.Authorized("GET", "/me", null, r => answer = r));
            Assert.AreEqual(401, answer.Status);
            Assert.AreEqual(0, http.Sent.Count);
        }

        [Test]
        public void Signing_out_forgets_the_tokens()
        {
            var http = SignedInService();
            var service = new AuthService(http, Base);
            Assert.IsTrue(SignIn(service).Ok);
            service.SignOut();
            Assert.IsFalse(service.IsSignedIn); Assert.IsNull(AuthService.Current);
            http.Sent.Clear();
            HttpResult answer = default;
            Run(service.Authorized("GET", "/me", null, r => answer = r));
            Assert.AreEqual(401, answer.Status);
            Assert.AreEqual(0, http.Sent.Count);
        }
    }
}
