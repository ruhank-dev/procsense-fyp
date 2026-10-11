using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The what-ifs bound to the P0 cases can really happen in them (p0-case-library s03, plan test T-E8; content/whatif_bindings.csv).</summary>
    public class WhatIfBindingTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);

        private static List<(string Case, string WhatIf, string Event, string Test)> Rows()
        {
            var lines = File.ReadAllLines(Path.Combine(TestCase.ContentRoot, "whatif_bindings.csv")).Skip(1).Where(l => l.Length > 0);
            return lines.Select(l => l.Split(',')).Select(p => (p[0], p[1], p[2], p[3])).ToList();
        }

        [Fact]
        public void The_bindings_cover_eight_P0_cases_with_25_rows_and_name_only_P0_cases()
        {
            var rows = Rows();
            Assert.Equal(25, rows.Count);
            foreach (var r in rows) Assert.Equal("P0", All.CaseFiles[r.Case].Tier);
            Assert.True(rows.Select(r => r.Case).Distinct().Count() >= 8);
        }

        [Fact]
        public void Every_bound_event_fires_in_its_case_when_its_trigger_is_forced()
        {
            int fired = 0;
            foreach (var r in Rows().Where(r => r.Event.Length > 0))
            {
                var s = new Session(All.Cases[r.Case], 42, SessionMode.Practice, Subsystems.Default(All));
                if (r.Event == "E-HYPERSENSITIVITY")
                    s.Patient.Clinical.SetFlag("allergy.reaction_eligible");
                else
                {
                    var def = All.Events.Events.Single(e => e.Id == r.Event);
                    // the case's own observation that crosses the threshold sets that one vital sign (the others stay as the case starts, so only this event is eligible)
                    var obs = All.CaseFiles[r.Case].Observations.First(o => Crosses(def.Watch, o));
                    var v = s.Patient.Physiological.Vitals;
                    s.Patient.Physiological.Vitals = def.Watch.Vital switch
                    {
                        "hr" => v with { HeartRate = obs.HeartRate },
                        "sbp" => v with { SystolicBp = obs.SystolicBp },
                        "dbp" => v with { DiastolicBp = obs.DiastolicBp },
                        "rr" => v with { RespiratoryRate = obs.RespiratoryRate },
                        "spo2" => v with { Spo2 = obs.Spo2 },
                        _ => v with { TemperatureC = obs.TemperatureC },
                    };
                }
                s.Tick(10);
                Assert.True(s.Director!.HasFired(r.Event), r.Case + " " + r.Event);
                fired++;
            }
            Assert.True(fired >= 10);
        }

        private static bool Crosses(Events.VitalWatch w, CaseFileObservation o)
        {
            double x = w.Vital switch { "hr" => o.HeartRate, "sbp" => o.SystolicBp, "dbp" => o.DiastolicBp, "rr" => o.RespiratoryRate, "spo2" => o.Spo2, _ => o.TemperatureC };
            return w.Op switch { "<" => x < w.Value, "<=" => x <= w.Value, ">" => x > w.Value, _ => x >= w.Value };
        }

        [Fact]
        public void Every_engine_handled_binding_names_a_test_file_that_exists_and_no_binding_is_for_a_data_only_what_if()
        {
            var coverage = File.ReadAllText(Path.Combine(TestCase.Repo, "docs", "research", "whatif_coverage.md"));
            foreach (var r in Rows())
            {
                if (r.Event.Length == 0) Assert.True(File.Exists(Path.Combine(TestCase.Repo, "tests", "ProcSense.Core.Tests", r.Test)), r.Test);
                Assert.DoesNotContain("| " + r.WhatIf + " |", string.Join("\n", coverage.Split('\n').Where(l => l.Contains("| data only |"))));
            }
        }
    }
}
