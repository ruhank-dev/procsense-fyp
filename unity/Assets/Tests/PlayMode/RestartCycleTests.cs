using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-SES-07's acceptance check on Ruhan's flow: 20 start, finish and retry cycles without reloading leave no duplicated objects and no growth in the live object count,
    /// and each cycle starts a fresh session (the mode lock is free again on the menu).
    /// </summary>
    public class RestartCycleTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private ulong _seed = 500;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "cycle_tests");
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            _host = new GameObject("cycle host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => _seed++) { EndSessionOnSubmit = false };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            if (Directory.Exists(SimulationHost.SessionsDirectoryOverride)) Directory.Delete(SimulationHost.SessionsDirectoryOverride, true);
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private static int Live() => Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        [Test]
        public void Twenty_cycles_leave_no_growth()
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            int before = Live();
            Session previous = _host.Session;
            for (int i = 0; i < 20; i++)
            {
                Assert.IsTrue(i % 2 == 0 ? _flow.Retry() : _flow.NewScenario(), _flow.Message);
                Assert.AreNotSame(previous, _host.Session, "a fresh session each cycle");
                previous = _host.Session;
                Assert.IsTrue(_flow.Begin());
                Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
            }
            Assert.AreEqual(before, Live(), "no object left behind after 20 cycles");
            _flow.BackToMenu();
            Assert.AreEqual(MenuPhase.MainMenu, _flow.Phase);
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Assessment), "the mode is free again on the menu");
        }
    }
}
