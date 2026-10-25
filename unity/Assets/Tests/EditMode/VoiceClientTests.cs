using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Auth;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The voice client's own rules (plan p1-voice-input-s02): the service is polled every 30 seconds and one failed check means offline at once; offline refuses the microphone with a banner; "say again" listens again;
    /// the speaker is muted while the trainee talks. The microphone, the speaker and the Quest (T-VC1 to T-VC5, DC-08) are Sannan's.</summary>
    public class VoiceClientTests
    {
        private sealed class FakeMic : IMicrophone
        {
            public int Begins;
            public bool Begin(out int rate) { rate = 16000; Begins++; return true; }
            public float[] End() => new float[0];
        }

        private sealed class AnswerHttp : IHttp
        {
            public HttpResult Answer;
            public string LastUrl = "";
            public int LastTimeout;
            public IEnumerator Send(string method, string url, string body, System.Collections.Generic.IDictionary<string, string> headers, int timeoutSeconds, System.Action<HttpResult> done)
            {
                LastUrl = url; LastTimeout = timeoutSeconds;
                done(Answer);
                yield break;
            }
        }

        private static PushToTalk Talk(FakeMic mic) { var t = new GameObject("talk").AddComponent<PushToTalk>(); t.Mic = mic; return t; }

        [Test]
        public void One_failed_check_is_offline_at_once_and_a_good_one_brings_voice_back_and_checks_are_30_seconds_apart()
        {
            var state = new VoiceHealthState();
            var changes = new System.Collections.Generic.List<bool>();
            state.Changed += changes.Add;
            Assert.IsTrue(state.Due(0f));
            state.Report(true, 0f);
            Assert.IsFalse(state.Offline);
            Assert.IsFalse(state.Due(29.9f));
            Assert.IsTrue(state.Due(30f));
            state.Report(false, 30f);
            Assert.IsTrue(state.Offline);
            Assert.AreEqual(VoiceHealthState.OfflineBanner, state.Banner);
            state.Report(false, 60f);
            state.Report(true, 90f);
            CollectionAssert.AreEqual(new[] { true, false }, changes);                          // told once when it went, once when it came back
            Assert.AreEqual("", state.Banner);
        }

        [Test]
        public void The_check_asks_slash_health_with_a_short_timeout_and_reports_the_answer()
        {
            var http = new AnswerHttp { Answer = new HttpResult(200, "{}") };
            bool? reachable = null;
            var e = VoiceHealth.Check(http, "https://svc", ok => reachable = ok);
            while (e.MoveNext()) { }
            Assert.AreEqual("https://svc/health", http.LastUrl);
            Assert.LessOrEqual(http.LastTimeout, 3);
            Assert.IsTrue(reachable);
            http.Answer = new HttpResult(0, "", true);
            e = VoiceHealth.Check(http, "https://svc", ok => reachable = ok);
            while (e.MoveNext()) { }
            Assert.IsFalse(reachable);
        }

        [Test]
        public void Offline_refuses_the_microphone_with_the_banner()
        {
            var mic = new FakeMic();
            var t = Talk(mic);
            t.Offline = () => true;
            Assert.IsFalse(t.Press());
            Assert.AreEqual(VoiceHealthState.OfflineBanner, t.Status);
            Assert.AreEqual(0, mic.Begins);
            Assert.IsFalse(t.Recording);
        }

        [Test]
        public void Say_again_listens_again_and_the_speaker_is_told_when_recording_starts_and_stops()
        {
            var mic = new FakeMic();
            var t = Talk(mic);
            var states = new System.Collections.Generic.List<bool>();
            t.RecordingChanged += states.Add;
            Assert.IsTrue(t.SayAgain());
            Assert.IsTrue(t.Recording);
            Assert.AreEqual("Listening...", t.Status);
            Assert.IsFalse(t.SayAgain());                                                       // already listening
            t.Release();
            CollectionAssert.AreEqual(new[] { true, false }, states);
            Assert.AreEqual(1, mic.Begins);
        }

        [Test]
        public void The_speaker_is_muted_while_the_trainee_talks()
        {
            var player = new GameObject("speaker").AddComponent<UnityClipPlayer>();
            player.Source = player.gameObject.AddComponent<AudioSource>();
            player.SetMuted(true);
            Assert.IsTrue(player.Muted);
            Assert.IsTrue(player.Source.mute);
            player.SetMuted(false);
            Assert.IsFalse(player.Source.mute);
        }
    }
}
