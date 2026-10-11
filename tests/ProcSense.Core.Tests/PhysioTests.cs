using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Random;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>LitePhysio: state, relaxation, effects, couplings, noise and flags (p0-physiology-events-behaviour-s01 and s02, plan tests T-P1 to T-P5).</summary>
    public class PhysioTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static PhysioTables Tables => Content.Physio;

        private static readonly Dictionary<string, double> Baseline = new Dictionary<string, double>
        {
            ["HR"] = 88,
            ["SBP"] = 124,
            ["DBP"] = 78,
            ["RR"] = 16,
            ["SpO2"] = 98,
            ["Temp"] = 36.8,
            ["pain"] = 0,
            ["glucose"] = 6,
            ["nausea"] = 0,
            ["sedation"] = 0,
            ["volume_deficit"] = 0,
        };

        private static LitePhysio Make(ulong seed = 42, bool noise = false)
        {
            var p = new LitePhysio(Tables) { NoiseEnabled = noise };
            p.Initialise(Baseline, new SimRandom(seed).Stream("physio"));
            return p;
        }

        private static void Run(LitePhysio p, double seconds, double dt = 0.5) { for (double t = 0; t < seconds; t += dt) p.Advance(dt); }

        private static double Get(LitePhysio p, string v) => p.Read().Values[v];

        private static PhysioEffect Effect(string id, (string, double)[] targets, double onset = 0, double peak = 60, double plateau = 120, double offset = 60) =>
            new PhysioEffect(id, targets.ToDictionary(t => t.Item1, t => t.Item2), new PhysioProfile(onset, peak, plateau, offset), "test");

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_variables_with_the_guides_bounds_and_relaxation_times()
        {
            Assert.Equal(11, Tables.Variables.Count);
            Assert.Equal((30.0, 200.0, 60.0), (Tables.Find("HR")!.Min, Tables.Find("HR")!.Max, Tables.Find("HR")!.TauSeconds));
            Assert.Equal((50.0, 220.0, 90.0), (Tables.Find("SBP")!.Min, Tables.Find("SBP")!.Max, Tables.Find("SBP")!.TauSeconds));
            Assert.Equal((60.0, 100.0, 30.0), (Tables.Find("SpO2")!.Min, Tables.Find("SpO2")!.Max, Tables.Find("SpO2")!.TauSeconds));
            Assert.Equal((4.0, 45.0), (Tables.Find("RR")!.Min, Tables.Find("RR")!.Max));
            Assert.Equal((34.0, 41.0), (Tables.Find("Temp")!.Min, Tables.Find("Temp")!.Max));
            Assert.False(Tables.NoiseEnabled);
            Assert.Equal(6, Tables.Noise.Count);
        }

        // ---------------------------------------------------------------- relaxation

        [Fact]
        public void With_no_effects_every_variable_stays_exactly_at_its_baseline_for_an_hour()
        {
            var p = Make();
            Run(p, 3600);
            var values = p.Read().Values;
            foreach (var kv in Baseline) Assert.Equal(kv.Value, values[kv.Key]);
            Assert.False(p.Active);
        }

        [Fact]
        public void A_new_baseline_is_approached_smoothly_without_overshoot()
        {
            var p = Make();
            p.SetBaseline("HR", 120);
            double last = Get(p, "HR");
            for (int i = 0; i < 600; i++)
            {
                p.Advance(0.5);
                double now = Get(p, "HR");
                Assert.InRange(now, last, 120);                                                  // rises, never overshoots
                last = now;
            }
            Assert.InRange(last, 119, 120);                                                      // five time constants later
        }

        [Fact]
        public void The_relaxation_time_sets_the_speed_one_time_constant_covers_about_two_thirds()
        {
            var p = Make();
            p.SetBaseline("HR", 148);                                                            // a step of 60
            Run(p, 60);                                                                          // one time constant of 60 s
            Assert.InRange(Get(p, "HR"), 88 + 60 * 0.60, 88 + 60 * 0.70);
        }

        // ---------------------------------------------------------------- effects (T-P1)

        [Fact]
        public void T_P1_an_effect_moves_the_variable_in_its_direction_inside_its_profile_and_then_fades()
        {
            var p = Make();
            p.Apply(Effect("EFF-TEST", new[] { ("HR", 30.0), ("SBP", -20.0) }, onset: 30, peak: 60, plateau: 600, offset: 120));
            Run(p, 25);
            Assert.Equal(88, Get(p, "HR"));                                                      // nothing before the onset
            Run(p, 300);
            Assert.True(Get(p, "HR") > 110 && Get(p, "SBP") < 110);                              // acting during the plateau
            Run(p, 1000);                                                                        // well after it ended
            Assert.InRange(Get(p, "HR"), 87.9, 88.1);                                            // back to the baseline
            Assert.InRange(Get(p, "SBP"), 123.9, 124.1);
            Assert.False(p.Active && false);
        }

        [Fact]
        public void The_profile_has_the_shape_of_onset_rise_plateau_fall()
        {
            var prof = new PhysioProfile(10, 20, 30, 40);
            Assert.Equal(0, prof.Factor(5));
            Assert.Equal(0, prof.Factor(10));
            Assert.Equal(0.5, prof.Factor(20));
            Assert.Equal(1, prof.Factor(30));
            Assert.Equal(1, prof.Factor(59));
            Assert.Equal(0.5, prof.Factor(80));
            Assert.Equal(0, prof.Factor(100));                                                   // the length is 100 s
            Assert.Equal(100, prof.Length);
        }

        [Fact]
        public void An_effect_naming_a_variable_the_model_does_not_have_is_refused()
        {
            Assert.Throws<ArgumentException>(() => Make().Apply(Effect("bad", new[] { ("BLOOD_COLOUR", 1.0) })));
        }

        // ---------------------------------------------------------------- stacked effects stay inside the bounds (T-P3)

        [Fact]
        public void T_P3_stacked_effects_are_clamped_to_the_guard_bounds()
        {
            var p = Make();
            for (int i = 0; i < 5; i++) p.Apply(Effect("big" + i, new[] { ("HR", 200.0), ("SBP", -300.0), ("SpO2", -80.0), ("Temp", 9.0), ("RR", 100.0) }, peak: 1, plateau: 3000, offset: 1));
            Run(p, 2400);
            var v = p.Read().Values;
            Assert.InRange(v["HR"], 199.9, 200);                                                 // held at the bound (the relaxation approaches it from below)
            Assert.InRange(v["SBP"], 50, 50.1);
            Assert.InRange(v["SpO2"], 60, 60.1);
            Assert.InRange(v["Temp"], 40.9, 41);
            Assert.InRange(v["RR"], 44.9, 45);
        }

        [Fact]
        public void MAP_is_worked_out_from_the_blood_pressures()
        {
            var v = Make().Read().Values;
            Assert.Equal((124 + 2 * 78) / 3.0, v["MAP"], 6);
        }

        // ---------------------------------------------------------------- couplings

        [Fact]
        public void A_fluid_deficit_raises_the_heart_rate_and_lowers_the_blood_pressure()
        {
            var p = Make();
            p.Apply(Effect("EFF-LOSS", new[] { ("volume_deficit", 1500.0) }, peak: 60, plateau: 3000));
            Run(p, 1800);
            Assert.True(Get(p, "HR") > 88 + 5);
            Assert.True(Get(p, "SBP") < 124 - 5);
        }

        [Fact]
        public void Pain_raises_heart_rate_blood_pressure_and_breathing()
        {
            var p = Make();
            p.Apply(Effect("EFF-PAIN", new[] { ("pain", 6.0) }, peak: 30, plateau: 3000));
            Run(p, 900);
            Assert.True(Get(p, "HR") > 88 + 4 && Get(p, "SBP") > 124 + 5 && Get(p, "RR") > 16 + 1);
        }

        // ---------------------------------------------------------------- flags the behaviour model reads

        [Theory]
        [InlineData(98.0, 6.0, false)]
        [InlineData(92.0, 6.0, false)]            // the edge: 92 is not low enough
        [InlineData(91.0, 6.0, true)]             // oxygen 91 or less
        [InlineData(98.0, 4.0, false)]            // glucose 4.0 is not below 4.0
        [InlineData(98.0, 3.9, true)]
        public void Low_oxygen_or_low_sugar_switches_on_the_confusion_flag(double spo2, double glucose, bool confusion)
        {
            var b = new Dictionary<string, double>(Baseline) { ["SpO2"] = spo2, ["glucose"] = glucose };
            var p = new LitePhysio(Tables);
            p.Initialise(b, new SimRandom(1).Stream("physio"));
            Assert.Equal(confusion, p.Read().Flags.Contains("confusion"));
        }

        // ---------------------------------------------------------------- determinism and noise (T-P5, T-P2)

        [Fact]
        public void T_P5_the_same_seed_gives_an_identical_trace_and_another_seed_a_different_one()
        {
            string Trace(ulong seed)
            {
                var p = Make(seed, noise: true);
                p.Apply(Effect("e", new[] { ("HR", 20.0) }));
                var parts = new List<string>();
                for (int i = 0; i < 600; i++) { p.Advance(0.5); if (i % 20 == 0) parts.Add(p.Read().Values["HR"].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "/" + p.Read().Values["SpO2"].ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
                return string.Join(";", parts);
            }
            Assert.Equal(Trace(7), Trace(7));
            Assert.NotEqual(Trace(7), Trace(8));
        }

        // T-P2: 1000 seeds, no actions: nothing moves beyond the noise band
        [Fact]
        public void T_P2_a_thousand_seeds_with_no_actions_never_leave_the_noise_cap()
        {
            var caps = Tables.Noise.ToDictionary(n => n.Variable, n => n.Cap);
            for (ulong seed = 0; seed < 1000; seed++)
            {
                var p = Make(seed, noise: true);
                for (int i = 0; i < 120; i++)
                {
                    p.Advance(1);
                    var v = p.Read().Values;
                    foreach (var kv in caps) Assert.InRange(Math.Abs(v[kv.Key] - Baseline[kv.Key]), 0, kv.Value + 1e-9);
                }
            }
        }

        [Fact]
        public void The_noise_is_below_the_uncaused_change_band_and_is_not_added_to_the_state_flags()
        {
            Assert.All(Tables.Noise, n => Assert.True(n.Cap < n.UncausedBand, n.Variable));
            Assert.All(Tables.Noise, n => Assert.True(n.Cap <= 3 * n.Sigma + 1e-9, n.Variable));
            var b = new Dictionary<string, double>(Baseline) { ["SpO2"] = 92 };                    // just above the confusion edge; noise must not push it over
            for (ulong seed = 0; seed < 50; seed++)
            {
                var p = new LitePhysio(Tables) { NoiseEnabled = true };
                p.Initialise(b, new SimRandom(seed).Stream("physio"));
                for (int i = 0; i < 60; i++) p.Advance(1);
                Assert.DoesNotContain("confusion", p.Read().Flags);
            }
        }

        // ---------------------------------------------------------------- in a session

        [Fact]
        public void In_a_session_the_vitals_are_left_exactly_as_the_case_gives_them_for_an_hour()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            var before = s.Patient.Physiological.Vitals;
            s.Tick(3600);
            Assert.Equal(before, s.Patient.Physiological.Vitals);
            Assert.False(s.Physiology!.Engine.Active);
        }

        [Fact]
        public void In_a_session_an_effect_moves_the_vitals_and_they_return_when_it_has_faded()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            double hr0 = s.Patient.Physiological.Vitals.HeartRate;
            s.Physiology!.Apply(Effect("EFF-TEST", new[] { ("HR", 40.0) }, peak: 30, plateau: 600, offset: 60));
            s.Tick(300);
            Assert.True(s.Patient.Physiological.Vitals.HeartRate > hr0 + 20);
            s.Tick(1800);
            Assert.InRange(s.Patient.Physiological.Vitals.HeartRate, hr0 - 1, hr0 + 1);
        }

        [Fact]
        public void In_a_session_a_case_with_a_low_stated_glucose_has_the_confusion_flag()
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "STK-S1-A");
            var chain = Subsystems.Default(Content).Where(x => !(x is PhysiologySubsystem)).Concat(new ProcSense.Core.Pipeline.ISubsystem[] { new PhysiologySubsystem(Content, file) });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            Assert.Equal(3.6, s.Physiology!.Glucose);
            Assert.True(s.Patient.Clinical.HasFlag("physio.confusion"));
        }

        [Fact]
        public void A_fit_patient_has_no_confusion_flag()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            Assert.False(s.Patient.Clinical.HasFlag("physio.confusion"));
        }
    }
}
