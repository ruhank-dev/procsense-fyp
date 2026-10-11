using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Random;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Treatment effects (p0-physiology-events-behaviour-s05, DT-068, plan tests T-P1 per effect): direction and timing, every treatment effect id has a row, and a treatment given in a session acts.</summary>
    public class EffectsTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static PhysioTables Tables => Content.Physio;

        private static readonly Dictionary<string, double> Hypoxic = new Dictionary<string, double>
        {
            ["HR"] = 100,
            ["SBP"] = 100,
            ["DBP"] = 60,
            ["RR"] = 22,
            ["SpO2"] = 88,
            ["Temp"] = 36.8,
            ["pain"] = 6,
            ["glucose"] = 3.2,
            ["nausea"] = 5,
            ["sedation"] = 2,
            ["volume_deficit"] = 0,
        };

        private static LitePhysio Patient()
        {
            var p = new LitePhysio(Tables);
            p.Initialise(Hypoxic, new SimRandom(1).Stream("physio"));
            return p;
        }

        private static double After(string effectId, string variable, double seconds)
        {
            var p = Patient();
            p.Apply(Tables.FindEffect(effectId)!);
            for (double t = 0; t < seconds; t += 1) p.Advance(1);
            return p.Read().Values[variable];
        }

        [Fact]
        public void Every_effect_a_treatment_names_has_a_row_with_a_source()
        {
            var json = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.ContentRoot, "treatments.json")));
            var named = json.RootElement.GetProperty("treatments").EnumerateArray().SelectMany(t => t.GetProperty("effects").EnumerateArray().Select(e => e.GetString()!)).Distinct().ToList();
            Assert.NotEmpty(named);
            foreach (var id in named) Assert.NotNull(Tables.FindEffect(id));
            foreach (var e in Tables.Effects) { Assert.Equal("design", e.Effect.Source); Assert.StartsWith("SR-", e.Ref); }
        }

        [Fact]
        public void Oxygen_raises_the_saturation_of_a_hypoxic_patient_after_its_onset_and_it_fades_again()
        {
            Assert.Equal(88, After("EFF-OXYGEN", "SpO2", 0), 3);
            Assert.True(After("EFF-OXYGEN", "SpO2", 200) > 90);
            Assert.True(After("EFF-OXYGEN", "SpO2", 200) < 93);
            Assert.True(After("EFF-OXYGEN", "SpO2", 7800) < 89);   // after the profile ends
        }

        [Theory]
        [InlineData("EFF-ANALGESIA", "pain", -1)]
        [InlineData("EFF-ANALGESIA", "HR", -1)]
        [InlineData("EFF-ANTIEMETIC", "nausea", -1)]
        [InlineData("EFF-FLUID-BOLUS", "SBP", 1)]
        [InlineData("EFF-FLUID-BOLUS", "HR", -1)]
        [InlineData("EFF-ADRENALINE", "SBP", 1)]
        [InlineData("EFF-GLUCOSE-RISE", "glucose", 1)]
        [InlineData("EFF-OPIOID-REVERSAL", "RR", 1)]
        [InlineData("EFF-OPIOID-REVERSAL", "sedation", -1)]
        [InlineData("EFF-ANAPHYLAXIS", "SBP", -1)]
        [InlineData("EFF-ANAPHYLAXIS", "SpO2", -1)]
        public void Each_effect_moves_its_variable_in_the_right_direction(string effect, string variable, int direction)
        {
            double before = After(effect, variable, 0), at = After(effect, variable, 1500);
            Assert.True(Math.Sign(at - before) == direction, effect + " " + variable + ": " + before + " -> " + at);
        }

        [Fact]
        public void An_effect_does_nothing_before_its_onset()
        {
            Assert.Equal(After("EFF-ANALGESIA", "pain", 0), After("EFF-ANALGESIA", "pain", 200), 6);
            Assert.True(After("EFF-ANALGESIA", "pain", 1000) < 6);
        }

        [Fact]
        public void Effects_that_act_elsewhere_have_no_targets_and_say_so()
        {
            foreach (var id in new[] { "EFF-ANTIBIOTIC", "EFF-POTASSIUM-LOWER" })
            {
                var d = Tables.Effects.Single(e => e.Effect.Id == id);
                Assert.Empty(d.Effect.Targets);
                Assert.False(string.IsNullOrWhiteSpace(d.Note));
            }
        }

        [Fact]
        public void A_treatment_given_in_a_session_changes_the_live_vital_signs_and_the_same_seed_gives_the_same_result()
        {
            string Run()
            {
                var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
                s.Events.Publish(new TreatmentGiven("TR-OXYGEN", new[] { "EFF-OXYGEN" }, 1));
                s.Events.Publish(new TreatmentGiven("TR-IV-FLUIDS", new[] { "EFF-FLUID-BOLUS" }, 2));
                s.Tick(1200);
                var v = s.Patient.Physiological.Vitals;
                return v.HeartRate + "/" + v.SystolicBp + "/" + v.Spo2;
            }
            var first = Run();
            Assert.Equal(first, Run());
            var quiet = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            quiet.Tick(1200);
            var q = quiet.Patient.Physiological.Vitals;
            Assert.NotEqual(q.HeartRate + "/" + q.SystolicBp + "/" + q.Spo2, first);
        }
    }
}
