using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-MNU-08, P3-MNU-09, P3-EXM-06 and P3-SES-08: pause stops the engine clock and refuses actions, only resume continues, pause and resume are in the log;
    /// each way out (restart, return, quit) waits for its confirmation, says what happens, and does it: practice ends as abandoned and restarts or returns to the menu,
    /// the examination ends as abandoned and shows its results.
    /// </summary>
    public class PauseMenuTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private PauseMenu _pause;
        private ulong _seed;
        private int _quit;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "pause_tests");
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            _host = new GameObject("pause host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _seed = 300;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            _pause = new GameObject("pause").AddComponent<PauseMenu>();
            _pause.Flow = _flow;
            _pause.Head = new GameObject("head").transform;
            _quit = 0;
            _pause.QuitApp = () => _quit++;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_pause.Head.gameObject);
            Object.DestroyImmediate(_pause.gameObject);
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private void Start(SessionMode mode)
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(mode));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
        }

        private void Press(string label) => Find(label).onClick.Invoke();

        private Button Find(string label)
        {
            foreach (var b in _pause.Canvas.GetComponentsInChildren<Button>(true)) if (b.name == "Button " + label) return b;
            Assert.Fail("no button " + label);
            return null;
        }

        [Test]
        public void The_settings_session_tab_leaves_the_same_way_as_the_pause_menu()   // P3-SET-07
        {
            Start(SessionMode.Practice);
            var s = _host.Session;
            var all = Object.FindObjectsByType<PauseMenu>(FindObjectsSortMode.None);
            Assert.AreSame(_pause, PauseMenu.Running(), "pause menus alive: " + all.Length + "; running one: " + (PauseMenu.Running() != null ? PauseMenu.Running().name : "none"));
            if (ProcSense.VR.UI.SettingsUi.SettingsPanel.Open != null) ProcSense.VR.UI.SettingsUi.SettingsPanel.Open.Close();
            var panel = ProcSense.VR.UI.SettingsUi.SettingsPanel.Toggle(null);
            panel.Show(ProcSense.VR.UI.SettingsUi.SettingsPanel.Tab.Session);
            foreach (var b in panel.Canvas.GetComponentsInChildren<Button>(true)) if (b.name == "Button Return to main menu") b.onClick.Invoke();
            panel.Dialog.Confirm.onClick.Invoke();
            Assert.IsTrue(s.Submitted && s.Abandoned, "ended as abandoned, like the pause menu");
            Assert.AreEqual(MenuPhase.MainMenu, _flow.Phase);
        }

        [Test]
        public void Nothing_to_pause_on_the_menu()
        {
            _flow.SignedIn();
            Assert.IsFalse(_pause.Pause());
            Assert.IsFalse(_pause.Showing);
        }

        [Test]
        public void Pause_stops_the_clock_and_actions_until_resume()
        {
            Start(SessionMode.Practice);
            _host.Tick(5.0);
            long steps = _host.Session.Clock.Steps;
            Assert.IsTrue(_pause.Pause());
            Assert.IsTrue(_pause.Showing);
            Assert.IsTrue(_host.Session.Paused);
            _host.Tick(60.0);
            Assert.AreEqual(steps, _host.Session.Clock.Steps, "the clock stands still");
            Assert.AreEqual(SessionControl.SessionPaused, _host.Apply(new SimAction("ask_identity", "patient", Channel.Vr)).RejectReason);
            Press("Resume");
            Assert.IsFalse(_host.Session.Paused);
            Assert.IsFalse(_pause.Showing);
            _host.Tick(5.0);
            Assert.Greater(_host.Session.Clock.Steps, steps);
            var verbs = new System.Collections.Generic.List<string>();
            foreach (var e in _host.Session.Log.Entries) verbs.Add(e.Verb);
            CollectionAssert.IsSubsetOf(new[] { SessionControl.Pause, SessionControl.Resume }, verbs, "both are in the log for the replay");
        }

        [Test]
        public void Practice_restart_waits_for_confirmation_then_starts_the_same_case_again()
        {
            Start(SessionMode.Practice);
            string caseId = _host.Session.Case.Id;
            ulong seed = _host.Session.Seed;
            var first = _host.Session;
            _pause.Pause();
            Press("Restart scenario");
            Assert.AreSame(first, _host.Session, "nothing until confirmed");
            Assert.IsNotNull(_pause.Dialog);
            StringAssert.Contains("same case starts again", _pause.Dialog.transform.Find("Body").GetComponent<TMPro.TMP_Text>().text, "the dialog says what happens");
            _pause.Dialog.Confirm.onClick.Invoke();
            Assert.IsTrue(first.Submitted && first.Abandoned, "the attempt ended as abandoned");
            Assert.AreNotSame(first, _host.Session);
            Assert.AreEqual((caseId, seed), (_host.Session.Case.Id, _host.Session.Seed));
            Assert.AreEqual(MenuPhase.Briefing, _flow.Phase);
        }

        [Test]
        public void Practice_return_ends_the_attempt_and_goes_to_the_menu()
        {
            Start(SessionMode.Practice);
            var s = _host.Session;
            _pause.Pause();
            Press("Return to main menu");
            _pause.Dialog.Cancel.onClick.Invoke();
            Assert.IsFalse(s.Submitted, "cancel changes nothing");
            Press("Return to main menu");
            _pause.Dialog.Confirm.onClick.Invoke();
            Assert.IsTrue(s.Submitted && s.Abandoned);
            Assert.AreEqual(MenuPhase.MainMenu, _flow.Phase);
        }

        [Test]
        public void Examination_has_no_restart_and_leaving_ends_it_as_not_completed()
        {
            Start(SessionMode.Assessment);
            var s = _host.Session;
            _pause.Pause();
            foreach (var b in _pause.Canvas.GetComponentsInChildren<Button>(true)) Assert.AreNotEqual("Button Restart scenario", b.name);
            StringAssert.Contains("not completed", _pause.Consequence("return"));
            Press("Quit ProcSense");
            _pause.Dialog.Confirm.onClick.Invoke();
            Assert.IsTrue(s.Submitted && s.Abandoned);
            Assert.AreEqual(MenuPhase.Results, _flow.Phase, "its results are shown");
            Assert.AreEqual("Not completed", _flow.Summary.Headline);   // saved and queued for upload by the same submit path as a finished case (EndSessionOnSubmit in the app)
            Assert.AreEqual(1, _quit);
        }
    }
}
