using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.UI.Debrief;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The debrief screen (p0-vr-interaction-ui-s09): critical items first (T-DU3), every item opens its log evidence with the rule and source (T-DU1), template only with the AI tab hidden (T-DU2), minor items
    /// collapsed until asked for, the timeline links to the log, an unneeded test is listed with its waiting time, and the next case is a practice aid only. The read time (T-DU4) is a tester check.
    /// </summary>
    public class DebriefViewTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        /// <summary>The real content with the case files raised to APPROVED_FOR_ASSESSMENT in memory only (no case is approved for assessment yet), as the tablet tests do.</summary>
        private sealed class Raised : IContentReader
        {
            private readonly IContentReader _inner = ProcSense.VR.Content.UnityContentReader.ForThisPlatform();
            public string ReadText(string path)
            {
                var text = _inner.ReadText(path);
                return path.StartsWith("case_files/") ? text.Replace("\"status\": \"APPROVED_FOR_TRAINING\"", "\"status\": \"APPROVED_FOR_ASSESSMENT\"") : text;
            }
        }

        private ActionOutcome Do(string verb, params (string k, string v)[] p) => _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.k, x => x.v)));

        // A session that goes wrong on purpose: an unneeded TSH, then proceeding with a critical potassium nobody answered the phone for.
        private DebriefView Played(SessionMode mode = SessionMode.Practice, string caseId = "STK-S1-C")
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            var go = new GameObject("host");
            _made.Add(go);
            _host = go.AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession(caseId, 42, mode, mode == SessionMode.Assessment ? new Raised() : null);
            ModeGate.SessionProvider = () => _host.Session;
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) { _host.Tick(4); Assert.IsTrue(Do(v).Accepted, v); }   // a few seconds apart, as played
            _host.Tick(4);
            Assert.IsTrue(Do("order_investigation", ("test", "tsh")).Accepted);
            _host.Tick(4);
            Assert.IsTrue(Do("order_investigation", ("test", "u_and_e")).Accepted);
            _host.Tick(4);
            Assert.IsTrue(Do("take_sample", ("test", "u_and_e"), ("tube", "serum_gold")).Accepted);
            _host.Tick(60 * 60);
            if (caseId == "STK-S1-C") Assert.AreEqual("RINGING", _host.Observable.Phone.State);
            Do("decide_ready", ("option", "proceed"));
            var view = new GameObject("debrief");
            _made.Add(view);
            view.transform.position = new Vector3(40f, 0f, 0f);
            return view.AddComponent<DebriefView>();
        }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Critical_items_come_first_and_every_item_opens_its_evidence()
        {
            var d = Played();
            Assert.IsNotNull(d.Facts);
            Assert.IsTrue(d.Facts.Lines.Any(l => l.Severity == "critical"), "proceeding with an unanswered critical result is a critical item");
            Assert.AreEqual("critical", d.ShownLines[0].Severity, "T-DU3: critical first");
            Assert.IsFalse(d.AiTabVisible, "T-DU2: template only until P1");

            foreach (var line in d.Facts.Lines.Where(l => l.Evidence.Count > 0))
            {
                d.Open(line);
                StringAssert.Contains("#" + line.Evidence[0], d.Drawer.Text, "T-DU1: the drawer shows the log entry " + line.Item);
                StringAssert.Contains("Rule: " + line.RuleId, d.Drawer.Text);
                if (Citations.Table.TryGetValue(line.Source, out var cite)) StringAssert.Contains(cite, d.Drawer.Text, "the source's citation, not only its ID (DT-261)");
            }
            Assert.IsTrue(d.Facts.Lines.Where(l => l.Severity != "credit").All(l => l.Evidence.Count > 0), "T-DU1: every item the trainee is told about has its log evidence");

            Assert.IsNotEmpty(d.Timeline.MarkerSeqs);
            d.OpenSeq(d.Timeline.MarkerSeqs[0]);
            StringAssert.Contains("#" + d.Timeline.MarkerSeqs[0], d.Drawer.Text, "a timeline marker opens the log entry it marks");

            StringAssert.Contains("Thyroid", d.UnneededText, "the unneeded TSH is listed");
            StringAssert.Contains("min", d.UnneededText, "with the waiting it cost");
            StringAssert.Contains("Next case", d.NextCaseText, "practice suggests the next case");
            Snapshots.Photograph(d.Canvas, Snapshots.Out("debrief.png"));
        }

        [Test]
        public void Minor_items_are_collapsed_until_asked_for_and_assessment_suggests_no_next_case()
        {
            var d = Played(SessionMode.Assessment, "VAR-S1-A");          // only a P0 case can be raised to assessment
            int minors = d.Facts.Lines.Count(DebriefText.Collapsed);
            Assert.IsFalse(d.ShownLines.Any(DebriefText.Collapsed), "minor items collapsed by default");
            d.ShowMinor(true);
            Assert.AreEqual(d.Facts.Lines.Count, d.ShownLines.Count);
            Assert.AreEqual(minors, d.ShownLines.Count(DebriefText.Collapsed));
            Assert.AreEqual("", d.NextCaseText, "no practice aid in assessment");
        }
    }
}
