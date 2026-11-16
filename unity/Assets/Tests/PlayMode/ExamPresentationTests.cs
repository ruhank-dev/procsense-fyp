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
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using ProcSense.VR.Sync;
using ProcSense.VR.UI.Checklist;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-EXM-09: the examination leak test over the glass flow panel (briefing, simulation bar, results: no goal list, no hint text that was not asked for, no checklist) and duplicate-submit protection
    /// at the MenuFlow.Submit arm/confirm level (a second Submit after the result changes nothing). P3-RES-07: the results panel labels the service's AI wording and keeps the engine's numbers.
    /// </summary>
    public class ExamPresentationTests
    {
        private static readonly string[] Markers = { "goal", "checklist", "next step", "missed:" };

        private SimulationHost _host;
        private MenuFlow _flow;
        private ulong _seed;
        private GameObject _panelObject;
        private FlowPanel _panel;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestHostHygiene.ResetToEmptyScene();                    // a leftover host makes ours a destroyed duplicate, and a leftover room's WHO board would be counted below
            ModeChoice.Reset();
            AuthService.Clear();
            MenuFlow.ConsentFactory = () => null;
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "exam_presentation_tests");
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            _host = new GameObject("exam host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _seed = 300;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            _panelObject = new GameObject("flow panel");
            _panel = _panelObject.AddComponent<FlowPanel>();
            _panel.Build(_flow, _panelObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_host != null) Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            AuthService.Clear();
            MenuFlow.ConsentFactory = MenuFlow.DefaultConsent;
            DebriefRewriteClient.Auth = () => AuthService.Current;
            if (SimulationHost.SessionsDirectoryOverride != null && Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private string Shown() => _panel.TitleText + "\n" + _panel.BodyText + "\n" + _panel.StatusText;

        private void StartExamination()
        {
            Assert.IsTrue(_panel.Click("Continue (signed in)"));
            Assert.IsTrue(_panel.Click("TEST / EXAMINATION MODE"));
            Assert.IsTrue(_panel.Click(_panel.VisibleButtons().First(b => b.StartsWith("Gallbladder removal"))), _flow.Message);
            Assert.AreEqual(SessionMode.Assessment, _host.Session.Mode);
        }

        private static void NoLeak(string phase, string text, IEnumerable<string> goalIds, IEnumerable<string> hintTexts)
        {
            foreach (var m in Markers) Assert.IsFalse(text.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0, phase + " shows '" + m + "': " + text);
            foreach (var g in goalIds.Where(g => !string.IsNullOrEmpty(g))) StringAssert.DoesNotContain(g, text, phase + " names the goal " + g);
            foreach (var h in hintTexts.Where(h => !string.IsNullOrEmpty(h))) StringAssert.DoesNotContain(h, text, phase + " repeats a hint");
        }

        [Test]
        public void The_examination_panel_shows_no_goal_list_hint_or_checklist()
        {
            StartExamination();
            Assert.AreEqual("Briefing", _panel.TitleText);
            string briefing = Shown();
            Assert.IsTrue(_panel.Click("Begin"));
            string playing = Shown();
            StringAssert.DoesNotStartWith("Hint ", _panel.BodyText, "no hint text unless the trainee asks (and pays) for one");
            Assert.IsTrue(_panel.Click("Submit patient to OT"));
            string armed = Shown();
            Assert.IsTrue(_panel.Click("Confirm: submit patient to OT"));
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            string results = Shown();

            var goals = _flow.Summary.MissedGoals.Concat(_flow.Summary.CompletedGoals).ToList();
            Assert.IsNotEmpty(goals, "an early submission misses goals, so the check below is not empty");
            foreach (var (phase, text) in new[] { ("briefing", briefing), ("simulation", playing), ("confirm", armed), ("results", results) })
                NoLeak(phase, text, goals, Array.Empty<string>());
            Assert.IsEmpty(Object.FindObjectsByType<ChecklistBoard>(FindObjectsInactive.Exclude).Where(b => b.isActiveAndEnabled), "no checklist board in the examination flow");
            Assert.IsEmpty(Object.FindObjectsByType<WardChecklistPanel>(FindObjectsInactive.Exclude).Where(b => b.isActiveAndEnabled), "no ward checklist in the examination flow");
        }

        [Test]
        public void A_hint_taken_in_the_examination_is_charged_and_not_repeated_on_the_results()
        {
            StartExamination();
            Assert.IsTrue(_panel.Click("Begin"));
            Assert.IsTrue(_panel.Click("Hint"));
            var hint = _host.Session.LastHint;
            Assert.IsNotNull(hint, _flow.Message);
            StringAssert.StartsWith("Hint 1 of", _panel.BodyText);                               // asked for: shown, and it costs points (D5)
            Assert.IsTrue(_panel.Click("Submit patient to OT"));
            Assert.IsTrue(_panel.Click("Confirm: submit patient to OT"));
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            StringAssert.Contains("Hints used: 1 (charged)", _panel.BodyText);
            NoLeak("results", Shown(), _flow.Summary.MissedGoals.Concat(_flow.Summary.CompletedGoals).Append(hint.GoalId), new[] { hint.Text });
        }

        [Test]
        public void A_second_submit_after_the_result_creates_no_second_summary_and_changes_nothing()
        {
            StartExamination();
            Assert.IsTrue(_flow.Begin());
            Assert.AreEqual(MenuFlow.ConfirmNeeded, _flow.Submit().RejectReason);               // arm
            Assert.IsTrue(_flow.SubmitArmed);
            Assert.IsTrue(_flow.Submit().Accepted);                                              // confirm
            var summary = _flow.Summary;
            string hash = _host.Session.Log.Hash();
            int actions = _host.Session.Log.Count;
            string body = _panel.BodyText;

            foreach (var again in new[] { _flow.Submit(), _flow.Submit(), _flow.Submit(abandoned: true) })
                Assert.AreEqual(MenuFlow.NotAllowedNow, again.RejectReason);
            Assert.IsFalse(_panel.Click("Confirm: submit patient to OT"), "no submit button on the results");
            Assert.IsFalse(_panel.Click("Submit patient to OT"));

            Assert.AreSame(summary, _flow.Summary, "no second summary");
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            Assert.IsFalse(_flow.SubmitArmed);
            Assert.AreEqual((hash, actions), (_host.Session.Log.Hash(), _host.Session.Log.Count), "MenuFlow sends nothing to the engine after the result");
            Assert.IsTrue(_host.Session.Submitted);
            Assert.AreEqual(body, _panel.BodyText);

            // Below MenuFlow the engine refuses a second submission too (it logs the refused attempt; the outcome and the summary stay the first ones).
            Assert.AreEqual(ExamControl.SessionSubmitted, _host.Apply(new SimAction(ExamControl.SubmitPatient, "", Channel.Vr)).RejectReason);
            Assert.AreSame(summary, _flow.Summary);
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
        }

        [Test]
        public void Cancelling_an_armed_submit_needs_a_new_confirmation()
        {
            StartExamination();
            Assert.IsTrue(_flow.Begin());
            _flow.Submit();
            _flow.CancelSubmit();
            Assert.IsFalse(_flow.SubmitArmed);
            Assert.AreEqual(MenuFlow.ConfirmNeeded, _flow.Submit().RejectReason, "after Cancel the next press arms again, it does not submit");
            Assert.IsFalse(_host.Session.Submitted);
            Assert.AreEqual(MenuPhase.Playing, _flow.Phase);
        }

        // ------------------------------------------------------------------ P3-RES-07 on the panel

        private sealed class ScriptedHttp : IHttp
        {
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public int RewriteCalls;
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                if (url.Contains("/debrief/")) RewriteCalls++;
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

        private static AuthService SignedIn(ScriptedHttp http, string rewriteAnswer)
        {
            const string Base = "https://service.test", Sb = "https://project.supabase.co", Org = "22222222-2222-2222-2222-222222222222";
            http.Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"u\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            if (rewriteAnswer != null) http.Answer("POST", Base + DebriefRewriteClient.PathFor(Org), 200, rewriteAnswer);
            var service = new AuthService(http, Base);
            SignInResult result = null;
            Run(service.SignIn("jane@example.org", "pw", r => result = r));
            Assert.IsTrue(result.Ok, result.Message);
            return service;
        }

        private IEnumerator PlayToResults(SessionMode mode)
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(mode));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
            _flow.Submit();
            _flow.Submit();
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            for (int i = 0; i < 5; i++) yield return null;                                     // the request runs on the panel
        }

        private static IEnumerable<string> EngineLines(string body) => body.Split('\n').Where(l => !l.StartsWith("- ") && !l.StartsWith("[Reworded]"));

        [UnityTest]
        public IEnumerator Reworded_results_are_labelled_and_keep_the_engine_numbers()
        {
            const string ai = "{\"template\":[],\"ai\":[{\"title\":\"What to improve\",\"text\":\"Reworded line from the same facts.\",\"fact_ids\":[\"f1\"]}],\"used\":\"ai\",\"reasons\":[]}";
            var http = new ScriptedHttp();
            var auth = SignedIn(http, ai);
            DebriefRewriteClient.Auth = () => null;                                              // first the offline result
            yield return PlayToResults(SessionMode.Practice);
            string offline = _panel.BodyText;
            Assert.IsFalse(_panel.ShowsRewording);
            StringAssert.DoesNotContain(DebriefRewriteClient.Label, offline);
            foreach (var r in _flow.Summary.Recommendations) StringAssert.Contains("- " + r, offline, "offline shows the engine's deterministic lines");

            DebriefRewriteClient.Auth = () => auth;
            Assert.IsTrue(_flow.Retry());                                                        // same case and seed: the same numbers
            _flow.Begin(); _flow.Submit(); _flow.Submit();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1, http.RewriteCalls);
            Assert.IsTrue(_panel.ShowsRewording, _panel.BodyText);
            string reworded = _panel.BodyText;
            StringAssert.Contains(DebriefRewriteClient.LabelLine, reworded);
            StringAssert.Contains("- What to improve: Reworded line from the same facts.", reworded);
            CollectionAssert.AreEqual(EngineLines(offline).ToList(), EngineLines(reworded).ToList(), "score, grade, points, result and times are the engine's, unchanged");
        }

        [UnityTest]
        public IEnumerator A_template_answer_from_the_service_shows_the_deterministic_text_without_the_label()
        {
            const string template = "{\"template\":[{\"title\":\"What to improve\",\"text\":\"x (penalty).\",\"fact_ids\":[\"f1\"]}],\"ai\":null,\"used\":\"template\",\"reasons\":[\"FEATURE_OFF\"]}";
            var http = new ScriptedHttp();
            var auth = SignedIn(http, template);
            DebriefRewriteClient.Auth = () => auth;
            yield return PlayToResults(SessionMode.Practice);
            Assert.AreEqual(1, http.RewriteCalls);
            Assert.IsFalse(_panel.ShowsRewording);
            StringAssert.DoesNotContain(DebriefRewriteClient.Label, _panel.BodyText);
            foreach (var r in _flow.Summary.Recommendations) StringAssert.Contains("- " + r, _panel.BodyText);
        }
    }
}
