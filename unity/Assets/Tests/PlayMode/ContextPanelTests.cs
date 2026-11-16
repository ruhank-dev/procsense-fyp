using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using ProcSense.VR.Interaction;
using ProcSense.VR.Settings;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-CTX-04/06/08, P3-VOX-03/04, P3-EXM-04/07: the contextual panel in a real session. Its rows' availability equals the engine's Check; a row sends the same request the tablet sends (tagged as
    /// a menu action); a refusal shows its reason in training and none in the examination; voice mode shows a reference card with no action buttons except Submit; Submit asks, then submits once.
    /// </summary>
    public class ContextPanelTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private GameObject _patient, _head;
        private InteractionModeSetting _mode;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            _mode = InteractionMode.Current;
            InteractionMode.Set(InteractionModeSetting.Menu);
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "ctx_tests");
            foreach (var old in Object.FindObjectsByType<SimulationHost>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);   // a leftover host of another test would take the panel's actions
            _host = new GameObject("ctx host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 11UL) { EndSessionOnSubmit = false };
            _flow.SignedIn();
            _head = new GameObject("head"); _head.transform.position = new Vector3(0, 1.6f, 0);
            _patient = GameObject.CreatePrimitive(PrimitiveType.Capsule); _patient.name = "patient"; _patient.transform.position = new Vector3(0, 1f, 1.2f);
            Interactable.Mark(_patient, "patient");
            ContextPanel.FlowSource = () => _flow;
            ContextPanel.HeadSource = () => _head.transform;
            ContextPanel.HostSource = () => _host;                 // another test's leftover host must not answer for this one
            ModeGate.SessionProvider = () => _host.Session;
        }

        [TearDown]
        public void TearDown()
        {
            if (ContextPanel.Open != null) Object.DestroyImmediate(ContextPanel.Open.gameObject);
            Object.DestroyImmediate(_patient); Object.DestroyImmediate(_head); Object.DestroyImmediate(_host.gameObject);
            InteractionMode.Set(_mode);
            ContextPanel.HostSource = () => SimulationHost.Instance;
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            ContextPanel.FlowSource = () => Object.FindAnyObjectByType<MenuFlowInstaller>()?.Flow;
            ContextPanel.HeadSource = () => Camera.main != null ? Camera.main.transform : null;
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private void Play(SessionMode mode)
        {
            Assert.IsTrue(_flow.ChooseMode(mode));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
        }

        private ContextPanel OpenOnPatient() => ContextPanel.Toggle(_patient.GetComponent<Interactable>());

        [Test]
        public void Availability_equals_the_engine_check_and_rows_send_the_tablets_request()
        {
            Play(SessionMode.Practice);
            var panel = OpenOnPatient();
            Assert.IsNotNull(panel);
            Assert.IsNotEmpty(panel.Rows);
            int greyed = 0;
            for (int g = 0; g < InteractionCatalogue.Current.Entities["patient"].Groups.Count; g++)
            {
                panel.ShowGroup(g);
                foreach (var row in panel.Rows.Where(r => !r.Entry.HasForm))
                {
                    var check = _host.Session.Check(new SimAction(row.Entry.Verb, "patient", Channel.Vr, new System.Collections.Generic.Dictionary<string, string>(row.Entry.Params)));
                    Assert.AreEqual(check.Accepted, row.Available, row.Entry.Id);
                    Assert.AreEqual(row.Available, row.Button.interactable);
                    if (!row.Available) { greyed++; StringAssert.Contains(HintTexts.For(check.Reason), row.Button.GetComponentInChildren<TMPro.TMP_Text>().text, "the reason in words"); }

                    var tablet = new GameObject("tablet button").AddComponent<UIButtonEmitter>();
                    tablet.ConfigureParams(row.Entry.Verb, row.Entry.Params);
                    var a = row.Button.GetComponent<ContextButtonEmitter>().BuildAction(); var b = tablet.BuildAction();
                    Assert.AreEqual((b.Verb, b.Target), (a.Verb, a.Target), row.Entry.Id);
                    CollectionAssert.AreEquivalent(b.Params, a.Params, "identical request: " + row.Entry.Id);
                    Object.DestroyImmediate(tablet.gameObject);
                }
            }
            TestContext.WriteLine("greyed at the start: " + greyed);
            Assert.IsNotNull(panel.SubmitButton, "the patient panel carries Submit");
            ContextPanel.Toggle(_patient.GetComponent<Interactable>());
            Assert.IsNull(ContextPanel.Open, "the second selection closes it");
        }

        [Test]
        public void A_press_goes_through_the_gateway_as_a_menu_action_and_a_refusal_says_why()
        {
            Play(SessionMode.Practice);
            ActionSource? source = null; bool? accepted = null;
            void Seen(ActionRequest req, GatewayOutcome o) { source = req.Source; accepted = o.Accepted; }
            ActionGateway.Answered += Seen;
            try
            {
                var panel = OpenOnPatient();
                var row = panel.Rows.First(r => !r.Entry.HasForm && r.Available && !r.Entry.Confirm);
                panel.Press(row);
                Assert.AreEqual(ActionSource.Menu, source);
                Assert.IsTrue(accepted);
                var panel2 = ContextPanel.Open;
                var refused = panel2.Rows.FirstOrDefault(r => !r.Entry.HasForm && !r.Available);
                if (refused != null) { refused.Available = true; panel2.Press(refused); Assert.IsFalse(accepted); Assert.IsNotEmpty(panel2.Status, "training says why"); }
            }
            finally { ActionGateway.Answered -= Seen; }
        }

        [Test]
        public void The_examination_greys_nothing_and_explains_nothing()
        {
            Play(SessionMode.Assessment);
            var panel = OpenOnPatient();
            for (int g = 0; g < InteractionCatalogue.Current.Entities["patient"].Groups.Count; g++)
            {
                panel.ShowGroup(g);
                Assert.IsTrue(panel.Rows.All(r => r.Available && r.Reason == ""), "no greying that reveals the order (P3-EXM-07)");
            }
            var refused = panel.Rows.First(r => !r.Entry.HasForm && !r.Entry.Confirm);
            for (int i = 0; i < 3 && (ContextPanel.Open == null || ContextPanel.Open.Status == ""); i++) ContextPanel.Open?.Press(ContextPanel.Open.Rows.First(r => r.Entry.Id == refused.Entry.Id));
            if (ContextPanel.Open != null && ContextPanel.Open.Status != "") Assert.AreEqual("Not now.", ContextPanel.Open.Status, "no reason in the examination");
        }

        [Test]
        public void Voice_mode_shows_a_reference_card_with_only_the_submit_exception()
        {
            Play(SessionMode.Practice);
            InteractionMode.Set(InteractionModeSetting.Voice);
            var panel = OpenOnPatient();
            Assert.IsTrue(panel.ReferenceCard);
            Assert.IsEmpty(panel.Rows, "no action list in voice mode");
            Assert.GreaterOrEqual(panel.Examples.Count, 3, "You can ask… with at least three examples");
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>().Select(b => b.name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Button Close", "Button Submit patient to OT" }, buttons.Where(n => !n.StartsWith("Button Dialog") && n != "Yes" && n != "Cancel").Distinct(), string.Join(", ", buttons));
        }

        [Test]
        public void Submit_asks_first_then_submits_once()
        {
            Play(SessionMode.Assessment);
            var panel = OpenOnPatient();
            panel.PressSubmit();
            Assert.IsNotNull(panel.Confirmation);
            Assert.AreEqual(MenuPhase.Playing, _flow.Phase, "nothing before the confirmation");
            var confirm = panel.Confirmation.Confirm;
            confirm.onClick.Invoke();
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
            Assert.IsTrue(_host.Session.Submitted);
            confirm.onClick.Invoke();                                                             // a second confirmation finds the case closed
            Assert.AreEqual(MenuPhase.Results, _flow.Phase);
        }
    }
}
