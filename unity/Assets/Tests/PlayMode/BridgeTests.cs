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
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
        private SensorAttachment MakeSensor(string sensor, out XRSocketInteractor socket, out XRGrabInteractable grab, out XRInteractionManager manager)
        {
            manager = Make<XRInteractionManager>("sensor manager");
            socket = Make<XRSocketInteractor>("patient socket");
            socket.interactionManager = manager;
            grab = Make<XRGrabInteractable>("physical sensor");
            grab.interactionManager = manager;
            var home = Make<BoxCollider>("tray pose").transform;
            home.position = new Vector3(1,1,1);
            var binding = grab.gameObject.AddComponent<SensorAttachment>();
            binding.ConfigureSensor(sensor,socket,home);
            binding.SyncNow();
            binding.SyncNow(); // Observe the prop clear of the socket after its initial tray reset.
            if (!binding.Attached) grab.transform.position = socket.transform.position;
            return binding;
        }

        [Test]
        public void Held_sensor_shows_guides_and_snaps_home_without_a_clinical_action()
        {
            foreach (string sensor in new[] { "spo2", "nibp_cuff" })
            {
                var binding = MakeSensor(sensor, out var socket, out var grab, out var manager);
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                try
                {
                    binding.ConfigurePlacementHighlight(material);
                    binding.SyncNow();
                    Assert.IsFalse(binding.PlacementGuidesVisible, "Idle targets should not clutter the bay");
                    var hand = Make<XRDirectInteractor>("returning sensor hand");
                    hand.interactionManager = manager;
                    manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                    binding.SyncNow();
                    Assert.IsTrue(binding.PlacementGuidesVisible);
                    grab.transform.position = new Vector3(1.03f, 1, 1);
                    manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
                    binding.SyncNow();
                    Assert.IsFalse(binding.PlacementGuidesVisible);
                    Assert.AreEqual(new Vector3(1, 1, 1), grab.transform.position, "Release inside the return target must dock exactly");
                    Assert.IsFalse(binding.Attached);
                    Assert.AreEqual(0, _host.Session.Log.Count, "Tray placement must never emit attach/detach actions");
                }
                finally { Object.DestroyImmediate(material); }
            }
        }

        [Test]
        public void Sensor_released_away_from_tray_does_not_teleport_home()
        {
            var binding = MakeSensor("spo2", out var socket, out var grab, out var manager);
            var hand = Make<XRDirectInteractor>("off-target hand");
            hand.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            grab.transform.position = new Vector3(2, 1, 1);
            manager.SelectExit((IXRSelectInteractor)hand, (IXRSelectInteractable)grab);
            binding.SyncNow();
            Assert.AreEqual(new Vector3(2, 1, 1), grab.transform.position);
            Assert.AreEqual(0, _host.Session.Log.Count);
        }

        [Test]
        public void Physical_probe_attach_and_immediate_removal_match_core_actions()
        {
            var binding=MakeSensor("spo2",out var socket,out var grab,out var manager);
            manager.SelectEnter((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            binding.SyncNow();
            Assert.That(_host.Observable.Vitals.Any(v=>v.Name=="spo2"),Is.True);
            Assert.That(_host.Session.Log.Count,Is.EqualTo(1));
            manager.SelectExit((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            binding.SyncNow();
            Assert.That(_host.Observable.Vitals.Any(v=>v.Name=="spo2"),Is.False);
            Assert.That(_host.Session.Log.Count,Is.EqualTo(2),"Rapid removal cannot be swallowed by cooldown");
            var content=ContentLoader.Load(UnityContentReader(),"VAR-S1-A");
            var reference=new Session(content.Cases["VAR-S1-A"],42,SessionMode.Practice,Subsystems.Default(content));
            reference.Apply(Vr("attach_sensor",("sensor","spo2")));
            reference.Apply(Vr("detach_sensor",("sensor","spo2")));
            Assert.That(_host.Session.Log.Hash(),Is.EqualTo(reference.Log.Hash()));
            Assert.That(_host.Observable.Hash(),Is.EqualTo(reference.Observable.Hash()));
        }

        [Test]
        public void Tablet_sensor_actions_restore_pose_without_duplicate_actions_and_reset_clears_it()
        {
            var binding=MakeSensor("nibp_cuff",out var socket,out var grab,out var manager);
            _host.Apply(Vr("attach_sensor",("sensor","nibp_cuff")));
            binding.SyncNow();
            Assert.That(grab.interactorsSelecting.Contains(socket),Is.True);
            Assert.That(_host.Session.Log.Count,Is.EqualTo(1));
            binding.enabled=false;
            binding.enabled=true;
            binding.SyncNow();
            Assert.That(_host.Session.Log.Count,Is.EqualTo(1));
            _host.Apply(Vr("detach_sensor",("sensor","nibp_cuff")));
            binding.SyncNow();
            Assert.That(grab.isSelected,Is.False);
            Assert.That(grab.transform.position,Is.EqualTo(new Vector3(1,1,1)));
            _host.Apply(Vr("attach_sensor",("sensor","nibp_cuff")));
            binding.SyncNow();
            _host.StartSession("VAR-S1-A",42,SessionMode.Practice);
            binding.SyncNow();
            Assert.That(grab.isSelected,Is.False);
            Assert.That(binding.Attached,Is.False);
            Assert.That(_host.Session.Log.Count,Is.Zero);
        }

        [Test]
        public void Sensor_hand_and_wrong_socket_do_not_attach_and_rejected_attachment_is_undone()
        {
            var binding=MakeSensor("invalid_sensor",out var socket,out var grab,out var manager);
            var wrong=Make<XRSocketInteractor>("wrong destination");
            wrong.interactionManager=manager;
            var hand=Make<XRDirectInteractor>("sensor hand");
            hand.interactionManager=manager;
            manager.SelectEnter((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);
            manager.SelectExit((IXRSelectInteractor)hand,(IXRSelectInteractable)grab);
            manager.SelectEnter((IXRSelectInteractor)wrong,(IXRSelectInteractable)grab);
            Assert.That(grab.isSelected, Is.False, "Wrong socket is filtered before selection");
            Assert.That(_host.Session.Log.Count,Is.Zero);
            manager.SelectEnter((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            binding.SyncNow();
            Assert.That(binding.Attached,Is.False);
            Assert.That(grab.isSelected,Is.False);
            Assert.That(_host.Observable.LastRejection,Is.EqualTo("UNKNOWN_SENSOR"));
            Assert.That(_host.Session.Log.Count,Is.EqualTo(1));
        }

        [Test]
        public void Physical_cuff_cycle_rejects_missing_cuff_and_uses_core_time()
        {
            var measure=Make<XRSelectEmitter>("monitor BP key");
            measure.Configure("measure_nibp");
            measure.DebounceSeconds=0;
            Assert.That(measure.Fire().RejectReason,Is.EqualTo("CUFF_NOT_ATTACHED"));
            var binding=MakeSensor("nibp_cuff",out var socket,out var grab,out var manager);
            manager.SelectEnter((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            Assert.That(measure.Fire().Accepted,Is.True);
            Assert.That(measure.Fire().RejectReason,Is.EqualTo("CYCLE_IN_PROGRESS"));
            Assert.That(_host.Observable.Vitals.Any(v=>v.Name=="systolic_bp"),Is.False);
            _host.Tick(45);
            Assert.That(_host.Observable.Vitals.Any(v=>v.Name=="systolic_bp"),Is.True);
        }

        [Test]
        public void Removing_cuff_cancels_pending_cycle_and_reentering_scene_restores_from_accepted_log()
        {
            var binding=MakeSensor("nibp_cuff",out var socket,out var grab,out var manager);
            manager.SelectEnter((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            _host.Apply(Vr("measure_nibp"));
            manager.SelectExit((IXRSelectInteractor)socket,(IXRSelectInteractable)grab);
            _host.Tick(45);
            Assert.That(_host.Observable.Vitals.Any(v=>v.Name=="systolic_bp"),Is.False);
            _host.Apply(Vr("attach_sensor",("sensor","nibp_cuff")));
            binding.SyncNow();
            int count=_host.Session.Log.Count;
            Object.DestroyImmediate(binding.gameObject);
            var restored=MakeSensor("nibp_cuff",out var restoredSocket,out var restoredGrab,out var restoredManager);
            restored.SyncNow();
            Assert.That(restored.Attached,Is.True);
            Assert.That(restoredGrab.interactorsSelecting.Contains(restoredSocket),Is.True);
            Assert.That(_host.Session.Log.Count,Is.EqualTo(count),"Scene removal/restore must not send detach or duplicate attach");
        }

        [Test]
        public void Equipment_surfaces_show_only_observed_values_and_reset_with_session()
        {
            var monitor = Make<EquipmentSurfaceView>("pilot monitor");
            var monitorText = Make<TMPro.TextMeshPro>("monitor text");
            monitor.Configure(EquipmentSurfaceView.Surface.Monitor, monitorText);
            var identity = Make<EquipmentSurfaceView>("pilot identity");
            var identityText = Make<TMPro.TextMeshPro>("identity text");
            identity.Configure(EquipmentSurfaceView.Surface.Identity, identityText);
            monitor.RenderNow();
            identity.RenderNow();
            Assert.That(monitor.DisplayText, Does.Contain("SpO2   --"));
            Assert.That(identity.DisplayText, Is.EqualTo("READ WRISTBAND"));
            _host.Apply(Vr("read_wristband"));
            _host.Apply(Vr("attach_sensor", ("sensor", "spo2")));
            monitor.RenderNow();
            identity.RenderNow();
            Assert.That(identity.DisplayText, Does.Contain(_host.Observable.Wristband.NameText));
            Assert.That(monitor.DisplayText, Does.Not.Contain("SpO2   --"));
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            monitor.RenderNow();
            identity.RenderNow();
            Assert.That(monitor.DisplayText, Does.Contain("SpO2   --"));
            Assert.That(identity.DisplayText, Is.EqualTo("READ WRISTBAND"));
        }

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

        [Test]
        public void Input_before_session_start_does_not_swallow_the_first_valid_press()
        {
            Object.DestroyImmediate(_host.gameObject);
            _host = Make<SimulationHost>("uninitialized host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            var emitter = Make<UIButtonEmitter>("early button");
            emitter.Configure("introduce_self");
            Assert.IsNull(emitter.Fire());
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Assert.IsNotNull(emitter.Fire());
            Assert.AreEqual(1, _host.Session.Log.Count);
        }

        [Test]
        public void A_new_session_does_not_inherit_the_previous_sessions_cooldown()
        {
            var emitter = Make<UIButtonEmitter>("button");
            emitter.Configure("introduce_self");
            emitter.Fire();
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Assert.IsNotNull(emitter.Fire());
            Assert.AreEqual(1, _host.Session.Log.Count);
        }

        [Test]
        public void Button_callbacks_unsubscribe_and_reenable_without_duplicates()
        {
            var emitter = Make<UIButtonEmitter>("button");
            emitter.Configure("introduce_self");
            emitter.DebounceSeconds = 0f; // Do not let debounce conceal duplicate listeners.
            var button = emitter.GetComponent<Button>();
            for (int i = 0; i < 3; i++) { emitter.enabled = false; emitter.enabled = true; }
            button.onClick.Invoke();
            Assert.AreEqual(1, _host.Session.Log.Count);
            emitter.enabled = false;
            button.onClick.Invoke();
            Assert.AreEqual(1, _host.Session.Log.Count);
        }

        [Test]
        public void Xr_selection_debounces_and_unsubscribes_without_duplicate_listeners()
        {
            var emitter = Make<XRSelectEmitter>("wristband");
            emitter.Configure("read_wristband");
            var interactable = emitter.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            var selection = new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs();
            interactable.selectEntered.Invoke(selection);
            interactable.selectEntered.Invoke(selection);
            Assert.AreEqual(1, _host.Session.Log.Count);
            emitter.DebounceSeconds = 0f;
            for (int i = 0; i < 3; i++) { emitter.enabled = false; emitter.enabled = true; }
            interactable.selectEntered.Invoke(selection);
            Assert.AreEqual(2, _host.Session.Log.Count);
            emitter.enabled = false;
            interactable.selectEntered.Invoke(selection);
            Assert.AreEqual(2, _host.Session.Log.Count);
        }

        [UnityTest]
        public System.Collections.IEnumerator Debounce_expires_during_unity_pause_and_rejections_remain_logged_once()
        {
            var emitter = Make<UIButtonEmitter>("button");
            emitter.Configure("confirm_identity");
            emitter.DebounceSeconds = 0.05f;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                Assert.IsNotNull(emitter.Fire());
                Assert.IsNull(emitter.Fire());
                Assert.IsFalse(_host.Observable.IdentityConfirmed);
                yield return new WaitForSecondsRealtime(0.08f);
                Assert.IsNotNull(emitter.Fire());
                Assert.AreEqual(2, _host.Session.Log.Count);
                Assert.IsFalse(_host.Observable.IdentityConfirmed);
            }
            finally { Time.timeScale = previousScale; }
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Invalid_debounce_values_do_not_disable_duplicate_protection(float value)
        {
            var emitter = Make<UIButtonEmitter>("button");
            emitter.Configure("introduce_self");
            emitter.DebounceSeconds = value;
            Assert.IsNotNull(emitter.Fire());
            Assert.IsNull(emitter.Fire());
        }

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
            emitter.Configure("explain_delay", "patient", fallbackLabel: "Wait a moment");
            var tablet = Make<TabletView>("tablet");

            tablet.RenderNow();

            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Wait a moment"));
        }

        // The skeleton-1 golden script, driven through the tablet buttons, must give the same log as Core run directly (VR channel).
        [Test]
        public void Fallbacks_follow_enable_reconfigure_and_destroy_without_removing_builtin_controls()
        {
            var emitter = Make<UIButtonEmitter>("world fallback");
            emitter.Configure("explain_delay", fallbackLabel: "Wait here");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            var original = tablet.Buttons.Single(b => b.label == "Wait here").button;
            emitter.Configure("explain_delay", fallbackLabel: "Wait briefly");
            tablet.RenderNow();
            Assert.AreSame(original, tablet.Buttons.Single(b => b.label == "Wait briefly").button);
            emitter.enabled = false;
            tablet.RenderNow();
            Assert.IsFalse(tablet.Buttons.Any(b => b.label == "Wait briefly"));
            emitter.Configure("explain_delay", fallbackLabel: "New wait");
            Assert.IsEmpty(TabletFallbackRegistry.All, "disabled configuration cannot register an active fallback");
            emitter.enabled = true;
            tablet.RenderNow();
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "New wait"));
            emitter.Configure("read_wristband", fallbackLabel: "Duplicate wristband");
            tablet.RenderNow();
            Assert.IsFalse(tablet.Buttons.Any(b => b.label == "New wait" || b.label == "Duplicate wristband"));
            Object.DestroyImmediate(emitter.gameObject);
            tablet.RenderNow();
            Assert.IsEmpty(TabletFallbackRegistry.All);
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Read wristband"));
        }

        [Test]
        public void Identical_fallback_actions_share_one_button_until_the_last_owner_leaves()
        {
            var first = Make<UIButtonEmitter>("first");
            var second = Make<UIButtonEmitter>("second");
            first.Configure("explain_delay", fallbackLabel: "First wait");
            second.Configure("explain_delay", fallbackLabel: "Second wait");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            Assert.AreEqual(1, tablet.Buttons.Count(b => b.label.EndsWith("wait")));
            first.enabled = false;
            tablet.RenderNow();
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Second wait"));
            second.enabled = false;
            tablet.RenderNow();
            Assert.IsFalse(tablet.Buttons.Any(b => b.label.EndsWith("wait")));
        }

        [Test]
        public void Equal_labels_do_not_merge_different_targets_or_parameters()
        {
            var first = Make<UIButtonEmitter>("first");
            var second = Make<UIButtonEmitter>("second");
            var third = Make<UIButtonEmitter>("third");
            first.Configure("explain_delay", "patient", "reason", "a", "Wait");
            second.Configure("explain_delay", "equipment", "reason", "a", "Wait");
            third.Configure("explain_delay", "equipment", "reason", "b", "Wait");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            var buttons = tablet.Buttons.Where(b => b.label == "Wait").ToArray();
            Assert.AreEqual(3, buttons.Length);
            foreach (var button in buttons) button.button.onClick.Invoke();
            var content = ContentLoader.Load(UnityContentReader(), "VAR-S1-A");
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            foreach (var emitter in new[] { first, second, third }) reference.Apply(emitter.BuildAction());
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash(), "fallback must preserve target, channel and parameters");
            Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
        }

        [Test]
        public void Stale_fallback_clicks_cannot_fire_removed_or_reconfigured_actions()
        {
            var emitter = Make<UIButtonEmitter>("source");
            emitter.Configure("explain_delay", fallbackLabel: "Wait");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            var oldButton = tablet.Buttons.Single(b => b.label == "Wait").button;
            emitter.Configure("introduce_self", fallbackLabel: "Introduction");
            oldButton.onClick.Invoke(); // Registry changed before next UI update.
            Assert.AreEqual(0, _host.Session.Log.Count);
            emitter.Configure("explain_delay", fallbackLabel: "Wait");
            emitter.enabled = false;
            oldButton.onClick.Invoke();
            Assert.AreEqual(0, _host.Session.Log.Count);
        }

        [Test]
        public void World_and_dynamic_fallback_share_cooldown_and_core_rejection()
        {
            var emitter = Make<UIButtonEmitter>("source");
            emitter.Configure("not_a_registered_verb", fallbackLabel: "Rejected fixture");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            var button = tablet.Buttons.Single(b => b.label == "Rejected fixture").button;
            var rejected = emitter.Fire();
            Assert.IsFalse(rejected.Accepted);
            button.onClick.Invoke();
            button.onClick.Invoke();
            Assert.AreEqual(1, _host.Session.Log.Count);
            Assert.IsFalse(_host.Observable.IdentityConfirmed);
        }

        [UnityTest]
        public System.Collections.IEnumerator Fallback_buttons_refresh_without_core_ticks_and_after_scene_unload()
        {
            var tablet = Make<TabletView>("persistent tablet");
            tablet.RenderNow();
            var room = UnityEngine.SceneManagement.SceneManager.CreateScene("FallbackLifecycleFixture");
            var emitter = Make<XRSelectEmitter>("room emitter");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(emitter.gameObject, room);
            emitter.Configure("explain_delay", fallbackLabel: "Room wait");
            yield return null;
            Assert.IsTrue(tablet.Buttons.Any(b => b.label == "Room wait"));
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(room);
            yield return null;
            Assert.IsEmpty(TabletFallbackRegistry.All);
            Assert.IsFalse(tablet.Buttons.Any(b => b.label == "Room wait"));
            Assert.AreEqual(0, _host.Session.Log.Count);
        }

        [Test]
        public void Clearing_registry_invalidates_buttons_and_reenabled_owners_can_register_again()
        {
            var emitter = Make<UIButtonEmitter>("source");
            emitter.Configure("explain_delay", fallbackLabel: "Wait");
            var tablet = Make<TabletView>("tablet");
            tablet.RenderNow();
            TabletFallbackRegistry.Clear();
            tablet.RenderNow();
            Assert.IsFalse(tablet.Buttons.Any(b => b.label == "Wait"));
            emitter.enabled = false;
            emitter.enabled = true;
            tablet.RenderNow();
            Assert.AreEqual(1, tablet.Buttons.Count(b => b.label == "Wait"));
        }

        [Test]
        public void Programmatic_tablet_dispatch_preserves_physical_button_debounce()
        {
            var tablet = Make<TabletView>("tablet");
            var button = tablet.Buttons.Single(b => b.label == "Introduce yourself").button;
            Assert.IsTrue(tablet.Press("introduce_self"));
            Assert.AreEqual(0.35f, button.GetComponent<UIButtonEmitter>().DebounceSeconds);
            button.onClick.Invoke();
            Assert.AreEqual(1, _host.Session.Log.Count);
        }

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

#if UNITY_EDITOR
        [TestCase(false)]
        [TestCase(true)]
        public void Diagnostics_preview_gates_reports_and_never_changes_core(bool sample)
        {
            var preview = Make<DiagnosticsPreview>("diagnostics fixture");
            string state = _host.Observable.Hash(), log = _host.Session.Log.Hash();
            double time = _host.Session.Clock.SimSeconds;
            preview.SelectRoute(sample);
            Assert.IsFalse(preview.Interrupt());
            for (int i = 0; i < 3; i++)
            {
                StringAssert.DoesNotContain("SYNTHETIC REPORT", preview.ReportText);
                Assert.IsTrue(preview.Advance());
            }
            Assert.IsTrue(preview.Interrupt());
            Assert.IsFalse(preview.Interrupt());
            Assert.IsFalse(preview.Advance());
            StringAssert.DoesNotContain("SYNTHETIC REPORT", preview.ReportText);
            Assert.IsTrue(preview.Resume());
            Assert.IsFalse(preview.Resume());
            Assert.IsTrue(preview.Advance());
            StringAssert.Contains("SYNTHETIC REPORT", preview.ReportText);
            if (sample) StringAssert.Contains("bedside throughout", preview.LocationText);
            Assert.IsTrue(preview.Advance());
            Assert.IsFalse(preview.Advance());
            preview.SelectRoute(!sample);
            Assert.AreEqual(DiagnosticsPreview.Phase.Idle, preview.Current);
            StringAssert.DoesNotContain("SYNTHETIC REPORT", preview.ReportText);
            Assert.AreEqual(state, _host.Observable.Hash());
            Assert.AreEqual(log, _host.Session.Log.Hash());
            Assert.AreEqual(time, _host.Session.Clock.SimSeconds);
        }

        [Test]
        public void Diagnostics_preview_buttons_enforce_interrupted_and_reset_states()
        {
            var preview = Make<DiagnosticsPreview>("diagnostics fixture");
            var buttons = preview.GetComponentsInChildren<UnityEngine.UI.Button>();
            var advance = buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "Inject request");
            advance.onClick.Invoke(); advance.onClick.Invoke(); advance.onClick.Invoke();
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "Inject attention stop").onClick.Invoke();
            Assert.IsFalse(advance.interactable);
            advance.onClick.Invoke(); // Even a direct callback must not bypass the interruption.
            Assert.AreEqual(DiagnosticsPreview.Phase.Processing, preview.Current);
            buttons.Single(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "Reset fixture").onClick.Invoke();
            Assert.IsTrue(advance.interactable);
            Assert.IsFalse(preview.Interrupted);
            Assert.AreEqual(0, _host.Session.Log.Count);
        }

#endif
        [TestCase(XRSelectEmitter.SelectionRoute.Grab)]
        [TestCase(XRSelectEmitter.SelectionRoute.Socket)]
        [TestCase(XRSelectEmitter.SelectionRoute.Poke)]
        public void Xri_manager_routes_real_interactors_once_with_core_equivalence(XRSelectEmitter.SelectionRoute route)
        {
            var manager = Make<XRInteractionManager>("manager");
            var collider = Make<BoxCollider>("prop");
            XRBaseInteractable target = route == XRSelectEmitter.SelectionRoute.Poke
                ? (XRBaseInteractable)collider.gameObject.AddComponent<XRSimpleInteractable>()
                : collider.gameObject.AddComponent<XRGrabInteractable>();
            target.interactionManager = manager;
            var emitter = collider.gameObject.AddComponent<XRSelectEmitter>();
            emitter.Route = route;
            emitter.Configure("read_wristband", fallbackLabel: "Read wristband");
            Assert.AreEqual(1, collider.GetComponents<XRBaseInteractable>().Length);
            XRBaseInteractor source;
            if (route == XRSelectEmitter.SelectionRoute.Socket)
            {
                var socket = Make<XRSocketInteractor>("socket");
                emitter.RequiredSocket = socket;
                source = socket;
            }
            else if (route == XRSelectEmitter.SelectionRoute.Poke)
            {
                var pokeObject = new GameObject("poke");
                _made.Add(pokeObject);
                pokeObject.SetActive(false);
                var poke = pokeObject.AddComponent<XRPokeInteractor>();
                // This fixture tests world-object selection, not UI. Avoid XRI creating
                // an unowned persistent EventSystem which leaks into later scene tests.
                poke.enableUIInteraction = false;
                pokeObject.SetActive(true);
                source = poke;
            }
            else source = Make<XRDirectInteractor>("hand");
            source.interactionManager = manager;
            for (int i = 0; i < 3; i++) { emitter.enabled = false; emitter.enabled = true; }
            emitter.DebounceSeconds = 0f; // Expose duplicate listeners.
            manager.SelectEnter((IXRSelectInteractor)source, (IXRSelectInteractable)target);
            Assert.IsTrue(target.isSelected, "XRI must actually select the prop");
            Assert.AreEqual(1, _host.Session.Log.Count);
            manager.SelectExit((IXRSelectInteractor)source, (IXRSelectInteractable)target);
            Assert.AreEqual(1, _host.Session.Log.Count, "release is not a second clinical action");
            var content = ContentLoader.Load(UnityContentReader(), "VAR-S1-A");
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            reference.Apply(emitter.BuildAction());
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
            emitter.enabled = false;
            manager.SelectEnter((IXRSelectInteractor)source, (IXRSelectInteractable)target);
            Assert.AreEqual(1, _host.Session.Log.Count);
            manager.SelectExit((IXRSelectInteractor)source, (IXRSelectInteractable)target);
        }

        [Test]
        public void Socket_route_rejects_unassigned_wrong_socket_and_hand_selections()
        {
            var emitter = Make<XRSelectEmitter>("sensor");
            emitter.Configure("attach_sensor", "patient", "sensor", "spo2", "Attach SpO2 probe");
            emitter.Route = XRSelectEmitter.SelectionRoute.Socket;
            var socket = Make<XRSocketInteractor>("correct socket");
            var wrong = Make<XRSocketInteractor>("wrong socket");
            var hand = Make<XRDirectInteractor>("hand");
            var target = emitter.GetComponent<XRSimpleInteractable>();
            target.selectEntered.Invoke(new SelectEnterEventArgs { interactorObject = socket });
            Assert.AreEqual(0, _host.Session.Log.Count, "socket destination must be configured");
            emitter.RequiredSocket = socket;
            foreach (var source in new IXRSelectInteractor[] { hand, wrong })
                target.selectEntered.Invoke(new SelectEnterEventArgs { interactorObject = source });
            Assert.AreEqual(0, _host.Session.Log.Count);
            target.selectEntered.Invoke(new SelectEnterEventArgs { interactorObject = socket });
            Assert.AreEqual(1, _host.Session.Log.Count);
            Assert.IsNull(TabletFallbackRegistry.Fire(emitter.BuildAction()), "fallback shares cooldown");
        }

        [UnityTest]
        public System.Collections.IEnumerator Views_rebind_after_host_replacement_and_clear_a_previous_pause_hint()
        {
            var tablet = Make<TabletView>("surviving tablet");
            tablet.MinInterval = 0f;
            _host.SubsystemFactory = content => new ProcSense.Core.Pipeline.ISubsystem[] { new Boom() };
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Core threw, session paused"));
            _host.Apply(Vr("boom"));
            yield return null;
            Assert.AreEqual(HintTexts.For("SESSION_PAUSED"), tablet.HintText);
            Object.DestroyImmediate(_host.gameObject);
            _host = Make<SimulationHost>("replacement host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            yield return null;
            Assert.AreNotEqual(HintTexts.For("SESSION_PAUSED"), tablet.HintText);
            for (int i = 0; i < 3; i++) { tablet.enabled = false; tablet.enabled = true; }
            _host.Apply(Vr("ask_identity"));
            yield return null;
            Assert.IsNotEmpty(tablet.SubtitleText);
            Assert.AreEqual(1, _host.Session.Log.Count);
        }
    }
}
