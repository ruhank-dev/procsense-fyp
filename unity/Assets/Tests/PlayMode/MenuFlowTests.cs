using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The whole game flow, headless (P3-02, P3-03, P3-07, P3-08, P3-VOX-13): sign-in stub, main menu, mode, surgery, a random valid scenario, briefing, hint, Submit Patient to OT with its confirmation,
    /// results, retry and new scenario, all through the same engine verbs; a spoken phrase and the button give the identical log; the glass panel is driven through its own buttons.
    /// </summary>
    public class MenuFlowTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private ulong _seed;
        private GameObject _panelObject;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "flow_tests");
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);   // no saved session from an earlier test is offered
            _host =new GameObject("flow host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _seed = 100;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
        }

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_host != null) Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            if (SimulationHost.SessionsDirectoryOverride != null && Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private void StartTraining(string procedure = "cholecystectomy")
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.IsTrue(_flow.ChooseSurgery(procedure, TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
        }

        [Test]
        public void The_whole_flow_runs_through_the_engine_verbs()
        {
            Assert.AreEqual(MenuPhase.Login, _flow.Phase);
            _flow.SignedIn();
            Assert.AreEqual(MenuPhase.MainMenu, _flow.Phase);
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.AreEqual(MenuPhase.Surgery, _flow.Phase);
            Assert.Greater(_flow.CaseCount(SessionMode.Practice, "cholecystectomy"), 0);

            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.AreEqual(MenuPhase.Briefing, _flow.Phase);
            Assert.IsNotEmpty(_flow.Briefing.Name);
            Assert.IsNotEmpty(_flow.Briefing.Reason);
            Assert.AreEqual(_flow.Plan.CaseId, _host.Session.Case.Id);
            Assert.IsNotNull(_host.Session.Checker);
            Assert.IsFalse(_host.Session.Check(new SimAction("no_such_verb")).Accepted);

            Assert.IsTrue(_flow.Begin());
            var hint = _flow.Hint();
            Assert.IsTrue(hint.Accepted, hint.RejectReason);
            Assert.AreEqual(1, _host.Session.Hints.Count);
            Assert.AreEqual(1, _host.Session.LastHint.Tier);

            var armed = _flow.Submit();
            Assert.AreEqual(MenuFlow.ConfirmNeeded, armed.RejectReason);
            Assert.AreEqual(MenuPhase.Playing, _flow.Phase);
            Assert.IsFalse(_host.Session.Submitted);
            var done = _flow.Submit();
            Assert.IsTrue(done.Accepted, done.RejectReason);
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            Assert.IsTrue(_flow.Summary.Submitted);
            Assert.AreEqual("FAIL", _flow.Summary.Result);                                       // sent on before the case could end
            Assert.IsNotEmpty(_flow.Summary.MissedGoals);
            Assert.AreEqual(ExamControl.SessionSubmitted, _host.Apply(new SimAction("ask_identity")).RejectReason);

            var first = _flow.Plan;
            Assert.IsTrue(_flow.Retry());
            Assert.AreEqual((first.CaseId, first.Seed), (_flow.Plan.CaseId, _flow.Plan.Seed));
            Assert.AreEqual(0, _host.Session.Log.Count);                                         // a fresh session of the same case and seed
            _flow.Begin(); _flow.Submit(); _flow.Submit();
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);

            Assert.IsTrue(_flow.NewScenario(), _flow.Message);
            Assert.AreNotEqual(first.Seed, _flow.Plan.Seed);
            Assert.AreNotEqual(first.CaseId, _flow.Plan.CaseId);                                 // another case when the pool has one
        }

        [Test]
        public void The_examination_starts_only_a_case_approved_for_assessment()
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Assessment));
            bool started = _flow.ChooseSurgery("cholecystectomy", TrainingLevel.Advanced);
            Assert.Greater(_flow.CaseCount(SessionMode.Assessment, "cholecystectomy"), 0);       // owner-attested approvals (ADR-0032 D1) fill the assessment pool
            Assert.IsTrue(started, _flow.Message);
            Assert.AreEqual(SessionMode.Assessment, _host.Session.Mode);
        }

        [Test]
        public void A_spoken_phrase_and_the_button_give_the_identical_log()
        {
            StartTraining();
            _flow.Hint(); _flow.Hint(); _flow.Submit(); _flow.Submit();
            string byButtons = _host.Session.Log.Hash();
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);

            Assert.IsTrue(_flow.Retry());
            _flow.Begin();
            Assert.IsTrue(FlowVoiceIntents.Handle(_flow, "Can I have a hint?"));
            Assert.IsTrue(FlowVoiceIntents.Handle(_flow, "give me a hint"));
            Assert.IsTrue(FlowVoiceIntents.Handle(_flow, "Submit the patient to theatre."));      // arms
            Assert.IsTrue(FlowVoiceIntents.Handle(_flow, "submit the patient to theatre"));       // confirms
            Assert.AreEqual(byButtons, _host.Session.Log.Hash());

            Assert.IsFalse(FlowVoiceIntents.Handle(_flow, "order a full blood count"));          // anything else goes to the normal interpreter
            Assert.AreEqual(FlowVoiceIntents.Intent.None, FlowVoiceIntents.Classify("hint me a drink"));
            var spoken = FlowVoiceIntents.ActionFor(FlowVoiceIntents.Intent.Hint);
            Assert.AreEqual((ExamControl.RequestHint, Channel.Vr), (spoken.Verb, spoken.Channel));
            Assert.IsTrue(FlowVoiceIntents.TryMap("submit patient to OT", out var submit));
            Assert.AreEqual(ExamControl.SubmitPatient, submit.Verb);
        }

        [Test]
        public void Retry_plays_the_same_events_and_ends_with_the_same_hash()
        {
            StartTraining("appendicectomy");
            var plan = _flow.Plan;
            _flow.Hint(); _flow.Submit(); _flow.Submit();
            string first = _host.Session.Log.Hash();
            _flow.Retry();
            Assert.AreEqual((plan.CaseId, plan.Seed), (_flow.Plan.CaseId, _flow.Plan.Seed));
            _flow.Begin(); _flow.Hint(); _flow.Submit(); _flow.Submit();
            Assert.AreEqual(first, _host.Session.Log.Hash());
        }

        [Test]
        public void An_interrupted_session_is_offered_and_resumes_exactly_where_it_stopped()
        {
            StartTraining();
            Assert.IsTrue(_flow.Hint().Accepted);
            string saved = _host.SaveSessionLog();                                               // what pausing or closing the app does
            Assert.IsNotNull(saved);
            string hash = _host.Session.Log.Hash();

            var again = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            again.SignedIn();
            Assert.IsNotNull(again.Interrupted);
            Assert.AreEqual(_flow.Plan.CaseId, again.Interrupted.CaseId);
            Assert.AreEqual(1, again.Interrupted.ActionsSaved);
            Assert.IsTrue(again.Resume());
            Assert.AreEqual(MenuPhase.Playing, again.Phase);
            Assert.AreEqual(hash, _host.Session.Log.Hash());
            Assert.AreEqual(1, _host.Session.Hints.Count);
            Assert.IsFalse(File.Exists(saved));                                                  // the next save writes the whole log again
            Assert.IsTrue(again.Hint().Accepted);                                                // and the session carries on
            Assert.IsNull(again.Interrupted);
        }

        [Test]
        public void A_finished_session_is_not_offered_and_an_interrupted_one_can_be_discarded()
        {
            StartTraining();
            _flow.Hint(); _flow.Submit(); _flow.Submit();
            _host.SaveSessionLog();                                                              // a submitted session
            var finished = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            finished.SignedIn();
            Assert.IsNull(finished.Interrupted);

            Assert.IsTrue(finished.Retry() == false);                                            // Retry belongs to the results screen
            _flow.Retry();
            _flow.Begin(); _flow.Hint();
            string saved = _host.SaveSessionLog();                                               // an interrupted one
            var third = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            third.SignedIn();
            Assert.IsNotNull(third.Interrupted);
            third.Discard();
            Assert.IsNull(third.Interrupted);
            Assert.IsFalse(File.Exists(saved));
        }

        [Test]
        public void The_glass_panel_drives_the_whole_flow_through_its_own_buttons()
        {
            _panelObject = new GameObject("flow panel");
            var panel = _panelObject.AddComponent<FlowPanel>();
            panel.Build(_flow, _panelObject.transform);
            Assert.IsTrue(panel.Click("Continue (signed in)"));
            CollectionAssert.Contains(panel.VisibleButtons(), "TRAINING MODE");
            CollectionAssert.Contains(panel.VisibleButtons(), "TEST / EXAMINATION MODE");

            Assert.IsTrue(panel.Click("Settings"));
            Assert.AreEqual("Settings", panel.TitleText);
            Assert.IsTrue(panel.Click("Back"));
            Assert.AreEqual("ProcSense", panel.TitleText);

            Assert.IsTrue(panel.Click("TRAINING MODE"));
            string surgery = panel.VisibleButtons().First(b => b.StartsWith("Gallbladder removal"));
            Assert.IsTrue(panel.Click(surgery));
            Assert.AreEqual("Briefing", panel.TitleText);
            StringAssert.Contains(_flow.Briefing.Name, panel.BodyText);
            Assert.IsTrue(panel.Click("Begin"));
            Assert.IsTrue(panel.Click("Hint"));
            StringAssert.StartsWith("Hint 1 of 3", panel.BodyText);
            Assert.IsTrue(panel.Click("Submit patient to OT"));
            StringAssert.Contains("Press again", panel.StatusText);
            Assert.IsTrue(panel.Click("Confirm: submit patient to OT"));
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            CollectionAssert.IsSupersetOf(panel.VisibleButtons(), new[] { "Retry", "New scenario", "Main menu" });
            StringAssert.Contains("Scenario: " + _flow.Briefing.Name, panel.BodyText);
            Assert.IsTrue(panel.Click("New scenario"));
            Assert.AreEqual("Briefing", panel.TitleText);
        }
    }
}
