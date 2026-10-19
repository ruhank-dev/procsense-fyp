using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Events;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The EMR sections and the new-document badges as text (plan p0-patient-records-emr-s04 and s05, Unity half): every tab of the record is drawn, a section says when it is empty, a new document puts "(n)" on its tab and opening it takes the badge away.
    /// The 3D screen, the layout and the headset check that the Labs badge appears when the patient returns are Sannan's (UX-P0-EMR).</summary>
    public class EmrTextTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private static SimulationHost Host()
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice, Real());
            return host;
        }

        [Test]
        public void Every_tab_of_the_record_is_drawn_with_text_and_the_profile_names_the_patient()
        {
            var host = Host();
            try
            {
                var tabs = EmrText.Build(host.Session.Records);
                CollectionAssert.AreEqual(host.Session.Records.Tabs.Select(t => t.Id).ToArray(), tabs.Select(t => t.Id).ToArray());
                Assert.GreaterOrEqual(tabs.Count, 7);
                foreach (var t in tabs) Assert.IsNotEmpty(t.Body, t.Id);
                StringAssert.Contains("Date of birth", tabs.First(t => t.Id == "profile").Body);
                Assert.IsTrue(tabs.First(t => t.Id == "labs").Body.Length > 0);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        private static ActionOutcome Act(SimulationHost host, string verb, params (string k, string v)[] p) =>
            host.Apply(new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v)));

        [Test]
        public void An_unopened_document_puts_a_number_on_its_tab_and_opening_it_removes_it()
        {
            var host = Host();
            try
            {
                var records = host.Session.Records;
                foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Act(host, verb).Accepted, verb);
                Assert.IsTrue(Act(host, "order_investigation", ("test", "lft")).Accepted);
                Assert.IsTrue(Act(host, "take_sample", ("test", "lft")).Accepted);
                Assert.AreEqual(0, EmrText.Build(records).First(t => t.Id == "labs").NewDocuments);
                host.Tick(120 * 60);
                var shown = EmrText.Build(records).First(t => t.Id == "labs");
                Assert.AreEqual(1, shown.NewDocuments);
                StringAssert.Contains("Lab results (1)", shown.Label);
                var doc = records.Badges.Unseen("labs").Single();
                Assert.IsTrue(Act(host, "open_document", ("id", doc)).Accepted);
                var after = EmrText.Build(records).First(t => t.Id == "labs");
                Assert.AreEqual(0, after.NewDocuments);
                Assert.AreEqual("Lab results", after.Label);
                StringAssert.Contains("lft", after.Body.ToLowerInvariant().Replace("liver function", "lft"));
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_panel_draws_the_tabs_switches_between_them_and_shows_the_badge()
        {
            var host = Host();
            var go = new GameObject("emr");
            try
            {
                var panel = go.AddComponent<EmrPanelView>();
                panel.Use(host.Session);
                Assert.AreEqual(host.Session.Records.Tabs.Count, panel.TabButtons.Count);
                StringAssert.Contains("Date of birth", panel.BodyText);                                   // the first tab is the profile
                panel.TabButtons[2].onClick.Invoke();
                Assert.AreEqual("labs", panel.SelectedTab);
                foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Act(host, verb);
                Act(host, "order_investigation", ("test", "lft"));
                Act(host, "take_sample", ("test", "lft"));
                host.Tick(120 * 60);
                panel.Refresh();
                CollectionAssert.Contains(panel.TabLabels, "Lab results (1)");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Event_cards_show_the_engines_cue_wording_only_and_hide_when_nothing_is_happening()
        {
            var go = new GameObject("cards");
            try
            {
                var view = go.AddComponent<EventCardsView>();
                view.Show(new ObservedCue[0]);
                Assert.AreEqual("", view.Text);
                Assert.IsFalse(view.Canvas.gameObject.activeSelf);
                view.Show(new[] { new ObservedCue("E-LOW-BP", "Blood pressure 82/48"), new ObservedCue("E-HIGH-HR", "Heart rate 132") });
                Assert.AreEqual("Blood pressure 82/48" + (char)10 + "Heart rate 132", view.Text);
                Assert.IsTrue(view.Canvas.gameObject.activeSelf);
                Assert.AreEqual("", EventCardsView.Render(null));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }

}
