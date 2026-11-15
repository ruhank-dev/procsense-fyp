using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-MNU-11: through the glass panel's own buttons only, from the main menu to the bay for both procedures (S1 gallbladder, S2 appendix) in both modes: mode, procedure card, briefing with
    /// the patient's name, Begin, and the session running the drawn case in that mode. A mode with no approved case for a procedure shows the disabled card instead (D1).
    /// </summary>
    public class MenuToBayTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private FlowPanel _panel;
        private ulong _seed = 900;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "menu_bay_tests");
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            _host = new GameObject("bay host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
            var go = new GameObject("panel");
            _panel = go.AddComponent<FlowPanel>();
            _panel.Build(_flow, go.transform);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_panel.gameObject);
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            SimulationHost.SessionsDirectoryOverride = null;
        }

        /// <summary>The procedure of a case, from the generated catalogue (content/ui/case_catalogue.json).</summary>
        private static string ProcedureOf(string caseId)
        {
            var root = (System.Collections.Generic.Dictionary<string, object>)ProcSense.Core.Content.MiniJson.Parse(UnityContentReader.ForThisPlatform().ReadText("ui/case_catalogue.json"));
            foreach (System.Collections.Generic.Dictionary<string, object> c in (System.Collections.Generic.List<object>)root["cases"])
                if ((string)c["id"] == caseId) return (string)c["procedure"];
            return "";
        }

        [TestCase(SessionMode.Practice, "Gallbladder removal", "cholecystectomy")]
        [TestCase(SessionMode.Practice, "Appendectomy", "appendicectomy")]
        [TestCase(SessionMode.Assessment, "Gallbladder removal", "cholecystectomy")]
        [TestCase(SessionMode.Assessment, "Appendectomy", "appendicectomy")]
        public void From_the_menu_to_the_bay(SessionMode mode, string card, string procedure)
        {
            Assert.IsTrue(_panel.Click("Continue (signed in)"));
            Assert.AreEqual("ProcSense", _panel.TitleText);
            Assert.IsTrue(_panel.Click(mode == SessionMode.Practice ? "TRAINING MODE" : "TEST / EXAMINATION MODE"));
            string label = _panel.VisibleButtons().First(b => b.StartsWith(card));
            if (_flow.CaseCount(mode, procedure) == 0)
            {
                StringAssert.Contains("No cases are", label);
                Assert.IsFalse(_panel.Click(label), "disabled card");
                Assert.Inconclusive(mode + " has no approved " + procedure + " case yet (D1): the disabled card was checked instead");
            }
            Assert.IsTrue(_panel.Click(label), _flow.Message);
            Assert.AreEqual("Briefing", _panel.TitleText);
            StringAssert.Contains(_flow.Briefing.Name, _panel.BodyText);
            Assert.IsTrue(_panel.Click("Begin"));
            Assert.AreEqual(MenuPhase.Playing, _flow.Phase);
            Assert.AreEqual(mode, _host.Session.Mode);
            Assert.AreEqual(_flow.Plan.CaseId, _host.Session.Case.Id);
            Assert.AreEqual(procedure, ProcedureOf(_host.Session.Case.Id), "the drawn case is of the chosen procedure");
            Assert.IsFalse(_host.Session.Paused);
            _host.Tick(5.0);
            Assert.Greater(_host.Session.Clock.Steps, 0, "the bay's clock runs");
        }
    }
}
