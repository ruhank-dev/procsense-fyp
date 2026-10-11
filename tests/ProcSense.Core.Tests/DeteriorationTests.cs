using System;
using System.Linq;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The deterioration clock and the Usman fixture (p0-physiology-events-behaviour-s04, plan tests T-P6 and T-P7).</summary>
    public class DeteriorationTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly DeteriorationTables Tables = Content.Deterioration;
        private static readonly DeteriorationClock Clock = new DeteriorationClock(Tables);
        private static CaseTimeline Usman => Tables.Timelines["VAR-S2-PD"];

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static Session Start(string caseId, ulong seed = 42)
        {
            var file = File_(caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is DeteriorationSubsystem)).Concat(new ISubsystem[] { new DeteriorationSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = file.Scenario };
            return new Session(card, seed, SessionMode.Practice, chain);
        }

        private static void Give(Session s, string effect) => s.Events.Publish(new TreatmentGiven("T", new[] { effect }, 1));

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_tables_rule_and_the_usman_timeline()
        {
            Assert.Equal((5, 10, 1, 30), (Tables.HrStepMin, Tables.HrStepMax, Tables.News2Step, Tables.IntervalMinutes));
            Assert.Equal(new[] { "EFF-FLUID-BOLUS", "EFF-ANTIBIOTIC" }, Tables.TreatedBy);
            Assert.Equal(new[] { 0.0, 45, 75, 105 }, Usman.Points.Select(p => p.Minutes));
            Assert.Equal(new[] { 102.0, 116, 128, 138 }, Usman.Points.Select(p => p.HeartRate));
            Assert.Equal((108.0, 64.0, 112.0), (Usman.Treated.Sbp, Usman.Treated.Dbp, Usman.Treated.HeartRate));
        }

        // ---------------------------------------------------------------- the clock

        [Fact]
        public void Untreated_the_clock_passes_through_each_reading_and_runs_in_straight_lines_between_them()
        {
            foreach (var p in Usman.Points) Assert.Equal(p.HeartRate, Clock.Untreated(Usman, p.Minutes).HeartRate);
            Assert.Equal(109, Clock.Untreated(Usman, 22.5).HeartRate);                    // halfway between 102 and 116
            Assert.Equal(138, Clock.Untreated(Usman, 240).HeartRate);                     // held after the last reading
        }

        // T-P6: untreated, the chart's NEWS2 follows the book's within one point
        [Fact]
        public void T_P6_Usman_untreated_matches_the_timeline_within_one_NEWS2_point()
        {
            var news2 = new News2(Content.News2);
            foreach (var p in Usman.Points)
            {
                var v = Clock.Untreated(Usman, p.Minutes);
                var n = news2.Score(new News2Input(v.RespiratoryRate, v.Spo2, v.Sbp, v.HeartRate, v.TemperatureC, v.Acvpu, false));
                Assert.InRange(n.Total, p.News2Book - 1, p.News2Book + 1);
            }
        }

        // T-P7: treated at 14:45, blood pressure rises and the heart rate falls
        [Fact]
        public void T_P7_Usman_treated_at_14_45_has_a_higher_blood_pressure_and_a_lower_heart_rate_than_untreated()
        {
            var treated = Clock.At(Usman, 75, 45);
            var untreated = Clock.Untreated(Usman, 75);
            Assert.True(treated.Sbp > untreated.Sbp);
            Assert.True(treated.HeartRate < untreated.HeartRate);
            Assert.Equal((108.0, 64.0, 112.0), (treated.Sbp, treated.Dbp, treated.HeartRate));         // the walkthrough's values after the correct action
            Assert.Equal(untreated.Spo2 > 0 ? Clock.Untreated(Usman, 45).Spo2 : 0, treated.Spo2);       // readings with no treated value are held
        }

        [Fact]
        public void Recovery_takes_the_recovery_time_and_never_overshoots()
        {
            double mid = Clock.At(Usman, 60, 45).Sbp;
            Assert.InRange(mid, 108, 112);                                                // half way from 112 to 108
            Assert.Equal(108, Clock.At(Usman, 500, 45).Sbp);
        }

        // ---------------------------------------------------------------- in a session

        [Fact]
        public void In_a_session_untreated_Usman_follows_his_timeline_on_the_sim_clock()
        {
            var s = Start("VAR-S2-PD");
            s.Tick(45 * 60);
            Assert.Equal(116, s.Patient.Physiological.Vitals.HeartRate);
            Assert.Equal(112, s.Patient.Physiological.Vitals.SystolicBp);
            s.Tick(30 * 60);
            Assert.Equal((128.0, 92.0, 26.0), (s.Patient.Physiological.Vitals.HeartRate, s.Patient.Physiological.Vitals.SystolicBp, s.Patient.Physiological.Vitals.RespiratoryRate));
            s.Tick(30 * 60);
            Assert.Equal("C", s.Patient.Physiological.Vitals.Acvpu);                      // confused at 15:45 if nobody acts
        }

        [Fact]
        public void In_a_session_a_fluid_bolus_at_14_45_turns_the_patient_round()
        {
            var s = Start("VAR-S2-PD");
            s.Tick(45 * 60);
            Give(s, "EFF-FLUID-BOLUS");
            Assert.True(s.Patient.Clinical.HasFlag(DeteriorationSubsystem.TreatedFlag));
            s.Tick(30 * 60);
            Assert.Equal((108.0, 64.0, 112.0), (s.Patient.Physiological.Vitals.SystolicBp, s.Patient.Physiological.Vitals.DiastolicBp, s.Patient.Physiological.Vitals.HeartRate));
            s.Tick(60 * 60);
            Assert.Equal(108, s.Patient.Physiological.Vitals.SystolicBp);                  // it stays treated
        }

        [Fact]
        public void A_treatment_that_does_not_stop_the_clock_changes_nothing()
        {
            var s = Start("VAR-S2-PD");
            s.Tick(45 * 60);
            Give(s, "EFF-ANALGESIA");
            Assert.False(s.Patient.Clinical.HasFlag(DeteriorationSubsystem.TreatedFlag));
            s.Tick(30 * 60);
            Assert.Equal(92, s.Patient.Physiological.Vitals.SystolicBp);
        }

        [Fact]
        public void Another_emergency_case_drifts_its_heart_rate_by_5_to_10_each_interval_and_stops_when_treated()
        {
            var s = Start("SC-1");
            double hr0 = s.Patient.Physiological.Vitals.HeartRate;
            s.Tick(30 * 60);
            double after1 = s.Patient.Physiological.Vitals.HeartRate;
            Assert.InRange(after1 - hr0, 5, 10);
            s.Tick(30 * 60);
            Assert.InRange(s.Patient.Physiological.Vitals.HeartRate - after1, 5, 10);
            double held = s.Patient.Physiological.Vitals.HeartRate;
            Give(s, "EFF-ANTIBIOTIC");
            s.Tick(120 * 60);
            Assert.Equal(held, s.Patient.Physiological.Vitals.HeartRate);
        }

        [Fact]
        public void The_drift_is_the_same_for_the_same_seed()
        {
            var a = Start("SC-1", 7); var b = Start("SC-1", 7);
            a.Tick(90 * 60); b.Tick(90 * 60);
            Assert.Equal(a.Patient.Physiological.Vitals.HeartRate, b.Patient.Physiological.Vitals.HeartRate);
        }

        [Fact]
        public void A_planned_case_never_deteriorates_on_the_clock()
        {
            var s = Start("STK-S1-A");
            double hr = s.Patient.Physiological.Vitals.HeartRate;
            s.Tick(120 * 60);
            Assert.Equal(hr, s.Patient.Physiological.Vitals.HeartRate);
        }

        [Fact]
        public void An_emergency_case_with_no_life_threat_or_time_critical_problem_does_not_drift()
        {
            var s = Start("VAR-S2-K");
            Assert.DoesNotContain(File_("VAR-S2-K").Problems, p => p.Type == "life_threat" || p.Type == "time_critical");
            double hr = s.Patient.Physiological.Vitals.HeartRate;
            s.Tick(120 * 60);
            Assert.Equal(hr, s.Patient.Physiological.Vitals.HeartRate);
        }
    }
}
