using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-QA-02, menu-only half: the engine's golden scripts played through the app, every action going through the action gateway as a menu action (the door the contextual panel, the tablet
    /// and the menus use), give the same log hash as the Core golden and the same verdict, for S1 and S2 in training and in the examination. The voice-only half needs the fake recogniser of
    /// Ruhan's P3-VOX-12 and is not here yet.
    /// </summary>
    public class GoldenPlaythroughTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Golden(string name) => File.ReadAllText(Path.Combine(Repo, "tests", "golden", name));
        private static IContentReader Reader() => new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content"));

        private SimulationHost _host;

        [SetUp]
        public void SetUp()
        {
            foreach (var old in Object.FindObjectsByType<SimulationHost>(FindObjectsSortMode.None)) Object.DestroyImmediate(old.gameObject);
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "golden_playthrough");
            _host = new GameObject("golden host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host.gameObject);
            SimulationHost.SessionsDirectoryOverride = null;
        }


        /// <summary>P3-INT-10: the goals a pure Core run of the script completes (the reference the app's run must equal).</summary>
        internal static System.Collections.Generic.List<string> CoreGoals(string script, string caseId, ulong seed, SessionMode mode, ProcSense.Core.Content.ContentPackage content, SessionOptions options)
        {
            var s = new Session(content.Cases[caseId], seed, mode, Subsystems.Default(content), options);
            SessionFile.Play(s, ActionScript.Parse(script));
            return s.GraphHistory.Where(e => e.Kind == "goal_complete").Select(e => e.Id).OrderBy(x => x).ToList();
        }

        internal static System.Collections.Generic.List<string> Goals(Session s) => s.GraphHistory.Where(e => e.Kind == "goal_complete").Select(e => e.Id).OrderBy(x => x).ToList();

        private Session Play(string script, string caseId, ulong seed, SessionMode mode, SessionOptions options)
        {
            _host.StartSession(caseId, seed, mode, Reader(), options);
            foreach (var a in ActionScript.Parse(script))
            {
                _host.Tick(a.T - _host.Session.Clock.SimSeconds);
                ActionGateway.Apply(_host, a.Action, ActionSource.Menu);
            }
            return _host.Session;
        }

        // the four playthroughs: S1 and S2, training and examination; where the repository has an examination golden it is used, otherwise the training script is played in the examination
        [TestCase("VAR-S1-A_correct", null)]
        [TestCase("VAR-S1-A_assessment", null)]
        [TestCase("VAR-S2-B_correct", null)]
        [TestCase("VAR-S2-B_correct", "assessment")]
        public void A_golden_played_through_the_menus_gives_the_cores_log_and_verdict(string golden, string modeOverride)
        {
            string script = Golden(golden + ".actions.json"), expected = Golden(golden + ".expected.json");
            var header = MiniJson.Parse(expected) as System.Collections.Generic.Dictionary<string, object>;
            string caseId = (string)header["case"];
            ulong seed = ulong.Parse((string)header["seed"]);
            var mode = (SessionMode)System.Enum.Parse(typeof(SessionMode), modeOverride ?? (string)header["mode"], true);
            var options = GoldenRun.Options(expected);

            var session = Play(script, caseId, seed, mode, options);
            var core = GoldenRun.Report(script, caseId, seed, mode, _host.Content, options);
            var app = ProcSense.Core.Scoring.Scorer.Score(session, _host.Content);
            Assert.AreEqual(core.Result, app.Result, golden + ": the verdict");
            Assert.AreEqual(core.EndState, app.EndState, golden + ": the end state");
            var goals = CoreGoals(script, caseId, seed, mode, _host.Content, options);
            CollectionAssert.AreEqual(goals, Goals(session), golden + ": every goal the engine completes is completed by menu (P3-INT-10)");
            Assert.IsNotEmpty(goals);
            if (modeOverride == null)
            {
                Assert.AreEqual((string)header["log_hash"], session.Log.Hash(), golden + ": the same log as the Core golden");
                var score = header["score"] as System.Collections.Generic.Dictionary<string, object>;
                Assert.AreEqual((string)score["result"], app.Result, golden + ": the golden's verdict");
            }
        }
    }
}
