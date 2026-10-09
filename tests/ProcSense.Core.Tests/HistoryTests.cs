using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    public class HistoryTests
    {
        private static Session Verified()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v));
            return s;
        }

        private static ActionOutcome Ask(Session s, string topic) =>
            s.Apply(new SimAction("ask", "patient", Channel.Cli, new Dictionary<string, string> { ["topic"] = topic }));

        private static string Answer(Session s, string topic) => s.Case.History.First(h => h.Topic == topic).Answer;

        [Fact] // L2-01
        public void History_needs_a_confirmed_identity()
        {
            var s = new Session(TestCase.Card(), 42, SessionMode.Practice, Subsystems.Default(TestCase.Package()));

            var early = Ask(s, "smoking");

            Assert.Equal("IDENTITY_NOT_CONFIRMED", early.RejectReason);
            Assert.Empty(s.Observable.DisclosedFacts);
            Assert.Equal("IDENTITY_NOT_CONFIRMED", s.Apply(new SimAction("summarise")).RejectReason);
        }

        [Fact] // T02-* : asking reveals the exact walkthrough wording
        public void Asking_reveals_the_exact_answer_from_the_case()
        {
            var s = Verified();

            Ask(s, "medicines");

            Assert.Contains(s.Observable.DisclosedFacts, f => f.Topic == "medicines" && f.Answer == Answer(s, "medicines"));
            Assert.Equal("None except paracetamol sometimes.", Answer(s, "medicines"));
        }

        [Fact]
        public void Only_the_main_complaint_is_volunteered_everything_else_stays_hidden()
        {
            var s = Verified();
            Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == "complaint"); // nothing before history starts

            Ask(s, "smoking");

            var topics = s.Observable.DisclosedFacts.Select(f => f.Topic).ToList();
            Assert.Contains("complaint", topics);
            Assert.Contains("smoking", topics);
            foreach (var hidden in new[] { "red_flags", "past_history", "medicines", "herbal", "previous_anaesthetic", "family_anaesthetic", "alcohol", "last_food", "last_drink", "pregnancy" })
                Assert.DoesNotContain(hidden, topics);
        }

        [Fact]
        public void Un_asked_answers_never_appear_in_the_observable_text()
        {
            var s = Verified();
            Ask(s, "smoking"); Ask(s, "alcohol");

            var json = s.Observable.ToJson();

            foreach (var f in s.Case.History.Where(h => h.Topic != "complaint" && h.Topic != "smoking" && h.Topic != "alcohol" && h.Answer.Length > 10))
                Assert.DoesNotContain(f.Answer, json);
        }

        [Fact]
        public void The_clerking_note_grows_in_the_order_things_were_asked_without_duplicates()
        {
            var s = Verified();
            Ask(s, "last_food"); Ask(s, "smoking");
            var again = Ask(s, "last_food");

            Assert.True(again.Accepted);
            Assert.Contains("already_known:last_food", again.Effects);
            var topics = s.Observable.DisclosedFacts.Select(f => f.Topic).ToList();
            Assert.Equal(new[] { "identity", "complaint", "last_food", "smoking" }, topics); // identity was asked while verifying
        }

        [Fact]
        public void Last_food_and_drink_are_stored_as_exact_clock_times()
        {
            var s = Verified();

            Ask(s, "last_food"); Ask(s, "last_drink");

            Assert.Equal(new TimeSpan(21, 0, 0), s.Patient.Clinical.LastFoodAt);
            Assert.Equal(new TimeSpan(6, 30, 0), s.Patient.Clinical.LastDrinkAt);
        }

        [Fact]
        public void Bad_asks_are_rejected_with_a_reason()
        {
            var s = Verified();

            Assert.Equal("MISSING_TOPIC", s.Apply(new SimAction("ask")).RejectReason);
            Assert.Equal("UNKNOWN_TOPIC", Ask(s, "favourite_colour").RejectReason);
            Assert.DoesNotContain(s.Observable.DisclosedFacts, f => f.Topic == "complaint"); // a rejected ask volunteers nothing
        }

        [Fact] // L2-05
        public void Summarise_after_five_topics_raises_trust_and_understanding_once()
        {
            var effect = TestCase.Package().Summarise;
            var s = Verified();
            foreach (var t in new[] { "red_flags", "past_history", "medicines", "smoking" }) Ask(s, t); // + volunteered complaint = 5

            var o = s.Apply(new SimAction("summarise"));

            Assert.Equal((int)effect.Trust, s.Patient.Behavioural.Trust);
            Assert.Equal((int)effect.Understanding, s.Patient.Behavioural.Understanding);
            Assert.Contains(o.Effects, e => e.StartsWith("trust+"));

            var second = s.Apply(new SimAction("summarise"));
            Assert.Contains("already_summarised", second.Effects);
            Assert.Equal((int)effect.Trust, s.Patient.Behavioural.Trust);
        }

        [Fact]
        public void Summarising_too_early_earns_nothing()
        {
            var s = Verified();
            Ask(s, "smoking");

            var o = s.Apply(new SimAction("summarise"));

            Assert.True(o.Accepted);
            Assert.Contains(o.Effects, e => e.StartsWith("too_early:"));
            Assert.Equal(0, s.Patient.Behavioural.Understanding);
        }

        [Fact]
        public void Pregnancy_is_flagged_sensitive()
        {
            var topics = TestCase.Package().HistoryTopics;
            Assert.Contains("pregnancy", topics.Sensitive);

            var o = Ask(Verified(), "pregnancy");
            Assert.Contains("sensitive:pregnancy", o.Effects);
        }

        [Fact]
        public void Every_topic_in_the_case_can_be_asked()
        {
            var s = Verified();
            foreach (var f in s.Case.History) Assert.True(Ask(s, f.Topic).Accepted, f.Topic);

            Assert.Equal(s.Case.History.Count, s.Observable.DisclosedFacts.Count(d => s.Case.History.Any(h => h.Topic == d.Topic)));
        }

        [Fact]
        public void A_history_session_replays_with_the_same_hash()
        {
            var s = Verified();
            Ask(s, "red_flags"); Ask(s, "last_food"); Ask(s, "pregnancy");
            for (int i = 0; i < 2; i++) s.Apply(new SimAction("summarise"));
            var saved = SessionFile.Write(s);

            var r = SessionFile.Replay(saved, TestCase.Package(), () => Subsystems.Default(TestCase.Package()));

            Assert.True(r.Match);
            Assert.Equal(s.Observable.Hash(), r.Session.Observable.Hash());
        }
    }
}
