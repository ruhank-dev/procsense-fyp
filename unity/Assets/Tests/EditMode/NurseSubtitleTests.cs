using System.Collections.Generic;
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
    /// <summary>The nurse's subtitles (plan p0-voice-audio-s06, Unity half): every state of the nurse's wristband walk and every outcome of a request has its authored line, in the order the engine published
    /// the steps, and a line nobody can reach is a failure. The audio, the avatar animation and the headset check (UX-P0-VOICE) are Sannan's.</summary>
    public class NurseSubtitleTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static List<NurseLine> Lines() => NurseSubtitles.Parse(File.ReadAllText(Path.Combine(Repo, "content", "voice", "nurse_wristband.json")));

        private static SimulationHost Host(string caseId)
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession(caseId, 42, SessionMode.Practice, new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))));
            return host;
        }

        private static (SimAction action, ActionOutcome outcome) Do(SimulationHost host, string verb)
        {
            var action = new SimAction(verb);
            return (action, host.Apply(action));
        }

        [Test]
        public void The_content_file_is_read_and_every_line_has_words_and_a_trigger()
        {
            var lines = Lines();
            Assert.AreEqual(10, lines.Count);
            Assert.IsTrue(lines.All(l => !string.IsNullOrWhiteSpace(l.text) && l.trigger != null));
            Assert.AreEqual(lines.Count, lines.Select(l => l.id).Distinct().Count());
        }

        [Test]
        public void A_reported_mismatch_gives_the_walk_in_order_and_the_replacement_line()
        {
            var host = Host("TEST-ID-MISMATCH");
            try
            {
                foreach (var v in new[] { "introduce_self", "ask_identity", "open_notes", "read_wristband" }) Do(host, v);
                var (action, outcome) = Do(host, "report_mismatch");
                var ids = NurseSubtitles.For(Lines(), action, outcome).Select(l => l.id).ToList();
                CollectionAssert.AreEqual(new[] { "NUR-WRISTBAND-CALLED", "NUR-WRISTBAND-AT_BEDSIDE-1", "NUR-WRISTBAND-AT_BEDSIDE-2", "NUR-WRISTBAND-REPLACING", "NUR-WRISTBAND-DONE" }, ids);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_missing_band_is_refused_before_identity_then_accepted_and_applied_with_the_allergy_band()
        {
            var host = Host("TEST-ID-MISSING-BAND-ALLERGY");
            try
            {
                Do(host, "ask_identity");
                var (a1, o1) = Do(host, "request_wristband");
                CollectionAssert.AreEqual(new[] { "NUR-WRISTBAND-VERIFY_FIRST" }, NurseSubtitles.For(Lines(), a1, o1).Select(l => l.id));
                Do(host, "open_notes");
                var (a2, o2) = Do(host, "request_wristband");
                var ids = NurseSubtitles.For(Lines(), a2, o2).Select(l => l.id).ToList();
                CollectionAssert.Contains(ids, "NUR-WRISTBAND-MISSING_ACK");
                CollectionAssert.Contains(ids, "NUR-WRISTBAND-APPLYING");
                CollectionAssert.Contains(ids, "NUR-WRISTBAND-DONE");
                CollectionAssert.Contains(ids, "NUR-WRISTBAND-ALLERGY_BAND");
                Assert.Less(ids.IndexOf("NUR-WRISTBAND-CALLED"), ids.IndexOf("NUR-WRISTBAND-DONE"));
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Nothing_to_fix_has_its_line_and_every_authored_line_was_reached_by_one_of_the_flows()
        {
            var host = Host("VAR-S1-A");
            var reached = new HashSet<string>();
            try
            {
                foreach (var v in new[] { "introduce_self", "ask_identity", "open_notes" }) Do(host, v);
                var (a, o) = Do(host, "request_wristband");                                    // the band is on and correct: nothing for the nurse to fix
                foreach (var l in NurseSubtitles.For(Lines(), a, o)) reached.Add(l.id);
                CollectionAssert.AreEquivalent(new[] { "NUR-WRISTBAND-MISSING_ACK", "NUR-WRISTBAND-NOTHING_TO_FIX" }, reached.ToArray());      // the acknowledgement, then the line that says nothing needed changing
            }
            finally { Object.DestroyImmediate(host.gameObject); }
            foreach (var caseId in new[] { "TEST-ID-MISMATCH", "TEST-ID-MISSING-BAND-ALLERGY" })
            {
                var h = Host(caseId);
                try
                {
                    Do(h, "ask_identity");
                    foreach (var verb in new[] { "request_wristband", "open_notes", "request_wristband", "read_wristband", "report_mismatch" })
                    {
                        var (a, o) = Do(h, verb);
                        foreach (var l in NurseSubtitles.For(Lines(), a, o)) reached.Add(l.id);
                    }
                }
                finally { Object.DestroyImmediate(h.gameObject); }
            }
            var all = Lines().Select(l => l.id).ToList();
            CollectionAssert.AreEquivalent(all, reached.ToList(), "every authored nurse line must be reachable by a flow of the engine");
        }

        [Test]
        public void The_view_shows_the_lines_one_after_the_other_and_applies_nothing()
        {
            var host = Host("TEST-ID-MISMATCH");
            try
            {
                var view = new GameObject("subtitles").AddComponent<NurseSubtitleView>();
                view.LinesJson = File.ReadAllText(Path.Combine(Repo, "content", "voice", "nurse_wristband.json"));
                view.Build();
                foreach (var v in new[] { "introduce_self", "ask_identity", "open_notes", "read_wristband" }) Do(host, v);
                string before = host.Session.Observable.ToJson();
                var (a, o) = Do(host, "report_mismatch");
                view.OnApplied(a, o);
                Assert.AreEqual(5, view.Pending);
                view.Step(0f);
                StringAssert.Contains("Let me check", view.Current);
                view.Step(NurseSubtitleView.SecondsPerLine);
                StringAssert.Contains("coming in to the bedside", view.Current);
                for (int i = 0; i < 5; i++) view.Step(NurseSubtitleView.SecondsPerLine);
                Assert.AreEqual(0, view.Pending);
                Assert.AreEqual(5, view.Shown.Count);
                Assert.AreNotEqual(before, host.Session.Observable.ToJson());                // the nurse's walk changed the session, the view did not
                Object.DestroyImmediate(view.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }
    }
}
