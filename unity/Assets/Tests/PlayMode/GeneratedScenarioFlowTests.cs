using System.Collections;
using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using ProcSense.VR.Sync;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-SCN-06: in training the trainee may choose generated cases; the briefing then fetches a scenario package of the procedure's family and level and briefs it with its source, or keeps the
    /// authored case and says why when the service cannot give one (offline). The examination never offers it.
    /// </summary>
    public class GeneratedScenarioFlowTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Package => File.ReadAllText(Path.Combine(Repo, "tests", "fixtures", "generated", "GEN-S1-0003-001.package.json"));
        private static IContentReader Real() => new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private SimulationHost _host;
        private MenuFlow _flow;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            TestHostHygiene.DestroyLeftoverHost();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "gen_flow_tests");
            _host = new GameObject("gen host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 11UL) { EndSessionOnSubmit = false };
            _flow.SignedIn();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        private void ToBriefing(SessionMode mode, CaseSource source)
        {
            Assert.IsTrue(_flow.ChooseMode(mode));
            _flow.SetSource(source);
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
        }

        [UnityTest]
        public IEnumerator Online_the_briefing_plays_the_generated_package_and_says_so()
        {
            string asked = null;
            _flow.Fetch = (family, level, seed, done) =>
            {
                asked = family + "/" + level;
                string hash = _host.StartFromPackage(Package, seed, SessionMode.Practice, Real());
                done(new PackageStart { Started = true, Hash = hash, CaseId = new ScenarioPackageReader(Package).CaseId });
                return Nothing();
            };
            ToBriefing(SessionMode.Practice, CaseSource.Generated);
            yield return _flow.Generate();
            Assert.AreEqual("S1/beginner", asked, "the procedure's family and the chosen level");
            Assert.AreEqual(CaseSource.Generated, _flow.Plan.Source);
            Assert.AreEqual(_host.Session.Case.Id, _flow.Plan.CaseId);
            StringAssert.StartsWith("Generated scenario GEN-", _flow.SourceLine);
            Assert.IsTrue(_flow.Begin(), "and it plays");
        }

        [UnityTest]
        public IEnumerator Offline_the_authored_case_plays_and_the_briefing_says_why()
        {
            _flow.Fetch = (family, level, seed, done) => { done(new PackageStart { Message = "Cannot reach the service right now.", PlayAuthoredInstead = true }); return Nothing(); };
            ToBriefing(SessionMode.Practice, CaseSource.Generated);
            var authored = _flow.Plan.CaseId;
            yield return _flow.Generate();
            Assert.AreEqual(CaseSource.Authored, _flow.Plan.Source);
            Assert.AreEqual(authored, _flow.Plan.CaseId, "the authored case is kept");
            StringAssert.EndsWith("The authored case is playing.", _flow.SourceLine);
            StringAssert.StartsWith("Cannot reach the service", _flow.SourceLine);
        }

        [UnityTest]
        public IEnumerator The_examination_never_asks_for_a_generated_case()
        {
            bool asked = false;
            _flow.Fetch = (family, level, seed, done) => { asked = true; return Nothing(); };
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Assessment));
            _flow.SetSource(CaseSource.Generated);
            Assert.AreEqual(CaseSource.Authored, _flow.Source, "not offered in the examination");
            if (_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner)) { yield return _flow.Generate(); Assert.AreEqual("Authored case", _flow.SourceLine); }
            Assert.IsFalse(asked);
        }

        private static IEnumerator Nothing() { yield break; }
    }
}
