using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.State;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>NICE NG253 sepsis tiers, timers and lateness (p0-clinical-logic-s04, plan tests T-SEP1 and T-SEP2).</summary>
    public class SepsisTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static SepsisTables Tables => Content.Sepsis;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_four_tiers_six_criteria_and_the_timers_of_the_table()
        {
            Assert.Equal(new[] { "high", "moderate", "low", "very_low" }, Tables.Tiers.Select(t => t.Id));
            Assert.Equal(6, Tables.Criteria.Count);
            var high = Tables.Find("high")!; var moderate = Tables.Find("moderate")!; var low = Tables.Find("low")!;
            Assert.Equal((1.0, 1.0, 1.0, 30, 30), (high.AntibioticsHours, high.FluidsHours, high.ReviewHours, high.ReassessMinMinutes, high.ReassessMaxMinutes));
            Assert.Equal((3.0, null, 1.0, 60, true), (moderate.AntibioticsHours, moderate.FluidsHours, moderate.ReviewHours, moderate.ReassessMinMinutes, moderate.Lactate));
            Assert.Equal((6.0, 240, 360), (low.AntibioticsHours, low.ReassessMinMinutes, low.ReassessMaxMinutes));
            Assert.Null(Tables.Find("very_low")!.AntibioticsHours);
            Assert.Equal(19, Tables.InfectionCases.Count);
            Assert.All(Tables.InfectionCases.Keys, id => Assert.True(System.IO.File.Exists(System.IO.Path.Combine(TestCase.ContentRoot, "case_files", id + ".json")), id));
        }

        // ---------------------------------------------------------------- the tier

        [Theory]
        [InlineData(7, false, "high")]
        [InlineData(10, false, "high")]
        [InlineData(6, true, "high")]       // NEWS2 5-6 with a single parameter scoring 3
        [InlineData(5, true, "high")]
        [InlineData(6, false, "moderate")]
        [InlineData(5, false, "moderate")]
        [InlineData(4, true, "moderate")]   // NEWS2 3-4 with a single 3
        [InlineData(3, true, "moderate")]
        [InlineData(4, false, "low")]
        [InlineData(1, false, "low")]
        [InlineData(2, true, "low")]        // a single 3 under NEWS2 3 does not raise the tier by itself
        [InlineData(0, false, "very_low")]
        public void The_highest_tier_whose_criteria_are_met_wins(int news2, bool single3, string tier)
        {
            Assert.Equal(tier, Tables.Assess(true, news2, single3, false).Tier);
        }

        [Fact]
        public void Without_suspected_infection_there_is_no_sepsis_tier()
        {
            Assert.Equal(SepsisTables.None, Tables.Assess(false, 12, true, true).Tier);
        }

        [Fact]
        public void Clinical_concern_raises_the_tier_by_one_and_high_stays_high()
        {
            Assert.Equal(("low", true), (Tables.Assess(true, 0, false, true).Tier, Tables.Assess(true, 0, false, true).Raised));
            Assert.Equal("moderate", Tables.Assess(true, 3, false, true).Tier);       // low raised
            Assert.Equal("high", Tables.Assess(true, 5, false, true).Tier);           // moderate raised
            Assert.False(Tables.Assess(true, 8, false, true).Raised);                 // already high
            Assert.Equal("high", Tables.Assess(true, 8, false, true).Tier);
        }

        private sealed record Row(double Rr, double Spo2, double Sbp, double Hr, double T, string A, int Total, string Tier);

        // T-SEP1: Mr Usman, walkthrough Part 13 Example Patient D. Tiers at each time point from the NEWS2 the chart gives.
        [Fact]
        public void T_SEP1_the_Usman_timeline_gives_low_moderate_high_high()
        {
            var news2 = new News2(Content.News2);
            var rows = new[]
            {
                new Row(20, 97, 124, 102, 38.3, "A", 2, "low"),
                new Row(22, 96, 112, 116, 38.9, "A", 5, "moderate"),
                new Row(26, 94, 92, 128, 39.2, "A", 10, "high"),
                new Row(30, 91, 82, 138, 39.4, "C", 17, "high"),     // the book prints 16: ISS-0016
            };
            foreach (var r in rows)
            {
                var n = news2.Score(new News2Input(r.Rr, r.Spo2, r.Sbp, r.Hr, r.T, r.A, false));
                Assert.Equal(r.Total, n.Total);
                Assert.Equal(r.Tier, Tables.Assess(true, n.Total, n.Single3, false).Tier);
            }
        }

        // ---------------------------------------------------------------- the timers

        private static (Session s, SepsisSubsystem step) Usman()
        {
            var file = File_("VAR-S2-PD");
            var step = new SepsisSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is SepsisSubsystem)).Concat(new ISubsystem[] { step });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            return (s, step);
        }

        // Puts a NEWS2 on the chart at this many minutes and lets the step see it.
        private static void Chart(Session s, double minutes, int total, bool single3)
        {
            double now = s.Clock.SimSeconds;
            if (minutes * 60 > now) s.Tick(minutes * 60 - now);
            s.Patient.Clinical.News2.Add(new News2Record(s.Clock.Steps, total, single3, "x", "", "", ""));
            Do(s, "say_concern", ("topic", "other"));        // any accepted action makes the step look at the chart
        }

        private static void Give(Session s, string effect, double minutes)
        {
            double now = s.Clock.SimSeconds;
            if (minutes * 60 > now) s.Tick(minutes * 60 - now);
            s.Events.Publish(new TreatmentGiven("T", new[] { effect }, 1));
        }

        [Fact]
        public void The_walkthrough_example_antibiotics_at_15_55_are_70_minutes_after_the_criteria_were_met_and_inside_the_high_tier_hour()
        {
            var (s, step) = Usman();
            Chart(s, 0, 2, false); Chart(s, 45, 5, false); Chart(s, 75, 10, true);
            Give(s, "EFF-ANTIBIOTIC", 115);
            var r = s.Sepsis!;
            Assert.True(r.Applies);
            Assert.Equal("high", r.Tier);
            Assert.Equal(45 * 60, r.FirstMetSeconds);
            var abx = r.Timers.Single(t => t.Kind == "antibiotics");
            Assert.Equal("high", abx.Tier);
            Assert.Equal(135 * 60, abx.DeadlineSeconds);                   // the high tier's hour from 15:15, tighter than the moderate tier's three hours from 14:45
            Assert.Equal(70, abx.SinceFirstMetMinutes);                    // the book's 'criteria met 14:45; antibiotics given 15:55' (ISS-0021)
            Assert.Equal(0, abx.LateMinutes);
            Assert.Contains(step.History, h => h.Kind == "sepsis_antibiotics_on_time" && h.ScoreItems.Contains("credit.sepsis_antibiotics_on_time"));
        }

        [Fact]
        public void Antibiotics_after_the_deadline_are_late_by_the_minutes_past_it_and_a_major_item()
        {
            var (s, step) = Usman();
            Chart(s, 0, 2, false); Chart(s, 45, 5, false); Chart(s, 75, 10, true);
            Give(s, "EFF-ANTIBIOTIC", 140);
            var abx = s.Sepsis!.Timers.Single(t => t.Kind == "antibiotics");
            Assert.Equal(5, abx.LateMinutes);
            Assert.Equal(95, abx.SinceFirstMetMinutes);
            Assert.Contains(step.History, h => h.Kind == "sepsis_antibiotics_late" && h.ScoreItems.Contains("major.sepsis_antibiotics_late"));
        }

        [Fact]
        public void A_treatment_not_yet_given_runs_late_by_the_minute_and_is_noted_once_when_it_becomes_overdue()
        {
            var (s, step) = Usman();
            Chart(s, 75, 10, true);
            s.Tick(70 * 60);                                                // 10 minutes past the high tier's hour
            var fluids = s.Sepsis!.Timers.Single(t => t.Kind == "fluids");
            Assert.Null(fluids.GivenSeconds);
            Assert.Equal(10, fluids.LateMinutes);
            Assert.Single(step.History, h => h.Kind == "sepsis_fluids_overdue");
            s.Tick(600);
            Assert.Single(step.History, h => h.Kind == "sepsis_fluids_overdue");
        }

        [Fact]
        public void Only_the_moderate_tier_asks_for_antibiotics_in_three_hours_and_not_for_fluids()
        {
            var (s, _) = Usman();
            Chart(s, 45, 5, false);
            Give(s, "EFF-ANTIBIOTIC", 200);
            var r = s.Sepsis!;
            Assert.Equal(new[] { "antibiotics" }, r.Timers.Select(t => t.Kind));
            Assert.Equal(180 * 60 + 45 * 60, r.Timers[0].DeadlineSeconds);
            Assert.Equal(0, r.Timers[0].LateMinutes);                       // 200 < 225
        }

        [Fact]
        public void A_case_without_an_infection_never_gets_a_tier()
        {
            var file = File_("VAR-S1-A");
            var step = new SepsisSubsystem(Content, file);
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content).Where(x => !(x is SepsisSubsystem)).Concat(new ISubsystem[] { step }));
            Chart(s, 5, 9, true);
            var r = s.Sepsis!;
            Assert.False(r.Applies);
            Assert.Equal(SepsisTables.None, r.Tier);
            Assert.Empty(r.Timers);
        }

        // T-SEP2: NEWS2 is not used in pregnancy, so there is no tier from it
        [Fact]
        public void T_SEP2_a_pregnant_patient_has_no_NEWS2_and_so_no_sepsis_tier()
        {
            var file = File_("VAR-S2-H");
            Assert.True(file.Pregnant);
            Assert.True(Tables.IsInfectionCase("VAR-S2-H"));
            var step = new SepsisSubsystem(Content, file);
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content).Where(x => !(x is SepsisSubsystem)).Concat(new ISubsystem[] { step }));
            s.Patient.Clinical.News2.Add(new News2Record(s.Clock.Steps, 0, false, "not_used", "", "", "NEWS2 is not used in pregnancy"));
            Do(s, "say_concern", ("topic", "other"));
            Assert.Equal(SepsisTables.None, s.Sepsis!.Tier);
            Assert.Empty(s.Sepsis.Tiers);
        }

        // ---------------------------------------------------------------- say it out loud

        [Fact]
        public void Saying_sepsis_after_the_criteria_are_met_is_credited_once_and_before_them_is_not()
        {
            var (s, step) = Usman();
            Chart(s, 0, 2, false);
            var early = Do(s, "say_concern", ("topic", "sepsis"));
            Assert.Contains("concern_before_criteria", early.Effects);
            Assert.DoesNotContain(step.History, h => h.Kind == "sepsis_recognised");
            Chart(s, 45, 5, false);
            var o = Do(s, "say_concern", ("topic", "sepsis"));
            Assert.Contains("sepsis_recognised", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag(SepsisSubsystem.RecognisedFlag));
            Do(s, "say_concern", ("topic", "sepsis"));
            Assert.Single(step.History, h => h.Kind == "sepsis_recognised" && h.ScoreItems.Contains("credit.sepsis_recognised"));
            Assert.True(s.Sepsis!.Recognised);
        }

        [Fact]
        public void A_concern_needs_a_known_topic()
        {
            var (s, _) = Usman();
            Assert.Equal(SepsisSubsystem.MissingTopic, Do(s, "say_concern").RejectReason);
            Assert.Equal(SepsisSubsystem.UnknownTopic, Do(s, "say_concern", ("topic", "gremlins")).RejectReason);
        }

        [Fact]
        public void Clinical_concern_on_the_chart_raises_the_tier_the_step_reports()
        {
            var (s, _) = Usman();
            s.Patient.Clinical.SetFlag(SepsisSubsystem.ConcernFlag);
            Chart(s, 10, 3, false);                                          // low, raised to moderate by concern
            Assert.Equal("moderate", s.Sepsis!.Tier);
        }
    }
}
