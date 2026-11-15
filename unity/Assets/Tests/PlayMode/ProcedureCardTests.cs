using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-MNU-04: the surgery screen shows two procedure cards with the plain name, the operation, "pre-operative preparation" and the number of cases the mode can draw; a card with no case is
    /// disabled and says why ("No cases are approved for examination yet") instead of failing after a press; leaving the screen puts the pooled buttons back in their places.
    /// </summary>
    public class ProcedureCardTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private FlowPanel _panel;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "card_tests");
            _host = new GameObject("card host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 7UL) { EndSessionOnSubmit = false };
            var go = new GameObject("panel");
            _panel = go.AddComponent<FlowPanel>();
            _panel.Build(_flow, go.transform);
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

        private Button Card(string name) => _panel.Canvas.GetComponentsInChildren<Button>(true).First(b => b.gameObject.activeSelf && b.GetComponentInChildren<TMP_Text>().text.StartsWith(name));

        [Test]
        public void Training_shows_both_procedures_with_their_counts_and_wording()
        {
            Assert.IsTrue(_panel.Click("TRAINING MODE"));
            foreach (var (name, procedure) in new[] { ("Gallbladder removal", "cholecystectomy"), ("Appendectomy", "appendicectomy") })
            {
                var card = Card(name);
                string text = card.GetComponentInChildren<TMP_Text>().text;
                int n = _flow.CaseCount(SessionMode.Practice, procedure);
                Assert.Greater(n, 0, procedure);
                StringAssert.Contains("Pre-operative preparation", text);
                StringAssert.Contains(n + " cases", text);
                Assert.IsTrue(card.interactable);
                Assert.GreaterOrEqual(((RectTransform)card.transform).rect.height, 150f, "a card, not a row button");
            }
        }

        [Test]
        public void An_empty_pool_shows_a_disabled_card_with_the_reason()
        {
            Assert.IsTrue(_panel.Click("TEST / EXAMINATION MODE"));
            foreach (var (name, procedure) in new[] { ("Gallbladder removal", "cholecystectomy"), ("Appendectomy", "appendicectomy") })
            {
                var card = Card(name);
                bool empty = _flow.CaseCount(SessionMode.Assessment, procedure) == 0;
                Assert.AreEqual(!empty, card.interactable, procedure);
                if (empty)
                {
                    StringAssert.Contains("No cases are approved for examination yet", card.GetComponentInChildren<TMP_Text>().text);
                    Assert.IsFalse(_panel.Click(card.GetComponentInChildren<TMP_Text>().text), "a disabled card cannot be pressed");
                }
            }
        }

        [Test]
        public void The_chosen_level_reaches_the_session_header()   // P3-MNU-05
        {
            Assert.IsTrue(_panel.Click("TRAINING MODE"));
            Assert.IsTrue(_panel.Click("Level: Beginner"));
            Assert.IsTrue(_panel.VisibleButtons().Contains("Level: " + FlowPanel.NextLevel(TrainingLevel.Beginner)));
            while (_flow.Level != TrainingLevel.Advanced) Assert.IsTrue(_panel.Click("Level: " + _flow.Level));
            Assert.IsTrue(_panel.Click(_panel.VisibleButtons().First(b => b.StartsWith("Gallbladder removal"))), _flow.Message);
            Assert.AreEqual(TrainingLevel.Advanced, _host.Session.Level);
            StringAssert.Contains("\"level\":\"advanced\"", ProcSense.Core.Replay.SessionFile.Write(_host.Session).Split('\n')[0], "in the log header the replay reads");
            var all = (TrainingLevel[])System.Enum.GetValues(typeof(TrainingLevel));
            Assert.AreEqual(all[0], FlowPanel.NextLevel(all[all.Length - 1]), "the button cycles");
        }

        [Test]
        public void Leaving_the_screen_puts_the_buttons_back()
        {
            Assert.IsTrue(_panel.Click("TRAINING MODE"));
            Assert.IsTrue(_panel.Click("Back"));
            var training = Card("TRAINING MODE");
            Assert.AreEqual(64f, ((RectTransform)training.transform).rect.height, .01f);
            Assert.IsTrue(training.interactable);
        }
    }
}
