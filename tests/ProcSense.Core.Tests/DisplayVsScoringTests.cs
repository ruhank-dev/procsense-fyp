using System;
using System.IO;
using System.Linq;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Display zones against NEWS2 scoring, the probe artefact and the pregnancy modifiers (p0-physiology-events-behaviour-s06, plan test T-SEP2).</summary>
    public class DisplayVsScoringTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static DisplayZoneTable Display => Content.Physio.Display;
        private static readonly News2 Calc = new News2(News2Table.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "news2", "news2.json")), "news2"));

        [Theory]
        [InlineData("heart_rate", 76, "normal")]
        [InlineData("heart_rate", 91, "watch")]
        [InlineData("heart_rate", 50, "watch")]
        [InlineData("heart_rate", 111, "worry")]
        [InlineData("heart_rate", 131, "emergency")]
        [InlineData("heart_rate", 40, "emergency")]
        [InlineData("systolic_bp_low", 111, "normal")]
        [InlineData("systolic_bp_low", 101, "watch")]
        [InlineData("systolic_bp_low", 100, "worry")]
        [InlineData("systolic_bp_low", 90, "emergency")]
        [InlineData("respiratory_rate", 16, "normal")]
        [InlineData("respiratory_rate", 21, "watch")]
        [InlineData("respiratory_rate", 25, "emergency")]
        [InlineData("respiratory_rate", 8, "emergency")]
        [InlineData("spo2", 96, "normal")]
        [InlineData("spo2", 95, "watch")]
        [InlineData("spo2", 93, "worry")]
        [InlineData("spo2", 91, "emergency")]
        [InlineData("spo2", 87, "critical")]
        [InlineData("temperature_c", 37.9, "normal")]
        [InlineData("temperature_c", 38.0, "watch")]
        [InlineData("temperature_c", 39.1, "worry")]
        [InlineData("temperature_c", 35.0, "emergency")]
        [InlineData("pain_score", 3, "normal")]
        [InlineData("pain_score", 5, "watch")]
        [InlineData("pain_score", 8, "worry")]
        public void The_zone_follows_the_books_part_17_table(string parameter, double value, string zone) => Assert.Equal(zone, Display.Zone(parameter, value));

        [Fact]
        public void A_value_the_book_lists_no_band_for_has_no_zone()
        {
            Assert.Equal("", Display.Zone("systolic_bp_low", 230));
            Assert.Equal("", Display.Zone("unknown_parameter", 5));
        }

        [Fact]
        public void The_same_state_has_a_display_zone_and_a_different_news2_score_because_the_tables_differ_on_purpose()
        {
            // temperature 38.0: the monitor shows the watch zone (38.0 to 39.0) but NEWS2 scores 0 (36.1 to 38.0)
            Assert.Equal("watch", Display.Zone("temperature_c", 38.0));
            var n = Calc.Score(new News2Input(16, 98, 120, 70, 38.0, "A", false));
            Assert.Equal(0, n.Parts.Single(p => p.Parameter.Contains("emp", StringComparison.OrdinalIgnoreCase)).Points);
            // heart rate 40: the monitor shows the emergency zone, and 41 the watch zone; the zone edges belong to Part 17 and are never read from the NEWS2 table
            Assert.Equal("emergency", Display.Zone("heart_rate", 40));
            Assert.Equal("watch", Display.Zone("heart_rate", 41));
        }

        [Fact]
        public void A_session_shows_the_zone_on_each_vital_and_a_poor_probe_reads_low_until_it_is_resited()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            s.Apply(new Actions.SimAction("attach_sensor", "patient", Actions.Channel.Cli, new System.Collections.Generic.Dictionary<string, string> { ["sensor"] = "spo2" }));
            var good = s.Observable.Vitals.Single(v => v.Name == "spo2");
            Assert.Equal("normal", good.Zone);
            Assert.Equal("", good.Quality);
            double truth = s.Patient.Physiological.Vitals.Spo2;

            s.Patient.Physiological.ProbeProblem = "cold_fingers";
            var bad = s.Observable.Vitals.Single(v => v.Name == "spo2");
            Assert.True(double.Parse(bad.Value, System.Globalization.CultureInfo.InvariantCulture) < truth);
            Assert.Equal("poor_pleth", bad.Quality);
            Assert.Equal(truth, s.Patient.Physiological.Vitals.Spo2);       // the patient is unchanged: only the reading is wrong

            var r = s.Apply(new Actions.SimAction("resite_probe", "patient"));
            Assert.Contains("probe_resited:signal_restored", r.Effects);
            var fixedUp = s.Observable.Vitals.Single(v => v.Name == "spo2");
            Assert.Equal("", fixedUp.Quality);
            Assert.Equal(good.Value, fixedUp.Value);
        }

        [Fact]
        public void Resiting_a_probe_that_is_not_on_is_refused()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            var r = s.Apply(new Actions.SimAction("resite_probe", "patient"));
            Assert.Equal(Workflow.Steps.ObservationsSubsystem.ProbeNotAttached, r.RejectReason);
        }

        [Fact]
        public void Pregnancy_raises_a_generated_patients_heart_rate_by_the_books_range_and_never_uses_news2()
        {
            var p = Content.Physio.Pregnancy;
            Assert.Equal((10.0, 20.0), (p.HrRiseMin, p.HrRiseMax));
            Assert.InRange(p.HrRiseUsed, p.HrRiseMin, p.HrRiseMax);
            var v = Content.Cases["VAR-S1-A"].BaselineVitals;
            Assert.Equal(v.HeartRate + p.HrRiseUsed, p.Apply(v).HeartRate);
            var n = Calc.Score(new News2Input(16, 98, 112, 108, 37.9, "A", false), pregnant: true);   // the walkthrough's S2-D patient
            Assert.True(n.Disabled);
            Assert.Equal("", n.Tier);
        }
    }
}
