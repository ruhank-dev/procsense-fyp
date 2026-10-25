using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Three voices in the scene (plan p2-ai-advanced-simulation-s03, Unity half): fixed order, one speaker at a time, distinct voices, nothing from an unknown role. Audio and the headset check (DC-07) are Sannan's.</summary>
    public class TeamVoicesTests
    {
        private static TeamVoicesView View() => new GameObject("team").AddComponent<TeamVoicesView>();
        private static TeamReply R(string role, string text) => new TeamReply(role, text);

        [Test]
        public void The_replies_play_in_a_fixed_order_whatever_order_they_arrive_in_and_unknown_or_empty_ones_are_left_out()
        {
            var order = TeamVoicesView.Arrange(new[] { R("anaesthesia", "c"), R("surgeon", "x"), R("patient", "a"), R("nurse", " "), R("patient", "again") }).Select(r => r.Role + ":" + r.Text);
            CollectionAssert.AreEqual(new[] { "patient:a", "anaesthesia:c" }, order);                       // the blank nurse reply is dropped, the surgeon is not in the scene, the first patient reply wins
            CollectionAssert.AreEqual(new[] { "patient", "nurse", "anaesthesia" }, TeamVoicesView.Arrange(new[] { R("anaesthesia", "c"), R("nurse", "b"), R("patient", "a") }).Select(r => r.Role));
        }

        [Test]
        public void Each_role_has_its_own_voice()
        {
            Assert.AreEqual(3, TeamVoicesView.Voices.Values.Distinct().Count());
        }

        [Test]
        public void One_character_speaks_at_a_time_and_the_voice_layer_hears_each_line_once()
        {
            var view = View();
            var heard = new List<string>();
            view.Spoke += (role, text, voice) => heard.Add(role + "|" + text + "|" + voice);
            view.Offer(new[] { R("nurse", "b"), R("patient", "a"), R("anaesthesia", "c") });
            view.Step(0f);
            Assert.AreEqual(1, view.SpokenRoles.Count);
            view.Step(TeamVoicesView.SecondsPerLine - 0.5f);                                                 // still the first speaker's turn
            Assert.AreEqual(1, view.SpokenRoles.Count);
            view.Step(0.5f);
            view.Step(TeamVoicesView.SecondsPerLine);
            view.Step(TeamVoicesView.SecondsPerLine);
            CollectionAssert.AreEqual(new[] { "patient|a|voice.patient", "nurse|b|voice.nurse", "anaesthesia|c|voice.anaesthetist" }, heard);
            Assert.AreEqual(0, view.Pending);
            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void A_new_question_drops_what_was_still_waiting()
        {
            var view = View();
            view.Offer(new[] { R("patient", "a"), R("nurse", "b"), R("anaesthesia", "c") });
            view.Step(0f);
            view.Offer(new[] { R("nurse", "again") });
            view.Step(0f);
            CollectionAssert.AreEqual(new[] { "patient", "nurse" }, view.SpokenRoles);
            Assert.AreEqual("again", view.Current("nurse"));
            Assert.AreEqual(0, view.Pending);
            Object.DestroyImmediate(view.gameObject);
        }

        [Test]
        public void The_anaesthetist_is_the_same_role_as_anaesthesia()
        {
            var arranged = TeamVoicesView.Arrange(new[] { R("anaesthetist", "c") });
            Assert.AreEqual("anaesthesia", arranged.Single().Role);
        }
    }
}
