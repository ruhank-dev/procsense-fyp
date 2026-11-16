using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.Core.Replay;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using ProcSense.VR.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-QA-02, voice-only half: the engine's golden scripts played by voice. Every step that speech may start (an enabled tablet button that is not a critical physical action) is spoken: the
    /// sentence goes to the real router with a fake recogniser and interpreter that hear exactly that step; the rest (the physical gestures and the forms with several fields) are done as gestures, as
    /// a trainee in voice mode would. No tablet press. The verdict and the end state equal the Core golden's, for S1 and S2 in training and in the examination.
    /// </summary>
    public class VoiceGoldenPlaythroughTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private static string Golden(string name) => File.ReadAllText(Path.Combine(Repo, "tests", "golden", name));
        private static IContentReader Reader() => new ProcSense.VR.Content.UnityContentReader(Path.Combine(Repo, "content"));

        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;
        private TabletView _tablet;
        private SpokenActionRouter _router;

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        [TearDown]
        public void TearDown() { foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go); _made.Clear(); TabletFallbackRegistry.Clear(); }

        private void Rig(string caseId, ulong seed, SessionMode mode, SessionOptions options)
        {
            TabletFallbackRegistry.Clear();
            TestHostHygiene.DestroyLeftoverHost();
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession(caseId, seed, mode, Reader(), options);
            _tablet = Make<TabletView>("tablet");
            _tablet.RenderNow();
            var go = new GameObject("voices");
            _made.Add(go);
            go.SetActive(false);
            var voices = go.AddComponent<TeamVoicesView>();
            var talk = go.AddComponent<TeamConversation>();
            talk.Voices = voices; talk.SpeakAloud = false; talk.Auth = () => null; talk.Host = () => _host;
            _router = go.AddComponent<SpokenActionRouter>();
            _router.Tablet = _tablet; _router.Talk = talk; _router.Host = () => _host; _router.Auth = () => null; _router.EvaluateSpeech = false;
            go.SetActive(true);
        }

        private static IEnumerator Heard(Action<SpokenActionResult> done, string id)
        {
            done(new SpokenActionResult { ActionIds = new List<string> { id }, Addressee = "patient", FromService = true, Cloud = true });
            yield break;
        }

        private static string IdOf(ProcSense.Core.Actions.SimAction a) => a.Params.Count == 0 ? a.Verb : a.Params.Count == 1 ? a.Verb + ":" + a.Params.Values.First() : null;

        [UnityTest]
        public IEnumerator Each_golden_played_by_voice_gives_the_cores_verdict()
        {
            var runs = new[] { ("VAR-S1-A_correct", (string)null), ("VAR-S1-A_assessment", null), ("VAR-S2-B_correct", null), ("VAR-S2-B_correct", "assessment") };
            var report = new List<string>();
            foreach (var (golden, modeOverride) in runs)
            {
                string script = Golden(golden + ".actions.json"), expected = Golden(golden + ".expected.json");
                var header = MiniJson.Parse(expected) as Dictionary<string, object>;
                string caseId = (string)header["case"];
                ulong seed = ulong.Parse((string)header["seed"]);
                var mode = (SessionMode)Enum.Parse(typeof(SessionMode), modeOverride ?? (string)header["mode"], true);
                var options = GoldenRun.Options(expected);
                Rig(caseId, seed, mode, options);

                int spoken = 0, gestures = 0;
                foreach (var step in ActionScript.Parse(script))
                {
                    _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                    string id = IdOf(step.Action);
                    bool speakable = id != null && _tablet.EnabledActionIds().Contains(id) && SpokenExamRules.StartableIds(new[] { id }).Count == 1;
                    if (speakable)
                    {
                        int before = _host.Session.Log.Count;
                        _router.Interpreter = (auth, text, available, turns, done) => Heard(done, id);
                        yield return _router.Handle("(spoken) " + id);
                        Assert.AreEqual(before + 1, _host.Session.Log.Count, golden + ": the spoken step '" + id + "' reached the engine once");
                        spoken++;
                    }
                    else { ActionGateway.Apply(_host, step.Action, ActionSource.Physical); gestures++; }
                }

                var core = GoldenRun.Report(script, caseId, seed, mode, _host.Content, options);
                var app = ProcSense.Core.Scoring.Scorer.Score(_host.Session, _host.Content);
                Assert.AreEqual(core.Result, app.Result, golden + " (" + mode + "): the verdict by voice");
                Assert.AreEqual(core.EndState, app.EndState, golden + " (" + mode + "): the end state by voice");
                CollectionAssert.AreEqual(GoldenPlaythroughTests.CoreGoals(script, caseId, seed, mode, _host.Content, options), GoldenPlaythroughTests.Goals(_host.Session), golden + ": every goal completed by voice (P3-INT-10)");
                Assert.Greater(spoken, 0, golden + ": something was spoken");
                report.Add(golden + " " + mode + ": " + spoken + " spoken, " + gestures + " gestures, " + app.Result);
                TearDown();
            }
            TestContext.WriteLine(string.Join("\n", report));
        }
    }
}
