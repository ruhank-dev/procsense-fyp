using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow.Steps;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ISubsystem = ProcSense.Core.Pipeline.ISubsystem;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    public sealed class DiagnosticWaitOverlayTests
    {
        private SimulationHost _host;
        private TabletView _tablet;
        private DiagnosticWaitOverlay Card => _tablet.GetComponent<DiagnosticWaitOverlay>();
        private static SimAction Act(string verb, params (string key, string value)[] args) =>
            new SimAction(verb, "patient", Channel.Vr, args.ToDictionary(p => p.key, p => p.value));
        [SetUp] public void Setup()
        {
            _host = new GameObject("wait host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _tablet = new GameObject("wait tablet").AddComponent<TabletView>();
        }
        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(_tablet.gameObject);
            Object.DestroyImmediate(_host.gameObject);
        }
        private void Identity()
        {
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" })
                Assert.IsTrue(_host.Apply(Act(verb)).Accepted);
        }
        private void Blood()
        {
            Identity();
            Assert.IsTrue(_host.Apply(Act("order_investigation", ("test", "lft"))).Accepted);
            Assert.IsTrue(_host.Apply(Act("take_sample", ("test", "lft"), ("tube", "serum_gold"), ("label", "bedside"))).Accepted);
            Card.RenderNow();
        }
        [Test] public void Transport_timing_uses_only_Core_published_arrival_and_does_not_guess_a_return()
        {
            Identity();
            Assert.IsTrue(_host.Apply(Act("request_transport", ("to", "ct"), ("mode", "wheelchair"), ("escort", "porter,nurse"))).Accepted);
            Card.RenderNow(); StringAssert.Contains("not published", Card.TimingText);
            for (int i = 0; i < 12000 && _host.Observable.Transport.State != "IN_TRANSIT"; i++) _host.Tick(.5);
            Assert.AreEqual("IN_TRANSIT", _host.Observable.Transport.State);
            Card.RenderNow(); StringAssert.Contains(_host.Observable.Transport.Arrives, Card.TimingText);
            for (int i = 0; i < 12000 && _host.Observable.Transport.State != "ARRIVED"; i++) _host.Tick(.5);
            Assert.AreEqual("ARRIVED", _host.Observable.Transport.State);
            Card.RenderNow(); StringAssert.Contains("Return time not available", Card.TimingText);
            StringAssert.Contains(_host.Content.Locations.Find("ct").Label, Card.LocationText);
        }
        [UnityTest] public IEnumerator Processing_and_ready_notifications_persist_across_tabs_without_disclosing_results()
        {
            Blood(); Assert.IsTrue(Card.Canvas.gameObject.activeSelf);
            StringAssert.Contains(_host.Observable.Investigations.Single().Ready, Card.TimingText);
            _tablet.ShowTab("ask"); yield return null;
            Assert.IsTrue(Card.Canvas.gameObject.activeInHierarchy);
            _host.Tick(7200); Card.RenderNow();
            Assert.AreEqual("RESULT_AVAILABLE", _host.Observable.Investigations.Single().State);
            StringAssert.Contains("1 result ready", Card.AlertText);
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
            string log = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            for (int i = 0; i < 20; i++) Card.RenderNow();
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            Photograph("result-ready.png");
            Assert.IsTrue(_host.Apply(Act("view_result", ("test", "lft"))).Accepted);
            Card.RenderNow(); Assert.IsFalse(Card.AlertText.Contains("result ready"));
        }
        [UnityTest] public IEnumerator Reading_a_critical_result_does_not_silence_the_call_notice_or_acknowledge_it()
        {
            var reader = new FileContentReader(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content")));
            var fixture = ContentLoader.LoadCaseFile(reader, "SC-8");
            _host.SubsystemFactory = c => Subsystems.Default(c).Where(x => !(x is InvestigationsSubsystem) && !(x is ResultReviewSubsystem))
                .Concat(new ISubsystem[] { new InvestigationsSubsystem(c, fixture), new ResultReviewSubsystem(c, fixture), new Attention() });
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Identity();
            Assert.IsTrue(_host.Apply(Act("order_investigation", ("test", "u_and_e"))).Accepted);
            Assert.IsTrue(_host.Apply(Act("take_sample", ("test", "u_and_e"), ("tube", "serum_gold"), ("label", "bedside"))).Accepted);
            _host.Tick(6000); Card.RenderNow();
            Assert.AreEqual("RINGING", _host.Observable.Phone.State);
            StringAssert.Contains("result ready", Card.AlertText);
            StringAssert.Contains("Lab phone ringing", Card.AlertText);
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
            Assert.IsTrue(_host.Apply(Act("time_skip", ("minutes", "1"))).Accepted);
            Card.RenderNow();
            StringAssert.Contains("Wait stopped", Card.AlertText);
            StringAssert.Contains("result ready", Card.AlertText);
            StringAssert.Contains("Lab phone ringing", Card.AlertText);
            Assert.IsTrue(_host.Apply(Act("view_result", ("test", "u_and_e"))).Accepted);
            Card.RenderNow();
            StringAssert.Contains("Lab phone ringing", Card.AlertText);
            Assert.IsFalse(Card.AlertText.Contains("result ready"));
            string log = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            yield return null; Photograph("critical-call-after-reading.png");
            Assert.AreEqual(log, _host.Session.Log.Hash()); Assert.AreEqual(state, _host.Observable.Hash());
            var refusal = _host.Apply(Act("acknowledge_result", ("test", "u_and_e")));
            Assert.IsFalse(refusal.Accepted); Assert.AreEqual("CALL_NOT_ANSWERED", refusal.RejectReason);
        }
        private sealed class Attention : ISubsystem
        {
            private Session _session;
            public int Order => 9999;
            public void Init(Session session) => _session = session;
            public void OnAction(SimAction action, ActionOutcome outcome) { }
            public void OnTick() => _session.RequestAttention("explicit_test_attention");
        }
        [UnityTest] public IEnumerator A_skip_reports_actual_clock_and_Core_attention_without_finishing_the_requested_minute()
        {
            _host.SubsystemFactory = c => Subsystems.Default(c).Concat(new ISubsystem[] { new Attention() });
            _host.StartSession("VAR-S1-A", 43, SessionMode.Practice);
            var outcome = _host.Apply(Act("time_skip", ("minutes", "1")));
            Assert.IsTrue(outcome.Accepted); Assert.AreEqual(1, _host.Session.Clock.Steps);
            Card.RenderNow(); StringAssert.Contains(_host.Observable.ClockTime, Card.AdvanceText);
            StringAssert.Contains("explicit_test_attention", Card.AlertText);
            Assert.AreEqual(1, _host.Session.Log.Count);
            yield return null; Photograph("wait-stopped.png");
        }
        /// <summary>The real content with a different wait (wait_overlay.seconds), as a content author would set it.</summary>
        private sealed class Wait : IContentReader
        {
            private readonly IContentReader _inner = ProcSense.VR.Content.UnityContentReader.ForThisPlatform();
            public string ReadText(string path)
            {
                var text = _inner.ReadText(path);
                return path == "diagnostics/diagnostics.json" ? text.Replace("\"seconds\": 10,", "\"seconds\": 1,") : text;
            }
        }

        [UnityTest] public IEnumerator The_time_advanced_note_stays_for_the_content_wait_and_then_goes()   // p0-diagnostic-transitions-s06: the on-screen wait comes from content
        {
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice, new Wait());
            Identity();
            Assert.IsTrue(_host.Apply(Act("time_skip", ("minutes", "5"))).Accepted);
            yield return null;
            Assert.AreEqual(1f, Card.AdvanceSeconds, "the wait comes from content, no code change");
            StringAssert.Contains("Time advanced", Card.AdvanceText);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return null;
            Assert.AreEqual("", Card.AdvanceText, "gone after the content's wait");
        }
        [Test] public void New_session_clears_old_ready_and_skip_notifications()
        {
            Blood(); _host.Tick(7200); Card.RenderNow();
            StringAssert.Contains("ready", Card.AlertText);
            _host.Apply(Act("time_skip", ("minutes", "1"))); Assert.IsNotEmpty(Card.AdvanceText);
            _host.StartSession("VAR-S1-A", 44, SessionMode.Practice); Card.RenderNow();
            Assert.IsEmpty(Card.AdvanceText); Assert.IsEmpty(Card.AlertText);
            Assert.IsFalse(Card.Canvas.gameObject.activeSelf);
        }
        [Test] public void Disabled_card_unsubscribes_and_reenabling_shows_current_state_only()
        {
            Card.enabled = false;
            _host.Apply(Act("time_skip", ("minutes", "1")));
            Assert.IsEmpty(Card.AdvanceText);
            Card.enabled = true; Card.RenderNow(); Assert.IsEmpty(Card.AdvanceText);
            _host.Apply(Act("time_skip", ("minutes", "1"))); Assert.IsNotEmpty(Card.AdvanceText);
        }
        [UnityTest] public IEnumerator Host_loss_hides_the_card_and_replacement_does_not_inherit_notices()
        {
            Blood(); Assert.IsTrue(Card.Canvas.gameObject.activeSelf);
            Object.DestroyImmediate(_host.gameObject); yield return null;
            Assert.IsFalse(Card.Canvas.gameObject.activeSelf);
            _host = new GameObject("replacement wait host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 45, SessionMode.Practice);
            yield return null; Card.RenderNow();
            Assert.IsEmpty(Card.AlertText); Assert.IsEmpty(Card.AdvanceText);
            Assert.IsFalse(Card.Canvas.gameObject.activeSelf);
        }
        [UnityTest] public IEnumerator Card_is_separate_from_tablet_controls_and_does_not_capture_pointer_rays()
        {
            Blood(); yield return null; Canvas.ForceUpdateCanvases();
            foreach (var graphic in Card.Canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) Assert.IsFalse(graphic.raycastTarget);
            foreach (var raycaster in Card.Canvas.GetComponents<UnityEngine.EventSystems.BaseRaycaster>()) Assert.IsFalse(raycaster.enabled);
            var tabletRect = (RectTransform)_tablet.Canvas.transform;
            var cardRect = (RectTransform)Card.Canvas.transform;
            float tabletBottom = tabletRect.position.y - tabletRect.rect.height * tabletRect.lossyScale.y / 2;
            float cardTop = cardRect.position.y + cardRect.rect.height * cardRect.lossyScale.y / 2;
            Assert.Less(cardTop, tabletBottom);
            Photograph("processing.png");
        }
        private void Photograph(string name)
        {
            _tablet.RenderNow(); Card.RenderNow(); Canvas.ForceUpdateCanvases();
            var go = new GameObject("wait snapshot camera"); var camera = go.AddComponent<Camera>();
            var target = new RenderTexture(1800, 1500, 24);
            var picture = new Texture2D(1800, 1500, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.orthographic = true; camera.orthographicSize = .25f; camera.aspect = 1.2f;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.transform.position = _tablet.Canvas.transform.position + new Vector3(0, -.055f, -1);
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, 1800, 1500), 0, 0); picture.Apply();
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "WaitOverlayReview"));
                Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name), picture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(target); Object.DestroyImmediate(picture);
            }
        }
    }
}
