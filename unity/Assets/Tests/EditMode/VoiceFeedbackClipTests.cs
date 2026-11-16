using System.IO;
using NUnit.Framework;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// P3-SND-08: every fixed voice-feedback phrase has a pre-rendered clip whose words are the words spoken in that mode (the phrase alone, or with the mode's extra sentence), the clip loads from Resources the way
    /// TeamConversation.SayFeedback loads it, a phrase with a placeholder stays live speech, and push-to-talk plays one listening cue per change of the recording state.
    /// </summary>
    public class VoiceFeedbackClipTests
    {
        private static string Json => File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content", "ui", "voice_feedback.json")));

        private static AudioClip Load(string clip) => Resources.Load<AudioClip>("Voice/" + Path.GetFileNameWithoutExtension(clip));

        [Test]
        public void Every_fixed_phrase_has_a_clip_in_both_modes_and_it_loads()
        {
            var t = VoiceFeedback.Parse(Json);
            foreach (var code in VoiceFeedback.Codes)
                foreach (var practice in new[] { true, false })
                {
                    var line = t.Resolve(code, practice);
                    string where = code + (practice ? " practice" : " examination");
                    if (code == VoiceFeedback.EngineRejected || code == VoiceFeedback.NeedsConfirmation) { Assert.IsNull(line.Clip, where + ": a placeholder phrase is live speech"); continue; }
                    Assert.IsNotNull(line.Clip, where);
                    Assert.IsNotNull(Load(line.Clip), where + ": " + line.Clip + " loads from Resources/Voice");
                }
        }

        [Test]
        public void The_clip_follows_the_extra_sentence_of_the_mode()
        {
            var t = VoiceFeedback.Parse(Json);
            StringAssert.EndsWith("VF-NOT_UNDERSTOOD-practice.ogg", t.Resolve(VoiceFeedback.NotUnderstood, true).Clip);   // the practice tip is in the clip
            StringAssert.EndsWith("VF-NOT_UNDERSTOOD.ogg", t.Resolve(VoiceFeedback.NotUnderstood, false).Clip);           // no tip in the examination
            StringAssert.EndsWith("VF-TIMEOUT.ogg", t.Resolve(VoiceFeedback.Timeout, true).Clip);                         // no extra sentence: the same clip in both modes
        }

        [Test]
        public void An_extra_sentence_without_its_clip_falls_back_to_live_speech()
        {
            const string json = "{\"schema\":\"procsense.voice_feedback/1\",\"outcomes\":[" +
                "{\"code\":\"NOT_UNDERSTOOD\",\"subtitle\":\"Again?\",\"clip\":\"a/VF-X.ogg\",\"practice_extra\":\"Tip.\",\"assessment_extra\":\"\"}," +
                "{\"code\":\"AMBIGUOUS\",\"subtitle\":\"a\"},{\"code\":\"WRONG_ADDRESSEE\",\"subtitle\":\"a\"},{\"code\":\"UNAVAILABLE_NOW\",\"subtitle\":\"a\"},{\"code\":\"ENGINE_REJECTED\",\"subtitle\":\"{reason_text}\"}," +
                "{\"code\":\"ASR_FAILED\",\"subtitle\":\"a\"},{\"code\":\"TIMEOUT\",\"subtitle\":\"a\"},{\"code\":\"NEEDS_CONFIRMATION\",\"subtitle\":\"a\"},{\"code\":\"SERVICE_OFFLINE\",\"subtitle\":\"a\"}," +
                "{\"code\":\"SCENE_LOAD_FAILED\",\"subtitle\":\"a\"},{\"code\":\"CONTENT_INTEGRITY\",\"subtitle\":\"a\"}]}";
            var t = VoiceFeedback.Parse(json);
            Assert.IsNull(t.Resolve(VoiceFeedback.NotUnderstood, true).Clip, "a clip without the tip would say less than the subtitle");
            Assert.AreEqual("a/VF-X.ogg", t.Resolve(VoiceFeedback.NotUnderstood, false).Clip);
        }

        [Test]
        public void Push_to_talk_plays_one_cue_when_listening_starts_and_one_when_it_stops()
        {
            Assert.IsNotNull(VoiceCues.Load(VoiceCues.ListenStart));
            Assert.IsNotNull(VoiceCues.Load(VoiceCues.ListenStop));
            Assert.AreNotEqual(VoiceCues.For(true), VoiceCues.For(false));
            var go = new GameObject("cues");
            try
            {
                var source = VoiceCues.MakeSource(go);
                Assert.AreEqual(0f, source.spatialBlend, "a flat cue, the same in both ears");
                Assert.IsFalse(source.playOnAwake);
                Assert.AreEqual(VoiceCues.ListenStart, VoiceCues.Play(source, true).name);
                Assert.AreEqual(VoiceCues.ListenStop, VoiceCues.Play(source, false).name);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
