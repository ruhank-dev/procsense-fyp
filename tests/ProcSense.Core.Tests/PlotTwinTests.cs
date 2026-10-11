using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Random;

namespace ProcSense.Core.Tests
{
    /// <summary>The review plots (docs/research/physio-plots/effects.csv, drawn by tools/physio/plot_effects.py) must show what the C# engine does (p0-physiology-events-behaviour-s07, EXP-PHY-1).</summary>
    public class PlotTwinTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

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

        [Fact]
        public void The_plotted_curves_are_the_engines_curves()
        {
            var lines = File.ReadAllLines(Path.Combine(TestCase.Repo, "docs", "research", "physio-plots", "effects.csv")).Skip(1).Select(l => l.Split(',')).ToList();
            Assert.NotEmpty(lines);
            var byEffect = lines.GroupBy(l => l[0]);
            int checkedPoints = 0;
            foreach (var g in byEffect)
            {
                var p = new LitePhysio(Content.Physio);
                p.Initialise(Normal, new SimRandom(1).Stream("physio"));
                p.Apply(Content.Physio.FindEffect(g.Key)!);
                var wanted = g.GroupBy(l => int.Parse(l[2], CultureInfo.InvariantCulture)).OrderBy(x => x.Key).ToList();
                int t = 0;
                foreach (var at in wanted)
                {
                    while (t < at.Key) { p.Advance(1); t++; }
                    foreach (var l in at)
                    {
                        double expected = double.Parse(l[3], CultureInfo.InvariantCulture);
                        Assert.True(Math.Abs(p.Read().Values[l[1]] - expected) < 0.001, g.Key + " " + l[1] + " at " + at.Key + " s: plot " + expected + " engine " + p.Read().Values[l[1]]);
                        checkedPoints++;
                    }
                }
            }
            Assert.True(checkedPoints > 1000);
        }

        [Fact]
        public void Every_effect_that_moves_a_vital_has_a_plot()
        {
            var plotted = File.ReadAllLines(Path.Combine(TestCase.Repo, "docs", "research", "physio-plots", "effects.csv")).Skip(1).Select(l => l.Split(',')[0]).Distinct().ToHashSet();
            foreach (var e in Content.Physio.Effects.Where(e => e.Effect.Targets.Count > 0)) Assert.Contains(e.Effect.Id, plotted);
        }
    }
}
