using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Behaviour;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The patient behaviour model (p0-physiology-events-behaviour-s10, plan tests T-BM1 to T-BM5).</summary>
    public class BehaviourTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static BehaviourTables Tables => Content.Behaviour;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId = "VAR-S1-A")
        {
            var file = File_(caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is BehaviourSubsystem) && !(x is ProcSense.Core.Physiology.PhysiologySubsystem) && !(x is ProcSense.Core.Workflow.Steps.SepsisSubsystem))
                .Concat(new ISubsystem[] { new ProcSense.Core.Physiology.PhysiologySubsystem(Content, file), new BehaviourSubsystem(Content, file), new ProcSense.Core.Workflow.Steps.SepsisSubsystem(Content, file) });
            return new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
        }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_attributes_the_transitions_and_the_modes()
        {
            Assert.Equal(new[] { "anxiety", "trust", "cooperation", "understanding", "pain", "fear", "frustration", "confusion", "willingness" }, Tables.Attributes.Select(a => a.Id));
            Assert.Equal((0.0, 3.0), (Tables.Find("confusion")!.Min, Tables.Find("confusion")!.Max));
            Assert.Equal(17, Tables.Rules.Count);
            Assert.Equal(8, Tables.Modes.Count);
            Assert.Equal("CALM", Tables.DefaultMode);
            Assert.Equal("talkative", Tables.Persona["communication_style"]);
        }

        // ---------------------------------------------------------------- the transitions

        [Fact]
        public void The_worked_example_reassurance_takes_anxiety_from_7_to_6_to_5_3_to_4_8()
        {
            var s = Start();
            s.Behaviour!.State.Set("anxiety", 7);
            var seen = new List<double>();
            for (int i = 0; i < 3; i++) { s.Behaviour.Trigger("reassure"); seen.Add(System.Math.Round(s.Behaviour.State.Get("anxiety"), 1)); }
            Assert.Equal(new[] { 6.0, 5.3, 4.8 }, seen);                                         // -1, then -0.7, then -0.49
        }

        // T-BM5
        [Fact]
        public void T_BM5_a_dismissive_reply_lowers_trust_by_2_and_cooperation_by_1()
        {
            var s = Start();
            double trust = s.Behaviour!.State.Get("trust"), coop = s.Behaviour.State.Get("cooperation");
            s.Behaviour.Trigger("dismissive_reply");
            Assert.Equal(trust - 2, s.Behaviour.State.Get("trust"));
            Assert.Equal(coop - 1, s.Behaviour.State.Get("cooperation"));
        }

        [Fact]
        public void Appropriate_explanation_raises_understanding_and_trust()
        {
            var s = Start();
            double u = s.Behaviour!.State.Get("understanding"), t = s.Behaviour.State.Get("trust");
            s.Behaviour.Trigger("explain_before_action");
            Assert.Equal((u + 1, t + 1), (s.Behaviour.State.Get("understanding"), s.Behaviour.State.Get("trust")));
        }

        [Fact]
        public void Jargon_lowers_understanding_and_adds_confusion_only_if_understanding_is_already_low()
        {
            var s = Start();
            s.Behaviour!.State.Set("understanding", 6);
            s.Behaviour.Trigger("jargon");
            Assert.Equal((5.0, 0.0), (s.Behaviour.State.Get("understanding"), s.Behaviour.State.Get("confusion")));
            s.Behaviour.State.Set("understanding", 3);
            s.Behaviour.Trigger("jargon");
            Assert.Equal((2.0, 1.0), (s.Behaviour.State.Get("understanding"), s.Behaviour.State.Get("confusion")));
        }

        [Fact]
        public void Every_second_unexplained_action_raises_anxiety_and_the_patient_asks_what_you_are_doing()
        {
            var s = Start();
            double a = s.Behaviour!.State.Get("anxiety");
            s.Behaviour.Trigger("unexplained_action", out var q1);
            Assert.Equal((a, ""), (s.Behaviour.State.Get("anxiety"), q1));
            s.Behaviour.Trigger("unexplained_action", out var q2);
            Assert.Equal((a + 1, "What are you doing?"), (s.Behaviour.State.Get("anxiety"), q2));
        }

        [Fact]
        public void An_approved_event_raises_fear_by_2_and_introducing_yourself_through_the_session_builds_trust()
        {
            var s = Start();
            double fear = s.Behaviour!.State.Get("fear"), trust = s.Behaviour.State.Get("trust"), anxiety = s.Behaviour.State.Get("anxiety");
            s.Behaviour.Trigger("approved_event");
            Assert.Equal(fear + 2, s.Behaviour.State.Get("fear"));
            Assert.True(Do(s, "introduce_self").Accepted);
            Assert.Equal((trust + 1, anxiety - 1), (s.Behaviour.State.Get("trust"), s.Behaviour.State.Get("anxiety")));
        }

        // T-BM2: the bounds hold
        [Fact]
        public void T_BM2_nothing_goes_outside_its_range()
        {
            var s = Start();
            for (int i = 0; i < 20; i++) { s.Behaviour!.Trigger("pressure"); s.Behaviour.Trigger("reassure"); s.Behaviour.Trigger("approved_event"); s.Behaviour.Trigger("jargon"); }
            foreach (var a in Tables.Attributes) Assert.InRange(s.Behaviour!.State.Get(a.Id), a.Min, a.Max);
            Assert.Equal(0, s.Behaviour!.State.Get("trust"));
            Assert.Equal(10, s.Behaviour.State.Get("fear"));
        }

        // T-BM3: same inputs, same outputs
        [Fact]
        public void T_BM3_the_same_acts_give_the_same_patient()
        {
            string Run()
            {
                var s = Start();
                foreach (var t in new[] { "introduce_self", "reassure", "jargon", "unexplained_action", "unexplained_action", "dismissive_reply", "reassure" }) s.Behaviour!.Trigger(t);
                s.Tick(2000);
                return string.Join(";", s.Behaviour!.State.Values.Select(kv => kv.Key + "=" + kv.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "|" + s.Behaviour.Mode;
            }
            Assert.Equal(Run(), Run());
        }

        // ---------------------------------------------------------------- long waits

        [Fact]
        public void A_wait_of_more_than_ten_minutes_with_nobody_talking_raises_anxiety_and_frustration_each_interval_and_talking_resets_it()
        {
            var s = Start();
            double a = s.Behaviour!.State.Get("anxiety"), f = s.Behaviour.State.Get("frustration");
            s.Tick(11 * 60);
            Assert.Equal((a + 1, f + 1), (s.Behaviour.State.Get("anxiety"), s.Behaviour.State.Get("frustration")));
            s.Tick(11 * 60);
            Assert.Equal((a + 2, f + 2), (s.Behaviour.State.Get("anxiety"), s.Behaviour.State.Get("frustration")));
            Do(s, "ask", ("topic", "complaint"));
            s.Tick(5 * 60);
            Assert.Equal(a + 2, s.Behaviour.State.Get("anxiety"));                              // spoken to: the clock starts again
        }

        // ---------------------------------------------------------------- modes (T-BM4, T-BM1)

        [Fact]
        public void The_patient_starts_calm_and_the_mode_is_what_the_trainee_sees_not_the_numbers()
        {
            var s = Start();
            Assert.Equal("CALM", s.Behaviour!.Mode);
            var json = s.Observable.ToJson();
            Assert.Contains("\"behaviour\":{\"mode\":\"CALM\"}", json);
            Assert.DoesNotContain("willingness", json);
        }

        // T-BM4: no flicker around the limit
        [Fact]
        public void T_BM4_the_mode_is_entered_half_a_point_past_the_limit_and_held_until_the_value_drops_below_it()
        {
            var s = Start();
            var b = s.Behaviour!;
            b.State.Set("anxiety", 5.2); b.Trigger("summarise");                                   // any trigger recomputes the mode; 5.2 is not yet 5.5
            Assert.Equal("CALM", b.Mode);
            b.State.Set("anxiety", 5.6); b.Trigger("explain_before_action");
            Assert.Equal("ANXIOUS", b.Mode);
            b.State.Set("anxiety", 5.1); b.Trigger("explain_before_action");
            Assert.Equal("ANXIOUS", b.Mode);                                                       // held while at or above 5
            b.State.Set("anxiety", 4.9); b.Trigger("explain_before_action");
            Assert.Equal("CALM", b.Mode);                                                          // the worked example: below 5 is calm again
            b.State.Set("anxiety", 5.2); b.Trigger("explain_before_action");
            Assert.Equal("CALM", b.Mode);                                                          // and it does not flip straight back
        }

        // T-BM1
        [Fact]
        public void T_BM1_a_good_and_a_poor_communicator_leave_the_patient_in_different_modes_and_willingness()
        {
            var good = Start(); var poor = Start();
            Do(good, "introduce_self"); good.Behaviour!.Trigger("explain_before_action"); good.Behaviour.Trigger("reassure"); good.Behaviour.Trigger("summarise");
            for (int i = 0; i < 3; i++) { poor.Behaviour!.Trigger("dismissive_reply"); poor.Behaviour.Trigger("unexplained_action"); poor.Behaviour.Trigger("unexplained_action"); poor.Behaviour.Trigger("jargon"); }
            poor.Behaviour!.Trigger("long_wait", marksTalk: false); poor.Behaviour.Trigger("long_wait", marksTalk: false); poor.Behaviour.Trigger("long_wait", marksTalk: false);
            Assert.Equal("CALM", good.Behaviour.Mode);
            Assert.NotEqual("CALM", poor.Behaviour.Mode);
            Assert.True(good.Behaviour.State.Get("willingness") > poor.Behaviour.State.Get("willingness"));
        }

        [Fact]
        public void Confusion_from_the_physiology_wins_over_everything_else()
        {
            var s = Start("STK-S1-A");                                                            // glucose 3.6: the confusion flag is on
            Assert.Equal("CONFUSED", s.Behaviour!.Mode);
        }

        [Fact]
        public void Severe_pain_puts_the_patient_in_pain_and_takes_capacity_away()
        {
            var s = Start();
            s.Patient.Physiological.Vitals = s.Patient.Physiological.Vitals with { PainScore = 8 };
            s.Tick(60);
            Assert.Equal("IN_PAIN", s.Behaviour!.Mode);
            Assert.Contains("severe_pain", s.Behaviour.CapacityCauses);
            Assert.False(s.Behaviour.HasCapacity);
        }

        // ---------------------------------------------------------------- capacity (walkthrough Part 10)

        [Fact]
        public void A_fit_patient_has_capacity()
        {
            Assert.True(Start().Behaviour!.HasCapacity);
        }

        [Fact]
        public void Low_sugar_takes_capacity_away_and_a_strong_painkiller_does_for_two_hours()
        {
            var low = Start("STK-S1-A");
            Assert.Contains("confusion", low.Behaviour!.CapacityCauses);
            var s = Start();
            s.Events.Publish(new TreatmentGiven("TR-OPIOID", new[] { "EFF-ANALGESIA" }, 1));
            s.Tick(60);
            Assert.Contains("strong_painkiller", s.Behaviour!.CapacityCauses);
            s.Tick(120 * 60);
            Assert.DoesNotContain("strong_painkiller", s.Behaviour.CapacityCauses);
        }

        [Fact]
        public void Sepsis_takes_capacity_away()
        {
            var s = Start("SC-1");
            Assert.True(s.Behaviour!.HasCapacity);
            s.Patient.Clinical.News2.Add(new News2Record(s.Clock.Steps, 8, true, "x", "", "", ""));
            Do(s, "say_concern", ("topic", "other"));                                              // the sepsis step reads the chart
            s.Tick(60);
            Assert.Contains("sepsis", s.Behaviour.CapacityCauses);
        }

        [Fact]
        public void A_low_sodium_in_the_case_takes_capacity_away()
        {
            var sodium = File_("SC-12").Results.First(r => r.Test.StartsWith("Sodium") && !r.Test.Contains("repeat"));
            double na = double.Parse(sodium.Value, System.Globalization.CultureInfo.InvariantCulture);
            var s = Start("SC-12");
            Assert.Equal(na < 125, s.Behaviour!.CapacityCauses.Contains("low_sodium"));
        }

        // ---------------------------------------------------------------- the disclosure gate

        [Fact]
        public void A_sensitive_fact_is_told_only_when_trust_and_willingness_reach_their_limits()
        {
            var s = Start();
            Assert.True(s.Behaviour!.MayDisclose("pregnancy"));
            Assert.True(s.Behaviour.MayDisclose("smoking"));                                      // not gated
            s.Behaviour.Trigger("pressure"); s.Behaviour.Trigger("pressure");                      // trust 1, willingness 1
            Assert.False(s.Behaviour.MayDisclose("pregnancy"));
            Assert.True(s.Behaviour.MayDisclose("smoking"));
        }
    }
}
