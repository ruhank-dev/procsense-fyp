using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using ProcSense.VR.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Speaking as an alternative to tapping (DT-251, ADR-0027): the words become the same tablet action through the same engine path, so the state, the log and the score are the same as for a tapped action; the engine's
    /// refusal is respected; a repeated recognition is handled once; a local model only proposes; physical actions are never offered to the interpreter; the speech evaluation of the same sentence is applied once. The
    /// interpreter and the evaluator are replaced by scripted answers (their quality is measured in reports/ai/speech-actions-eval.json); the engine, the tablet and the conversation are the real ones.
    /// </summary>
    public class SpokenActionTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;
        private TabletView _tablet;
        private SpokenActionRouter _router;
        private TeamConversation _talk;

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        private SimulationHost NewHost()
        {
            var host = Make<SimulationHost>("host");
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            return host;
        }

        private void Rig()
        {
            TabletFallbackRegistry.Clear();
            _host = NewHost();
            _tablet = Make<TabletView>("tablet");
            _tablet.RenderNow();
            var go = new GameObject("voices");
            _made.Add(go);
            go.SetActive(false);
            var voices = go.AddComponent<TeamVoicesView>();
            _talk = go.AddComponent<TeamConversation>();
            _talk.Voices = voices; _talk.SpeakAloud = false; _talk.Auth = () => null; _talk.Host = () => _host;
            _router = go.AddComponent<SpokenActionRouter>();
            _router.Tablet = _tablet; _router.Talk = _talk; _router.Host = () => _host; _router.Auth = () => null;
            go.SetActive(true);
        }

        [TearDown]
        public void TearDown() { foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go); _made.Clear(); TabletFallbackRegistry.Clear(); }

        private static void Identify(SimulationHost host) { foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) host.Apply(new SimAction(v)); }

        private void Says(params string[] ids) => _router.Interpreter = (auth, text, available, turns, done) => Answer(done, ids, "patient", true);

        private static IEnumerator Answer(Action<SpokenActionResult> done, string[] ids, string addressee, bool cloud)
        {
            done(new SpokenActionResult { ActionIds = ids.ToList(), Addressee = addressee, FromService = true, Cloud = cloud });
            yield break;
        }

        private IEnumerator Speak(string text) { yield return _router.Handle(text); }

        [UnityTest]
        public IEnumerator A_spoken_question_changes_the_state_exactly_as_the_tablet_button_does()
        {
            Rig();
            Identify(_host);
            Assert.IsTrue(_tablet.Press("ask", "topic", "medicines"));                                              // the tablet path
            string tapped = _host.Session.Observable.Hash() + "|" + _host.Session.Log.ToJsonl();
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);                                              // the same session again, from the start
            Identify(_host);
            var spokenHost = _host;
            Says("ask:medicines");
            yield return Speak("What medications are you taking?");
            Assert.AreEqual(tapped, spokenHost.Session.Observable.Hash() + "|" + spokenHost.Session.Log.ToJsonl());       // the same state and the same log, byte for byte
            Assert.IsTrue(spokenHost.Session.Observable.DisclosedFacts.Any(f => f.Topic == "medicines"));
        }

        [UnityTest]
        public IEnumerator The_interpreter_is_offered_only_conversational_ids_that_are_enabled_and_nothing_physical_is_started()
        {
            Rig();
            Identify(_host);
            var offered = new List<string>();
            _router.Interpreter = (auth, text, available, turns, done) => { offered.AddRange(available); return Answer(done, new[] { "confirm_identity" }, "patient", true); };
            yield return Speak("I am happy with the identity.");
            CollectionAssert.Contains(offered, "ask:medicines");
            CollectionAssert.Contains(offered, "ask_allergies");
            // physical gestures such as attaching a sensor are tablet buttons too, but the service's closed list leaves them out; the ids are only an offer
            int before = _host.Session.Log.Count;
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new[] { "no_such_action" }, "patient", true);
            yield return Speak("do something");
            Assert.AreEqual(before, _host.Session.Log.Count);                                                       // an id with no enabled button starts nothing
        }

        [UnityTest]
        public IEnumerator The_engine_still_decides_a_spoken_ask_before_identity_is_refused_and_no_patient_answers()
        {
            Rig();                                                   // identity not confirmed
            Says("ask:medicines");
            yield return Speak("What tablets do you take?");
            Assert.IsFalse(_host.Session.Observable.DisclosedFacts.Any(f => f.Topic == "medicines"));
            Assert.IsNull(_talk.Last);                               // the patient does not answer a question the simulator refused
            StringAssert.Contains("did not allow", _router.Status);
        }

        [UnityTest]
        public IEnumerator A_repeated_recognition_of_the_same_sentence_is_handled_once()
        {
            Rig();
            Identify(_host);
            int calls = 0;
            _router.Interpreter = (auth, text, available, turns, done) => { calls++; return Answer(done, new[] { "ask:smoking" }, "patient", true); };
            Assert.IsTrue(_router.OnTranscribed("Do you smoke?"));
            Assert.IsFalse(_router.OnTranscribed("do you smoke? "));                                              // the same event again inside the window
            yield return null;
            yield return null;
            Assert.AreEqual(1, calls);
            Assert.AreEqual(1, _host.Session.Log.Entries.Count(e => e.Verb == "ask" && e.Params.ContainsKey("topic") && e.Params["topic"] == "smoking"));
        }

        [UnityTest]
        public IEnumerator A_follow_up_gets_the_conversation_so_far_and_a_sentence_with_two_actions_starts_both_in_order()
        {
            Rig();
            Identify(_host);
            IReadOnlyList<ContextTurn> seen = null;
            _router.Interpreter = (auth, text, available, turns, done) => { seen = turns.ToList(); return Answer(done, new[] { "ask:medicines" }, "patient", true); };
            yield return Speak("What medications are you taking?");
            _router.Interpreter = (auth, text, available, turns, done) => { seen = turns.ToList(); return Answer(done, new[] { "ask:herbal", "ask:smoking" }, "patient", true); };
            yield return new WaitForSeconds(0.05f);
            yield return Speak("And anything herbal, and do you smoke?");
            Assert.IsTrue(seen.Any(t => t.Speaker == "trainee" && t.Text.Contains("medications")));                 // the first turn is in the context of the second
            var asked = _host.Session.Log.Entries.Where(e => e.Verb == "ask").Select(e => e.Params["topic"]).ToList();
            CollectionAssert.AreEqual(new[] { "medicines", "herbal", "smoking" }, asked);
        }

        [UnityTest]
        public IEnumerator A_local_model_only_proposes_and_the_trainee_confirms()
        {
            Rig();
            Identify(_host);
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new[] { "ask:alcohol" }, "patient", false);
            int before = _host.Session.Log.Count;
            yield return Speak("And what about drink?");
            Assert.AreEqual(before, _host.Session.Log.Count);                                                       // nothing started by itself
            CollectionAssert.AreEqual(new[] { "ask:alcohol" }, _router.Suggestions);
            Assert.IsTrue(_router.Confirm("ask:alcohol"));
            Assert.IsTrue(_host.Session.Observable.DisclosedFacts.Any(f => f.Topic == "alcohol"));
            Assert.IsFalse(_router.Confirm("ask:alcohol"));                                                         // a suggestion is used once
        }

        [UnityTest]
        public IEnumerator An_answer_with_no_action_is_just_conversation_and_the_staff_voice_comes_only_from_the_service()
        {
            Rig();
            Identify(_host);
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new string[0], "nurse", true);
            int before = _host.Session.Log.Count;
            yield return Speak("Nurse, what are the latest observations?");
            yield return null;
            yield return null;
            Assert.AreEqual(before, _host.Session.Log.Count);                                                       // no action: the state is untouched
            Assert.IsNotNull(_talk.Last);                                                                          // and the conversation was asked (offline here: the patient's not-sure line)
        }

        [UnityTest]
        public IEnumerator An_action_that_is_not_a_history_topic_is_answered_with_the_engines_own_authored_line()
        {
            Rig();
            Identify(_host);
            Says("ask_allergies");
            yield return Speak("Do you have any allergies?");
            var disclosed = _host.Session.Observable.DisclosedFacts;
            Assert.IsTrue(disclosed.Any(f => f.Topic == "allergies"));
            Assert.AreEqual("engine", _talk.Last.Replies[0].Source);                                                 // the patient's line is the case's authored answer, not a model's
            Assert.AreEqual(disclosed.First(f => f.Topic == "allergies").Answer, _talk.Last.Replies[0].Text);
        }

        [UnityTest]
        public IEnumerator The_same_sentence_is_also_judged_for_communication_once_by_the_unchanged_speech_evaluation()
        {
            Rig();
            Identify(_host);
            int judged = 0;
            string idSeen = null;
            _router.Evaluator = (auth, host, text, id, turns, addressee, done) => { judged++; idSeen = id; done(SpeechEvaluation.SafeUnavailable()); return Empty(); };
            Says("ask:smoking");
            yield return Speak("Do you smoke?");
            yield return null;
            yield return null;
            Assert.AreEqual(1, judged);
            StringAssert.StartsWith("u1-", idSeen);
        }

        private static IEnumerator Empty() { yield break; }

        [UnityTest]
        public IEnumerator Assessment_gets_no_spoken_actions()
        {
            Rig();
            var content = ProcSense.Core.Content.ContentLoader.Load(new ProcSense.Core.Content.FileContentReader(System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "..", "content"))), "VAR-S1-A");
            var assessment = new Session(content.Cases["VAR-S1-A"], 1, SessionMode.Assessment, ProcSense.Core.Workflow.Subsystems.Default(content));
            ModeGate.SessionProvider = () => assessment;
            try
            {
                int calls = 0;
                _router.Interpreter = (auth, text, available, turns, done) => { calls++; return Answer(done, new[] { "ask:smoking" }, "patient", true); };
                yield return Speak("Do you smoke?");
                Assert.AreEqual(0, calls);
            }
            finally { ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null; }
        }

        [Test]
        public void The_nurse_reads_the_observations_from_the_engine_and_not_from_a_model()
        {
            Rig();
            Identify(_host);
            Assert.AreEqual("There are no readings on the monitor yet.", ObservationReport.Build(_host.Observable));
            _host.Apply(new SimAction("attach_sensor", "patient", Channel.Vr, new Dictionary<string, string> { ["sensor"] = "spo2" }));
            _host.Tick(5);
            string line = ObservationReport.Build(_host.Session.Observable);
            StringAssert.StartsWith("On the monitor now:", line);
            foreach (var v in _host.Session.Observable.Vitals) StringAssert.Contains(v.Value, line);
        }
    }
}
