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
    /// <summary>The nine vital-sign patterns of walkthrough 4.11 and the time drivers (p0-physiology-events-behaviour-s03, plan test T-P1).</summary>
    public class PatternTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        // the book's all-normal row
        private static readonly Dictionary<string, double> Normal = new Dictionary<string, double>
        {
            ["HR"] = 76,
            ["SBP"] = 124,
            ["DBP"] = 78,
            ["RR"] = 14,
            ["SpO2"] = 98,
            ["Temp"] = 36.7,
            ["pain"] = 0,
            ["glucose"] = 5.5,
            ["nausea"] = 0,
            ["sedation"] = 0,
            ["volume_deficit"] = 0,
        };

        // the example numbers in walkthrough 4.11, typed here a second time so the content cannot drift unnoticed
        public static IEnumerable<object[]> Book() => new[]
        {
            new object[] { "PAT-PAIN", new Dictionary<string, double> { ["HR"] = 104, ["SBP"] = 146, ["DBP"] = 88, ["RR"] = 20, ["Temp"] = 37.0 } },
            new object[] { "PAT-INFECTION", new Dictionary<string, double> { ["HR"] = 108, ["Temp"] = 38.6, ["RR"] = 20, ["SBP"] = 124 } },
            new object[] { "PAT-DEHYDRATION", new Dictionary<string, double> { ["HR"] = 112, ["SBP"] = 104, ["DBP"] = 66 } },
            new object[] { "PAT-SEPSIS", new Dictionary<string, double> { ["HR"] = 128, ["SBP"] = 92, ["DBP"] = 58, ["RR"] = 26, ["Temp"] = 39.2 } },
            new object[] { "PAT-BLEEDING", new Dictionary<string, double> { ["HR"] = 122, ["SBP"] = 96, ["DBP"] = 60 } },
            new object[] { "PAT-ALLERGY", new Dictionary<string, double> { ["SBP"] = 84, ["DBP"] = 50 } },
            new object[] { "PAT-OPIOID", new Dictionary<string, double> { ["RR"] = 8, ["SpO2"] = 89 } },
            new object[] { "PAT-AF", new Dictionary<string, double> { ["HR"] = 128, ["SBP"] = 132, ["DBP"] = 84 } },
        };

        private static LitePhysio Patient()
        {
            var p = new LitePhysio(Content.Physio);
            p.Initialise(Normal, new SimRandom(1).Stream("physio"));
            return p;
        }

        [Fact]
        public void There_are_nine_patterns_and_the_normal_one_changes_nothing()
        {
            var ids = Content.Physio.Effects.Where(e => e.Effect.Id.StartsWith("PAT-", StringComparison.Ordinal)).Select(e => e.Effect.Id).ToList();
            Assert.Equal(9, ids.Count);
            var p = Patient();
            p.Apply(Content.Physio.FindEffect("PAT-NORMAL")!);
            for (int i = 0; i < 600; i++) p.Advance(1);
            foreach (var kv in Normal) Assert.Equal(kv.Value, p.Read().Values[kv.Key], 6);
        }

        [Theory]
        [MemberData(nameof(Book))]
        public void Each_pattern_reaches_the_numbers_the_book_gives(string pattern, Dictionary<string, double> book)
        {
            var p = Patient();
            p.Apply(Content.Physio.FindEffect(pattern)!);
            for (int i = 0; i < 1800; i++) p.Advance(1);
            var v = p.Read().Values;
            foreach (var kv in book) Assert.True(Math.Abs(v[kv.Key] - kv.Value) <= (kv.Key == "Temp" ? 0.15 : 1.0), pattern + " " + kv.Key + ": " + v[kv.Key] + " not " + kv.Value);
        }

        [Fact]
        public void The_pain_pattern_does_not_count_the_pain_couplings_twice()
        {
            var p = Patient();
            p.Apply(Content.Physio.FindEffect("PAT-PAIN")!);
            for (int i = 0; i < 1800; i++) p.Advance(1);
            Assert.InRange(p.Read().Values["HR"], 103, 105);       // 76 + the bundle + 1.5 per pain point, not more
            Assert.InRange(p.Read().Values["pain"], 7.9, 8.1);
        }

        [Fact]
        public void The_dehydration_driver_runs_until_a_bolus_is_given_and_the_bolus_takes_one_dose_off()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            var phys = s.Physiology!;
            phys.StartDriver("DRV-DEHYDRATION");
            Assert.True(phys.DriverRunning("DRV-DEHYDRATION"));
            s.Tick(4 * 3600);
            double deficit = phys.Engine.GetBaseline("volume_deficit");
            Assert.InRange(deficit, 550, 650);                                    // 150 mL an hour for 4 hours
            s.Events.Publish(new TreatmentGiven("TR-IV-FLUIDS", new[] { "EFF-FLUID-BOLUS" }, 1));
            Assert.False(phys.DriverRunning("DRV-DEHYDRATION"));
            Assert.InRange(phys.Engine.GetBaseline("volume_deficit"), 50, 150);   // 600 minus a 500 mL bolus
            s.Tick(3600);
            Assert.InRange(phys.Engine.GetBaseline("volume_deficit"), 50, 150);   // and it no longer grows
        }

        [Fact]
        public void An_unknown_driver_is_refused()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            Assert.Throws<ArgumentException>(() => s.Physiology!.StartDriver("DRV-NOPE"));
        }
    }
}
