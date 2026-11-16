using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-RES-04/05/06/08/09, P3-TRN-07: the results card equals the engine's summary (verdict, score, critical items first, case and seed), the buttons (Retry, New scenario, Main menu, Review details,
    /// Quit), each path starting clean, the verification badge only for the shown session's "verified", nothing offline, and after a retry the comparison with the previous attempt.
    /// </summary>
    public class ResultsCardTests
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
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 11UL) { EndSessionOnSubmit = false };
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

        private void ToResults()
        {
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
        }

        [Test]
        public void The_card_equals_the_summary_and_offers_every_way_on()
        {
            ToResults();
            var s = _flow.Summary;
            var card = _panel.Card;
            Assert.IsNotNull(card);
            Assert.IsTrue(card.gameObject.activeSelf);
            Assert.AreSame(s, card.Shown);
            Assert.AreEqual(ResultsCardText.Verdict(s), card.VerdictText);
            Assert.AreEqual(s.Grade != null ? s.Grade.Score100.ToString() : "–", card.ScoreText);
            if (s.CriticalCount > 0) Assert.IsTrue(card.SectionsShown[0].Critical, "critical items first");
            var texts = card.GetComponentsInChildren<TMPro.TMP_Text>();
            Assert.IsTrue(System.Array.Exists(texts, t => t.text == ResultsCardText.CaseLine(s)), "case and seed shown");
            CollectionAssert.AreEqual(new[] { "Retry", "New scenario", "Main menu", "Review details", "Quit" }, _panel.VisibleButtons());
            Assert.IsFalse(card.Verified, "no badge before the service answers, and none offline");

            Assert.IsTrue(_panel.Click("Review details"));
            Assert.IsTrue(_panel.DebriefOpen, "the debrief opens from the results (P3-RES-05)");
            Assert.IsTrue(_panel.Click("Hide details"));
            Assert.IsFalse(_panel.DebriefOpen);

            Assert.IsTrue(_panel.Click("Main menu"));
            Assert.AreEqual(MenuPhase.MainMenu, _flow.Phase);
            Assert.IsFalse(card.gameObject.activeSelf, "the card leaves with the results");
        }

        [Test]
        public void The_badge_shows_only_for_the_shown_sessions_verified_answer()
        {
            ToResults();
            var card = _panel.Card;
            var s = card.Shown;
            card.Report(s, "pending");
            Assert.IsFalse(card.Verified);
            card.Report(s, "");
            Assert.IsFalse(card.Verified, "offline: nothing");
            card.Report(s, "verified");
            Assert.IsTrue(card.Verified);
            Assert.IsTrue(System.Array.Exists(card.GetComponentsInChildren<TMPro.TMP_Text>(), t => t.text == ResultsCardText.VerifiedText));
            Assert.IsTrue(_panel.Click("New scenario"));
            Assert.AreEqual(MenuPhase.Briefing, _flow.Phase, "a new scenario starts clean");
            card.Report(s, "verified");
            Assert.IsFalse(card.gameObject.activeSelf);
        }

        [Test]
        public void After_a_retry_the_results_compare_with_the_previous_attempt()
        {
            ToResults();
            var first = _flow.Summary;
            Assert.IsTrue(_panel.Click("Retry"));
            Assert.AreEqual(MenuPhase.Briefing, _flow.Phase, "retry starts clean, same case and seed");
            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            var card = _panel.Card;
            Assert.AreSame(first, card.Previous);
            Assert.AreEqual(ResultsCardText.Comparison(first, _flow.Summary), card.ComparisonText);
            StringAssert.StartsWith("Previous attempt:", card.ComparisonText);

            Assert.IsTrue(_panel.Click("New scenario"));
            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            if (_flow.Summary.Seed != first.Seed) Assert.AreEqual("", card.ComparisonText, "a new scenario has nothing to compare with");
        }
    }
}
