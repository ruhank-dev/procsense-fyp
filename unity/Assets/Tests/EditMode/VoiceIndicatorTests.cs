using NUnit.Framework;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-VOX-09 with a fake microphone: idle, listening while recording, recognising after release, the words echoed for 3 s, a failure shown, the 8 s time-out, the controller ring in step, and the near indicator inside the head-locked limits.</summary>
    public class VoiceIndicatorTests
    {
        private sealed class FakeMic : IMicrophone
        {
            public float[] Samples = new float[1600];
            public bool Begin(out int rate) { rate = 16000; return true; }
            public float[] End() => Samples;
        }

        private GameObject _go, _controller;
        private PushToTalk _talk;
        private VoiceIndicator _indicator;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ptt");
            _controller = new GameObject("controller");
            _talk = _go.AddComponent<PushToTalk>();
            _talk.Mic = new FakeMic();
            _talk.Auth = () => null;
            _indicator = _go.AddComponent<VoiceIndicator>();
            _indicator.Init(_talk, _controller.transform);
        }

        [TearDown] public void TearDown() { Object.DestroyImmediate(_go); Object.DestroyImmediate(_controller); }

        [Test]
        public void It_follows_the_recording_from_idle_to_listening_to_recognising()
        {
            Assert.AreEqual(KitListening.Mode.Idle, _indicator.State);
            Assert.IsFalse(_indicator.Visible, "nothing shows when idle");
            Assert.IsTrue(_talk.Press());
            Assert.AreEqual(KitListening.Mode.Listening, _indicator.State);
            Assert.AreEqual(KitListening.Mode.Listening, _indicator.OnController.State, "the controller ring in step");
            Assert.IsTrue(_indicator.Visible);
            _indicator.OnRecording(false);   // released with speech: the clip goes to the recogniser
            _indicator.OnStatus("Recognising...");
            Assert.AreEqual(KitListening.Mode.Processing, _indicator.State);
        }

        [Test]
        public void The_words_heard_are_echoed_for_three_seconds()
        {
            _indicator.OnRecording(true);
            _indicator.OnRecording(false);
            _indicator.OnStatus("Heard: check the blood pressure");
            Assert.AreEqual(KitListening.Mode.Idle, _indicator.State);
            StringAssert.Contains("check the blood pressure", _indicator.Echo.text);
            _indicator.Step(2.9f);
            Assert.IsTrue(_indicator.Visible);
            _indicator.Step(.2f);
            Assert.IsFalse(_indicator.Visible, "gone after 3 s");
        }

        [Test]
        public void Silence_and_failures_end_the_wait_with_their_text()
        {
            ((FakeMic)_talk.Mic).Samples = new float[1600];   // all zeros: nothing heard
            _talk.Press();
            _talk.Release();
            Assert.AreEqual(KitListening.Mode.Idle, _indicator.State);
            StringAssert.Contains("did not hear", _indicator.Echo.text);

            _indicator.OnRecording(true);
            _indicator.OnRecording(false);
            _indicator.OnFeedback(VoiceFeedback.Defaults().Resolve(VoiceFeedback.ServiceOffline, true));
            Assert.AreEqual(KitListening.Mode.Idle, _indicator.State);
            StringAssert.Contains("Voice is not available", _indicator.Echo.text);
        }

        [Test]
        public void It_never_waits_longer_than_eight_seconds()
        {
            _indicator.OnRecording(true);
            _indicator.OnRecording(false);
            _indicator.Step(7.9f);
            Assert.AreEqual(KitListening.Mode.Processing, _indicator.State);
            _indicator.Step(.2f);
            Assert.AreEqual(KitListening.Mode.Idle, _indicator.State);
            Assert.AreEqual(VoiceFeedback.Loaded.Resolve(VoiceFeedback.Timeout, true).Subtitle, _indicator.Echo.text);
        }

        [Test]
        public void The_near_indicator_keeps_the_head_locked_rules()
        {
            Assert.IsEmpty(Readability.HeadLockedProblems(VoiceIndicator.HeadOffset, .56f));
        }
    }
}
