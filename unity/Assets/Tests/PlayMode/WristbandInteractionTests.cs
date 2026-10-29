using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The wristband in VR and on the tablet (p0-vr-interaction-ui-s12): the grab and the tablet give the same log, which is the headless golden's; the mismatch, nurse
    /// and re-read path; the missing band and its allergy band; the identity banner. Uses the three identity fixtures of tests/fixtures (the golden suite's).
    /// </summary>
    public sealed class WristbandInteractionTests
    {
        private static readonly string Repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;
        private TabletView _tablet;

        private IdentityStatusView Banner => _tablet.GetComponent<IdentityStatusView>();

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        private void Start(string caseId)
        {
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Make<SimulationHost>("wristband host");
            _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            var reader = new CompositeContentReader(new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));
            _host.StartSession(caseId, 42, SessionMode.Practice, reader);
            _tablet = Make<TabletView>("wristband tablet");
            _tablet.Build();
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private static string Golden(string name) => File.ReadAllText(Path.Combine(Repo, "tests", "golden", name));

        // the golden script, with read_wristband by grab (an XRSelectEmitter on the band) or by the tablet button
        private string Play(string golden, string caseId, bool grab)
        {
            Start(caseId);
            var band = Make<XRSelectEmitter>("band");
            band.Configure("read_wristband", "patient", fallbackLabel: "Read wristband");
            band.DebounceSeconds = 0;
            foreach (var step in ActionScript.Parse(Golden(golden + ".actions.json")))
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                var p = step.Action.Params.FirstOrDefault();
                if (step.Action.Verb == "read_wristband" && grab) Assert.IsNotNull(band.Fire());
                else if (!_tablet.Press(step.Action.Verb, p.Key ?? "", p.Value ?? "")) _host.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, step.Action.Params));
            }
            return _host.Session.Log.Hash();
        }

        [TestCase("wristband_mismatch_resolved", "TEST-ID-MISMATCH")]
        [TestCase("wristband_missing_applied", "TEST-ID-MISSING-BAND")]
        [TestCase("wristband_missing_allergy", "TEST-ID-MISSING-BAND-ALLERGY")]
        public void Grab_and_tablet_give_the_same_log_and_the_headless_golden_state(string golden, string caseId)
        {
            string byGrab = Play(golden, caseId, true);
            string grabState = _host.Session.Observable.Hash();
            Cleanup();
            string byTablet = Play(golden, caseId, false);
            Assert.AreEqual(byGrab, byTablet, "the log does not know which way the band was read");
            Assert.AreEqual(grabState, _host.Session.Observable.Hash());
            var expected = (Dictionary<string, object>)MiniJson.Parse(Golden(golden + ".expected.json"));
            Assert.AreEqual((string)expected["state_hash"], _host.Session.Observable.Hash(), "the headless golden (p0-simulation-workflow-s18)");
        }

        [Test]
        public void A_mismatch_calls_the_nurse_the_banner_follows_her_and_then_asks_for_the_new_band()
        {
            Start("TEST-ID-MISMATCH");
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes" }) Assert.IsTrue(_tablet.Press(v));
            Banner.RenderNow();
            StringAssert.Contains("not confirmed", Banner.BannerText);
            Assert.AreEqual(IdentityStatusView.NotConfirmed, Banner.BannerColor);
            _tablet.Press("confirm_identity");
            Assert.AreEqual("ST1_MISMATCH", _host.Observable.LastRejection);

            _tablet.Press("report_mismatch");
            Assert.AreEqual("CALLED", Banner.NurseStep);
            StringAssert.Contains("Nurse called", Banner.BannerText);
            Banner.FinishVisit();
            Banner.RenderNow();
            StringAssert.Contains("read it again", Banner.BannerText);
            Assert.IsFalse(_host.Observable.IdentityConfirmed);

            _tablet.Press("read_wristband");
            _tablet.Press("confirm_identity");
            Assert.IsTrue(_host.Observable.IdentityConfirmed);
            Banner.RenderNow();
            StringAssert.Contains("Identity confirmed", Banner.BannerText);
            Assert.AreEqual(IdentityStatusView.Confirmed, Banner.BannerColor);
        }

        [Test]
        public void A_missing_band_says_so_and_the_nurse_applies_it_with_the_allergy_band()
        {
            Start("TEST-ID-MISSING-BAND-ALLERGY");
            var card = Make<IdentityCardView>("band card");
            _tablet.Press("read_wristband");
            Assert.AreEqual("WRISTBAND_MISSING", _host.Observable.LastRejection);
            Banner.RenderNow();
            StringAssert.Contains("No wristband", Banner.BannerText);

            _tablet.Press("request_wristband");
            Assert.AreEqual("VERIFY_IDENTITY_FIRST", _host.Observable.LastRejection, "the nurse comes once identity is checked another way");
            Banner.RenderNow();
            StringAssert.Contains("No wristband", Banner.BannerText, "the missing band stays on the banner after another refusal");
            Assert.IsTrue(_tablet.Press("ask_identity"));
            Assert.IsTrue(_tablet.Press("open_notes"));
            _tablet.Press("request_wristband");
            Assert.AreEqual("CALLED", Banner.NurseStep);
            Banner.FinishVisit();
            Banner.RenderNow();
            StringAssert.Contains("read it again", Banner.BannerText);
            _tablet.Press("read_wristband");
            card.RenderNow();
            StringAssert.Contains("Allergy band on", card.BodyText);
        }

        [Test]
        public void The_tablet_button_is_named_as_the_alternative_to_the_grab()
        {
            Start("TEST-ID-MISMATCH");
            StringAssert.Contains("tablet", _tablet.LabelOf("read_wristband"));
        }
    }
}
