using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-VOX-07 in Unity: the voice feedback table (content/ui/voice_feedback.json) is read, every outcome code has its phrase, the mode picks the extra sentence, the placeholders are filled, a missing or invalid
    /// file falls back to built-in phrases, and a recognition failure shows the phrase as the push-to-talk status (with the say-again button when the phrase invites a retry) and raises it for the voice path.
    /// </summary>
    public class VoiceFeedbackTests
    {
        private static string Json => File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "ui", "voice_feedback.json")));

        private static void Run(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0) { var top = stack.Peek(); if (!top.MoveNext()) { stack.Pop(); continue; } if (top.Current is IEnumerator inner) stack.Push(inner); }
        }

        [Test]
        public void The_shipped_table_has_a_phrase_for_every_outcome_code()
        {
            var t = VoiceFeedback.Parse(Json);
            Assert.IsTrue(t.FromFile);
            foreach (var code in VoiceFeedback.Codes)
            {
                var line = t.Resolve(code, true, "Identity not confirmed", "Ask about smoking");
                Assert.AreEqual(code, line.Code);
                Assert.IsNotEmpty(line.Subtitle, code);
                Assert.IsNotEmpty(line.Spoken, code);
                StringAssert.DoesNotContain("{", line.Subtitle, code);
            }
        }

        [Test]
        public void The_mode_picks_the_extra_sentence_and_the_placeholders_are_filled()
        {
            var t = VoiceFeedback.Parse(Json);
            StringAssert.EndsWith("Try a shorter sentence, or use the tablet.", t.Resolve(VoiceFeedback.NotUnderstood, true).Subtitle);                  // a practice tip
            Assert.AreEqual("Sorry, I did not understand that. Could you say it again?", t.Resolve(VoiceFeedback.NotUnderstood, false).Subtitle);        // no tip in the examination
            Assert.AreEqual("Identity not confirmed.", t.Resolve(VoiceFeedback.EngineRejected, true, "Identity not confirmed").Subtitle);
            Assert.AreEqual("That was not allowed now.", t.Resolve(VoiceFeedback.EngineRejected, false, "that was not allowed now").Subtitle);           // the examination's neutral refusal
            Assert.AreEqual("That cannot be done yet.", t.Resolve(VoiceFeedback.EngineRejected, true, "").Subtitle);                                     // the outcome's own fallback
            StringAssert.StartsWith("Please confirm: Ask about smoking. Say yes", t.Resolve(VoiceFeedback.NeedsConfirmation, true, null, "Ask about smoking").Subtitle);
            Assert.IsTrue(t.Resolve(VoiceFeedback.AsrFailed, true).Retry);
            Assert.AreEqual(VoiceFeedback.NotUnderstood, t.Resolve("NO_SUCH_CODE", true).Code);
        }

        [Test]
        public void A_missing_or_invalid_file_falls_back_to_built_in_phrases()
        {
            Assert.Throws<System.FormatException>(() => VoiceFeedback.Parse("{\"schema\":\"something/else\",\"outcomes\":[]}"));
            Assert.Throws<System.FormatException>(() => VoiceFeedback.Parse("{\"schema\":\"procsense.voice_feedback/1\",\"outcomes\":[{\"code\":\"TIMEOUT\",\"subtitle\":\"x\"}]}"));      // codes missing
            Assert.Throws<System.FormatException>(() => VoiceFeedback.Parse("[1,2]"));
            var d = VoiceFeedback.Defaults();
            Assert.IsFalse(d.FromFile);
            foreach (var code in VoiceFeedback.Codes) Assert.IsNotEmpty(d.Resolve(code, false, "x", "y").Subtitle, code);
            var saved = VoiceFeedback.Loaded;
            try
            {
                VoiceFeedback.Loaded = null;
                Assert.IsTrue(VoiceFeedback.Loaded.FromFile);                                                                                        // the editor reads the repository's file
            }
            finally { VoiceFeedback.Loaded = saved; }
        }

        [Test]
        public void An_interpreter_failure_maps_to_its_outcome_and_an_answer_maps_to_none()
        {
            Assert.AreEqual(VoiceFeedback.ServiceOffline, VoiceFeedback.CodeForReason("NO_NETWORK"));
            Assert.AreEqual(VoiceFeedback.ServiceOffline, VoiceFeedback.CodeForReason("NOT_SIGNED_IN"));
            Assert.AreEqual(VoiceFeedback.Timeout, VoiceFeedback.CodeForReason("HTTP_504"));
            Assert.AreEqual(VoiceFeedback.NotUnderstood, VoiceFeedback.CodeForReason("UNREADABLE"));
            Assert.IsNull(VoiceFeedback.CodeForReason(""));
        }

        [Test]
        public void A_recognition_failure_shows_the_phrase_offers_say_again_and_is_raised_for_the_voice_path()
        {
            var go = new GameObject("ptt");
            try
            {
                var t = go.AddComponent<PushToTalk>();
                t.Auth = () => null;                                                                                    // no sign-in: the recogniser cannot be reached
                VoiceFeedbackLine raised = null;
                t.FeedbackGiven += l => raised = l;
                Run(t.Send(new byte[64]));
                Assert.AreEqual(VoiceFeedback.ServiceOffline, raised.Code);
                Assert.AreEqual(raised.Subtitle, t.Status);
                Assert.IsFalse(t.CanRetry);
                t.FeedbackTable = VoiceFeedback.Parse(Json);
                Assert.AreEqual("I could not hear you clearly. Please try again, or use the tablet.", t.FeedbackTable.Resolve(VoiceFeedback.AsrFailed, false).Subtitle);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void A_feedback_phrase_is_spoken_in_the_team_voice_and_is_not_a_conversation_turn()
        {
            var go = new GameObject("talk");
            go.SetActive(false);
            try
            {
                var talk = go.AddComponent<TeamConversation>();
                talk.SpeakAloud = false;
                go.SetActive(true);
                var line = VoiceFeedback.Parse(Json).Resolve(VoiceFeedback.Timeout, false);
                talk.SayFeedback(line);
                Assert.AreSame(line, talk.LastFeedback);
                Assert.AreEqual(0, talk.Turns.Count);                                                                   // nothing goes to the dialogue service
                Assert.AreEqual("team", TtsClient.VoiceFor("team", "F"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
