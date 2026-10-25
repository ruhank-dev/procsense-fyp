using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    internal static class ObservationsSubsystem_Codes { public const string OutOfRange = "PULSE_SECONDS_OUT_OF_RANGE"; }

    public class ObservationsTests
    {
        private static Session New(ulong seed = 42) =>
            new Session(TestCase.Card(), seed, SessionMode.Practice, Subsystems.Default(TestCase.Package()));

        private static ActionOutcome Do(Session s, string verb, string? sensor = null) =>
            s.Apply(new SimAction(verb, "patient", Channel.Cli, sensor == null ? null : new Dictionary<string, string> { ["sensor"] = sensor }));

        private static string Num(double x) => x.ToString("0.#", CultureInfo.InvariantCulture);

        private static Dictionary<string, string> Vitals(Session s) => s.Observable.Vitals.ToDictionary(v => v.Name, v => v.Value);

        private static ActionOutcome Palpate(Session s, string? seconds = null) =>
            s.Apply(new SimAction("palpate_pulse", "patient", Channel.Cli, seconds == null ? null : new Dictionary<string, string> { ["seconds"] = seconds }));

        [Fact]
        public void Feeling_the_pulse_takes_the_seconds_chosen_and_then_shows_the_rate_and_the_rhythm()   // DT-040
        {
            var s = New();
            Assert.Equal("pulse_started:30_s", Palpate(s).Effects.Single());
            Assert.Empty(s.Observable.Vitals);                                       // nothing is known until the time has passed
            s.Tick(31);
            var v = Vitals(s);
            Assert.Equal(Num(s.Case.BaselineVitals.HeartRate), v["heart_rate"]);
            Assert.Equal(s.Case.BaselineVitals.HeartRhythm, v["heart_rhythm"]);
        }

        [Fact]
        public void The_finger_clip_shows_the_rate_but_not_the_rhythm()
        {
            var s = New();
            Do(s, "attach_sensor", "spo2");
            var v = Vitals(s);
            Assert.Contains("heart_rate", v.Keys);
            Assert.DoesNotContain("heart_rhythm", v.Keys);
        }

        [Fact]
        public void The_pulse_is_felt_for_30_to_60_seconds_and_one_count_runs_at_a_time()
        {
            var s = New();
            Assert.Equal(ObservationsSubsystem_Codes.OutOfRange, Palpate(s, "10").RejectReason);
            Assert.Equal(ObservationsSubsystem_Codes.OutOfRange, Palpate(s, "90").RejectReason);
            Assert.Equal(ObservationsSubsystem_Codes.OutOfRange, Palpate(s, "soon").RejectReason);
            Assert.True(Palpate(s, "60").Accepted);
            Assert.Equal("PULSE_IN_PROGRESS", Palpate(s, "30").RejectReason);
            s.Tick(59);
            Assert.DoesNotContain("heart_rhythm", Vitals(s).Keys);
            s.Tick(2);
            Assert.Contains("heart_rhythm", Vitals(s).Keys);
        }

        [Fact]
        public void The_monitor_is_blank_until_a_sensor_is_attached()
        {
            var s = New();
            Assert.Empty(s.Observable.Vitals);

            Do(s, "attach_sensor", "nibp_cuff"); // a cuff alone shows nothing until it has measured
            Assert.Empty(s.Observable.Vitals);
        }

        [Fact]
        public void Attaching_the_probe_shows_spo2_oxygen_and_pulse_from_the_case()
        {
            var s = New();
            var b = s.Case.BaselineVitals;

            Do(s, "attach_sensor", "spo2");

            var v = Vitals(s);
            Assert.Equal(Num(b.Spo2), v["spo2"]);
            Assert.Equal(b.Oxygen, v["oxygen"]);
            Assert.Equal(Num(b.HeartRate), v["heart_rate"]);
            Assert.False(v.ContainsKey("systolic_bp"));
        }

        [Fact]
        public void Detaching_the_probe_blanks_its_values_again()
        {
            var s = New();
            Do(s, "attach_sensor", "spo2");
            Do(s, "detach_sensor", "spo2");

            Assert.Empty(s.Observable.Vitals);
        }

        [Fact] // L3-02
        public void Blood_pressure_appears_only_after_the_cuff_cycle_and_inside_the_content_range()
        {
            var s = New();
            var nibp = TestCase.Package().Nibp;
            Do(s, "attach_sensor", "nibp_cuff");
            var start = s.Clock.SimSeconds;
            var started = Do(s, "measure_nibp");
            Assert.True(started.Accepted);

            s.Tick(nibp.CycleSecondsMin - 1); // still inflating
            Assert.False(Vitals(s).ContainsKey("systolic_bp"));

            s.Tick(nibp.CycleSecondsMax - nibp.CycleSecondsMin + 1); // past the longest possible cycle
            var v = Vitals(s);
            Assert.Equal(Num(s.Case.BaselineVitals.SystolicBp), v["systolic_bp"]);
            Assert.Equal(Num(s.Case.BaselineVitals.DiastolicBp), v["diastolic_bp"]);

            var at = System.TimeSpan.Parse(s.Observable.Vitals.First(x => x.Name == "systolic_bp").At, CultureInfo.InvariantCulture);
            double took = (at - s.Clock.Start).TotalSeconds - start;
            Assert.InRange(took, nibp.CycleSecondsMin - 0.5, nibp.CycleSecondsMax + 0.5);
        }

        [Fact]
        public void The_cycle_length_is_repeatable_for_a_seed()
        {
            string Reading(ulong seed)
            {
                var s = New(seed);
                Do(s, "attach_sensor", "nibp_cuff"); Do(s, "measure_nibp");
                s.Tick(60);
                return s.Observable.Vitals.First(v => v.Name == "systolic_bp").At;
            }

            Assert.Equal(Reading(7), Reading(7));
        }

        [Fact]
        public void Measuring_needs_the_cuff_and_cannot_overlap()
        {
            var s = New();
            Assert.Equal("CUFF_NOT_ATTACHED", Do(s, "measure_nibp").RejectReason);

            Do(s, "attach_sensor", "nibp_cuff"); Do(s, "measure_nibp");
            Assert.Equal("CYCLE_IN_PROGRESS", Do(s, "measure_nibp").RejectReason);
        }

        [Fact]
        public void Taking_the_cuff_off_cancels_the_cycle()
        {
            var s = New();
            Do(s, "attach_sensor", "nibp_cuff"); Do(s, "measure_nibp");
            s.Tick(5);
            Do(s, "detach_sensor", "nibp_cuff");

            s.Tick(60);

            Assert.False(Vitals(s).ContainsKey("systolic_bp"));
        }

        [Fact]
        public void Temperature_rr_alertness_and_pain_are_recorded_with_times()
        {
            var s = New();
            var b = s.Case.BaselineVitals;
            s.Tick(10);

            foreach (var verb in new[] { "measure_temperature", "count_rr", "assess_acvpu", "ask_pain_score" }) Assert.True(Do(s, verb).Accepted, verb);
            Assert.False(Vitals(s).ContainsKey("respiratory_rate"));       // the breaths are still being counted (DT-040)
            s.Tick(35);                                                     // more than the 30 seconds

            var v = Vitals(s);
            Assert.Equal(Num(b.TemperatureC), v["temperature_c"]);
            Assert.Equal(Num(b.RespiratoryRate), v["respiratory_rate"]);
            Assert.Equal(b.Acvpu, v["acvpu"]);
            Assert.Equal(b.PainScore.ToString(CultureInfo.InvariantCulture), v["pain_score"]);
            Assert.Equal("08:00:10", s.Observable.Vitals.First(x => x.Name == "temperature_c").At);
            Assert.Equal("08:00:40", s.Observable.Vitals.First(x => x.Name == "respiratory_rate").At);
        }

        [Fact] // DT-040: the breath count takes 30 to 60 seconds, one at a time
        public void Counting_breaths_takes_the_chosen_seconds_and_refuses_a_bad_length_or_a_second_count()
        {
            var s = New();
            Assert.Equal(ProcSense.Core.Workflow.Steps.ObservationsSubsystem.RrSecondsOutOfRange, s.Apply(new SimAction("count_rr", parameters: new System.Collections.Generic.Dictionary<string, string> { ["seconds"] = "10" })).RejectReason);
            Assert.Equal(ProcSense.Core.Workflow.Steps.ObservationsSubsystem.RrSecondsOutOfRange, s.Apply(new SimAction("count_rr", parameters: new System.Collections.Generic.Dictionary<string, string> { ["seconds"] = "x" })).RejectReason);
            Assert.True(s.Apply(new SimAction("count_rr", parameters: new System.Collections.Generic.Dictionary<string, string> { ["seconds"] = "60" })).Accepted);
            Assert.Equal(ProcSense.Core.Workflow.Steps.ObservationsSubsystem.RrInProgress, Do(s, "count_rr").RejectReason);
            s.Tick(50);
            Assert.False(Vitals(s).ContainsKey("respiratory_rate"));
            s.Tick(15);
            Assert.True(Vitals(s).ContainsKey("respiratory_rate"));
            Assert.True(Do(s, "count_rr").Accepted);                       // a new count may start once the last is read
        }

        [Fact]
        public void Observations_are_not_gated_on_identity()
        {
            var s = New();

            Assert.False(s.Patient.Clinical.IdentityVerified);
            Assert.True(Do(s, "attach_sensor", "spo2").Accepted);
            Assert.True(Do(s, "measure_temperature").Accepted);
        }

        [Fact]
        public void Bad_sensor_requests_are_rejected()
        {
            var s = New();

            Assert.Equal("MISSING_SENSOR", Do(s, "attach_sensor").RejectReason);
            Assert.Equal("UNKNOWN_SENSOR", Do(s, "attach_sensor", "thermometer").RejectReason);
            Assert.Contains("already_attached:spo2", Do(s, "attach_sensor", "spo2").Effects.Concat(Do(s, "attach_sensor", "spo2").Effects));
            Assert.Contains("not_attached:nibp_cuff", Do(s, "detach_sensor", "nibp_cuff").Effects);
        }

        [Fact]
        public void A_full_observations_session_replays_with_the_same_hash()
        {
            var s = New();
            Do(s, "attach_sensor", "spo2"); Do(s, "attach_sensor", "nibp_cuff"); Do(s, "measure_nibp");
            s.Tick(20); Do(s, "measure_temperature");
            s.Tick(30); Do(s, "wait");
            var saved = SessionFile.Write(s);

            var r = SessionFile.Replay(saved, TestCase.Package(), () => Subsystems.Default(TestCase.Package()));

            Assert.True(r.Match);
            Assert.Equal(s.Observable.Hash(), r.Session.Observable.Hash());
            Assert.Contains(r.Session.Observable.Vitals, v => v.Name == "systolic_bp");
        }
    }
}
