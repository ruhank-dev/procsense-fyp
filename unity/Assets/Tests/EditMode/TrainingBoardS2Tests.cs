using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Training;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-TRN-03's "S1 and S2 from data only" for the second procedure (TrainingBoardTests covers S1): an appendicectomy case builds its board from the session and the guide, with an objective,
    /// progress and S2 stages only, and the view draws it on its glass panel.
    /// </summary>
    public class TrainingBoardS2Tests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private SimulationHost _host;

        [TearDown]
        public void Reset()
        {
            if (_host != null) Object.DestroyImmediate(_host.gameObject);
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
        }

        [Test]
        public void An_S2_case_has_its_own_board_from_data()
        {
            _host = new GameObject("s2 host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S2-B", 7, SessionMode.Practice,
                new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))));
            ModeGate.SessionProvider = () => _host.Session;
            var guide = TrainingGuide.Load(new FileContentReader(Path.Combine(Repo, "content")));
            var m = TrainingBoardText.Build(_host.Session, guide);
            Assert.IsTrue(m.Visible);
            Assert.IsNotEmpty(m.Sections);
            Assert.IsNotEmpty(m.Objective);
            StringAssert.StartsWith("Goals 0 of ", m.Progress);
            Assert.IsFalse(m.AllSteps.Any(r => r.Id.StartsWith("S1.")), "an S2 case shows S2 stages only");
            Assert.IsTrue(m.AllSteps.Any(), "steps come from the engine and the guide");

            var view = new GameObject("board").AddComponent<TrainingBoardView>();
            try
            {
                view.Guide = guide;
                view.SessionProvider = () => _host.Session;
                view.Build();
                StringAssert.Contains(m.Objective, view.ObjectiveText);
                Assert.IsNotNull(view.Canvas.transform.Find("Glass"), "drawn on the kit's glass (P3-UIX-05)");
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }
    }
}
