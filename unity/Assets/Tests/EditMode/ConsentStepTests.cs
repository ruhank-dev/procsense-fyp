using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The consent step between sign-in and the main menu (owner decision D2): the paired LAN demonstration user answers the service's own consent questions through the normal route
    /// (GET/POST /privacy/consent with its device token). Nothing is agreed automatically, a decline is recorded and keeps the player on the step, and the menu opens only when the service says
    /// the answers are complete. A scripted laptop stands in for the network.
    /// </summary>
    public class ConsentStepTests
    {
        private const string Base = "http://192.168.1.20:8000";
        private const string Token = "lan-device-token-1";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class MemoryStorage : DeviceTokenStore.IStorage
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string Get(string key) => _values.TryGetValue(key, out var v) ? v : "";
            public void Set(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }

        /// <summary>A laptop that keeps the answers it was given and replies like server/routers/privacy.py (only the fields the headset reads).</summary>
        private sealed class Laptop : IHttp
        {
            public readonly Dictionary<string, bool> Answers = new Dictionary<string, bool>();
            public readonly List<string> Calls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            public readonly List<string> Bearers = new List<string>();
            public bool Offline;
            public long PostStatus = 200;
            public string PostBody = "";

            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                string path = url.Substring(Base.Length);
                Calls.Add(method + " " + path);
                if (headers != null && headers.TryGetValue("Authorization", out var auth)) Bearers.Add(auth);
                if (Offline) { done(new HttpResult(0, "", true)); yield break; }
                if (path == AuthService.DemoIdentityPath)
                {
                    done(new HttpResult(200, "{\"user_id\":\"33333333-3333-3333-3333-333333333333\",\"pseudonym\":\"demo-dev1\",\"display_name\":\"Demonstration user\",\"organisation_id\":\"44444444-4444-4444-4444-444444444444\",\"organisation_name\":\"LAN demonstration (synthetic)\",\"role\":\"trainee\"}"));
                    yield break;
                }
                if (path != ConsentStep.Path) { done(new HttpResult(404, "")); yield break; }
                if (method == "POST")
                {
                    Bodies.Add(jsonBody);
                    if (PostStatus != 200) { done(new HttpResult(PostStatus, PostBody)); yield break; }
                    var answer = JsonUtility.FromJson<AnswerDto>(jsonBody);
                    Answers[answer.kind] = answer.granted;
                }
                done(new HttpResult(200, State()));
            }

            [Serializable] private sealed class AnswerDto { public string kind; public bool granted; }

            private string State()
            {
                string Kind(string kind, string title, bool required)
                {
                    bool answered = Answers.TryGetValue(kind, out var granted);
                    bool current = answered && (granted || !required);
                    return "{\"kind\":\"" + kind + "\",\"title\":\"" + title + "\",\"text\":\"The " + kind + " text.\",\"required\":" + (required ? "true" : "false")
                         + ",\"granted\":" + (answered ? (granted ? "true" : "false") : "null") + ",\"answered_version\":null,\"documents\":[],\"current\":" + (current ? "true" : "false") + "}";
                }
                bool needs = !(Answers.TryGetValue("terms", out var t) && t && Answers.TryGetValue("results", out var r) && r);
                return "{\"version\":\"2026-10-25.1\",\"needs_consent\":" + (needs ? "true" : "false") + ",\"kinds\":[" + Kind("terms", "Terms and Conditions", true) + ","
                     + Kind("results", "Storing my training results", true) + "," + Kind("research", "Research (optional)", false) + "," + Kind("cloud", "Cloud AI (optional)", false) + "]}";
            }
        }

        private DeviceTokenStore.IStorage _storage;
        private bool _privateBefore;
        private SimulationHost _host;
        private string _sessions;
        private Laptop _laptop;

        [SetUp]
        public void SetUp()
        {
            _storage = DeviceTokenStore.Storage;
            DeviceTokenStore.Storage = new MemoryStorage();
            DeviceTokenStore.Set(Token);
            _privateBefore = ServiceSettings.AllowPrivateHttp;
            ServiceSettings.AllowPrivateHttp = true;
            AuthService.Clear();
            ModeChoice.Reset();
            _sessions = Path.Combine(Application.temporaryCachePath, "consent_step_tests");
            if (Directory.Exists(_sessions)) Directory.Delete(_sessions, true);
            SimulationHost.SessionsDirectoryOverride = _sessions;
            _host = new GameObject("consent host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _laptop = new Laptop();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host.gameObject);
            DeviceTokenStore.Storage = _storage;
            ServiceSettings.AllowPrivateHttp = _privateBefore;
            AuthService.Clear();
            MenuFlow.ConsentFactory = MenuFlow.DefaultConsent;
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        /// <summary>Runs a coroutine to the end, nested ones included (the scripted laptop answers at once).</summary>
        private static void Run(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (top.MoveNext()) { if (top.Current is IEnumerator inner) stack.Push(inner); }
                else stack.Pop();
            }
        }

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private AuthService SignedInDemo()
        {
            var auth = new AuthService(_laptop, Base, 5);
            SignInResult result = null;
            Run(auth.SignInPairedDevice(r => result = r));
            Assert.IsTrue(result.Ok, result.Message);
            Assert.IsTrue(auth.IsDemoDevice);
            return auth;
        }

        /// <summary>Signs in as the demonstration user and lets MenuFlow run the consent check the panel would start.</summary>
        private MenuFlow SignInAndCheck()
        {
            SignedInDemo();
            var flow = new MenuFlow(_host, Real(), () => 7UL);
            flow.SignedIn();
            Assert.AreEqual(MenuPhase.Consent, flow.Phase, "the consent step comes between sign-in and the main menu");
            Run(flow.ConsentStep.Load(flow.ConsentUpdated));
            return flow;
        }

        private int Posts => _laptop.Calls.Count(c => c == "POST " + ConsentStep.Path);

        [Test]
        public void Without_a_service_identity_the_menu_opens_directly()
        {
            var flow = new MenuFlow(_host, Real(), () => 7UL);
            flow.SignedIn();
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase, "the offline demonstration has nothing to upload, so there is nothing to agree to");
            Assert.IsNull(flow.ConsentStep);
        }

        [Test]
        public void The_demo_user_is_asked_the_servers_own_questions_and_nothing_is_agreed_for_it()
        {
            var flow = SignInAndCheck();
            Assert.AreEqual(MenuPhase.Consent, flow.Phase);
            Assert.AreEqual(ConsentStatus.Needed, flow.ConsentStep.Status);
            Assert.AreEqual("2026-10-25.1", flow.ConsentStep.Version);
            CollectionAssert.AreEqual(new[] { "terms", "results" }, flow.ConsentStep.Questions.Select(q => q.Kind).ToArray(), "only the required questions; their text comes from the server");
            Assert.AreEqual("Storing my training results", flow.ConsentStep.Questions[1].Title);
            Assert.AreEqual(0, Posts, "consent is never granted automatically");
            Assert.IsTrue(_laptop.Bearers.All(b => b == "Bearer " + Token), "the check carries the paired device token");
            Assert.IsFalse(flow.ChooseMode(SessionMode.Practice), "no training before the consent step");
            flow.BackToMenu();
            Assert.AreEqual(MenuPhase.Consent, flow.Phase, "the menu cannot be reached around the consent step");
        }

        [Test]
        public void Agreeing_records_yes_to_each_required_question_and_opens_the_menu()
        {
            var flow = SignInAndCheck();
            Run(flow.ConsentStep.Answer(true, flow.ConsentUpdated));
            Assert.AreEqual(ConsentStatus.Agreed, flow.ConsentStep.Status);
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase);
            CollectionAssert.AreEquivalent(new[] { "{\"kind\":\"terms\",\"granted\":true}", "{\"kind\":\"results\",\"granted\":true}" }, _laptop.Bodies);
            Assert.IsTrue(_laptop.Answers["terms"] && _laptop.Answers["results"]);
            Assert.IsFalse(_laptop.Answers.ContainsKey("research") || _laptop.Answers.ContainsKey("cloud"), "the optional questions are left for the person");
            Assert.IsTrue(flow.ChooseMode(SessionMode.Practice));
        }

        [Test]
        public void Declining_is_recorded_and_keeps_the_player_on_the_step_until_they_agree()
        {
            var flow = SignInAndCheck();
            Run(flow.ConsentStep.Answer(false, flow.ConsentUpdated));
            Assert.AreEqual(ConsentStatus.Declined, flow.ConsentStep.Status);
            Assert.AreEqual(MenuPhase.Consent, flow.Phase);
            Assert.IsFalse(_laptop.Answers["terms"] || _laptop.Answers["results"], "the no is sent to the server");
            StringAssert.Contains("cannot be stored", flow.Message);
            Assert.IsFalse(flow.ChooseMode(SessionMode.Practice), "training cannot continue after a decline");

            Run(flow.ConsentStep.Load(flow.ConsentUpdated));                 // Review again
            Assert.AreEqual(ConsentStatus.Needed, flow.ConsentStep.Status);
            Run(flow.ConsentStep.Answer(true, flow.ConsentUpdated));
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase, "changing the answer to yes opens the menu");
        }

        [Test]
        public void An_identity_that_already_agreed_goes_straight_to_the_menu()
        {
            _laptop.Answers["terms"] = true;
            _laptop.Answers["results"] = true;
            var flow = SignInAndCheck();
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase);
            Assert.AreEqual(0, Posts);
        }

        [Test]
        public void Only_the_questions_not_yet_current_are_answered()
        {
            _laptop.Answers["terms"] = true;
            var flow = SignInAndCheck();
            Run(flow.ConsentStep.Answer(true, flow.ConsentUpdated));
            CollectionAssert.AreEqual(new[] { "{\"kind\":\"results\",\"granted\":true}" }, _laptop.Bodies);
            Assert.AreEqual(MenuPhase.MainMenu, flow.Phase);
        }

        [Test]
        public void An_unreachable_laptop_keeps_the_step_and_says_why()
        {
            var flow = SignInAndCheck();
            _laptop.Offline = true;
            Run(flow.ConsentStep.Answer(true, flow.ConsentUpdated));
            Assert.AreEqual(ConsentStatus.Unreachable, flow.ConsentStep.Status);
            Assert.AreEqual(MenuPhase.Consent, flow.Phase);
            StringAssert.Contains("Cannot reach the service", flow.Message);
        }

        [Test]
        public void A_service_that_wants_the_web_says_so_and_does_not_open_the_menu()
        {
            var flow = SignInAndCheck();
            _laptop.PostStatus = 403;
            _laptop.PostBody = "{\"detail\":\"web_sign_in_required\"}";
            Run(flow.ConsentStep.Answer(true, flow.ConsentUpdated));
            Assert.AreEqual(ConsentStatus.WebOnly, flow.ConsentStep.Status);
            Assert.AreEqual(MenuPhase.Consent, flow.Phase);
            StringAssert.Contains(Base, flow.Message);
        }

        [Test]
        public void The_glass_panel_shows_the_question_with_agree_and_decline()
        {
            SignedInDemo();
            var step = new ConsentStep(AuthService.Current);
            Run(step.Load());
            MenuFlow.ConsentFactory = () => step;
            var flow = new MenuFlow(_host, Real(), () => 7UL);
            var go = new GameObject("consent panel");
            try
            {
                var panel = go.AddComponent<FlowPanel>();
                panel.Build(flow, go.transform);
                flow.SignedIn();
                Assert.AreEqual("Your consent", panel.TitleText);
                StringAssert.Contains("Storing my training results", panel.BodyText);
                StringAssert.Contains("The results text.", panel.BodyText);
                CollectionAssert.AreEqual(new[] { "Agree", "Decline" }, panel.VisibleButtons());
                Assert.AreEqual(0, Posts);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
