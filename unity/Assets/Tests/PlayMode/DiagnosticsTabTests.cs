using System.Collections;
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
using ProcSense.Core.Workflow.Steps;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;
using ISubsystem = ProcSense.Core.Pipeline.ISubsystem;

namespace ProcSense.Tests.PlayMode
{
    public class DiagnosticsTabTests
    {
        private SimulationHost _host;
        private TabletView _tablet;
        private DiagnosticsTabView Tab => _tablet.DiagnosticsTab;
        private static SimAction Act(string verb, params (string key, string value)[] p) =>
            new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.key, x => x.value));

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("diagnostics host").AddComponent<SimulationHost>();
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _tablet = new GameObject("diagnostics tablet").AddComponent<TabletView>();
            _tablet.ShowTab(TabletView.DiagnosticsTabId); Tab.RenderNow();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tablet.gameObject);
            Object.DestroyImmediate(_host.gameObject);
        }

        [Test]
        public void Diagnostics_coexists_with_content_tabs_forms_and_session_without_overlapping_notes()
        {
            Assert.AreEqual(15, _tablet.TabIds.Count);
            CollectionAssert.AreEqual(new[] { TabletView.SessionTabId, TabletView.DiagnosticsTabId, TabletView.SettingsTabId }, _tablet.TabIds.Skip(12).ToArray());
            Assert.IsTrue(Tab.gameObject.activeInHierarchy);
            Assert.IsFalse(_tablet.Canvas.transform.Find("Clerking note").gameObject.activeSelf);
            Assert.IsFalse(_tablet.Canvas.transform.Find("Side list").gameObject.activeSelf);
            Assert.IsTrue(_tablet.Canvas.transform.Find("Hint").gameObject.activeSelf);
            _host.Apply(Act("order_investigation", ("test", "lft")));
            _tablet.RenderNow();
            Assert.IsNotEmpty(_tablet.HintText, "An external action refusal remains visible while Diagnostics is open");
            var hintRect = (RectTransform)_tablet.Canvas.transform.Find("Hint");
            var feedbackRect = (RectTransform)Tab.transform.Find("DiagnosticsFeedback");
            Assert.GreaterOrEqual(-hintRect.anchoredPosition.y, -feedbackRect.anchoredPosition.y + feedbackRect.sizeDelta.y,
                "The shared refusal hint must be below the pane feedback, with no overlap");

            var cancel = (RectTransform)Tab.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                .Single(b => b.GetComponentInChildren<TMPro.TMP_Text>()?.text == "Cancel transport").transform;
            var title = (RectTransform)Tab.transform.Find("Test");
            Rect Bounds(RectTransform rect) => new Rect(rect.anchoredPosition.x, -rect.anchoredPosition.y, rect.rect.width, rect.rect.height);
            foreach (var other in new[] { hintRect, feedbackRect, title })
                Assert.IsFalse(Bounds(cancel).Overlaps(Bounds(other)), "Cancel transport must not overlap " + other.name);

            _tablet.ShowTab("orders");
            Assert.IsFalse(Tab.gameObject.activeSelf);
            _tablet.Buttons.Single(b => b.label == "Request transport").button.onClick.Invoke();
            Assert.AreEqual("Request transport", _tablet.FormTitle);
            _tablet.ShowTab(TabletView.DiagnosticsTabId);
            Assert.AreEqual("", _tablet.FormTitle);
            Assert.IsTrue(Tab.gameObject.activeSelf);
            Assert.IsFalse(_tablet.Canvas.transform.Find("Form").gameObject.activeSelf);
            _tablet.ShowTab(TabletView.SessionTabId);
            Assert.IsTrue(_tablet.SessionTab.gameObject.activeSelf);
            Assert.IsFalse(Tab.gameObject.activeSelf);
            _tablet.ShowTab("admission");
            Assert.IsTrue(_tablet.Canvas.transform.Find("Clerking note").gameObject.activeSelf);
            Assert.IsTrue(_tablet.Canvas.transform.Find("Hint").gameObject.activeSelf);
            Assert.IsFalse(_tablet.SessionTab.gameObject.activeSelf);
        }

        private void Identity()
        {
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(_host.Apply(Act(verb)).Accepted, verb);
            Tab.RenderNow();
        }

        private void Click(string label)
        {
            var button = Tab.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b => b.name == "Button - " + label);
            Assert.IsTrue(button.interactable, label + " must be enabled");
            button.onClick.Invoke();
            Tab.RenderNow();
        }

        [Test]
        public void Orders_require_identity_and_double_clicks_do_not_duplicate_requests()
        {
            Tab.SelectTest("lft"); Click("Order test"); Click("Order test");
            Assert.AreEqual(1, _host.Session.Log.Count);
            Assert.IsEmpty(_host.Observable.Investigations);
            StringAssert.Contains("IDENTITY", Tab.FeedbackText);
            Assert.IsEmpty(_host.Observable.Investigations.SelectMany(o => o.Results));
        }

        [UnityTest]
        public IEnumerator Sample_loop_discloses_only_after_open_and_matches_direct_core()
        {
            var content = ContentLoader.Load(new FileContentReader(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content"))), "VAR-S1-A");
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            Identity();
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) reference.Apply(Act(verb));
            Tab.SelectTest("lft"); Click("Order test"); reference.Apply(Act("order_investigation", ("test", "lft")));
            yield return new WaitForSecondsRealtime(0.36f);
            Click("Tube"); // serum_gold, explicitly selected by the trainee
            Click("Collect sample / test");
            reference.Apply(Act("take_sample", ("tube", "serum_gold"), ("label", _host.SampleLabels[0]), ("test", "lft")));
            Assert.AreEqual("PROCESSING", _host.Observable.Investigations.Single().State);
            Assert.AreEqual(ProcSense.Core.State.PatientFactory.PatientBay, _host.Observable.Location, "Blood sampling must not transport the patient");
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
            yield return new WaitForSecondsRealtime(0.36f);
            Click("Wait 1 sim minute"); reference.Apply(Act("time_skip", ("minutes", "1")));
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
            _host.Tick(7200); reference.Tick(7200); Tab.RenderNow();
            Assert.AreEqual("RESULT_AVAILABLE", _host.Observable.Investigations.Single().State);
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
            yield return new WaitForSecondsRealtime(0.1f);
            Photograph("diagnostics-ready.png");
            yield return new WaitForSecondsRealtime(0.36f);
            Click("Open result"); reference.Apply(Act("view_result", ("test", "lft")));
            Assert.AreEqual("VIEWED", _host.Observable.Investigations.Single().State);
            Assert.IsNotEmpty(_host.Observable.Investigations.Single().Results);
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash());
            Assert.AreEqual(reference.Observable.Hash(), _host.Observable.Hash());
            StringAssert.Contains(_host.Observable.Investigations.Single().Results[0].Analyte, Tab.ReportText);
            yield return new WaitForSecondsRealtime(0.1f);
            Photograph("diagnostics-open.png");
        }

        [UnityTest]
        public IEnumerator Unlabelled_sample_is_rejected_and_can_be_reordered()
        {
            Identity(); Tab.SelectTest("lft"); Click("Order test");
            Click("Tube");
            for (int i = 0; i < _host.SampleLabels.Count && _host.SampleLabels[i] != "unlabelled"; i++) Click("Label");
            yield return new WaitForSecondsRealtime(0.36f);
            Click("Collect sample / test"); _host.Tick(7200); Tab.RenderNow();
            Assert.AreEqual("REJECTED", _host.Observable.Investigations.Single().State);
            Assert.IsEmpty(_host.Observable.Investigations.Single().Results);
            yield return new WaitForSecondsRealtime(0.36f);
            Click("Order test");
            Assert.AreEqual(2, _host.Observable.Investigations.Count);
            Assert.AreEqual("REQUESTED", _host.Observable.Investigations.Last().State);
        }

        private sealed class Attention : ISubsystem
        {
            private Session _s;
            public int Order => 9999;
            public void Init(Session session) => _s = session;
            public void OnAction(SimAction a, ActionOutcome o) { }
            public void OnTick() => _s.RequestAttention("test_attention");
        }

        [UnityTest]
        public IEnumerator Opening_a_critical_result_does_not_acknowledge_the_lab_call()
        {
            var reader = new FileContentReader(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content")));
            var fixture = ContentLoader.LoadCaseFile(reader, "SC-8");
            _host.SubsystemFactory = c => Subsystems.Default(c).Where(x => !(x is InvestigationsSubsystem) && !(x is ResultReviewSubsystem))
                .Concat(new ISubsystem[] { new InvestigationsSubsystem(c, fixture), new ResultReviewSubsystem(c, fixture) });
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Identity(); Tab.SelectTest("u_and_e"); Click("Order test"); Click("Tube");
            yield return new WaitForSecondsRealtime(0.36f); Click("Collect sample / test");
            _host.Tick(6000); Tab.RenderNow();
            Assert.AreEqual("RINGING", _host.Observable.Phone.State);
            yield return new WaitForSecondsRealtime(0.36f); Click("Open result");
            Assert.AreEqual("VIEWED", _host.Observable.Investigations.Single().State);
            Assert.AreEqual("RINGING", _host.Observable.Phone.State);
            yield return new WaitForSecondsRealtime(0.36f); Click("Acknowledge result");
            StringAssert.Contains("CALL_NOT_ANSWERED", Tab.FeedbackText);
            yield return new WaitForSecondsRealtime(0.36f); Click("Answer lab phone");
            Assert.AreEqual("ANSWERED", _host.Observable.Phone.State);
            yield return new WaitForSecondsRealtime(0.36f); Click("Acknowledge result");
            StringAssert.Contains("CALL_IDENTITY_FIRST", Tab.FeedbackText);
            Assert.AreEqual("VIEWED", _host.Observable.Investigations.Single().State);
        }

        private void Photograph(string filename)
        {
            var go = new GameObject("diagnostics snapshot camera");
            var camera = go.AddComponent<Camera>();
            var texture = new RenderTexture(1800, 1200, 24);
            var picture = new Texture2D(1800, 1200, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases();
                _tablet.RenderNow();
                var canvas = _tablet.Canvas;
                var rect = (RectTransform)canvas.transform;
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.transform.SetPositionAndRotation(canvas.transform.position - canvas.transform.forward, canvas.transform.rotation);
                camera.orthographicSize = rect.sizeDelta.y * rect.localScale.y * 0.52f;
                camera.aspect = 1.5f;
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                picture.ReadPixels(new Rect(0, 0, 1800, 1200), 0, 0);
                picture.Apply();
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Stage2DiagnosticsReview"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, filename), picture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Object.DestroyImmediate(go); Object.DestroyImmediate(texture); Object.DestroyImmediate(picture);
            }
        }

        [UnityTest]
        public IEnumerator Wait_stops_at_core_attention_and_requires_review_before_another_skip()
        {
            _host.SubsystemFactory = c => Subsystems.Default(c).Concat(new ISubsystem[] { new Attention() });
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Identity(); Tab.SelectTest("lft"); Click("Order test"); Click("Tube");
            yield return new WaitForSecondsRealtime(0.36f); Click("Collect sample / test");
            yield return new WaitForSecondsRealtime(0.36f); Click("Wait 1 sim minute");
            Assert.AreEqual(1, _host.Session.Clock.Steps);
            StringAssert.Contains("test_attention", Tab.FeedbackText);
            int count = _host.Session.Log.Count;
            Click("Wait 1 sim minute");
            Assert.AreEqual(count, _host.Session.Log.Count, "Reviewing an interruption must not skip time");
        }

        [Test]
        public void Session_replacement_clears_selection_feedback_and_old_reports()
        {
            Identity(); Tab.SelectTest("lft"); Click("Order test");
            _host.StartSession("VAR-S1-A", 43, SessionMode.Practice); Tab.RenderNow();
            Assert.AreEqual(_host.InvestigationCatalogue[0].Id, Tab.SelectedTest);
            Assert.IsEmpty(Tab.FeedbackText);
            StringAssert.Contains("Not ordered", Tab.StatusText);
            Assert.IsEmpty(_host.Observable.Investigations);
            Click("Order test"); Assert.AreEqual(1, _host.Session.Log.Count, "A new session gets a fresh debounce");
        }
    }
}
