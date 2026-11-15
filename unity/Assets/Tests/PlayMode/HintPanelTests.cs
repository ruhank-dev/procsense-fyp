using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>P3-EXM-03: the lightbulb panel asks first in the examination (with the cost and hints left), gives the hint at once in training, shows the counter and the hint card, stops at the limit, and shows only while a case is played; in voice mode too.</summary>
    public class HintPanelTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private HintPanel _panel;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "hint_tests");
            _host = new GameObject("hint host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 21UL) { EndSessionOnSubmit = false };
            _panel = new GameObject("hint").AddComponent<HintPanel>();
            _panel.Build(_flow, HintPanel.CostFrom(UnityContentReader.ForThisPlatform()));
            _flow.SignedIn();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_panel.gameObject);
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private void Play(SessionMode mode)
        {
            Assert.IsTrue(_flow.ChooseMode(mode));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsFalse(_panel.Canvas.gameObject.activeSelf, "hidden at the briefing");
            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_panel.Canvas.gameObject.activeSelf, "shown while playing");
        }

        [Test]
        public void The_examination_asks_with_the_cost_before_a_hint()
        {
            Assert.AreEqual(5, _panel.Cost, "grades.json hint_penalty");
            Play(SessionMode.Assessment);
            _panel.Lightbulb.onClick.Invoke();
            Assert.AreEqual(0, _host.Session.Hints.Count, "nothing until confirmed");
            StringAssert.Contains("costs 5 points (3 of 3 left)", _panel.Dialog.transform.Find("Body").GetComponent<TMPro.TMP_Text>().text);
            _panel.Dialog.Cancel.onClick.Invoke();
            Assert.AreEqual(0, _host.Session.Hints.Count);
            _panel.Lightbulb.onClick.Invoke();
            _panel.Dialog.Confirm.onClick.Invoke();
            Assert.AreEqual(1, _host.Session.Hints.Count);
            Assert.AreEqual(_host.Session.LastHint.Text, _panel.Card.text, "the hint card");
            StringAssert.StartsWith("Hints used: 1 of 3", _panel.Counter.text);
        }

        [Test]
        public void Training_hints_are_free_and_stop_at_the_limit_in_voice_mode_too()
        {
            ProcSense.VR.Settings.InteractionMode.Set(ProcSense.VR.Settings.InteractionModeSetting.Voice);
            try
            {
                Play(SessionMode.Practice);
                for (int i = 0; i < _host.Session.HintLimit; i++)
                {
                    _panel.Lightbulb.onClick.Invoke();
                    Assert.IsNull(_panel.Dialog, "no confirmation in training");
                }
                Assert.AreEqual(_host.Session.HintLimit, _host.Session.Hints.Count);
                StringAssert.Contains("Free in training", _panel.Counter.text);
                Assert.IsFalse(_panel.Lightbulb.interactable, "at the limit");
                _panel.Press();
                StringAssert.Contains("used all", _panel.Card.text);
            }
            finally { ProcSense.VR.Settings.InteractionMode.Set(ProcSense.VR.Settings.InteractionModeSetting.Menu); }
        }
    }
}
