using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.VR.Audio;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Flow;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Training;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// P3-SND-04/05, P3-MNU-10, P3-TRN-05: the menu theme plays only on the menu screens and never during a case, the results play their verdict's sting once, each screen change fires one hook,
    /// every kit and feedback event plays exactly one sound, a completed goal chimes and ticks once in training and not at all in the examination, and every clip the player names exists.
    /// </summary>
    public class SoundEventsTests
    {
        private SimulationHost _host;
        private MenuFlow _flow;
        private SoundPlayer _sound;

        [SetUp]
        public void SetUp()
        {
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = Path.Combine(Application.temporaryCachePath, "sound_tests");
            TestHostHygiene.DestroyLeftoverHost();                         // another test's host must not decide the mode
            _host = new GameObject("sound host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _flow = new MenuFlow(_host, UnityContentReader.ForThisPlatform(), () => 11UL) { EndSessionOnSubmit = false };
            if (SoundPlayer.Instance != null) Object.DestroyImmediate(SoundPlayer.Instance.gameObject);
            _sound = new GameObject("sound player").AddComponent<SoundPlayer>();
            _sound.FlowSource = () => _flow;
            ModeGate.SessionProvider = () => _host.Session;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sound.gameObject);
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            Object.DestroyImmediate(_host.gameObject);
            ModeChoice.Reset();
            SimulationHost.SessionsDirectoryOverride = null;
        }

        [Test]
        public void Music_plays_on_the_menus_never_during_a_case_and_the_results_sting_once()
        {
            _flow.SignedIn(); _sound.Step();
            Assert.IsTrue(_sound.MusicOn, "menu theme on the main menu");
            Assert.AreEqual("music_menu_loop", _sound.Music.clip.name);
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice)); _sound.Step();
            Assert.IsTrue(_sound.MusicOn);
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message); _sound.Step();
            Assert.IsFalse(_sound.MusicOn, "the briefing starts the case: no music");
            Assert.IsTrue(_flow.Begin()); _sound.Step();
            Assert.IsFalse(_sound.MusicOn, "never during a case");
            Assert.IsTrue(_flow.Submit(abandoned: true).Accepted); _sound.Step(); _sound.Step();
            Assert.IsFalse(_sound.MusicOn);
            Assert.AreEqual(1, _sound.Count("sting_not_completed"), "one sting for the result");
            _flow.BackToMenu(); _sound.Step();
            Assert.IsTrue(_sound.MusicOn, "back on the menu");
            CollectionAssert.AreEqual(new[] { "music_start", "open", "music_stop", "select", "open", "open", "music_start", "back" }, _sound.Hooks, string.Join(", ", _sound.Hooks));
        }

        [Test]
        public void Each_kit_event_plays_exactly_one_sound()
        {
            foreach (var id in new[] { KitEvents.Hover, KitEvents.Select, KitEvents.Open, KitEvents.Close, KitEvents.Confirm, KitEvents.Submit, KitEvents.Toggle, KitEvents.Toast, KitEvents.Error })
            {
                KitEvents.Raise(id);
                Assert.AreEqual(1, _sound.Count(id), id);
                Assert.IsNotNull(SoundPlayer.Clip(id), id + " is in the sound list");
            }
            var canvas = Kit.Canvas("t", null, new Vector2(800, 600), .9f);
            try
            {
                var dialog = Kit.Dialog(canvas.transform, "Sure?", "", "Yes", "No", Vector2.zero);
                dialog.Open();
                Assert.AreEqual(2, _sound.Count(KitEvents.Open), "a dialog opening: one more open");
                dialog.Confirm.onClick.Invoke();
                Assert.AreEqual(2, _sound.Count(KitEvents.Close));
                Assert.AreEqual(2, _sound.Count(KitEvents.Confirm));
            }
            finally { Object.DestroyImmediate(canvas.gameObject); }
            foreach (var id in new[] { "trn_board_open", "trn_board_close", "trn_step_chime", "exm_hint_reveal", "exm_submit_confirm", "exm_end", "tr_whoosh", "music_menu_loop", "sting_pass", "sting_not_pass", "sting_not_completed" })
                Assert.IsNotNull(SoundPlayer.Clip(id), id);
        }

        [Test]
        public void Staff_and_clothing_actions_play_their_foley()
        {
            _sound.OnFeedback(new SimFeedbackEvent(FeedbackKind.ActionAccepted, new[] { "tell_anaesthetist", "patient" }, "", 1));
            _sound.OnFeedback(new SimFeedbackEvent(FeedbackKind.ActionAccepted, new[] { "prepare", "patient" }, "", 2));
            Assert.AreEqual(1, _sound.Count("foley_rustle_coat"));
            Assert.AreEqual(1, _sound.Count("foley_rustle_scrubs"));
        }

        [Test]
        public void A_completed_goal_chimes_and_ticks_once_in_training_and_not_in_the_examination()
        {
            _flow.SignedIn();
            Assert.IsTrue(_flow.ChooseMode(SessionMode.Practice));
            Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
            Assert.IsTrue(_flow.Begin());
            var done = new GameObject("completion").AddComponent<CompletionFeedback>();
            done.Board = () => null; done.Head = () => null;
            try
            {
                var e = new SimFeedbackEvent(FeedbackKind.GoalCompleted, new[] { "identity_confirmed" }, "", 10);
                _sound.OnFeedback(e); done.OnFeedback(e);
                done.OnFeedback(e); done.OnFeedback(new SimFeedbackEvent(FeedbackKind.GoalCompleted, new[] { "identity_confirmed" }, "", 12));
                Assert.AreEqual(1, _sound.Count("trn_step_chime"));
                Assert.AreEqual(1, done.Toasts + done.Ticks, "one per engine completion, never more");
                StringAssert.StartsWith("Done: ", done.LastText);

                Assert.IsTrue(_flow.Submit(abandoned: true).Accepted);
                _flow.BackToMenu();
                Assert.IsTrue(_flow.ChooseMode(SessionMode.Assessment));
                Assert.IsTrue(_flow.ChooseSurgery("cholecystectomy", TrainingLevel.Beginner), _flow.Message);
                Assert.IsTrue(_flow.Begin());
                var exam = new SimFeedbackEvent(FeedbackKind.GoalCompleted, new[] { "consent_checked" }, "", 5);
                _sound.OnFeedback(exam); done.OnFeedback(exam);
                Assert.AreEqual(1, _sound.Count("trn_step_chime"), "no chime in the examination");
                Assert.AreEqual(1, done.Toasts + done.Ticks, "no tick in the examination");
            }
            finally { Object.DestroyImmediate(done.gameObject); }
        }
    }
}
