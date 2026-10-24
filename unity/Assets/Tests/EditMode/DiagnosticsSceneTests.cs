using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The Unity half of p0-diagnostic-transitions-s04 and s05 (lab-sample tube, tube station, porter, printed report): the props follow the Core diagnostics loop and apply no action.
    /// The placement in the real bay and the headset check (UX-P0-DIAG) stay with Sannan.</summary>
    public class DiagnosticsSceneTests
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
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Act(host, verb).Accepted, verb);
            return host;
        }

        private static ActionOutcome Act(SimulationHost host, string verb, params (string k, string v)[] p) =>
            host.Apply(new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v)));

        [Test]
        public void With_no_order_nothing_is_shown_and_the_summary_says_so()
        {
            var host = Host();
            try
            {
                var view = new GameObject("view").AddComponent<DiagnosticsSceneView>();
                view.Use(host.Session);
                Assert.AreEqual(0, view.Lines.Count);
                Assert.AreEqual(0, view.VisibleTubes);
                Assert.IsFalse(view.Porter.activeSelf);
                Assert.IsFalse(view.Report.activeSelf);
                StringAssert.Contains("No tests in progress", view.FolderPage);
                Object.DestroyImmediate(view.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_kept_sample_sits_on_the_bedside_tray_then_goes_by_the_tube_station_without_a_porter()
        {
            var host = Host();
            try
            {
                var view = new GameObject("view").AddComponent<DiagnosticsSceneView>();
                Assert.IsTrue(Act(host, "order_investigation", ("test", "lft")).Accepted);
                Assert.IsTrue(Act(host, "take_sample", ("test", "lft"), ("send", "later")).Accepted);
                view.Use(host.Session);
                Assert.AreEqual(TubeSite.Bedside, view.Lines.Single().Tube);
                Assert.AreEqual(1, view.VisibleTubes);
                Assert.AreEqual(view.BedsideTray, view.Tubes[0].transform.parent);
                Assert.IsFalse(view.Porter.activeSelf);
                Assert.IsTrue(Act(host, "send_samples", ("via", "tube_station")).Accepted);
                view.Refresh();
                Assert.AreEqual(TubeSite.TubeStation, view.Lines.Single().Tube);
                Assert.AreEqual(view.TubeStation, view.Tubes[0].transform.parent);
                Assert.IsFalse(view.Porter.activeSelf);
                Object.DestroyImmediate(view.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_porter_is_shown_only_while_a_porter_carries_the_sample_and_the_report_lands_on_the_desk_when_delivered()
        {
            var host = Host();
            try
            {
                var view = new GameObject("view").AddComponent<DiagnosticsSceneView>();
                Assert.IsTrue(Act(host, "order_investigation", ("test", "lft")).Accepted);
                Assert.IsTrue(Act(host, "take_sample", ("test", "lft")).Accepted);          // sent at once, by porter
                view.Use(host.Session);
                Assert.AreEqual(TubeSite.Porter, view.Lines.Single().Tube);
                Assert.IsTrue(view.Porter.activeSelf);
                Assert.IsFalse(view.Report.activeSelf);
                host.Tick(6 * 3600);
                view.Refresh();
                Assert.IsFalse(view.Porter.activeSelf);
                Assert.IsTrue(view.Lines.Single().ReportOnDesk);
                Assert.IsTrue(view.Report.activeSelf);
                StringAssert.Contains("Report delivered", view.FolderPage);
                Object.DestroyImmediate(view.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_view_changes_nothing_in_the_session()
        {
            var host = Host();
            try
            {
                Assert.IsTrue(Act(host, "order_investigation", ("test", "lft")).Accepted);
                string before = host.Session.Observable.ToJson();
                var view = new GameObject("view").AddComponent<DiagnosticsSceneView>();
                view.Use(host.Session);
                for (int i = 0; i < 5; i++) view.Refresh();
                Assert.AreEqual(before, host.Session.Observable.ToJson());
                Object.DestroyImmediate(view.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }
    }
}
