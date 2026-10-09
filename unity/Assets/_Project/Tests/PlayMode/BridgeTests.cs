using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>The Unity side of the bridge: emitters, host, tablet and monitor, driven without a headset (plan tests T-B1..T-B5).</summary>
    public class BridgeTests
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

        [SetUp]
        public void SetUp()
        {
            TabletFallbackRegistry.Clear();
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private static SimAction Vr(string verb, params (string key, string value)[] parameters) =>
            new SimAction(verb, "patient", Channel.Vr, parameters.ToDictionary(p => p.key, p => p.value));

        // T-B1: one press, one action
        [Test]
        public void An_emitter_sends_exactly_one_action_per_press()
        {
            var button = Make<Image>("button");
            button.gameObject.AddComponent<Button>();
            var emitter = button.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure("introduce_self");

            var first = emitter.Fire();
            var second = emitter.Fire();   // a repeated select event inside the debounce window

            Assert.IsNotNull(first);
            Assert.IsNull(second);
            Assert.AreEqual(1, _host.Session.Log.Count);
        }

        [Test]
        public void An_emitted_action_is_logged_in_the_same_format_as_the_command_line()
        {
            var button = Make<Image>("button");
            button.gameObject.AddComponent<Button>();
            var emitter = button.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure("introduce_self");
            emitter.Fire();

            var content = ContentLoader.Load(UnityContentReader(), "VAR-S1-A");
            var cli = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            cli.Apply(new SimAction("introduce_self", "patient", Channel.Cli));

            Assert.AreEqual(cli.Log.ToJsonl().Replace("\"channel\":\"cli\"", "\"channel\":\"vr\""), _host.Session.Log.ToJsonl());
            Assert.AreEqual("{\"seq\":0,\"step\":0,\"verb\":\"introduce_self\",\"target\":\"patient\",\"channel\":\"vr\",\"params\":{},\"accepted\":true,\"reject\":\"\",\"effects\":[\"trust+1\",\"anxiety-1\"]}\n", _host.Session.Log.ToJsonl());
        }

        // T-B2: a refused action shows a hint and changes nothing
        [Test]
        public void A_refused_action_shows_a_hint_and_changes_nothing_visible()
        {
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            string noteBefore = tablet.NoteText, subtitleBefore = tablet.SubtitleText;

            Assert.IsTrue(tablet.Press("confirm_identity"));
            tablet.RenderNow();

            Assert.AreEqual(HintTexts.For("IDENTITY_CHECK_INCOMPLETE"), tablet.HintText);
            Assert.AreEqual(noteBefore, tablet.NoteText);
            Assert.AreEqual(subtitleBefore, tablet.SubtitleText);
            Assert.IsFalse(_host.Observable.IdentityConfirmed);
        }

        // T-B5: a Core exception pauses the session instead of crashing
        private sealed class Boom : ProcSense.Core.Pipeline.ISubsystem
        {
            public int Order => PipelineOrder.Clinical;
            public void Init(Session session) => session.Verbs.Register("boom");
            public void OnAction(SimAction action, ActionOutcome outcome) { if (action.Verb == "boom") throw new System.InvalidOperationException("test failure"); }
            public void OnTick() { }
        }

        [Test]
        public void A_core_exception_pauses_the_session_without_crashing()
        {
            _host.SubsystemFactory = c => Subsystems.Default(c).Concat(new ProcSense.Core.Pipeline.ISubsystem[] { new Boom() });
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            string reason = null;
            _host.SessionPaused += r => reason = r;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Core threw"));

            var outcome = _host.Apply(Vr("boom"));

            Assert.IsFalse(outcome.Accepted);
            Assert.IsTrue(_host.Paused);
            StringAssert.Contains("test failure", reason);
            Assert.AreEqual("SESSION_PAUSED", _host.Apply(Vr("introduce_self")).RejectReason);   // and nothing else happens while paused
        }

        [Test]
        public void Tablet_buttons_are_enabled_only_when_core_state_allows()
        {
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            Assert.IsFalse(Button("Confirm identity", tablet).interactable);
            Assert.IsFalse(Button("Report mismatch", tablet).interactable);

            tablet.Press("ask_identity"); tablet.Press("read_wristband"); tablet.Press("open_notes");
            tablet.RenderNow();

            Assert.IsTrue(Button("Confirm identity", tablet).interactable);
            Assert.IsTrue(Button("Report mismatch", tablet).interactable);
        }

        private static Button Button(string label, TabletView tablet) => tablet.Buttons.First(b => b.label == label).button;

        [Test]
        public void The_monitor_is_blank_until_a_sensor_is_attached()
        {
            var tablet = Make<TabletView>("tablet");
            var monitor = Make<MonitorView>("monitor");
            monitor.RenderNow();
            Assert.AreEqual("--", monitor.ValueText("SpO2"));
            Assert.AreEqual("--", monitor.ValueText("BP"));

            tablet.Press("attach_sensor", "sensor", "spo2");
            monitor.RenderNow();

            StringAssert.StartsWith(_host.Session.Case.BaselineVitals.Spo2.ToString("0.#"), monitor.ValueText("SpO2"));
            Assert.AreEqual("--", monitor.ValueText("BP"));
        }

        [Test]
        public void The_wristband_card_and_notes_appear_only_after_they_are_read()
        {
            var card = Make<IdentityCardView>("card");
            var notes = Make<NotesPanelView>("notes");
            var tablet = Make<TabletView>("tablet");
            card.RenderNow(); notes.RenderNow();
            StringAssert.DoesNotContain("AYESHA", card.BodyText);
            StringAssert.DoesNotContain("Ayesha", notes.BodyText);

            tablet.Press("read_wristband"); tablet.Press("open_notes");
            card.RenderNow(); notes.RenderNow();

            StringAssert.Contains("AYESHA KHAN", card.BodyText);
            StringAssert.Contains("Ayesha Khan", notes.BodyText);
        }

        [Test]
        public void An_emitter_in_the_world_gets_a_tablet_fallback_button()
        {
            var button = Make<Image>("socket proxy");
            button.gameObject.AddComponent<Button>();
            var emitter = button.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure("wait", "patient", fallbackLabel: "Wait a moment");
            var tablet = Make<TabletView>("tablet");

            tablet.RenderNow();

            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Wait a moment"));
        }

        // The skeleton-1 golden script, driven through the tablet buttons, must give the same log as Core run directly (VR channel).
        [Test]
        public void The_golden_script_through_the_tablet_gives_the_same_log_hash_as_core_alone()
        {
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(Repo, "tests", "golden", "skeleton1_correct.actions.json")));
            var tablet = Make<TabletView>("tablet");
            var content = ContentLoader.Load(UnityContentReader(), "VAR-S1-A");
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));

            foreach (var step in script)
            {
                double dt = step.T - _host.Session.Clock.SimSeconds;
                _host.Tick(dt);
                reference.Tick(step.T - reference.Clock.SimSeconds);
                var p = step.Action.Params.FirstOrDefault();
                var viaVr = Vr(step.Action.Verb, p.Key == null ? new (string, string)[0] : new[] { (p.Key, p.Value) });
                reference.Apply(viaVr);
                if (!tablet.Press(step.Action.Verb, p.Key ?? "", p.Value ?? "")) _host.Apply(viaVr);   // "wait" has no button
            }

            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(reference.Observable.Hash(), _host.Session.Observable.Hash());
            Assert.IsTrue(_host.Observable.IdentityConfirmed);
        }

        private static ProcSense.VR.Content.UnityContentReader UnityContentReader() =>
            new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content"));
    }
}
