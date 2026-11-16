using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.Views;
using ProcSense.VR.Voice;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-VOX-06, voice-only examination (decision D3): with the real engine, the real tablet and the real router, an examination is taken through identity, allergies and the whole consent talk by speech, with
    /// only the physical gestures speech may never start (wristband, notes, confirming identity, signing) done as gestures and no tablet menu press. The state, the log and the score equal the same run made with
    /// tablet presses, and two voice runs are identical. A refused spoken action shows the examination's neutral feedback, an unreachable interpreter shows the offline feedback, and a local proposal is confirmed by
    /// a spoken yes. The interpreter is replaced by a scripted answer (its quality is measured in reports/ai/speech-actions-eval.json); the headset run is Sannan's.
    /// </summary>
    public class SpokenExamVoiceOnlyTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;
        private TabletView _tablet;
        private SpokenActionRouter _router;
        private TeamConversation _talk;

        private static readonly string[] Gestures = { "read_wristband", "open_notes", "confirm_identity" };
        private static readonly string[] Script =
        {
            "introduce_self", "ask_identity", "|gestures|", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission",
            "consent_discussion:diagnosis", "consent_discussion:operation", "consent_discussion:benefits", "consent_discussion:risks", "consent_discussion:alternatives",
            "consent_discussion:no_treatment", "consent_discussion:matters", "check_capacity", "teach_back", "|sign|",
        };

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        private void Rig()
        {
            TabletFallbackRegistry.Clear();
            TestHostHygiene.DestroyLeftoverHost();
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Assessment);
            _tablet = Make<TabletView>("tablet");
            _tablet.RenderNow();
            var go = new GameObject("voices");
            _made.Add(go);
            go.SetActive(false);
            var voices = go.AddComponent<TeamVoicesView>();
            _talk = go.AddComponent<TeamConversation>();
            _talk.Voices = voices; _talk.SpeakAloud = false; _talk.Auth = () => null; _talk.Host = () => _host;
            _router = go.AddComponent<SpokenActionRouter>();
            _router.Tablet = _tablet; _router.Talk = _talk; _router.Host = () => _host; _router.Auth = () => null; _router.EvaluateSpeech = false;
            go.SetActive(true);
        }

        [TearDown]
        public void TearDown() { foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go); _made.Clear(); TabletFallbackRegistry.Clear(); }

        private static IEnumerator Answer(Action<SpokenActionResult> done, string[] ids, bool cloud = true, string reason = "")
        {
            done(new SpokenActionResult { ActionIds = ids.ToList(), Addressee = "patient", FromService = reason == "", Cloud = cloud, Reason = reason });
            yield break;
        }

        private void Gesture(string verb) => _host.Apply(new SimAction(verb, "", Channel.Vr));

        /// <summary>The run: every spoken step through the router (voice) or the tablet (menu); the gestures the same either way.</summary>
        private IEnumerator Play(bool voice)
        {
            foreach (var step in Script)
            {
                if (step == "|gestures|") { foreach (var g in Gestures) Gesture(g); continue; }
                if (step == "|sign|") { Gesture("sign_consent"); continue; }
                if (voice)
                {
                    string id = step;
                    _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new[] { id });
                    yield return _router.Handle("(spoken) " + id);
                }
                else Assert.IsTrue(_tablet.PressId(step), step);
            }
        }

        private string Fingerprint() => _host.Session.Observable.Hash() + "|" + _host.Session.Log.ToJsonl() + "|" + ProcSense.Core.Scoring.Scorer.Score(_host.Session, _host.Content).Total;

        [UnityTest]
        public IEnumerator Identity_allergy_and_consent_are_completed_by_speech_with_no_menu_press_and_score_as_the_menu_run()
        {
            Rig();
            Assert.IsFalse(_host.Session.PracticeAids);
            yield return Play(voice: true);
            Assert.IsTrue(_host.Session.Observable.IdentityConfirmed);                                                        // identity
            Assert.IsTrue(_host.Session.Observable.DisclosedFacts.Any(f => f.Topic == "allergies"));                         // allergy
            var signed = _host.Session.Log.Entries.Last(e => e.Verb == "sign_consent");
            Assert.IsTrue(signed.Accepted && signed.Effects.Contains("consent_signed"), signed.Reject);                         // consent, after the whole talk by speech
            Assert.AreEqual(Script.Length - 2, _router.Handled);                                                                // every conversational step went through speech
            Assert.AreEqual(0, _router.Suggestions.Count);
            string voiceRun = Fingerprint();

            _host.StartSession("VAR-S1-A", 42, SessionMode.Assessment);                                                       // the same examination with tablet presses
            yield return Play(voice: false);
            Assert.AreEqual(voiceRun, Fingerprint());                                                                           // same state, same log, same score

            _host.StartSession("VAR-S1-A", 42, SessionMode.Assessment);                                                       // and a second voice run
            yield return Play(voice: true);
            Assert.AreEqual(voiceRun, Fingerprint());                                                                           // deterministic
        }

        [UnityTest]
        public IEnumerator A_refused_spoken_step_in_the_examination_shows_the_neutral_feedback_and_no_reason()
        {
            Rig();                                                                                                             // identity not confirmed yet
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new[] { "ask:medicines" });
            yield return _router.Handle("What tablets do you take?");
            Assert.AreEqual(VoiceFeedback.EngineRejected, _router.LastFeedback.Code);
            Assert.AreEqual("That was not allowed now.", _router.LastFeedback.Subtitle);                                       // no refusal explanation in the examination
            Assert.AreSame(_router.LastFeedback, _talk.LastFeedback);                                                          // handed to the voice path
            StringAssert.Contains("did not allow", _router.Status);
        }

        [UnityTest]
        public IEnumerator An_unreachable_interpreter_gives_the_offline_feedback_and_the_examination_goes_on_by_menu()
        {
            Rig();
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new string[0], true, "NO_NETWORK");
            int before = _host.Session.Log.Count;
            yield return _router.Handle("Hello, I am the doctor.");
            Assert.AreEqual(VoiceFeedback.ServiceOffline, _router.LastFeedback.Code);
            Assert.AreEqual(before, _host.Session.Log.Count);                                                                  // nothing applied
            Assert.IsTrue(_tablet.PressId("introduce_self"));                                                                  // the menu still works
            Assert.AreEqual(before + 1, _host.Session.Log.Count);
        }

        [UnityTest]
        public IEnumerator A_local_proposal_is_read_back_and_confirmed_by_a_spoken_yes()
        {
            Rig();
            foreach (var g in new[] { "ask_identity" }) _host.Apply(new SimAction(g));
            foreach (var g in Gestures) Gesture(g);
            _router.Interpreter = (auth, text, available, turns, done) => Answer(done, new[] { "ask_allergies" }, cloud: false);
            yield return _router.Route("Any allergies?");
            Assert.AreEqual(VoiceFeedback.NeedsConfirmation, _router.LastFeedback.Code);
            StringAssert.Contains(_tablet.LabelOf("ask_allergies"), _router.LastFeedback.Subtitle);
            Assert.IsFalse(_host.Session.Observable.DisclosedFacts.Any(f => f.Topic == "allergies"));                         // nothing yet
            yield return _router.Route("Yes.");
            Assert.IsTrue(_host.Session.Observable.DisclosedFacts.Any(f => f.Topic == "allergies"));                          // the spoken yes confirmed it
            Assert.AreEqual(0, _router.Suggestions.Count);
        }
    }
}
