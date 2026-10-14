#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Login;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The login screen with a real account: it asks the service (a scripted one here), shows what happened and only then opens the workspace (p1-backend-s10).</summary>
    public class LoginServiceTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Config = "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"sb_publishable_test\"}";
        private const string Token = "{\"access_token\":\"AT1\",\"refresh_token\":\"RT1\",\"expires_in\":3600}";
        private const string Me = "{\"user_id\":\"u1\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"Jane\",\"memberships\":[{\"organisation_id\":\"o1\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}";

        private sealed class ScriptedHttp : IHttp
        {
            public readonly Dictionary<string, HttpResult> Answers = new Dictionary<string, HttpResult>();
            public int Sent;
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { Answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Sent++;
                done(Answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
        }

        private static ScriptedHttp Service(bool consent = true) => new ScriptedHttp()
            .Answer("GET", Base + "/config.json", 200, Config)
            .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, Token)
            .Answer("GET", Base + "/me", 200, Me)
            .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":" + (consent ? "false" : "true") + "}");

        [TearDown]
        public void Restore()
        {
            DemoLoginSettings.LoginRequiredOverride = null;
            DemoLoginSettings.OfflineDemoAllowed = true;
            AuthService.Factory = AuthService.CreateDefault;
            AuthService.Clear();
        }

        private static IEnumerator OpenLogin(ScriptedHttp http)
        {
            DemoLoginSettings.LoginRequiredOverride = true;
            AuthService.Factory = () => new AuthService(http, Base);
            yield return SceneManager.LoadSceneAsync("Boot");
            for (int i = 0; i < 20; i++) yield return null;
        }

        private static IEnumerator Settle(DemoLoginView view)
        {
            for (int i = 0; i < 30 && !view.Completed; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator A_real_account_signs_in_through_the_service_and_opens_the_workspace()
        {
            yield return OpenLogin(Service());
            var view = UnityEngine.Object.FindFirstObjectByType<DemoLoginView>();
            Assert.IsNotNull(view);
            view.Email.text = "jane@example.org";
            view.Password.text = "a real password";
            view.Submit();
            yield return Settle(view);
            Assert.IsTrue(view.Completed, view.Status);
            Assert.AreEqual("", view.Password.text, "the password is not kept after sign-in");
            Assert.IsNotNull(AuthService.Current);
            Assert.AreEqual("T-ab12cd34", AuthService.Current.Pseudonym);
            Assert.AreEqual("o1", AuthService.Current.OrganisationId);
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance, "the workspace opens after a real sign-in too");
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        [UnityTest]
        public IEnumerator A_wrong_password_stays_on_the_form_with_a_plain_message_and_the_button_works_again()
        {
            var http = Service().Answer("POST", Sb + "/auth/v1/token?grant_type=password", 400, "{\"error\":\"invalid_grant\"}");
            yield return OpenLogin(http);
            var view = UnityEngine.Object.FindFirstObjectByType<DemoLoginView>();
            view.Email.text = "jane@example.org";
            view.Password.text = "wrong";
            view.Submit();
            yield return Settle(view);
            Assert.IsFalse(view.Completed);
            Assert.AreEqual("Email or password is not right.", view.Status);
            Assert.IsNull(AuthService.Current);
            Assert.IsNull(SimulationHost.Instance, "nothing starts behind a failed sign-in");
            Assert.IsTrue(view.transform.Find("Sign-in card/Sign in").GetComponent<UnityEngine.UI.Button>().interactable);
        }

        [UnityTest]
        public IEnumerator Without_a_connection_the_screen_says_so_and_the_demo_account_still_opens_the_workspace_offline()
        {
            yield return OpenLogin(new ScriptedHttp());   // nothing answers
            var view = UnityEngine.Object.FindFirstObjectByType<DemoLoginView>();
            view.Email.text = "jane@example.org";
            view.Password.text = "a real password";
            view.Submit();
            yield return Settle(view);
            Assert.IsFalse(view.Completed);
            StringAssert.Contains("Cannot reach", view.Status);
            StringAssert.Contains("offline", view.Status);
            view.Email.text = DemoLoginSettings.DemoEmail;
            view.Password.text = DemoLoginSettings.DemoPassword;
            view.Submit();
            Assert.IsTrue(view.Completed);
            Assert.IsNull(AuthService.Current, "the offline demo has no service identity");
            for (int i = 0; i < 60; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        [UnityTest]
        public IEnumerator A_person_who_has_not_agreed_is_told_where_to_do_it_and_the_workspace_stays_closed()
        {
            yield return OpenLogin(Service(consent: false));
            var view = UnityEngine.Object.FindFirstObjectByType<DemoLoginView>();
            view.Email.text = "jane@example.org";
            view.Password.text = "a real password";
            view.Submit();
            yield return Settle(view);
            Assert.IsFalse(view.Completed);
            StringAssert.Contains(Base, view.Status);
            Assert.IsNull(AuthService.Current);
            Assert.IsNull(SimulationHost.Instance);
        }

        [UnityTest]
        public IEnumerator With_the_demo_account_switched_off_it_is_refused_and_nothing_is_sent_to_the_service()
        {
            var http = Service();
            yield return OpenLogin(http);
            DemoLoginSettings.OfflineDemoAllowed = false;   // as a release build sets it
            var view = UnityEngine.Object.FindFirstObjectByType<DemoLoginView>();
            view.Email.text = DemoLoginSettings.DemoEmail;
            view.Password.text = DemoLoginSettings.DemoPassword;
            view.Submit();
            Assert.IsFalse(view.Completed);
            StringAssert.Contains("ProcSense account", view.Status);
            Assert.AreEqual(0, http.Sent);
        }
    }
}
#endif
