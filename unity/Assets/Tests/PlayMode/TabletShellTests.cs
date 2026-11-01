using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The tablet shell (p0-vr-interaction-ui-s03): every P0 Step 1 to 12 action is reachable from the tablet, each panel renders the state (T-UI1),
    /// forms send exactly the action their fields describe, and practice aids stay out of assessment.
    /// </summary>
    public class TabletShellTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        /// <summary>The real content, with the case files raised to APPROVED_FOR_ASSESSMENT in memory only, as ModeGateTests does (no case is approved for assessment yet).</summary>
        private sealed class Raised : IContentReader
        {
            private readonly IContentReader _inner;
            public Raised(IContentReader inner) { _inner = inner; }
            public string ReadText(string path)
            {
                var text = _inner.ReadText(path);
                return path.StartsWith("case_files/") ? text.Replace("\"status\": \"APPROVED_FOR_TRAINING\"", "\"status\": \"APPROVED_FOR_ASSESSMENT\"") : text;
            }
        }

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private SimulationHost Host(string caseId, SessionMode mode = SessionMode.Practice)
        {
            TabletFallbackRegistry.Clear();
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession(caseId, 42, mode, mode == SessionMode.Assessment ? new Raised(Real()) : null);
            ModeGate.SessionProvider = () => _host.Session;
            return _host;
        }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        /// <summary>The P0 correct paths (tests/golden/*_correct.actions.json, the 17 core cases).</summary>
        private static IEnumerable<string> CorrectPaths() =>
            Directory.GetFiles(Path.Combine(Repo, "tests", "golden"), "*_correct.actions.json").Select(Path.GetFileName)
                     .Where(f => !f.StartsWith("skeleton")).Select(f => f.Substring(0, f.Length - "_correct.actions.json".Length)).OrderBy(f => f);

        [TestCaseSource(nameof(CorrectPaths))]
        public void Every_action_of_the_correct_path_is_made_from_the_tablet_with_the_same_log_as_core_alone(string caseId)
        {
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(Repo, "tests", "golden", caseId + "_correct.actions.json")));
            try { Host(caseId); }
            catch (ContentException e) when (e.Message.Contains("STATUS_TOO_LOW")) { Assert.Ignore(caseId + " is not approved for practice yet (clinical sign-off pending): " + e.Message); }
            var tablet = Make<TabletView>("tablet");
            var content = ContentLoader.Load(new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content")), caseId);
            var reference = new Session(content.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(content));

            foreach (var step in script)
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                reference.Tick(step.T - reference.Clock.SimSeconds);
                var action = new SimAction(step.Action.Verb, "patient", Channel.Vr, step.Action.Params.ToDictionary(kv => kv.Key, kv => kv.Value));
                reference.Apply(action);
                Assert.IsTrue(tablet.PressAction(action.Verb, action.Params), caseId + " t=" + step.T + ": the tablet cannot make " + action.Verb + " " + string.Join(",", action.Params.Select(kv => kv.Key + "=" + kv.Value)));
            }

            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash(), "the tablet made a different log");
            Assert.AreEqual(reference.Observable.Hash(), _host.Session.Observable.Hash());
        }

        [Test]
        public void Every_tab_renders_the_state_and_the_lists_follow_the_session()
        {
            Host("VAR-S1-A");
            var tablet = Make<TabletView>("tablet");
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(tablet.Press(verb));
            Assert.IsTrue(tablet.PressAction("order_investigation", new Dictionary<string, string> { ["test"] = "fbc" }));
            tablet.RenderNow();
            Assert.AreEqual(15, tablet.TabIds.Count, "11 content tabs, More, Session, runtime Diagnostics and Settings");
            for (int i = 0; i < tablet.TabIds.Count; i++)
            {
                tablet.ShowTab(i);
                tablet.RenderNow();
                Assert.AreEqual(i, tablet.CurrentTab);
            }
            tablet.ShowTab("known");
            tablet.RenderNow();
            StringAssert.Contains(_host.Observable.DisclosedFacts.Last().Answer, tablet.SideText, "Known lists what the patient said");
            tablet.ShowTab("results");
            tablet.RenderNow();
            StringAssert.Contains(_host.Observable.Investigations.First().Label, tablet.SideText, "Results lists the ordered tests");
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Confirm identity" && b.button.interactable));
        }

        [Test]
        public void A_form_sends_exactly_the_action_its_fields_describe()
        {
            Host("VAR-S1-A");
            var tablet = Make<TabletView>("tablet");
            tablet.ShowTab("assess");
            tablet.Buttons.Single(b => b.label == "STOP-BANG").button.onClick.Invoke();
            Assert.AreEqual("STOP-BANG", tablet.FormTitle);
            var p = tablet.FormParams();
            Assert.AreEqual("stopbang", p["score"]);
            Assert.AreEqual(9, p.Count, "the score and its eight questions, each true or false");
            Assert.IsTrue(p.Where(kv => kv.Key != "score").All(kv => kv.Value == "false"));

            tablet.ShowTab("orders");
            tablet.Buttons.Single(b => b.label == "Request transport").button.onClick.Invoke();
            var transport = tablet.FormParams();
            Assert.IsFalse(transport.ContainsKey("escort"), "a multi field with nothing chosen is not sent");
            CollectionAssert.IsSubsetOf(new[] { "to", "mode", "monitoring", "accompany", "oxygen" }, transport.Keys.ToList());
            int before = _host.Session.Log.Count;
            Assert.IsTrue(tablet.Send());
            Assert.AreEqual(before + 1, _host.Session.Log.Count, "one form, one action");
            Assert.AreEqual("", tablet.FormTitle, "the form closes after sending");
        }

        [Test]
        public void Assessment_shows_neither_the_hint_chip_nor_the_outstanding_goals()
        {
            Host("VAR-S1-A", SessionMode.Assessment);
            var tablet = Make<TabletView>("tablet");
            tablet.Press("introduce_self");
            tablet.ShowTab("debrief");
            tablet.RenderNow();
            Assert.AreEqual("", tablet.ChipText);
            Assert.AreEqual("", tablet.SideText);
        }
    }
}
