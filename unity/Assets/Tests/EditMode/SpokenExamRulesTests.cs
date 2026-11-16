using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.VR.Flow;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-VOX-06 and P3-VOX-13 on the Unity side: speech is allowed in both modes, shows no hint text in the examination, never starts a critical physical action, and a spoken hint or submit is exactly the button's action with the same engine outcome.</summary>
    public class SpokenExamRulesTests
    {
        private static string Content => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "content"));

        private static Session New(SessionMode mode)
        {
            var c = ContentLoader.Load(new FileContentReader(Content), "VAR-S1-A");
            return new Session(c.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(c));
        }

        [Test]
        public void Speaking_is_allowed_in_both_modes_and_not_without_a_session()
        {
            Assert.IsTrue(SpokenExamRules.Allowed(New(SessionMode.Practice)));
            Assert.IsTrue(SpokenExamRules.Allowed(New(SessionMode.Assessment)));
            Assert.IsFalse(SpokenExamRules.Allowed(null));
        }

        [Test]
        public void The_examination_shows_no_hint_text_and_no_refusal_explanation()
        {
            var practice = New(SessionMode.Practice);
            var exam = New(SessionMode.Assessment);
            Assert.AreEqual("a hint", SpokenExamRules.HintToShow(practice, "a hint"));
            Assert.AreEqual("", SpokenExamRules.HintToShow(exam, "a hint"));
            Assert.AreEqual("Identity not confirmed", SpokenExamRules.RefusalText(practice, "IDENTITY_NOT_CONFIRMED"));
            Assert.AreEqual("that was not allowed now", SpokenExamRules.RefusalText(exam, "IDENTITY_NOT_CONFIRMED"));
        }

        [Test]
        public void Critical_physical_actions_are_never_startable_by_speech()
        {
            var kept = SpokenExamRules.StartableIds(new[] { "ask:smoking", "confirm_identity", "read_wristband", "attach_sensor", "ask_allergies", "report_mismatch", "measure_nibp" });
            CollectionAssert.AreEqual(new[] { "ask:smoking", "ask_allergies" }, kept);
        }

        [Test]
        public void The_spoken_catalogue_holds_no_critical_physical_action()
        {
            var text = File.ReadAllText(Path.Combine(Content, "ai", "spoken_actions.json"));
            var ids = System.Text.RegularExpressions.Regex.Matches(text, "\"id\": \"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Greater(ids.Count, 10);
            Assert.IsEmpty(ids.Where(SpokenExamRules.IsCriticalPhysical));
        }

        [TestCase(SessionMode.Practice)]
        [TestCase(SessionMode.Assessment)]
        public void A_spoken_hint_and_submit_are_the_buttons_action_with_the_same_engine_outcome(SessionMode mode)
        {
            foreach (var (phrase, button) in new[] { ("Can I have a hint?", new SimAction(ExamControl.RequestHint, "", Channel.Vr)), ("Submit the patient to OT.", new SimAction(ExamControl.SubmitPatient, "", Channel.Vr, new Dictionary<string, string>())) })
            {
                Assert.IsTrue(FlowVoiceIntents.TryMap(phrase, out var spoken), phrase);
                Assert.AreEqual((button.Verb, button.Target, button.Channel, button.Params.Count), (spoken.Verb, spoken.Target, spoken.Channel, spoken.Params.Count));
                var a = New(mode); var b = New(mode);
                var viaVoice = a.Apply(spoken);
                var viaMenu = b.Apply(button);
                Assert.AreEqual((viaMenu.Accepted, viaMenu.RejectReason), (viaVoice.Accepted, viaVoice.RejectReason));
                CollectionAssert.AreEqual(viaMenu.Effects, viaVoice.Effects);
                Assert.AreEqual(b.Observable.Hash(), a.Observable.Hash());
            }
        }

        [Test]
        public void A_spoken_hint_costs_the_same_as_the_button_in_the_examination_and_nothing_in_practice()
        {
            Assert.IsTrue(FlowVoiceIntents.TryMap("hint", out var spoken));
            var exam = New(SessionMode.Assessment).Apply(spoken);
            var practice = New(SessionMode.Practice).Apply(spoken);
            Assert.IsTrue(exam.Accepted && practice.Accepted);
            CollectionAssert.Contains(exam.Effects, "score:" + ExamControl.HintScoreItem);
            CollectionAssert.DoesNotContain(practice.Effects, "score:" + ExamControl.HintScoreItem);
        }
    }
}
